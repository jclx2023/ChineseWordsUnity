using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Classroom.Player;

namespace Classroom.Teacher
{
    public enum SpeechType
    {
        Normal, Warning, Encouragement, System, CardAction, Excited
    }

    [System.Serializable]
    public class SpeechData
    {
        public string text;
        public SpeechType type;
        public float duration;

        public SpeechData(string text, SpeechType type = SpeechType.Normal, float duration = 3f)
        {
            this.text = text;
            this.type = type;
            this.duration = duration;
        }
    }

    /// <summary>
    /// 3D����ռ����ʦ��������ϵͳ - ֧�ֶ�̬���������
    /// </summary>
    public class TeacherSpeechBubble : MonoBehaviour
    {
        [Header("3D��Ⱦ����")]
        [SerializeField] private SpriteRenderer bubbleRenderer;
        [SerializeField] private TextMeshPro bubbleText;
        [SerializeField] private Sprite bubbleSprite;
        [SerializeField] private Animator bubbleAnimator;

        [Header("λ������")]
        [SerializeField] private Vector3 offset = new Vector3(0, 2.5f, 0);
        [SerializeField] private bool billboardToCamera = true;

        [Header("��������")]
        [SerializeField] private float fadeSpeed = 3f;
        [SerializeField] private float defaultDuration = 3f;

        [Header("�ı���ɫ")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = Color.red;
        [SerializeField] private Color encouragementColor = Color.green;
        [SerializeField] private Color systemColor = Color.gray;
        [SerializeField] private Color cardActionColor = Color.magenta;
        [SerializeField] private Color excitedColor = Color.yellow;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        private static TeacherSpeechBubble instance;
        private Camera playerCamera;
        private Queue<SpeechData> speechQueue = new Queue<SpeechData>();
        private bool isShowing = false;
        private Coroutine currentSpeechCoroutine;
        private bool cameraSetupCompleted = false;

        public static TeacherSpeechBubble Instance => instance;

        #region ��̬�ӿ�
        public static void Say(string message) => Say(message, SpeechType.Normal);
        public static void Say(string message, SpeechType type) => Say(message, type, Instance?.defaultDuration ?? 3f);
        public static void Say(string message, SpeechType type, float duration)
        {
            if (Instance != null)
                Instance.ShowSpeech(new SpeechData(message, type, duration));
        }
        #endregion

        private void Awake()
        {
            instance = this;
            Initialize();
        }

        private void Start()
        {
            SubscribeToClassroomEvents();
            StartCoroutine(FindAndSetupCamera());
        }

        private void OnDestroy()
        {
            UnsubscribeFromClassroomEvents();
            if (instance == this) instance = null;
        }

        #region ��������ã��ο�BlackboardDisplayManager��

        private void SubscribeToClassroomEvents()
        {
            Classroom.ClassroomManager.OnCameraSetupCompleted += OnCameraSetupCompleted;
            Classroom.ClassroomManager.OnClassroomInitialized += OnClassroomInitialized;
        }

        private void UnsubscribeFromClassroomEvents()
        {
            Classroom.ClassroomManager.OnCameraSetupCompleted -= OnCameraSetupCompleted;
            Classroom.ClassroomManager.OnClassroomInitialized -= OnClassroomInitialized;
        }

        private void OnCameraSetupCompleted()
        {
            LogDebug("�յ��������������¼�");
            cameraSetupCompleted = true;
            StartCoroutine(FindAndSetupCamera());
        }

        private void OnClassroomInitialized()
        {
            LogDebug("�յ����ҳ�ʼ������¼�");
            if (!cameraSetupCompleted)
            {
                StartCoroutine(FindAndSetupCamera());
            }
        }

        private IEnumerator FindAndSetupCamera()
        {
            Camera foundCamera = null;
            float timeoutCounter = 0f;
            float timeout = 10f;

            LogDebug("��ʼ������������");

            while (foundCamera == null && timeoutCounter < timeout)
            {
                foundCamera = FindPlayerCamera();

                if (foundCamera == null)
                {
                    timeoutCounter += 0.2f;
                    yield return new WaitForSeconds(0.2f);
                }
            }

            if (foundCamera != null)
            {
                SetupCamera(foundCamera);
                cameraSetupCompleted = true;
                LogDebug($"�ɹ����������: {foundCamera.name}");
            }
            else
            {
                LogDebug("δ�ҵ�����������ʹ�����������Ϊ����");
                SetupCamera(Camera.main);
                cameraSetupCompleted = true;
            }
        }

        private Camera FindPlayerCamera()
        {
            // ��ʽ1: ͨ��ClassroomManager��ȡ
            var classroomManager = FindObjectOfType<Classroom.ClassroomManager>();
            if (classroomManager != null && classroomManager.IsInitialized)
            {
                var cameraController = classroomManager.GetLocalPlayerCameraController();
                if (cameraController != null && cameraController.PlayerCamera != null)
                {
                    LogDebug($"ͨ��ClassroomManager�ҵ������: {cameraController.PlayerCamera.name}");
                    return cameraController.PlayerCamera;
                }
            }

            // ��ʽ2: ͨ��PlayerCameraController�������
            var playerCameraControllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in playerCameraControllers)
            {
                if (controller.IsLocalPlayer && controller.PlayerCamera != null)
                {
                    LogDebug($"ͨ��PlayerCameraController�ҵ������: {controller.PlayerCamera.name}");
                    return controller.PlayerCamera;
                }
            }

            return null;
        }

        private void SetupCamera(Camera camera)
        {
            playerCamera = camera;

            // ����ı��Ѿ����������������
            if (bubbleText != null && playerCamera != null)
            {
                // ����TextMeshPro 3D�����ǲ���Ҫ����canvas camera
                // ����Ҫȷ���ı��ܱ��������ȷ��Ⱦ
                LogDebug($"TextMeshPro 3D�ı������������ȷ��Ⱦ: {playerCamera.name}");
            }
        }

        #endregion

        private void Initialize()
        {
            // ����������Ⱦ��
            if (bubbleRenderer == null)
            {
                GameObject bubbleGO = new GameObject("SpeechBubble");
                bubbleGO.transform.SetParent(transform);
                bubbleGO.transform.localPosition = offset;

                bubbleRenderer = bubbleGO.AddComponent<SpriteRenderer>();
                bubbleRenderer.sprite = bubbleSprite;
                bubbleRenderer.sortingOrder = 10;
                bubbleRenderer.color = new Color(1, 1, 1, 0);

                // ��Ӷ������
                bubbleAnimator = bubbleGO.AddComponent<Animator>();
            }

            // ����3D�ı� - ��Ϊbubble���Ӷ�����ҪY����ת180����������
            if (bubbleText == null)
            {
                GameObject textGO = new GameObject("SpeechText");
                textGO.transform.SetParent(bubbleRenderer.transform);
                textGO.transform.localPosition = Vector3.zero;
                textGO.transform.localRotation = Quaternion.Euler(0, 180, 0); // Y����ת180����������

                bubbleText = textGO.AddComponent<TextMeshPro>();
                bubbleText.text = "";
                bubbleText.fontSize = 1f;
                bubbleText.alignment = TextAlignmentOptions.Center;
                bubbleText.sortingOrder = 11;
                bubbleText.color = new Color(1, 1, 1, 0);

                // ����3D�ı�����Ⱦ����
                bubbleText.autoSizeTextContainer = true;
                bubbleText.rectTransform.sizeDelta = new Vector2(10, 3); // �����ı�������С
            }

            SetBubbleVisible(false);
            LogDebug("TeacherSpeechBubble ��ʼ�����");
        }

        private void LateUpdate()
        {
            if (billboardToCamera && playerCamera != null && bubbleRenderer != null)
            {
                // ������ʼ�����������
                Vector3 directionToCamera = playerCamera.transform.position - bubbleRenderer.transform.position;
                bubbleRenderer.transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }

        private void ShowSpeech(SpeechData speechData)
        {
            speechQueue.Enqueue(speechData);

            if (!isShowing)
            {
                ProcessNextSpeech();
            }
        }

        private void ProcessNextSpeech()
        {
            if (speechQueue.Count == 0) return;

            SpeechData speech = speechQueue.Dequeue();

            if (currentSpeechCoroutine != null)
                StopCoroutine(currentSpeechCoroutine);

            currentSpeechCoroutine = StartCoroutine(SpeechCoroutine(speech));
        }

        private IEnumerator SpeechCoroutine(SpeechData speech)
        {
            isShowing = true;

            // ��������
            bubbleText.text = speech.text;
            bubbleText.color = GetTextColor(speech.type);

            // ����
            yield return StartCoroutine(FadeIn());

            // ��ʾʱ��
            yield return new WaitForSeconds(speech.duration);

            // ����
            yield return StartCoroutine(FadeOut());

            isShowing = false;

            // ������һ����Ϣ
            if (speechQueue.Count > 0)
            {
                ProcessNextSpeech();
            }
        }

        private IEnumerator FadeIn()
        {
            SetBubbleVisible(true);
            float alpha = 0f;

            while (alpha < 1f)
            {
                alpha += fadeSpeed * Time.deltaTime;
                SetBubbleAlpha(alpha);
                yield return null;
            }

            SetBubbleAlpha(1f);
        }

        private IEnumerator FadeOut()
        {
            float alpha = 1f;

            while (alpha > 0f)
            {
                alpha -= fadeSpeed * Time.deltaTime;
                SetBubbleAlpha(alpha);
                yield return null;
            }

            SetBubbleVisible(false);
        }

        private void SetBubbleVisible(bool visible)
        {
            if (bubbleRenderer != null)
            {
                bubbleRenderer.gameObject.SetActive(visible);

                // ���ƶ�������
                if (bubbleAnimator != null)
                {
                    if (visible)
                        bubbleAnimator.Play("BubbleAnimation");
                }
            }
        }

        private void SetBubbleAlpha(float alpha)
        {
            if (bubbleRenderer != null)
            {
                Color bubbleColor = bubbleRenderer.color;
                bubbleColor.a = alpha;
                bubbleRenderer.color = bubbleColor;
            }

            if (bubbleText != null)
            {
                Color textColor = bubbleText.color;
                textColor.a = alpha;
                bubbleText.color = textColor;
            }
        }

        private Color GetTextColor(SpeechType type)
        {
            return type switch
            {
                SpeechType.Warning => warningColor,
                SpeechType.Encouragement => encouragementColor,
                SpeechType.System => systemColor,
                SpeechType.CardAction => cardActionColor,
                SpeechType.Excited => excitedColor,
                _ => normalColor
            };
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[TeacherSpeechBubble] {message}");
            }
        }
    }
}