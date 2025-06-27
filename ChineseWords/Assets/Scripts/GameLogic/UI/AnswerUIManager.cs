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
    /// 沉浸式答题界面管理器
    /// 提供统一的半透明答题UI，支持所有题型
    /// </summary>
    public class AnswerUIManager : MonoBehaviour
    {
        [Header("答题触发设置")]
        [SerializeField] private KeyCode answerTriggerKey = KeyCode.Return;

        [Header("UI组件引用")]
        [SerializeField] private Canvas answerCanvas;
        [SerializeField] private CanvasGroup answerCanvasGroup;
        [SerializeField] private RectTransform answerPanel;
        [SerializeField] private Button submitButton;

        [Header("输入类UI")]
        [SerializeField] private GameObject inputUIPanel;
        [SerializeField] private TMP_InputField answerInputField;

        [Header("选项类UI")]
        [SerializeField] private GameObject optionsUIPanel;
        [SerializeField] private Transform optionsContainer;
        [SerializeField] private GameObject optionButtonPrefab;

        [Header("提示文本")]
        [SerializeField] private TextMeshProUGUI instructionText;

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
        private string selectedAnswer = "";
        private int selectedOptionIndex = -1;
        private Button[] optionButtons;

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
                answerCanvas.sortingOrder = 100; // 确保在最前面
            }

            // 设置初始状态
            if (answerCanvasGroup != null)
            {
                answerCanvasGroup.alpha = 0f;
                answerCanvasGroup.interactable = false;
                answerCanvasGroup.blocksRaycasts = false;
            }

            // 绑定提交按钮
            if (submitButton != null)
            {
                submitButton.onClick.AddListener(OnSubmitButtonClicked);
            }

            // 隐藏所有UI面板
            SetPanelVisibility(false, false);

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

            // 快捷键支持
            if (currentState == AnswerUIState.Active)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    HideAnswerUI();
                }
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    // 在输入框中回车时提交答案
                    if (IsInputType() && answerInputField != null && answerInputField.isFocused)
                    {
                        OnSubmitButtonClicked();
                    }
                }
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
                // TODO: 后续添加提示信息显示
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

            // 设置UI内容
            SetupUIForQuestion(currentQuestion);

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
        /// 为题目设置UI
        /// </summary>
        private void SetupUIForQuestion(NetworkQuestionData question)
        {
            bool isInput = IsInputType(question.questionType);
            bool isOptions = IsOptionType(question.questionType);

            // 设置面板可见性
            SetPanelVisibility(isInput, isOptions);

            if (isInput)
            {
                SetupInputUI();
            }
            else if (isOptions)
            {
                SetupOptionsUI(question.options);
            }

            // 设置提示文本
            SetInstructionText();

            // 重置答案状态
            ResetAnswerState();
        }

        /// <summary>
        /// 设置输入类UI
        /// </summary>
        private void SetupInputUI()
        {
            if (answerInputField != null)
            {
                answerInputField.text = "";
                answerInputField.placeholder.GetComponent<TextMeshProUGUI>().text = "请输入答案...";
            }
        }

        /// <summary>
        /// 设置选项类UI
        /// </summary>
        private void SetupOptionsUI(string[] options)
        {
            // 清理现有选项按钮
            ClearOptionButtons();

            if (options == null || options.Length == 0)
                return;

            // 创建选项按钮
            optionButtons = new Button[options.Length];

            for (int i = 0; i < options.Length; i++)
            {
                GameObject optionObj = Instantiate(optionButtonPrefab, optionsContainer);
                Button optionButton = optionObj.GetComponent<Button>();
                TextMeshProUGUI optionText = optionObj.GetComponentInChildren<TextMeshProUGUI>();

                if (optionText != null)
                {
                    optionText.text = $"{(char)('A' + i)}. {options[i]}";
                }

                optionButtons[i] = optionButton;

                // 绑定点击事件
                int index = i; // 避免闭包问题
                optionButton.onClick.AddListener(() => OnOptionSelected(index));
            }
        }

        /// <summary>
        /// 选项被选择
        /// </summary>
        private void OnOptionSelected(int index)
        {
            if (currentState != AnswerUIState.Active || optionButtons == null)
                return;

            selectedOptionIndex = index;

            // 更新选项按钮的视觉状态
            for (int i = 0; i < optionButtons.Length; i++)
            {
                var colors = optionButtons[i].colors;
                colors.normalColor = (i == index) ? Color.yellow : Color.white;
                optionButtons[i].colors = colors;
            }

            // 设置选中的答案
            if (currentQuestion != null && currentQuestion.options != null && index < currentQuestion.options.Length)
            {
                selectedAnswer = currentQuestion.options[index];
            }

            LogDebug($"选择了选项 {index}: {selectedAnswer}");
        }

        /// <summary>
        /// 提交按钮点击
        /// </summary>
        private void OnSubmitButtonClicked()
        {
            if (currentState != AnswerUIState.Active)
                return;

            string answer = GetCurrentAnswer();

            if (string.IsNullOrEmpty(answer))
            {
                LogDebug("答案为空，无法提交");
                return;
            }

            LogDebug($"提交答案: {answer}");

            // 触发答案提交事件
            OnAnswerSubmitted?.Invoke(answer);

            // 隐藏UI
            HideAnswerUI();
        }

        /// <summary>
        /// 获取当前答案
        /// </summary>
        private string GetCurrentAnswer()
        {
            if (IsInputType())
            {
                return answerInputField?.text?.Trim() ?? "";
            }
            else if (IsOptionType())
            {
                return selectedAnswer;
            }

            return "";
        }

        /// <summary>
        /// 设置面板可见性
        /// </summary>
        private void SetPanelVisibility(bool showInput, bool showOptions)
        {
            if (inputUIPanel != null)
                inputUIPanel.SetActive(showInput);

            if (optionsUIPanel != null)
                optionsUIPanel.SetActive(showOptions);
        }

        /// <summary>
        /// 设置提示文本
        /// </summary>
        private void SetInstructionText()
        {
            if (instructionText != null)
            {
                string instruction = IsInputType() ?
                    "请输入答案，然后点击提交" :
                    "请选择答案，然后点击提交";
                instruction += $"\n\n按 {answerTriggerKey} 键可取消答题";

                instructionText.text = instruction;
            }
        }

        /// <summary>
        /// 重置答案状态
        /// </summary>
        private void ResetAnswerState()
        {
            selectedAnswer = "";
            selectedOptionIndex = -1;
        }

        /// <summary>
        /// 清理选项按钮
        /// </summary>
        private void ClearOptionButtons()
        {
            if (optionsContainer != null)
            {
                foreach (Transform child in optionsContainer)
                {
                    Destroy(child.gameObject);
                }
            }
            optionButtons = null;
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

            // 自动聚焦到输入框
            if (IsInputType() && answerInputField != null)
            {
                answerInputField.ActivateInputField();
            }

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
        /// 判断是否是输入类题型
        /// </summary>
        private bool IsInputType()
        {
            return currentQuestion != null && IsInputType(currentQuestion.questionType);
        }

        /// <summary>
        /// 判断是否是选项类题型
        /// </summary>
        private bool IsOptionType()
        {
            return currentQuestion != null && IsOptionType(currentQuestion.questionType);
        }

        /// <summary>
        /// 判断是否是输入类题型
        /// </summary>
        private static bool IsInputType(QuestionType type)
        {
            return type == QuestionType.HardFill ||
                   type == QuestionType.SoftFill ||
                   type == QuestionType.IdiomChain ||
                   type == QuestionType.TextPinyin;
        }

        /// <summary>
        /// 判断是否是选项类题型
        /// </summary>
        private static bool IsOptionType(QuestionType type)
        {
            return type == QuestionType.ExplanationChoice ||
                   type == QuestionType.SimularWordChoice ||
                   type == QuestionType.SentimentTorF ||
                   type == QuestionType.UsageTorF;
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
            ClearOptionButtons();

            if (submitButton != null)
            {
                submitButton.onClick.RemoveAllListeners();
            }
        }
    }
}