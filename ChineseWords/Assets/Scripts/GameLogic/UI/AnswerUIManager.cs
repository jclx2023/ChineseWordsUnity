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
    /// ����ʽ������������
    /// �ṩͳһ�İ�͸������UI��֧����������
    /// </summary>
    public class AnswerUIManager : MonoBehaviour
    {
        [Header("���ⴥ������")]
        [SerializeField] private KeyCode answerTriggerKey = KeyCode.Return;

        [Header("UI�������")]
        [SerializeField] private Canvas answerCanvas;
        [SerializeField] private CanvasGroup answerCanvasGroup;
        [SerializeField] private RectTransform answerPanel;
        [SerializeField] private Button submitButton;

        [Header("������UI")]
        [SerializeField] private GameObject inputUIPanel;
        [SerializeField] private TMP_InputField answerInputField;

        [Header("ѡ����UI")]
        [SerializeField] private GameObject optionsUIPanel;
        [SerializeField] private Transform optionsContainer;
        [SerializeField] private GameObject optionButtonPrefab;

        [Header("��ʾ�ı�")]
        [SerializeField] private TextMeshProUGUI instructionText;

        [Header("��������")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static AnswerUIManager Instance { get; private set; }

        // ״̬����
        private enum AnswerUIState
        {
            Hidden,      // ����״̬
            FadingIn,    // ������
            Active,      // ����״̬���ɽ�����
            FadingOut    // ������
        }

        private AnswerUIState currentState = AnswerUIState.Hidden;
        private NetworkQuestionData currentQuestion;
        private string selectedAnswer = "";
        private int selectedOptionIndex = -1;
        private Button[] optionButtons;

        // ���������
        private PlayerCameraController playerCameraController;

        // �¼�
        public static event System.Action<string> OnAnswerSubmitted;
        public static event System.Action OnAnswerUIClosed;

        // ����
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
        /// ��ʼ������UI
        /// </summary>
        private void InitializeAnswerUI()
        {
            // ȷ��Canvas������ȷ
            if (answerCanvas != null)
            {
                answerCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                answerCanvas.sortingOrder = 100; // ȷ������ǰ��
            }

            // ���ó�ʼ״̬
            if (answerCanvasGroup != null)
            {
                answerCanvasGroup.alpha = 0f;
                answerCanvasGroup.interactable = false;
                answerCanvasGroup.blocksRaycasts = false;
            }

            // ���ύ��ť
            if (submitButton != null)
            {
                submitButton.onClick.AddListener(OnSubmitButtonClicked);
            }

            // ��������UI���
            SetPanelVisibility(false, false);

            LogDebug("AnswerUIManager ��ʼ�����");
        }

        /// <summary>
        /// ������������������
        /// </summary>
        private void FindPlayerCameraController()
        {
            var controllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in controllers)
            {
                if (controller.IsLocalPlayer)
                {
                    playerCameraController = controller;
                    LogDebug($"�ҵ�������������������: {controller.name}");
                    break;
                }
            }

            if (playerCameraController == null)
            {
                LogDebug("δ�ҵ�������������������");
            }
        }

        /// <summary>
        /// ��������
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

            // ��ݼ�֧��
            if (currentState == AnswerUIState.Active)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    HideAnswerUI();
                }
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    // ��������лس�ʱ�ύ��
                    if (IsInputType() && answerInputField != null && answerInputField.isFocused)
                    {
                        OnSubmitButtonClicked();
                    }
                }
            }
        }

        /// <summary>
        /// ������ʾ����UI
        /// </summary>
        public bool TryShowAnswerUI()
        {
            if (!CanShowAnswerUI)
            {
                LogDebug($"�޷���ʾ����UI - ״̬: {currentState}, ����Ŀ: {HasValidQuestion()}, �ҵĻغ�: {IsMyTurn()}");
                // TODO: ���������ʾ��Ϣ��ʾ
                return false;
            }

            ShowAnswerUI();
            return true;
        }

        /// <summary>
        /// ��ʾ����UI
        /// </summary>
        private void ShowAnswerUI()
        {
            if (currentState != AnswerUIState.Hidden)
                return;

            LogDebug("��ʾ����UI");

            currentState = AnswerUIState.FadingIn;

            // ��ȡ��ǰ��Ŀ
            currentQuestion = GetCurrentQuestion();
            if (currentQuestion == null)
            {
                LogDebug("�޷���ȡ��ǰ��Ŀ");
                return;
            }

            // ����UI����
            SetupUIForQuestion(currentQuestion);

            // �������������
            SetCameraControl(false);

            // ��ʾUI����ʼ���붯��
            answerPanel.gameObject.SetActive(true);
            StartCoroutine(FadeInCoroutine());
        }

        /// <summary>
        /// ���ش���UI
        /// </summary>
        public void HideAnswerUI()
        {
            if (currentState != AnswerUIState.Active)
                return;

            LogDebug("���ش���UI");

            currentState = AnswerUIState.FadingOut;

            // �������������
            SetCameraControl(true);

            // ��ʼ��������
            StartCoroutine(FadeOutCoroutine());

            // �����ر��¼�
            OnAnswerUIClosed?.Invoke();
        }

        /// <summary>
        /// Ϊ��Ŀ����UI
        /// </summary>
        private void SetupUIForQuestion(NetworkQuestionData question)
        {
            bool isInput = IsInputType(question.questionType);
            bool isOptions = IsOptionType(question.questionType);

            // �������ɼ���
            SetPanelVisibility(isInput, isOptions);

            if (isInput)
            {
                SetupInputUI();
            }
            else if (isOptions)
            {
                SetupOptionsUI(question.options);
            }

            // ������ʾ�ı�
            SetInstructionText();

            // ���ô�״̬
            ResetAnswerState();
        }

        /// <summary>
        /// ����������UI
        /// </summary>
        private void SetupInputUI()
        {
            if (answerInputField != null)
            {
                answerInputField.text = "";
                answerInputField.placeholder.GetComponent<TextMeshProUGUI>().text = "�������...";
            }
        }

        /// <summary>
        /// ����ѡ����UI
        /// </summary>
        private void SetupOptionsUI(string[] options)
        {
            // ��������ѡ�ť
            ClearOptionButtons();

            if (options == null || options.Length == 0)
                return;

            // ����ѡ�ť
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

                // �󶨵���¼�
                int index = i; // ����հ�����
                optionButton.onClick.AddListener(() => OnOptionSelected(index));
            }
        }

        /// <summary>
        /// ѡ�ѡ��
        /// </summary>
        private void OnOptionSelected(int index)
        {
            if (currentState != AnswerUIState.Active || optionButtons == null)
                return;

            selectedOptionIndex = index;

            // ����ѡ�ť���Ӿ�״̬
            for (int i = 0; i < optionButtons.Length; i++)
            {
                var colors = optionButtons[i].colors;
                colors.normalColor = (i == index) ? Color.yellow : Color.white;
                optionButtons[i].colors = colors;
            }

            // ����ѡ�еĴ�
            if (currentQuestion != null && currentQuestion.options != null && index < currentQuestion.options.Length)
            {
                selectedAnswer = currentQuestion.options[index];
            }

            LogDebug($"ѡ����ѡ�� {index}: {selectedAnswer}");
        }

        /// <summary>
        /// �ύ��ť���
        /// </summary>
        private void OnSubmitButtonClicked()
        {
            if (currentState != AnswerUIState.Active)
                return;

            string answer = GetCurrentAnswer();

            if (string.IsNullOrEmpty(answer))
            {
                LogDebug("��Ϊ�գ��޷��ύ");
                return;
            }

            LogDebug($"�ύ��: {answer}");

            // �������ύ�¼�
            OnAnswerSubmitted?.Invoke(answer);

            // ����UI
            HideAnswerUI();
        }

        /// <summary>
        /// ��ȡ��ǰ��
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
        /// �������ɼ���
        /// </summary>
        private void SetPanelVisibility(bool showInput, bool showOptions)
        {
            if (inputUIPanel != null)
                inputUIPanel.SetActive(showInput);

            if (optionsUIPanel != null)
                optionsUIPanel.SetActive(showOptions);
        }

        /// <summary>
        /// ������ʾ�ı�
        /// </summary>
        private void SetInstructionText()
        {
            if (instructionText != null)
            {
                string instruction = IsInputType() ?
                    "������𰸣�Ȼ�����ύ" :
                    "��ѡ��𰸣�Ȼ�����ύ";
                instruction += $"\n\n�� {answerTriggerKey} ����ȡ������";

                instructionText.text = instruction;
            }
        }

        /// <summary>
        /// ���ô�״̬
        /// </summary>
        private void ResetAnswerState()
        {
            selectedAnswer = "";
            selectedOptionIndex = -1;
        }

        /// <summary>
        /// ����ѡ�ť
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
        /// ���������
        /// </summary>
        private void SetCameraControl(bool enabled)
        {
            if (playerCameraController != null)
            {
                playerCameraController.SetControlEnabled(enabled);
                LogDebug($"���������: {(enabled ? "����" : "����")}");
            }
        }

        /// <summary>
        /// ���붯��
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

            // ��ɽ���
            if (answerCanvasGroup != null)
            {
                answerCanvasGroup.alpha = 1f;
                answerCanvasGroup.interactable = true;
                answerCanvasGroup.blocksRaycasts = true;
            }

            currentState = AnswerUIState.Active;

            // �Զ��۽��������
            if (IsInputType() && answerInputField != null)
            {
                answerInputField.ActivateInputField();
            }

            LogDebug("����UI�������");
        }

        /// <summary>
        /// ��������
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

            // ��ɽ���
            if (answerCanvasGroup != null)
            {
                answerCanvasGroup.alpha = 0f;
            }

            answerPanel.gameObject.SetActive(false);
            currentState = AnswerUIState.Hidden;

            LogDebug("����UI�������");
        }

        /// <summary>
        /// ��ȡ��ǰ��Ŀ
        /// </summary>
        private NetworkQuestionData GetCurrentQuestion()
        {
            var nqmc = NetworkQuestionManagerController.Instance;
            if (nqmc != null)
            {
                return nqmc.GetCurrentNetworkQuestion();
            }

            // ���÷�������BlackboardDisplayManager��ȡ
            var blackboard = BlackboardDisplayManager.Instance;
            if (blackboard != null)
            {
                return blackboard.CurrentQuestion;
            }

            return null;
        }

        /// <summary>
        /// ����Ƿ�����Ч��Ŀ
        /// </summary>
        private bool HasValidQuestion()
        {
            return GetCurrentQuestion() != null;
        }

        /// <summary>
        /// ����Ƿ����ҵĻغ�
        /// </summary>
        private bool IsMyTurn()
        {
            var nqmc = NetworkQuestionManagerController.Instance;
            return nqmc != null && nqmc.IsMyTurn;
        }

        /// <summary>
        /// �ж��Ƿ�������������
        /// </summary>
        private bool IsInputType()
        {
            return currentQuestion != null && IsInputType(currentQuestion.questionType);
        }

        /// <summary>
        /// �ж��Ƿ���ѡ��������
        /// </summary>
        private bool IsOptionType()
        {
            return currentQuestion != null && IsOptionType(currentQuestion.questionType);
        }

        /// <summary>
        /// �ж��Ƿ�������������
        /// </summary>
        private static bool IsInputType(QuestionType type)
        {
            return type == QuestionType.HardFill ||
                   type == QuestionType.SoftFill ||
                   type == QuestionType.IdiomChain ||
                   type == QuestionType.TextPinyin;
        }

        /// <summary>
        /// �ж��Ƿ���ѡ��������
        /// </summary>
        private static bool IsOptionType(QuestionType type)
        {
            return type == QuestionType.ExplanationChoice ||
                   type == QuestionType.SimularWordChoice ||
                   type == QuestionType.SentimentTorF ||
                   type == QuestionType.UsageTorF;
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[AnswerUIManager] {message}");
            }
        }

        /// <summary>
        /// ������Դ
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