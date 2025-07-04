using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;
using Core.Network;
using System.Collections;
using Classroom.Player;

namespace UI.Blackboard
{
    /// <summary>
    /// 黑板显示管理器 - 基于手动配置的LayoutGroup系统
    /// 专注于显示逻辑，所有布局组件在Unity编辑器中预先配置
    /// 修改为监听ClassroomManager的摄像机设置完成事件，确保摄像机设置后再显示题目
    /// </summary>
    public class BlackboardDisplayManager : MonoBehaviour
    {
        [Header("预配置的UI组件引用")]
        [SerializeField] private Canvas blackboardCanvas;
        [SerializeField] private RectTransform blackboardArea;
        [SerializeField] private RectTransform questionArea;
        [SerializeField] private RectTransform optionsArea;
        [SerializeField] private RectTransform statusArea;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject optionTemplate;

        [Header("显示配置")]
        [SerializeField] private Color questionColor = Color.black;
        [SerializeField] private Color optionColor = Color.black;
        [SerializeField] private Color statusColor = Color.blue;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static BlackboardDisplayManager Instance { get; private set; }

        // 动态创建的选项文本
        private TextMeshProUGUI[] optionTexts;
        private NetworkQuestionData currentQuestion;
        private bool isDisplaying = false;
        private bool cameraSetupCompleted = false;
        private bool blackboardInitialized = false;

        // 待显示的题目（当摄像机还未设置时）
        private NetworkQuestionData pendingQuestion = null;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeBlackboard();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 订阅ClassroomManager的事件
            SubscribeToClassroomManagerEvents();

            // 延迟查找摄像机，确保玩家摄像机已经生成
            StartCoroutine(FindAndSetCamera());
        }

        private void OnEnable()
        {
            SubscribeToClassroomManagerEvents();
            SubscribeToQuestionManagerEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromClassroomManagerEvents();
            UnsubscribeFromQuestionManagerEvents();
        }

        #region 事件订阅管理

        /// <summary>
        /// 订阅ClassroomManager事件
        /// </summary>
        private void SubscribeToClassroomManagerEvents()
        {
            Classroom.ClassroomManager.OnCameraSetupCompleted += OnCameraSetupCompleted;
            Classroom.ClassroomManager.OnClassroomInitialized += OnClassroomInitialized;
        }

        /// <summary>
        /// 取消订阅ClassroomManager事件
        /// </summary>
        private void UnsubscribeFromClassroomManagerEvents()
        {
            Classroom.ClassroomManager.OnCameraSetupCompleted -= OnCameraSetupCompleted;
            Classroom.ClassroomManager.OnClassroomInitialized -= OnClassroomInitialized;
        }

        /// <summary>
        /// 订阅题目管理器事件
        /// </summary>
        private void SubscribeToQuestionManagerEvents()
        {
            if (NetworkQuestionManagerController.Instance != null)
            {
                LogDebug("已连接到NetworkQuestionManagerController，开始监控题目变化");

                // 启动NQMC题目监控协程
                StartCoroutine(MonitorNQMCQuestionChanges());
            }
            else
            {
                LogDebug("NQMC不可用，尝试订阅NetworkManager事件作为备用");
                if (NetworkManager.Instance != null)
                {
                    NetworkManager.OnQuestionReceived += OnQuestionReceivedFromNetwork;
                    LogDebug("已订阅NetworkManager的OnQuestionReceived事件");
                }
            }
        }

        /// <summary>
        /// 取消订阅题目管理器事件
        /// </summary>
        private void UnsubscribeFromQuestionManagerEvents()
        {
            // 取消订阅NetworkManager的题目事件
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnQuestionReceived -= OnQuestionReceivedFromNetwork;
                LogDebug("已取消订阅NetworkManager的OnQuestionReceived事件");
            }
        }

        /// <summary>
        /// 监控NQMC的题目变化（主要方法）
        /// </summary>
        private IEnumerator MonitorNQMCQuestionChanges()
        {
            NetworkQuestionData lastQuestion = null;

            while (true)
            {
                yield return new WaitForSeconds(0.3f); // 每0.3秒检查一次

                if (NetworkQuestionManagerController.Instance != null)
                {
                    var currentQuestion = NetworkQuestionManagerController.Instance.GetCurrentNetworkQuestion();

                    // 如果发现新题目且不同于上次的题目
                    if (currentQuestion != null && !AreQuestionsEqual(currentQuestion, lastQuestion))
                    {
                        LogDebug($"NQMC题目变化检测到新题目: {currentQuestion.questionText}");
                        DisplayQuestion(currentQuestion);
                        lastQuestion = currentQuestion;
                    }
                    // 如果题目被清空了
                    else if (currentQuestion == null && lastQuestion != null)
                    {
                        LogDebug("NQMC题目被清空，清空黑板显示");
                        ClearDisplay();
                        lastQuestion = null;
                    }
                }
            }
        }

        /// <summary>
        /// 比较两个题目是否相等
        /// </summary>
        private bool AreQuestionsEqual(NetworkQuestionData q1, NetworkQuestionData q2)
        {
            if (q1 == null && q2 == null) return true;
            if (q1 == null || q2 == null) return false;

            return q1.questionText == q2.questionText && q1.questionType == q2.questionType;
        }

        /// <summary>
        /// 响应NetworkManager的题目接收事件（备用方案）
        /// </summary>
        private void OnQuestionReceivedFromNetwork(NetworkQuestionData questionData)
        {
            LogDebug($"从NetworkManager收到题目事件: {questionData?.questionText}");

            if (questionData != null)
            {
                // 显示题目到黑板
                DisplayQuestion(questionData);
            }
        }

        /// <summary>
        /// 响应摄像机设置完成事件
        /// </summary>
        private void OnCameraSetupCompleted()
        {
            LogDebug("收到摄像机设置完成事件");
            cameraSetupCompleted = true;

            // 如果有待显示的题目，现在显示它
            if (pendingQuestion != null)
            {
                DisplayQuestion(pendingQuestion);
                pendingQuestion = null;
            }
        }

        /// <summary>
        /// 响应教室初始化完成事件
        /// </summary>
        private void OnClassroomInitialized()
        {
            LogDebug("收到教室初始化完成事件");
            if (!cameraSetupCompleted)
            {
                StartCoroutine(FindAndSetCamera());
            }
        }

        #endregion

        /// <summary>
        /// 查找并设置动态生成的玩家摄像机
        /// </summary>
        private IEnumerator FindAndSetCamera()
        {
            Camera playerCamera = null;
            float timeoutCounter = 0f;
            float timeout = 10f; // 增加到10秒超时

            LogDebug("开始查找玩家摄像机");

            // 循环查找玩家摄像机
            while (playerCamera == null && timeoutCounter < timeout)
            {
                // 尝试多种可能的摄像机查找方式
                playerCamera = FindPlayerCamera();

                if (playerCamera == null)
                {
                    timeoutCounter += 0.2f;
                    yield return new WaitForSeconds(0.2f);
                }
            }

            // 设置找到的摄像机
            if (playerCamera != null)
            {
                SetBlackboardCamera(playerCamera);
                cameraSetupCompleted = true;

                // 如果有待显示的题目，现在显示它
                if (pendingQuestion != null)
                {
                    LogDebug("摄像机设置完成，显示待处理的题目");
                    DisplayQuestion(pendingQuestion);
                    pendingQuestion = null;
                }
            }
            else
            {
                LogDebug("未能找到玩家摄像机，使用主摄像机作为备用");
                SetBlackboardCamera(Camera.main);
                cameraSetupCompleted = true;
            }
        }

        /// <summary>
        /// 查找玩家摄像机的多种方式（基于PlayerCameraController的逻辑优化）
        /// </summary>
        private Camera FindPlayerCamera()
        {
            // 方式1: 通过ClassroomManager获取（最可靠）
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

            // 方式2: 通过PlayerCameraController组件查找（最可靠）
            var playerCameraControllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in playerCameraControllers)
            {
                if (controller.IsLocalPlayer && controller.PlayerCamera != null)
                {
                    LogDebug($"通过PlayerCameraController找到摄像机: {controller.PlayerCamera.name}");
                    return controller.PlayerCamera;
                }
            }

            // 方式3: 查找名称为"PlayerCamera"的摄像机
            Camera[] allCameras = FindObjectsOfType<Camera>();
            foreach (var cam in allCameras)
            {
                if (cam.gameObject.name == "PlayerCamera" && cam.enabled)
                {
                    LogDebug($"通过名称找到PlayerCamera: {cam.name}");
                    return cam;
                }
            }

            return null;
        }

        /// <summary>
        /// 设置黑板Canvas的摄像机
        /// </summary>
        private void SetBlackboardCamera(Camera camera)
        {
            if (blackboardCanvas != null && camera != null)
            {
                blackboardCanvas.worldCamera = camera;
                LogDebug($"已设置黑板摄像机: {camera.name}");

                // 强制刷新Canvas
                StartCoroutine(RefreshCanvasLayout());
            }
        }

        /// <summary>
        /// 刷新Canvas布局
        /// </summary>
        private IEnumerator RefreshCanvasLayout()
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();

            if (blackboardArea != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(blackboardArea);
            }
        }

        /// <summary>
        /// 初始化黑板（验证组件引用）
        /// </summary>
        private void InitializeBlackboard()
        {
            // 确保模板处于非激活状态
            optionTemplate.SetActive(false);

            // 应用颜色设置
            ApplyColorSettings();

            // 初始清空显示
            ClearDisplay();

            blackboardInitialized = true;
            LogDebug("BlackboardDisplayManager 初始化完成");
        }

        /// <summary>
        /// 应用颜色设置
        /// </summary>
        private void ApplyColorSettings()
        {
            questionText.color = questionColor;
            statusText.color = statusColor;
        }

        /// <summary>
        /// 创建选项文本组件
        /// </summary>
        private void CreateOptionTexts(int count)
        {
            // 清理旧的选项文本
            if (optionTexts != null)
            {
                foreach (var optionText in optionTexts)
                {
                    if (optionText != null)
                    {
                        DestroyImmediate(optionText.gameObject);
                    }
                }
            }

            // 创建新的选项文本
            optionTexts = new TextMeshProUGUI[count];

            for (int i = 0; i < count; i++)
            {
                // 从模板实例化
                GameObject optionObj = Instantiate(optionTemplate, optionsArea);
                optionObj.name = $"Option{i + 1}";
                optionObj.SetActive(true);

                var optionText = optionObj.GetComponent<TextMeshProUGUI>();
                optionTexts[i] = optionText;

                // 应用颜色设置
                if (optionText != null)
                {
                    optionText.color = optionColor;
                }
            }
        }

        /// <summary>
        /// 显示题目（修改为检查摄像机状态）
        /// </summary>
        public void DisplayQuestion(NetworkQuestionData questionData)
        {
            LogDebug($"收到显示题目请求: {questionData.questionText}");

            ExecuteDisplayQuestion(questionData);
        }

        /// <summary>
        /// 执行实际的题目显示逻辑
        /// </summary>
        private void ExecuteDisplayQuestion(NetworkQuestionData questionData)
        {
            LogDebug($"开始显示题目: {questionData.questionText}");

            currentQuestion = questionData;
            isDisplaying = true;

            // 显示题干
            if (questionText != null)
            {
                questionText.text = questionData.questionText;
                questionArea.gameObject.SetActive(true);
            }

            // 根据题型显示内容
            switch (questionData.questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                    DisplayChoiceQuestion(questionData);
                    break;

                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    DisplayTrueFalseQuestion(questionData);
                    break;

                default:
                    DisplayFillQuestion(questionData);
                    break;
            }

            // 显示状态
            UpdateDisplayStatus("请按下鼠标右键作答", statusColor);

            // 强制更新布局
            StartCoroutine(RefreshLayout());
        }

        /// <summary>
        /// 显示选择题
        /// </summary>
        private void DisplayChoiceQuestion(NetworkQuestionData questionData)
        {
            // 创建选项文本
            CreateOptionTexts(questionData.options.Length);

            // 设置选项内容
            for (int i = 0; i < optionTexts.Length; i++)
            {
                optionTexts[i].text = $"{(char)('A' + i)}. {questionData.options[i]}";
            }

            optionsArea.gameObject.SetActive(true);
        }

        /// <summary>
        /// 显示判断题
        /// </summary>
        private void DisplayTrueFalseQuestion(NetworkQuestionData questionData)
        {
            CreateOptionTexts(2);

            optionTexts[0].text = "A. 正确";
            optionTexts[1].text = "B. 错误";

            optionsArea.gameObject.SetActive(true);
        }

        /// <summary>
        /// 显示填空题
        /// </summary>
        private void DisplayFillQuestion(NetworkQuestionData questionData)
        {
            optionsArea.gameObject.SetActive(true);

            if (optionTexts != null)
            {
                foreach (var optionText in optionTexts)
                {
                    if (optionText != null)
                    {
                        DestroyImmediate(optionText.gameObject);
                    }
                }
                optionTexts = null;
            }
        }

        /// <summary>
        /// 更新显示状态
        /// </summary>
        public void UpdateDisplayStatus(string status, Color color)
        {
            LogDebug($"更新状态: {status}");

            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = color;
                statusArea.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 清空显示内容
        /// </summary>
        public void ClearDisplay()
        {
            LogDebug("清空黑板显示");

            currentQuestion = null;
            isDisplaying = false;
            pendingQuestion = null; // 清空待显示的题目

            if (questionText != null)
            {
                questionText.text = "";
            }

            if (statusText != null)
            {
                statusText.text = "";
            }

            // 清理选项文本
            if (optionTexts != null)
            {
                foreach (var optionText in optionTexts)
                {
                    if (optionText != null)
                    {
                        DestroyImmediate(optionText.gameObject);
                    }
                }
                optionTexts = null;
            }

            // 隐藏所有区域
            if (questionArea != null) questionArea.gameObject.SetActive(false);
            if (optionsArea != null) optionsArea.gameObject.SetActive(false);
            if (statusArea != null) statusArea.gameObject.SetActive(false);
        }

        /// <summary>
        /// 强制刷新布局
        /// </summary>
        private IEnumerator RefreshLayout()
        {
            yield return new WaitForEndOfFrame();

            // 强制更新Canvas布局
            Canvas.ForceUpdateCanvases();

            // 重建所有布局
            if (blackboardArea != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(blackboardArea);
            }

            if (optionsArea != null && optionsArea.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(optionsArea);
            }

            LogDebug("布局刷新完成");
        }

        /// <summary>
        /// 获取当前显示的题目
        /// </summary>
        public NetworkQuestionData CurrentQuestion => currentQuestion;

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[BlackboardDisplayManager] {message}");
            }
        }

        #region 清理资源

        private void OnDestroy()
        {
            UnsubscribeFromClassroomManagerEvents();
            UnsubscribeFromQuestionManagerEvents();
        }

        #endregion
    }
}