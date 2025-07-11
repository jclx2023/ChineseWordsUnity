using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Core;
using Core.Network;
using Classroom.Player;
using UI.Blackboard;
using UI.MessageSystem;

namespace UI.Answer
{
    /// <summary>
    /// 沉浸式答题界面管理器 - 简化版本
    /// 职责：显示题目信息，实例化对应的答题界面，传递提交事件
    /// 修改为监听ClassroomManager的摄像机设置完成事件
    /// </summary>
    public class AnswerUIManager : MonoBehaviour
    {
        [Header("答题触发设置")]
        [SerializeField] private KeyCode answerTriggerKey = KeyCode.Mouse1;

        [Header("UI组件引用")]
        [SerializeField] private Canvas answerCanvas;
        [SerializeField] private CanvasGroup answerCanvasGroup;
        [SerializeField] private RectTransform answerPanel;
        [SerializeField] private RectTransform contentArea; // 动态加载答题界面的容器

        [Header("色相设置")]
        [SerializeField] private Image answerImage; // 需要变色的Image
        [SerializeField] private bool enableRandomHue = true; // 色相随机化开关
        [SerializeField] private Material hueShiftMaterial; // 色相偏移材质

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
        private bool cameraControllerReady = false;

        // 色相管理
        private float currentRoundHue = 0f;
        private float currentRoundSaturation = 1f;
        private bool hasRoundColorSet = false;
        private Material answerImageMaterial; // 运行时材质实例

        // 事件
        public static event System.Action OnAnswerUIClosed;

        // 属性
        public bool IsAnswerUIVisible => currentState == AnswerUIState.Active || currentState == AnswerUIState.FadingIn;
        public bool CanShowAnswerUI => currentState == AnswerUIState.Hidden && HasValidQuestion() && IsMyTurn() && cameraControllerReady;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeAnswerUI();
                InitializeHueMaterial();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 只订阅ClassroomManager的事件，不再过早查找摄像机
            // 摄像机查找将在OnCameraSetupCompleted事件中进行
        }

        private void OnEnable()
        {
            SubscribeToClassroomManagerEvents();
            SubscribeToGameEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromClassroomManagerEvents();
            UnsubscribeFromGameEvents();
        }

        private void Update()
        {
            HandleInput();
        }

        #region 色相管理

        /// <summary>
        /// 初始化色相材质
        /// </summary>
        private void InitializeHueMaterial()
        {
            if (answerImage != null && hueShiftMaterial != null)
            {
                // 创建材质实例
                answerImageMaterial = new Material(hueShiftMaterial);
                answerImage.material = answerImageMaterial;
                LogDebug("色相材质初始化完成");
            }
            else
            {
                LogDebug("警告：AnswerImage或HueShiftMaterial未设置");
            }
        }

        /// <summary>
        /// 为新回合生成随机色相参数
        /// </summary>
        private void GenerateNewRoundColor()
        {
            if (!enableRandomHue || answerImageMaterial == null) return;

            currentRoundHue = Random.Range(0f, 1f);
            currentRoundSaturation = Random.Range(0.6f, 0.8f);
            hasRoundColorSet = true;

            LogDebug($"生成新回合色相: H={currentRoundHue:F2}, S={currentRoundSaturation:F2}");
        }

        /// <summary>
        /// 应用当前回合色相到材质
        /// </summary>
        private void ApplyRoundColor()
        {
            if (!enableRandomHue || answerImageMaterial == null) return;

            if (!hasRoundColorSet)
            {
                GenerateNewRoundColor();
            }

            // 设置材质参数
            answerImageMaterial.SetFloat("_HueShift", currentRoundHue);
            answerImageMaterial.SetFloat("_Saturation", currentRoundSaturation);
            answerImageMaterial.SetFloat("_Brightness", 1f);

            LogDebug($"应用色相参数: H={currentRoundHue:F2}, S={currentRoundSaturation:F2}");
        }

        #endregion

        #region 游戏事件订阅

        /// <summary>
        /// 订阅游戏事件
        /// </summary>
        private void SubscribeToGameEvents()
        {
            // 监听回合变化事件
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerTurnChanged += OnPlayerTurnChanged;
                NetworkManager.OnQuestionReceived += OnNewQuestionReceived;
            }
        }

        /// <summary>
        /// 取消订阅游戏事件
        /// </summary>
        private void UnsubscribeFromGameEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerTurnChanged -= OnPlayerTurnChanged;
                NetworkManager.OnQuestionReceived -= OnNewQuestionReceived;
            }
        }

        /// <summary>
        /// 回合变化时的处理
        /// </summary>
        private void OnPlayerTurnChanged(ushort newPlayerId)
        {
            // 每次回合变化时生成新颜色
            GenerateNewRoundColor();
            LogDebug($"回合变化，生成新颜色，当前玩家: {newPlayerId}");
        }

        /// <summary>
        /// 新题目接收时的处理
        /// </summary>
        private void OnNewQuestionReceived(NetworkQuestionData questionData)
        {
            // 新题目时也生成新颜色（备用方案）
            if (!hasRoundColorSet)
            {
                GenerateNewRoundColor();
                LogDebug("接收新题目，生成回合颜色");
            }
        }

        #endregion

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
        /// 响应摄像机设置完成事件
        /// </summary>
        private void OnCameraSetupCompleted()
        {
            LogDebug("收到摄像机设置完成事件，开始查找摄像机控制器");
            StartCoroutine(FindPlayerCameraControllerCoroutine());
        }

        /// <summary>
        /// 响应教室初始化完成事件（备用方案）
        /// </summary>
        private void OnClassroomInitialized()
        {
            if (!cameraControllerReady)
            {
                LogDebug("教室初始化完成，尝试查找摄像机控制器（备用方案）");
                StartCoroutine(FindPlayerCameraControllerCoroutine());
            }
        }

        #endregion

        #region 摄像机控制器查找

        /// <summary>
        /// 协程方式查找玩家摄像机控制器
        /// </summary>
        private IEnumerator FindPlayerCameraControllerCoroutine()
        {
            LogDebug("开始查找玩家摄像机控制器");

            float timeout = 5f; // 5秒超时
            float elapsed = 0f;

            while (elapsed < timeout && playerCameraController == null)
            {
                playerCameraController = FindPlayerCameraController();

                if (playerCameraController != null)
                {
                    cameraControllerReady = true;
                    LogDebug($"成功找到摄像机控制器: {playerCameraController.name}");
                    break;
                }

                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>
        /// 查找玩家摄像机控制器
        /// </summary>
        private PlayerCameraController FindPlayerCameraController()
        {
            // 方式1: 通过ClassroomManager获取（推荐方式）
            var classroomManager = FindObjectOfType<Classroom.ClassroomManager>();
            if (classroomManager != null && classroomManager.IsInitialized)
            {
                var cameraController = classroomManager.GetLocalPlayerCameraController();
                if (cameraController != null)
                {
                    LogDebug($"通过ClassroomManager找到摄像机控制器: {cameraController.name}");
                    return cameraController;
                }
            }

            // 方式2: 遍历所有PlayerCameraController找到本地玩家的
            var controllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in controllers)
            {
                if (controller.IsLocalPlayer)
                {
                    LogDebug($"通过遍历找到本地玩家摄像机控制器: {controller.name}");
                    return controller;
                }
            }

            // 方式3: 查找包含"Local"、"Player"等关键词的摄像机控制器
            foreach (var controller in controllers)
            {
                if (controller.gameObject.name.Contains("Local") ||
                    controller.gameObject.name.Contains("Player"))
                {
                    LogDebug($"通过关键词找到摄像机控制器: {controller.name}");
                    return controller;
                }
            }

            return null;
        }

        #endregion

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

            // 隐藏答题面板
            if (answerPanel != null)
            {
                answerPanel.gameObject.SetActive(false);
            }

            LogDebug("AnswerUIManager 初始化完成");
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
            if (currentState == AnswerUIState.Active && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Mouse1)))
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
                LogDebug($"无法显示答题UI - 状态: {currentState}, 有题目: {HasValidQuestion()}, 我的回合: {IsMyTurn()}, 摄像机就绪: {cameraControllerReady}");
                MessageNotifier.Show("现在没法答题哦", MessageType.Info);
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

            // 应用回合颜色
            ApplyRoundColor();

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
        }

        /// <summary>
        /// 获取对应题型的答题UI预制体
        /// </summary>
        private GameObject GetAnswerUIPrefab(QuestionType questionType)
        {
            return questionType switch
            {
                QuestionType.HardFill or QuestionType.SoftFill or
                QuestionType.IdiomChain or QuestionType.TextPinyin => fillBlankUIPrefab,

                QuestionType.ExplanationChoice or QuestionType.SimularWordChoice => chooseUIPrefab,

                QuestionType.SentimentTorF or QuestionType.UsageTorF => torFUIPrefab,

                _ => null
            };
        }

        /// <summary>
        /// 向答题界面传递题目数据
        /// </summary>
        private void SendQuestionDataToAnswerUI(GameObject answerUIInstance, NetworkQuestionData questionData)
        {
            LogDebug($"开始向答题界面传递数据: {questionData.questionType}");

            // 根据题型使用不同的数据传递方式
            switch (questionData.questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                    SetupChoiceUI(answerUIInstance, questionData);
                    break;

                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    SetupTrueFalseUI(answerUIInstance, questionData);
                    break;

                case QuestionType.HardFill:
                case QuestionType.SoftFill:
                case QuestionType.IdiomChain:
                case QuestionType.TextPinyin:
                    SetupFillBlankUI(answerUIInstance, questionData);
                    break;

                default:
                    LogDebug($"未知题型: {questionData.questionType}");
                    break;
            }
        }

        /// <summary>
        /// 设置选择题UI
        /// </summary>
        private void SetupChoiceUI(GameObject choiceUIInstance, NetworkQuestionData questionData)
        {
            LogDebug("设置选择题UI");

            if (questionData.options == null || questionData.options.Length == 0)
            {
                LogDebug("选择题选项为空");
                return;
            }

            // 查找选项按钮 OptionButton1-4
            for (int i = 0; i < questionData.options.Length && i < 4; i++)
            {
                string buttonName = $"OptionButton{i + 1}";
                Transform buttonTransform = choiceUIInstance.transform.Find(buttonName);

                if (buttonTransform != null)
                {
                    var button = buttonTransform.GetComponent<Button>();
                    var textComponent = buttonTransform.GetComponentInChildren<TextMeshProUGUI>();

                    if (textComponent != null)
                    {
                        string optionText = $"{(char)('A' + i)}. {questionData.options[i]}";
                        textComponent.text = optionText;
                        LogDebug($"设置选项按钮 {buttonName}: {optionText}");
                    }

                    if (button != null)
                    {
                        // 绑定点击事件
                        string optionAnswer = questionData.options[i];
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => OnOptionSelected(optionAnswer, choiceUIInstance));
                        LogDebug($"绑定选项按钮 {buttonName} 点击事件: {optionAnswer}");
                    }
                }
                else
                {
                    LogDebug($"未找到选项按钮: {buttonName}");
                }
            }
        }

        /// <summary>
        /// 设置判断题UI
        /// </summary>
        private void SetupTrueFalseUI(GameObject torFUIInstance, NetworkQuestionData questionData)
        {
            LogDebug("设置判断题UI");

            // 查找TrueButton和FalseButton
            Transform trueButtonTransform = torFUIInstance.transform.Find("TrueButton");
            Transform falseButtonTransform = torFUIInstance.transform.Find("FalseButton");

            // 根据题型和选项内容决定按钮显示文本
            string trueButtonText = "A. 正确";
            string falseButtonText = "B. 错误";

            if (questionData.options != null && questionData.options.Length >= 2)
            {
                trueButtonText = $"A. {questionData.options[0]}";
                falseButtonText = $"B. {questionData.options[1]}";
            }

            if (trueButtonTransform != null)
            {
                var trueButton = trueButtonTransform.GetComponent<Button>();
                var trueText = trueButtonTransform.GetComponentInChildren<TextMeshProUGUI>();

                if (trueText != null)
                {
                    trueText.text = trueButtonText;
                }

                if (trueButton != null)
                {
                    trueButton.onClick.RemoveAllListeners();
                    trueButton.onClick.AddListener(() => OnTrueFalseOptionSelected(0, questionData, torFUIInstance));
                    LogDebug($"绑定TrueButton点击事件: {trueButtonText}");
                }
            }
            else
            {
                LogDebug("未找到TrueButton");
            }

            if (falseButtonTransform != null)
            {
                var falseButton = falseButtonTransform.GetComponent<Button>();
                var falseText = falseButtonTransform.GetComponentInChildren<TextMeshProUGUI>();

                if (falseText != null)
                {
                    falseText.text = falseButtonText;
                }

                if (falseButton != null)
                {
                    falseButton.onClick.RemoveAllListeners();
                    falseButton.onClick.AddListener(() => OnTrueFalseOptionSelected(1, questionData, torFUIInstance));
                    LogDebug($"绑定FalseButton点击事件: {falseButtonText}");
                }
            }
            else
            {
                LogDebug("未找到FalseButton");
            }
        }
        /// <summary>
        /// 判断题选项被选中时的处理（新增方法 - 支持多种判断题类型）
        /// </summary>
        private void OnTrueFalseOptionSelected(int optionIndex, NetworkQuestionData questionData, GameObject answerUIInstance)
        {
            LogDebug($"用户选择了判断题选项索引: {optionIndex}");

            string answerToSubmit = "";

            // 根据具体题型确定提交策略
            switch (questionData.questionType)
            {
                case QuestionType.SentimentTorF:
                    // SentimentTorF：提交选项内容（如 "褒义", "贬义"）
                    if (optionIndex >= 0 && optionIndex < questionData.options.Length)
                    {
                        answerToSubmit = questionData.options[optionIndex];
                        LogDebug($"SentimentTorF提交选项内容: {answerToSubmit}");
                    }
                    break;

                case QuestionType.UsageTorF:
                    // UsageTorF：也提交选项内容（如 "正确", "错误"），因为验证器支持标准化
                    if (optionIndex >= 0 && optionIndex < questionData.options.Length)
                    {
                        answerToSubmit = questionData.options[optionIndex];
                        LogDebug($"UsageTorF提交选项内容: {answerToSubmit}");
                    }
                    break;

                default:
                    // 其他判断题类型：提交标准化的 true/false
                    answerToSubmit = optionIndex == 0 ? "true" : "false";
                    LogDebug($"标准判断题提交: {answerToSubmit}");
                    break;
            }

            // 验证答案不为空
            if (string.IsNullOrEmpty(answerToSubmit))
            {
                LogDebug($"无效的选项索引: {optionIndex}，题型: {questionData.questionType}");
                return;
            }

                NetworkManager.Instance.SubmitAnswer(answerToSubmit);
                LogDebug($"通过NetworkManager提交答案: {answerToSubmit}");

            HideAnswerUI();
        }

        /// <summary>
        /// 设置填空题UI
        /// </summary>
        private void SetupFillBlankUI(GameObject fillBlankUIInstance, NetworkQuestionData questionData)
        {
            LogDebug("设置填空题UI");

            // 查找answerinput输入框
            Transform inputTransform = fillBlankUIInstance.transform.Find("AnswerInput");
            if (inputTransform != null)
            {
                var inputField = inputTransform.GetComponent<TMP_InputField>();
                if (inputField != null)
                {
                    inputField.text = "";
                    inputField.placeholder.GetComponent<TextMeshProUGUI>().text = "请输入答案...";
                    LogDebug("设置输入框成功");
                }
            }

            // 查找submitbutton提交按钮
            Transform submitTransform = fillBlankUIInstance.transform.Find("SubmitButton");
            if (submitTransform != null)
            {
                var submitButton = submitTransform.GetComponent<Button>();
                if (submitButton != null)
                {
                    submitButton.onClick.RemoveAllListeners();
                    submitButton.onClick.AddListener(() => OnFillBlankSubmit(fillBlankUIInstance));
                    LogDebug("绑定填空题提交按钮");
                }
            }
        }

        /// <summary>
        /// 选项被选中时的处理
        /// </summary>
        private void OnOptionSelected(string selectedOption, GameObject answerUIInstance)
        {
            NetworkManager.Instance.SubmitAnswer(selectedOption);
            LogDebug($"通过NetworkManager提交答案: {selectedOption}");

            HideAnswerUI();
        }

        /// <summary>
        /// 填空题提交处理
        /// </summary>
        private void OnFillBlankSubmit(GameObject fillBlankUIInstance)
        {
            LogDebug("填空题提交按钮被点击");

            // 获取输入框内容
            Transform inputTransform = fillBlankUIInstance.transform.Find("AnswerInput");
            if (inputTransform != null)
            {
                var inputField = inputTransform.GetComponent<TMP_InputField>();
                if (inputField != null)
                {
                    string answer = inputField.text.Trim();
                    LogDebug($"填空题答案: '{answer}'");

                    // 直接调用NetworkManager提交答案，与其他题型保持一致
                    if (NetworkManager.Instance != null)
                    {
                        NetworkManager.Instance.SubmitAnswer(answer);
                        LogDebug($"通过NetworkManager提交答案: {answer}");
                    }
                    else
                    {
                        LogDebug("NetworkManager.Instance 为空，无法提交答案");
                    }

                    HideAnswerUI();
                }
            }
        }

        /// <summary>
        /// 绑定答题界面的提交事件
        /// </summary>
        private void BindAnswerUIEvents(GameObject answerUIInstance)
        {
            LogDebug($"绑定答题界面事件，题型: {currentQuestion.questionType}");

            // 事件绑定已经在SetupXXXUI方法中完成
            // 这里只需要记录日志
            LogDebug("答题界面事件绑定完成");
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
            else
            {
                LogDebug("摄像机控制器不可用，无法控制摄像机");
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
            UnsubscribeFromClassroomManagerEvents();
            UnsubscribeFromGameEvents();

            // 清理材质实例
            if (answerImageMaterial != null)
            {
                DestroyImmediate(answerImageMaterial);
                answerImageMaterial = null;
            }
        }
    }
}