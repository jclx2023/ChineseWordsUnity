using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace UI.MessageSystem
{
    /// <summary>
    /// 消息类型枚举
    /// </summary>
    public enum MessageType
    {
        Info, Warning, Error, Success, Turn, System
    }

    /// <summary>
    /// 消息数据结构
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
    /// 顶部下拉式消息提示器
    /// 使用方式：MessageNotifier.Show("轮到你啦！");
    /// </summary>
    public class MessageNotifier : MonoBehaviour
    {
        #region 单例
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

        #region 配置参数
        [Header("基础配置")]
        [SerializeField] private Canvas messageCanvas;
        [SerializeField] private GameObject notificationPanelPrefab;
        [SerializeField] private bool autoFindCanvas = true;

        [Header("动画设置")]
        [SerializeField] private float slideDownDuration = 0.5f;
        [SerializeField] private float slideUpDuration = 0.4f;
        [SerializeField] private float defaultDisplayDuration = 3f;
        [SerializeField] private AnimationCurve slideDownCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve slideUpCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("位置设置")]
        [SerializeField] private float topMargin = 20f;
        [SerializeField] private float hiddenOffset = 100f; // 隐藏时向上偏移的距离

        [Header("行为设置")]
        [SerializeField] private bool allowInterrupt = true;
        [SerializeField] private float queueDelay = 0.5f;

        [Header("颜色配置")]
        [SerializeField] private Color infoColor = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color successColor = new Color(0.3f, 1f, 0.3f, 1f);
        [SerializeField] private Color turnColor = new Color(0.8f, 0.3f, 1f, 1f);
        [SerializeField] private Color systemColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        #endregion

        #region 私有变量
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

        #region 静态接口
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

        #region Unity生命周期
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

        #region 初始化
        private void Initialize()
        {
            FindCanvas();
            if (messageCanvas == null)
            {
                Debug.LogError("[MessageNotifier] 未找到MessageCanvas，请确保场景中有名为'MessageCanvas'的Canvas或手动分配");
                return;
            }

            if (notificationPanelPrefab == null)
            {
                Debug.LogError("[MessageNotifier] 通知面板预制体未分配，请在Inspector中分配notificationPanelPrefab");
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
            // 设置锚点为顶部居中
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);

            visiblePosition = new Vector2(0f, -topMargin);
            hiddenPosition = new Vector2(0f, hiddenOffset);

            panelRect.anchoredPosition = hiddenPosition;
            panelCanvasGroup.alpha = 0f;
        }
        #endregion

        #region 消息显示
        private void ShowMessage(MessageData messageData)
        {
            // 检查是否已初始化
            if (panelInstance == null)
            {
                Debug.LogError("[MessageNotifier] 系统未正确初始化，无法显示消息");
                return;
            }

            // 高优先级消息可以中断当前消息
            if (isShowing && allowInterrupt && messageData.priority > 0)
            {
                HideMessage();
            }

            if (isShowing || isProcessingQueue)
            {
                // 添加到队列，支持优先级
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

            // 设置内容
            panelText.text = messageData.text;
            panelText.color = GetMessageColor(messageData.type);  // 修改文字颜色而不是背景
            panelInstance.SetActive(true);

            // 下拉动画
            yield return StartCoroutine(SlideAnimation(hiddenPosition, visiblePosition, slideDownDuration, slideDownCurve, true));

            // 显示时间
            yield return new WaitForSeconds(messageData.displayDuration);

            // 上拉动画
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

        #region 控制方法
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

        #region 编辑器测试
#if UNITY_EDITOR
        [Header("编辑器测试")]
        [SerializeField] private string testMessage = "测试消息";
        [SerializeField] private MessageType testMessageType = MessageType.Info;

        [ContextMenu("测试单条消息")]
        private void TestMessage()
        {
            if (Application.isPlaying)
                Show(testMessage, testMessageType);
        }

        [ContextMenu("测试所有类型")]
        private void TestAllTypes()
        {
            if (Application.isPlaying)
                StartCoroutine(TestAllTypesCoroutine());
        }

        [ContextMenu("测试队列")]
        private void TestQueue()
        {
            if (Application.isPlaying)
            {
                Show("消息 1", MessageType.Info);
                Show("消息 2", MessageType.Warning);
                Show("紧急消息", MessageType.Error, 2f, 10);
                Show("消息 3", MessageType.Success);
            }
        }

        private IEnumerator TestAllTypesCoroutine()
        {
            var types = (MessageType[])System.Enum.GetValues(typeof(MessageType));
            foreach (var type in types)
            {
                Show($"{type} 类型测试", type);
                yield return new WaitForSeconds(3.5f);
            }
        }
#endif
        #endregion
    }
}