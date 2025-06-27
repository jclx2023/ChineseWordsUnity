using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Core;
using Core.Network;
using Classroom.Player;
using UI.Blackboard;

namespace UI.Answer
{
    /// <summary>
    /// 沉浸式答题界面管理器 - 简化版本
    /// 职责：显示题目信息，实例化对应的答题界面，传递提交事件
    /// </summary>
    public class AnswerUIManager : MonoBehaviour
    {
        [Header("答题触发设置")]
        [SerializeField] private KeyCode answerTriggerKey = KeyCode.Return;

        [Header("UI组件引用")]
        [SerializeField] private Canvas answerCanvas;
        [SerializeField] private CanvasGroup answerCanvasGroup;
        [SerializeField] private RectTransform answerPanel;
        [SerializeField] private RectTransform contentArea; // 动态加载答题界面的容器

        [Header("题型答题界面预制体")]
        [SerializeField] private GameObject fillBlankUIPrefab;   // 填空题UI
        [SerializeField] private GameObject chooseUIPrefab;      // 选择题UI
        [SerializeField] private GameObject torFUIPrefab;        // 判断题UI

        [Header("题目显示")]
        [SerializeField] private TextMeshProUGUI questionDisplayText; // 题目文本显示

        [Header("动画设置")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static AnswerUIManager Instance { get; private set; }

        // 状态管理
        private enum AnswerUIState
        {
            Hidden,      // 隐藏状态
            FadingIn,    // 渐入中
            Active,      // 激活状态（可交互）
            FadingOut    // 渐出中
        }

        private AnswerUIState currentState = AnswerUIState.Hidden;
        private NetworkQuestionData currentQuestion;
        private GameObject currentAnswerUIInstance; // 当前实例化的答题界面

        // 摄像机控制
        private PlayerCameraController playerCameraController;

        // 事件
        public static event System.Action<string> OnAnswerSubmitted;
        public static event System.Action OnAnswerUIClosed;

        // 属性
        public bool IsAnswerUIVisible => currentState == AnswerUIState.Active || currentState == AnswerUIState.FadingIn;
        public bool CanShowAnswerUI => currentState == AnswerUIState.Hidden && HasValidQuestion() && IsMyTurn();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeAnswerUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            FindPlayerCameraController();
        }

        private void Update()
        {
            HandleInput();
        }

        /// <summary>
        /// 初始化答题UI
        /// </summary>
        private void InitializeAnswerUI()
        {
            // 确保Canvas设置正确
            if (answerCanvas != null)
            {
                answerCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                answerCanvas.sortingOrder = 100;
            }

            // 设置初始状态
            if (answerCanvasGroup != null)
            {
                answerCanvasGroup.alpha = 0f;
                answerCanvasGroup.interactable = false;
                answerCanvasGroup.blocksRaycasts = false;
            }

            // 确保ContentArea存在
            if (contentArea == null)
            {
                LogDebug("警告：ContentArea未设置，尝试自动查找");
                contentArea = answerPanel?.Find("ContentArea")?.GetComponent<RectTransform>();
            }

            // 隐藏答题面板
            if (answerPanel != null)
            {
                answerPanel.gameObject.SetActive(false);
            }

            LogDebug("AnswerUIManager 初始化完成");
        }

        /// <summary>
        /// 查找玩家摄像机控制器
        /// </summary>
        private void FindPlayerCameraController()
        {
            var controllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in controllers)
            {
                if (controller.IsLocalPlayer)
                {
                    playerCameraController = controller;
                    LogDebug($"找到本地玩家摄像机控制器: {controller.name}");
                    break;
                }
            }

            if (playerCameraController == null)
            {
                LogDebug("未找到本地玩家摄像机控制器");
            }
        }

        /// <summary>
        /// 处理输入
        /// </summary>
        private void HandleInput()
        {
            if (Input.GetKeyDown(answerTriggerKey))
            {
                if (currentState == AnswerUIState.Hidden)
                {
                    TryShowAnswerUI();
                }
                else if (currentState == AnswerUIState.Active)
                {
                    HideAnswerUI();
                }
            }

            // Esc键取消答题
            if (currentState == AnswerUIState.Active && Input.GetKeyDown(KeyCode.Escape))
            {
                HideAnswerUI();
            }
        }

        /// <summary>
        /// 尝试显示答题UI
        /// </summary>
        public bool TryShowAnswerUI()
        {
            if (!CanShowAnswerUI)
            {
                LogDebug($"无法显示答题UI - 状态: {currentState}, 有题目: {HasValidQuestion()}, 我的回合: {IsMyTurn()}");
                return false;
            }

            ShowAnswerUI();
            return true;
        }

        /// <summary>
        /// 显示答题UI
        /// </summary>
        private void ShowAnswerUI()
        {
            if (currentState != AnswerUIState.Hidden)
                return;

            LogDebug("显示答题UI");

            currentState = AnswerUIState.FadingIn;

            // 获取当前题目
            currentQuestion = GetCurrentQuestion();
            if (currentQuestion == null)
            {
                LogDebug("无法获取当前题目");
                return;
            }

            // 显示题目信息
            DisplayQuestionInfo(currentQuestion);

            // 实例化对应的答题界面
            InstantiateAnswerUI(currentQuestion.questionType);

            // 禁用摄像机控制
            SetCameraControl(false);

            // 显示UI并开始渐入动画
            answerPanel.gameObject.SetActive(true);
            StartCoroutine(FadeInCoroutine());
        }

        /// <summary>
        /// 隐藏答题UI
        /// </summary>
        public void HideAnswerUI()
        {
            if (currentState != AnswerUIState.Active)
                return;

            LogDebug("隐藏答题UI");

            currentState = AnswerUIState.FadingOut;

            // 启用摄像机控制
            SetCameraControl(true);

            // 开始渐出动画
            StartCoroutine(FadeOutCoroutine());

            // 触发关闭事件
            OnAnswerUIClosed?.Invoke();
        }

        /// <summary>
        /// 显示题目信息
        /// </summary>
        private void DisplayQuestionInfo(NetworkQuestionData question)
        {
            // 显示题目文本
            if (questionDisplayText != null)
            {
                questionDisplayText.text = question.questionText;
            }
        }

        /// <summary>
        /// 根据题型实例化对应的答题界面
        /// </summary>
        private void InstantiateAnswerUI(QuestionType questionType)
        {
            // 清理现有的答题界面
            ClearCurrentAnswerUI();

            GameObject prefabToLoad = GetAnswerUIPrefab(questionType);

            if (prefabToLoad != null && contentArea != null)
            {
                try
                {
                    // 实例化答题界面
                    currentAnswerUIInstance = Instantiate(prefabToLoad, contentArea);

                    // 设置RectTransform以填满ContentArea
                    var rectTransform = currentAnswerUIInstance.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        rectTransform.anchorMin = Vector2.zero;
                        rectTransform.anchorMax = Vector2.one;
                        rectTransform.offsetMin = Vector2.zero;
                        rectTransform.offsetMax = Vector2.zero;
                    }

                    // 传递题目数据给答题界面（通过公共方法或事件）
                    SendQuestionDataToAnswerUI(currentAnswerUIInstance, currentQuestion);

                    // 绑定答题界面的提交事件
                    BindAnswerUIEvents(currentAnswerUIInstance);

                    LogDebug($"成功实例化答题界面: {prefabToLoad.name}");
                }
                catch (System.Exception e)
                {
                    LogDebug($"实例化答题界面失败: {e.Message}");
                }
            }
            else
            {
                LogDebug($"无法找到题型 {questionType} 对应的预制体或ContentArea为空");
            }
        }

        /// <summary>
        /// 获取对应题型的答题UI预制体
        /// </summary>
        private GameObject GetAnswerUIPrefab(QuestionType questionType)
        {
            return questionType switch
            {
                // 填空类题型
                QuestionType.HardFill or QuestionType.SoftFill or
                QuestionType.IdiomChain or QuestionType.TextPinyin => fillBlankUIPrefab,

                // 选择类题型
                QuestionType.ExplanationChoice or QuestionType.SimularWordChoice => chooseUIPrefab,

                // 判断类题型
                QuestionType.SentimentTorF or QuestionType.UsageTorF => torFUIPrefab,

                _ => null
            };
        }

        /// <summary>
        /// 向答题界面传递题目数据
        /// </summary>
        private void SendQuestionDataToAnswerUI(GameObject answerUIInstance, NetworkQuestionData questionData)
        {
            // 方式1：通过组件接口传递数据
            var answerUIComponent = answerUIInstance.GetComponent<MonoBehaviour>();
            if (answerUIComponent != null)
            {
                // 尝试调用SetQuestionData方法（如果存在）
                var setQuestionMethod = answerUIComponent.GetType().GetMethod("SetQuestionData");
                if (setQuestionMethod != null)
                {
                    setQuestionMethod.Invoke(answerUIComponent, new object[] { questionData });
                    LogDebug("通过SetQuestionData方法传递题目数据");
                    return;
                }

                // 尝试调用Initialize方法（如果存在）
                var initializeMethod = answerUIComponent.GetType().GetMethod("Initialize");
                if (initializeMethod != null)
                {
                    initializeMethod.Invoke(answerUIComponent, new object[] { questionData });
                    LogDebug("通过Initialize方法传递题目数据");
                    return;
                }
            }

            // 方式2：通过事件传递数据（如果答题UI组件监听了特定事件）
            LogDebug("使用默认方式传递题目数据");
        }

        /// <summary>
        /// 绑定答题界面的提交事件
        /// </summary>
        private void BindAnswerUIEvents(GameObject answerUIInstance)
        {
            // 对于填空题，查找提交按钮并绑定事件
            if (IsFillBlankType(currentQuestion.questionType))
            {
                Button submitButton = FindSubmitButton(answerUIInstance);
                if (submitButton != null)
                {
                    submitButton.onClick.AddListener(() => OnAnswerUISubmitted(answerUIInstance));
                    LogDebug($"绑定填空题提交按钮事件: {submitButton.name}");
                }
            }
            // 对于选择题和判断题，可以直接点击选项提交，也可以保留提交按钮作为备选
            else
            {
                Button submitButton = FindSubmitButton(answerUIInstance);
                if (submitButton != null)
                {
                    submitButton.onClick.AddListener(() => OnAnswerUISubmitted(answerUIInstance));
                    LogDebug($"绑定选择/判断题提交按钮事件: {submitButton.name}");
                }
            }
        }

        /// <summary>
        /// 查找提交按钮
        /// </summary>
        private Button FindSubmitButton(GameObject answerUIInstance)
        {
            Button[] buttons = answerUIInstance.GetComponentsInChildren<Button>();
            foreach (var btn in buttons)
            {
                if (btn.name.Contains("Submit") || btn.name.Contains("提交") || btn.name.Contains("确认"))
                {
                    return btn;
                }
            }
            return null;
        }

        /// <summary>
        /// 判断是否是填空类题型
        /// </summary>
        private bool IsFillBlankType(QuestionType questionType)
        {
            return questionType == QuestionType.HardFill ||
                   questionType == QuestionType.SoftFill ||
                   questionType == QuestionType.IdiomChain ||
                   questionType == QuestionType.TextPinyin;
        }

        /// <summary>
        /// 答题界面提交事件处理
        /// </summary>
        private void OnAnswerUISubmitted(GameObject answerUIInstance)
        {
            string answer = GetAnswerFromUI(answerUIInstance);

            if (!string.IsNullOrEmpty(answer))
            {
                LogDebug($"收到答题界面提交的答案: {answer}");

                // 触发答案提交事件
                OnAnswerSubmitted?.Invoke(answer);

                // 隐藏答题UI
                HideAnswerUI();
            }
            else
            {
                LogDebug("答题界面返回的答案为空");
            }
        }

        /// <summary>
        /// 从答题界面获取答案
        /// </summary>
        private string GetAnswerFromUI(GameObject answerUIInstance)
        {
            var answerUIComponent = answerUIInstance.GetComponent<MonoBehaviour>();
            if (answerUIComponent != null)
            {
                // 尝试调用GetAnswer方法
                var getAnswerMethod = answerUIComponent.GetType().GetMethod("GetAnswer");
                if (getAnswerMethod != null)
                {
                    var result = getAnswerMethod.Invoke(answerUIComponent, null);
                    return result?.ToString() ?? "";
                }

                // 尝试获取Answer属性
                var answerProperty = answerUIComponent.GetType().GetProperty("Answer");
                if (answerProperty != null)
                {
                    var result = answerProperty.GetValue(answerUIComponent);
                    return result?.ToString() ?? "";
                }
            }

            LogDebug("无法从答题界面获取答案");
            return "";
        }

        /// <summary>
        /// 清理当前答题界面
        /// </summary>
        private void ClearCurrentAnswerUI()
        {
            if (currentAnswerUIInstance != null)
            {
                Destroy(currentAnswerUIInstance);
                currentAnswerUIInstance = null;
            }
        }

        /// <summary>
        /// 控制摄像机
        /// </summary>
        private void SetCameraControl(bool enabled)
        {
            if (playerCameraController != null)
            {
                playerCameraController.SetControlEnabled(enabled);
                LogDebug($"摄像机控制: {(enabled ? "启用" : "禁用")}");
            }
        }

        /// <summary>
        /// 渐入动画
        /// </summary>
        private IEnumerator FadeInCoroutine()
        {
            float elapsed = 0f;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;
                float alpha = fadeCurve.Evaluate(t);

                if (answerCanvasGroup != null)
                {
                    answerCanvasGroup.alpha = alpha;
                }

                yield return null;
            }

            // 完成渐入
            if (answerCanvasGroup != null)
            {
                answerCanvasGroup.alpha = 1f;
                answerCanvasGroup.interactable = true;
                answerCanvasGroup.blocksRaycasts = true;
            }

            currentState = AnswerUIState.Active;
            LogDebug("答题UI渐入完成");
        }

        /// <summary>
        /// 渐出动画
        /// </summary>
        private IEnumerator FadeOutCoroutine()
        {
            if (answerCanvasGroup != null)
            {
                answerCanvasGroup.interactable = false;
                answerCanvasGroup.blocksRaycasts = false;
            }

            float elapsed = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;
                float alpha = 1f - fadeCurve.Evaluate(t);

                if (answerCanvasGroup != null)
                {
                    answerCanvasGroup.alpha = alpha;
                }

                yield return null;
            }

            // 完成渐出
            if (answerCanvasGroup != null)
            {
                answerCanvasGroup.alpha = 0f;
            }

            answerPanel.gameObject.SetActive(false);
            currentState = AnswerUIState.Hidden;

            // 清理当前答题界面
            ClearCurrentAnswerUI();

            LogDebug("答题UI渐出完成");
        }

        /// <summary>
        /// 获取当前题目
        /// </summary>
        private NetworkQuestionData GetCurrentQuestion()
        {
            var nqmc = NetworkQuestionManagerController.Instance;
            if (nqmc != null)
            {
                return nqmc.GetCurrentNetworkQuestion();
            }

            // 备用方案：从BlackboardDisplayManager获取
            var blackboard = BlackboardDisplayManager.Instance;
            if (blackboard != null)
            {
                return blackboard.CurrentQuestion;
            }

            return null;
        }

        /// <summary>
        /// 检查是否有有效题目
        /// </summary>
        private bool HasValidQuestion()
        {
            return GetCurrentQuestion() != null;
        }

        /// <summary>
        /// 检查是否是我的回合
        /// </summary>
        private bool IsMyTurn()
        {
            var nqmc = NetworkQuestionManagerController.Instance;
            return nqmc != null && nqmc.IsMyTurn;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[AnswerUIManager] {message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            ClearCurrentAnswerUI();
        }
    }
}