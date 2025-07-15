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
    /// 3D世界空间的老师语音气泡系统 - 支持动态摄像机查找
    /// </summary>
    public class TeacherSpeechBubble : MonoBehaviour
    {
        [Header("3D渲染设置")]
        [SerializeField] private SpriteRenderer bubbleRenderer;
        [SerializeField] private TextMeshPro bubbleText;
        [SerializeField] private Sprite bubbleSprite;
        [SerializeField] private Animator bubbleAnimator;

        [Header("位置设置")]
        [SerializeField] private Vector3 offset = new Vector3(0, 2.5f, 0);
        [SerializeField] private bool billboardToCamera = true;

        [Header("动画设置")]
        [SerializeField] private float fadeSpeed = 3f;
        [SerializeField] private float defaultDuration = 3f;

        [Header("文本颜色")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = Color.red;
        [SerializeField] private Color encouragementColor = Color.green;
        [SerializeField] private Color systemColor = Color.gray;
        [SerializeField] private Color cardActionColor = Color.magenta;
        [SerializeField] private Color excitedColor = Color.yellow;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        private static TeacherSpeechBubble instance;
        private Camera playerCamera;
        private Queue<SpeechData> speechQueue = new Queue<SpeechData>();
        private bool isShowing = false;
        private Coroutine currentSpeechCoroutine;
        private bool cameraSetupCompleted = false;

        public static TeacherSpeechBubble Instance => instance;

        #region 静态接口
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

        #region 摄像机设置（参考BlackboardDisplayManager）

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
            LogDebug("收到摄像机设置完成事件");
            cameraSetupCompleted = true;
            StartCoroutine(FindAndSetupCamera());
        }

        private void OnClassroomInitialized()
        {
            LogDebug("收到教室初始化完成事件");
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

            LogDebug("开始查找玩家摄像机");

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
                LogDebug($"成功设置摄像机: {foundCamera.name}");
            }
            else
            {
                LogDebug("未找到玩家摄像机，使用主摄像机作为备用");
                SetupCamera(Camera.main);
                cameraSetupCompleted = true;
            }
        }

        private Camera FindPlayerCamera()
        {
            // 方式1: 通过ClassroomManager获取
            var classroomManager = FindObjectOfType<Classroom.ClassroomManager>();
            if (classroomManager != null && classroomManager.IsInitialized)
            {
                var cameraController = classroomManager.GetLocalPlayerCameraController();
                if (cameraController != null && cameraController.PlayerCamera != null)
                {
                    LogDebug($"通过ClassroomManager找到摄像机: {cameraController.PlayerCamera.name}");
                    return cameraController.PlayerCamera;
                }
            }

            // 方式2: 通过PlayerCameraController组件查找
            var playerCameraControllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in playerCameraControllers)
            {
                if (controller.IsLocalPlayer && controller.PlayerCamera != null)
                {
                    LogDebug($"通过PlayerCameraController找到摄像机: {controller.PlayerCamera.name}");
                    return controller.PlayerCamera;
                }
            }

            return null;
        }

        private void SetupCamera(Camera camera)
        {
            playerCamera = camera;

            // 如果文本已经创建，设置摄像机
            if (bubbleText != null && playerCamera != null)
            {
                // 对于TextMeshPro 3D，我们不需要设置canvas camera
                // 但需要确保文本能被摄像机正确渲染
                LogDebug($"TextMeshPro 3D文本将被摄像机正确渲染: {playerCamera.name}");
            }
        }

        #endregion

        private void Initialize()
        {
            // 创建气泡渲染器
            if (bubbleRenderer == null)
            {
                GameObject bubbleGO = new GameObject("SpeechBubble");
                bubbleGO.transform.SetParent(transform);
                bubbleGO.transform.localPosition = offset;

                bubbleRenderer = bubbleGO.AddComponent<SpriteRenderer>();
                bubbleRenderer.sprite = bubbleSprite;
                bubbleRenderer.sortingOrder = 10;
                bubbleRenderer.color = new Color(1, 1, 1, 0);

                // 添加动画组件
                bubbleAnimator = bubbleGO.AddComponent<Animator>();
            }

            // 创建3D文本 - 作为bubble的子对象，需要Y轴旋转180度修正朝向
            if (bubbleText == null)
            {
                GameObject textGO = new GameObject("SpeechText");
                textGO.transform.SetParent(bubbleRenderer.transform);
                textGO.transform.localPosition = Vector3.zero;
                textGO.transform.localRotation = Quaternion.Euler(0, 180, 0); // Y轴旋转180度修正朝向

                bubbleText = textGO.AddComponent<TextMeshPro>();
                bubbleText.text = "";
                bubbleText.fontSize = 1f;
                bubbleText.alignment = TextAlignmentOptions.Center;
                bubbleText.sortingOrder = 11;
                bubbleText.color = new Color(1, 1, 1, 0);

                // 设置3D文本的渲染设置
                bubbleText.autoSizeTextContainer = true;
                bubbleText.rectTransform.sizeDelta = new Vector2(10, 3); // 设置文本容器大小
            }

            SetBubbleVisible(false);
            LogDebug("TeacherSpeechBubble 初始化完成");
        }

        private void LateUpdate()
        {
            if (billboardToCamera && playerCamera != null && bubbleRenderer != null)
            {
                // 让气泡始终面向摄像机
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

            // 设置内容
            bubbleText.text = speech.text;
            bubbleText.color = GetTextColor(speech.type);

            // 淡入
            yield return StartCoroutine(FadeIn());

            // 显示时间
            yield return new WaitForSeconds(speech.duration);

            // 淡出
            yield return StartCoroutine(FadeOut());

            isShowing = false;

            // 处理下一个消息
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

                // 控制动画播放
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