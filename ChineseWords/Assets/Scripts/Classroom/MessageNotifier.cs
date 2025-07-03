using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace UI.MessageSystem
{
    /// <summary>
    /// ��Ϣ����ö��
    /// </summary>
    public enum MessageType
    {
        Info, Warning, Error, Success, Turn, System
    }

    /// <summary>
    /// ��Ϣ���ݽṹ
    /// </summary>
    [System.Serializable]
    public class MessageData
    {
        public string text;
        public MessageType type;
        public float displayDuration;
        public int priority;

        public MessageData(string text, MessageType type = MessageType.Info, float displayDuration = 3f, int priority = 0)
        {
            this.text = text;
            this.type = type;
            this.displayDuration = displayDuration;
            this.priority = priority;
        }
    }

    /// <summary>
    /// ��������ʽ��Ϣ��ʾ��
    /// ʹ�÷�ʽ��MessageNotifier.Show("�ֵ�������");
    /// </summary>
    public class MessageNotifier : MonoBehaviour
    {
        #region ����
        private static MessageNotifier instance;
        public static MessageNotifier Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("MessageNotifier");
                    instance = go.AddComponent<MessageNotifier>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        #endregion

        #region ���ò���
        [Header("��������")]
        [SerializeField] private Canvas messageCanvas;
        [SerializeField] private GameObject notificationPanelPrefab;
        [SerializeField] private bool autoFindCanvas = true;

        [Header("��������")]
        [SerializeField] private float slideDownDuration = 0.5f;
        [SerializeField] private float slideUpDuration = 0.4f;
        [SerializeField] private float defaultDisplayDuration = 3f;
        [SerializeField] private AnimationCurve slideDownCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve slideUpCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("λ������")]
        [SerializeField] private float topMargin = 20f;
        [SerializeField] private float hiddenOffset = 100f; // ����ʱ����ƫ�Ƶľ���

        [Header("��Ϊ����")]
        [SerializeField] private bool allowInterrupt = true;
        [SerializeField] private float queueDelay = 0.5f;

        [Header("��ɫ����")]
        [SerializeField] private Color infoColor = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color successColor = new Color(0.3f, 1f, 0.3f, 1f);
        [SerializeField] private Color turnColor = new Color(0.8f, 0.3f, 1f, 1f);
        [SerializeField] private Color systemColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        #endregion

        #region ˽�б���
        private GameObject panelInstance;
        private RectTransform panelRect;
        private TextMeshProUGUI panelText;
        private Image panelImage;
        private CanvasGroup panelCanvasGroup;

        private Queue<MessageData> messageQueue = new Queue<MessageData>();
        private bool isShowing = false;
        private bool isProcessingQueue = false;
        private Coroutine currentCoroutine;

        private Vector2 visiblePosition;
        private Vector2 hiddenPosition;
        #endregion

        #region ��̬�ӿ�
        public static void Show(string message) => Show(message, MessageType.Info);
        public static void Show(string message, MessageType type) => Show(message, type, Instance.defaultDisplayDuration);
        public static void Show(string message, MessageType type, float duration, int priority = 0)
        {
            Instance.ShowMessage(new MessageData(message, type, duration, priority));
        }
        public static void ShowUrgent(string message, MessageType type = MessageType.Error)
        {
            Show(message, type, Instance.defaultDisplayDuration, 999);
        }
        public static void Hide() => Instance.HideMessage();
        public static void ClearAll() => Instance.ClearAllMessages();
        #endregion

        #region Unity��������
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }
        #endregion

        #region ��ʼ��
        private void Initialize()
        {
            FindCanvas();
            if (messageCanvas == null)
            {
                Debug.LogError("[MessageNotifier] δ�ҵ�MessageCanvas����ȷ������������Ϊ'MessageCanvas'��Canvas���ֶ�����");
                return;
            }

            if (notificationPanelPrefab == null)
            {
                Debug.LogError("[MessageNotifier] ֪ͨ���Ԥ����δ���䣬����Inspector�з���notificationPanelPrefab");
                return;
            }

            CreatePanel();
            SetupPositions();
        }

        private void FindCanvas()
        {
            if (messageCanvas == null && autoFindCanvas)
            {
                GameObject canvasGO = GameObject.Find("MessageCanvas");
                if (canvasGO != null)
                {
                    messageCanvas = canvasGO.GetComponent<Canvas>();
                }
            }
        }

        private void CreatePanel()
        {
            panelInstance = Instantiate(notificationPanelPrefab, messageCanvas.transform);
            panelInstance.name = "NotificationPanel";

            panelRect = panelInstance.GetComponent<RectTransform>();
            panelText = panelInstance.GetComponentInChildren<TextMeshProUGUI>();
            panelImage = panelInstance.GetComponent<Image>();
            panelCanvasGroup = panelInstance.GetComponent<CanvasGroup>();

            if (panelCanvasGroup == null)
                panelCanvasGroup = panelInstance.AddComponent<CanvasGroup>();

            panelInstance.SetActive(false);
        }

        private void SetupPositions()
        {
            // ����ê��Ϊ��������
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);

            visiblePosition = new Vector2(0f, -topMargin);
            hiddenPosition = new Vector2(0f, hiddenOffset);

            panelRect.anchoredPosition = hiddenPosition;
            panelCanvasGroup.alpha = 0f;
        }
        #endregion

        #region ��Ϣ��ʾ
        private void ShowMessage(MessageData messageData)
        {
            // ����Ƿ��ѳ�ʼ��
            if (panelInstance == null)
            {
                Debug.LogError("[MessageNotifier] ϵͳδ��ȷ��ʼ�����޷���ʾ��Ϣ");
                return;
            }

            // �����ȼ���Ϣ�����жϵ�ǰ��Ϣ
            if (isShowing && allowInterrupt && messageData.priority > 0)
            {
                HideMessage();
            }

            if (isShowing || isProcessingQueue)
            {
                // ��ӵ����У�֧�����ȼ�
                if (messageData.priority > 0)
                {
                    var tempList = new List<MessageData>(messageQueue);
                    tempList.Add(messageData);
                    tempList.Sort((a, b) => b.priority.CompareTo(a.priority));
                    messageQueue.Clear();
                    foreach (var msg in tempList) messageQueue.Enqueue(msg);
                }
                else
                {
                    messageQueue.Enqueue(messageData);
                }

                if (!isProcessingQueue)
                    StartCoroutine(ProcessQueue());
            }
            else
            {
                DisplayMessage(messageData);
            }
        }

        private IEnumerator ProcessQueue()
        {
            isProcessingQueue = true;
            while (messageQueue.Count > 0)
            {
                while (isShowing) yield return new WaitForSeconds(0.1f);
                DisplayMessage(messageQueue.Dequeue());
                yield return new WaitForSeconds(queueDelay);
            }
            isProcessingQueue = false;
        }

        private void DisplayMessage(MessageData messageData)
        {
            if (currentCoroutine != null) StopCoroutine(currentCoroutine);
            currentCoroutine = StartCoroutine(MessageCoroutine(messageData));
        }

        private IEnumerator MessageCoroutine(MessageData messageData)
        {
            isShowing = true;

            // ��������
            panelText.text = messageData.text;
            panelText.color = GetMessageColor(messageData.type);  // �޸�������ɫ�����Ǳ���
            panelInstance.SetActive(true);

            // ��������
            yield return StartCoroutine(SlideAnimation(hiddenPosition, visiblePosition, slideDownDuration, slideDownCurve, true));

            // ��ʾʱ��
            yield return new WaitForSeconds(messageData.displayDuration);

            // ��������
            yield return StartCoroutine(SlideAnimation(visiblePosition, hiddenPosition, slideUpDuration, slideUpCurve, false));

            panelInstance.SetActive(false);
            isShowing = false;
        }

        private IEnumerator SlideAnimation(Vector2 from, Vector2 to, float duration, AnimationCurve curve, bool fadeIn)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = curve.Evaluate(elapsed / duration);

                panelRect.anchoredPosition = Vector2.Lerp(from, to, progress);
                panelCanvasGroup.alpha = fadeIn ? progress : 1f - progress;

                yield return null;
            }

            panelRect.anchoredPosition = to;
            panelCanvasGroup.alpha = fadeIn ? 1f : 0f;
        }
        #endregion

        #region ���Ʒ���
        private void HideMessage()
        {
            if (currentCoroutine != null)
            {
                StopCoroutine(currentCoroutine);
                StartCoroutine(SlideAnimation(panelRect.anchoredPosition, hiddenPosition, slideUpDuration * 0.5f, slideUpCurve, false));
                panelInstance.SetActive(false);
                isShowing = false;
            }
        }

        private void ClearAllMessages()
        {
            messageQueue.Clear();
            HideMessage();
            isProcessingQueue = false;
        }

        private Color GetMessageColor(MessageType type)
        {
            switch (type)
            {
                case MessageType.Info: return infoColor;
                case MessageType.Warning: return warningColor;
                case MessageType.Error: return errorColor;
                case MessageType.Success: return successColor;
                case MessageType.Turn: return turnColor;
                case MessageType.System: return systemColor;
                default: return infoColor;
            }
        }
        #endregion

        #region �༭������
#if UNITY_EDITOR
        [Header("�༭������")]
        [SerializeField] private string testMessage = "������Ϣ";
        [SerializeField] private MessageType testMessageType = MessageType.Info;

        [ContextMenu("���Ե�����Ϣ")]
        private void TestMessage()
        {
            if (Application.isPlaying)
                Show(testMessage, testMessageType);
        }

        [ContextMenu("������������")]
        private void TestAllTypes()
        {
            if (Application.isPlaying)
                StartCoroutine(TestAllTypesCoroutine());
        }

        [ContextMenu("���Զ���")]
        private void TestQueue()
        {
            if (Application.isPlaying)
            {
                Show("��Ϣ 1", MessageType.Info);
                Show("��Ϣ 2", MessageType.Warning);
                Show("������Ϣ", MessageType.Error, 2f, 10);
                Show("��Ϣ 3", MessageType.Success);
            }
        }

        private IEnumerator TestAllTypesCoroutine()
        {
            var types = (MessageType[])System.Enum.GetValues(typeof(MessageType));
            foreach (var type in types)
            {
                Show($"{type} ���Ͳ���", type);
                yield return new WaitForSeconds(3.5f);
            }
        }
#endif
        #endregion
    }
}