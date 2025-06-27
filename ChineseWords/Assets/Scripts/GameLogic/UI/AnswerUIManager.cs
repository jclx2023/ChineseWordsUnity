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
    /// ����ʽ������������ - �򻯰汾
    /// ְ����ʾ��Ŀ��Ϣ��ʵ������Ӧ�Ĵ�����棬�����ύ�¼�
    /// �޸�Ϊ����ClassroomManager���������������¼�
    /// </summary>
    public class AnswerUIManager : MonoBehaviour
    {
        [Header("���ⴥ������")]
        [SerializeField] private KeyCode answerTriggerKey = KeyCode.Return;

        [Header("UI�������")]
        [SerializeField] private Canvas answerCanvas;
        [SerializeField] private CanvasGroup answerCanvasGroup;
        [SerializeField] private RectTransform answerPanel;
        [SerializeField] private RectTransform contentArea; // ��̬���ش�����������

        [Header("���ʹ������Ԥ����")]
        [SerializeField] private GameObject fillBlankUIPrefab;   // �����UI
        [SerializeField] private GameObject chooseUIPrefab;      // ѡ����UI
        [SerializeField] private GameObject torFUIPrefab;        // �ж���UI

        [Header("��Ŀ��ʾ")]
        [SerializeField] private TextMeshProUGUI questionDisplayText; // ��Ŀ�ı���ʾ

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
        private GameObject currentAnswerUIInstance; // ��ǰʵ�����Ĵ������

        // ���������
        private PlayerCameraController playerCameraController;
        private bool cameraControllerReady = false;

        // �¼�
        public static event System.Action<string> OnAnswerSubmitted;
        public static event System.Action OnAnswerUIClosed;

        // ����
        public bool IsAnswerUIVisible => currentState == AnswerUIState.Active || currentState == AnswerUIState.FadingIn;
        public bool CanShowAnswerUI => currentState == AnswerUIState.Hidden && HasValidQuestion() && IsMyTurn() && cameraControllerReady;

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
            // ֻ����ClassroomManager���¼������ٹ�����������
            // ��������ҽ���OnCameraSetupCompleted�¼��н���
        }

        private void OnEnable()
        {
            SubscribeToClassroomManagerEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromClassroomManagerEvents();
        }

        private void Update()
        {
            HandleInput();
        }

        #region �¼����Ĺ���

        /// <summary>
        /// ����ClassroomManager�¼�
        /// </summary>
        private void SubscribeToClassroomManagerEvents()
        {
            Classroom.ClassroomManager.OnCameraSetupCompleted += OnCameraSetupCompleted;
            Classroom.ClassroomManager.OnClassroomInitialized += OnClassroomInitialized;
        }

        /// <summary>
        /// ȡ������ClassroomManager�¼�
        /// </summary>
        private void UnsubscribeFromClassroomManagerEvents()
        {
            Classroom.ClassroomManager.OnCameraSetupCompleted -= OnCameraSetupCompleted;
            Classroom.ClassroomManager.OnClassroomInitialized -= OnClassroomInitialized;
        }

        /// <summary>
        /// ��Ӧ�������������¼�
        /// </summary>
        private void OnCameraSetupCompleted()
        {
            LogDebug("�յ��������������¼�����ʼ���������������");
            StartCoroutine(FindPlayerCameraControllerCoroutine());
        }

        /// <summary>
        /// ��Ӧ���ҳ�ʼ������¼������÷�����
        /// </summary>
        private void OnClassroomInitialized()
        {
            if (!cameraControllerReady)
            {
                LogDebug("���ҳ�ʼ����ɣ����Բ�������������������÷�����");
                StartCoroutine(FindPlayerCameraControllerCoroutine());
            }
        }

        #endregion

        #region ���������������

        /// <summary>
        /// Э�̷�ʽ������������������
        /// </summary>
        private IEnumerator FindPlayerCameraControllerCoroutine()
        {
            LogDebug("��ʼ������������������");

            float timeout = 5f; // 5�볬ʱ
            float elapsed = 0f;

            while (elapsed < timeout && playerCameraController == null)
            {
                playerCameraController = FindPlayerCameraController();

                if (playerCameraController != null)
                {
                    cameraControllerReady = true;
                    LogDebug($"�ɹ��ҵ������������: {playerCameraController.name}");
                    break;
                }

                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            if (playerCameraController == null)
            {
                LogDebug("δ���ҵ����������������ʹ�ñ��÷���");
                // ������ӱ��ò����߼��������
            }
        }

        /// <summary>
        /// ������������������
        /// </summary>
        private PlayerCameraController FindPlayerCameraController()
        {
            // ��ʽ1: ͨ��ClassroomManager��ȡ���Ƽ���ʽ��
            var classroomManager = FindObjectOfType<Classroom.ClassroomManager>();
            if (classroomManager != null && classroomManager.IsInitialized)
            {
                var cameraController = classroomManager.GetLocalPlayerCameraController();
                if (cameraController != null)
                {
                    LogDebug($"ͨ��ClassroomManager�ҵ������������: {cameraController.name}");
                    return cameraController;
                }
            }

            // ��ʽ2: ��������PlayerCameraController�ҵ�������ҵ�
            var controllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in controllers)
            {
                if (controller.IsLocalPlayer)
                {
                    LogDebug($"ͨ�������ҵ�������������������: {controller.name}");
                    return controller;
                }
            }

            // ��ʽ3: ���Ұ���"Local"��"Player"�ȹؼ��ʵ������������
            foreach (var controller in controllers)
            {
                if (controller.gameObject.name.Contains("Local") ||
                    controller.gameObject.name.Contains("Player"))
                {
                    LogDebug($"ͨ���ؼ����ҵ������������: {controller.name}");
                    return controller;
                }
            }

            return null;
        }

        /// <summary>
        /// �ֶ���������������������ⲿ���ã�
        /// </summary>
        public void SetPlayerCameraController(PlayerCameraController cameraController)
        {
            if (cameraController != null)
            {
                playerCameraController = cameraController;
                cameraControllerReady = true;
                LogDebug($"�ֶ����������������: {cameraController.name}");
            }
        }

        #endregion

        /// <summary>
        /// ��ʼ������UI
        /// </summary>
        private void InitializeAnswerUI()
        {
            // ȷ��Canvas������ȷ
            if (answerCanvas != null)
            {
                answerCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                answerCanvas.sortingOrder = 100;
            }

            // ���ó�ʼ״̬
            if (answerCanvasGroup != null)
            {
                answerCanvasGroup.alpha = 0f;
                answerCanvasGroup.interactable = false;
                answerCanvasGroup.blocksRaycasts = false;
            }

            // ���ش������
            if (answerPanel != null)
            {
                answerPanel.gameObject.SetActive(false);
            }

            LogDebug("AnswerUIManager ��ʼ�����");
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

            // Esc��ȡ������
            if (currentState == AnswerUIState.Active && Input.GetKeyDown(KeyCode.Escape))
            {
                HideAnswerUI();
            }
        }

        /// <summary>
        /// ������ʾ����UI
        /// </summary>
        public bool TryShowAnswerUI()
        {
            if (!CanShowAnswerUI)
            {
                LogDebug($"�޷���ʾ����UI - ״̬: {currentState}, ����Ŀ: {HasValidQuestion()}, �ҵĻغ�: {IsMyTurn()}, ���������: {cameraControllerReady}");
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

            // ��ʾ��Ŀ��Ϣ
            DisplayQuestionInfo(currentQuestion);

            // ʵ������Ӧ�Ĵ������
            InstantiateAnswerUI(currentQuestion.questionType);

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
        /// ��ʾ��Ŀ��Ϣ
        /// </summary>
        private void DisplayQuestionInfo(NetworkQuestionData question)
        {
            // ��ʾ��Ŀ�ı�
            if (questionDisplayText != null)
            {
                questionDisplayText.text = question.questionText;
            }
        }

        /// <summary>
        /// ��������ʵ������Ӧ�Ĵ������
        /// </summary>
        private void InstantiateAnswerUI(QuestionType questionType)
        {
            // �������еĴ������
            ClearCurrentAnswerUI();

            GameObject prefabToLoad = GetAnswerUIPrefab(questionType);

            if (prefabToLoad != null && contentArea != null)
            {
                // ʵ�����������
                currentAnswerUIInstance = Instantiate(prefabToLoad, contentArea);

                // ����RectTransform������ContentArea
                var rectTransform = currentAnswerUIInstance.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                }

                // ������Ŀ���ݸ�������棨ͨ�������������¼���
                SendQuestionDataToAnswerUI(currentAnswerUIInstance, currentQuestion);

                // �󶨴��������ύ�¼�
                BindAnswerUIEvents(currentAnswerUIInstance);

                LogDebug($"�ɹ�ʵ�����������: {prefabToLoad.name}");
            }
            else
            {
                LogDebug($"�޷��ҵ����� {questionType} ��Ӧ��Ԥ�����ContentAreaΪ��");
            }
        }

        /// <summary>
        /// ��ȡ��Ӧ���͵Ĵ���UIԤ����
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
        /// �������洫����Ŀ����
        /// </summary>
        private void SendQuestionDataToAnswerUI(GameObject answerUIInstance, NetworkQuestionData questionData)
        {
            LogDebug($"��ʼ�������洫������: {questionData.questionType}");

            // ��������ʹ�ò�ͬ�����ݴ��ݷ�ʽ
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
                    LogDebug($"δ֪����: {questionData.questionType}");
                    break;
            }
        }

        /// <summary>
        /// ����ѡ����UI
        /// </summary>
        private void SetupChoiceUI(GameObject choiceUIInstance, NetworkQuestionData questionData)
        {
            LogDebug("����ѡ����UI");

            if (questionData.options == null || questionData.options.Length == 0)
            {
                LogDebug("ѡ����ѡ��Ϊ��");
                return;
            }

            // ����ѡ�ť OptionButton1-4
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
                        LogDebug($"����ѡ�ť {buttonName}: {optionText}");
                    }

                    if (button != null)
                    {
                        // �󶨵���¼�
                        string optionAnswer = ((char)('A' + i)).ToString();
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => OnOptionSelected(optionAnswer, choiceUIInstance));
                        LogDebug($"��ѡ�ť {buttonName} ����¼�: {optionAnswer}");
                    }
                }
                else
                {
                    LogDebug($"δ�ҵ�ѡ�ť: {buttonName}");
                }
            }
        }

        /// <summary>
        /// �����ж���UI
        /// </summary>
        private void SetupTrueFalseUI(GameObject torFUIInstance, NetworkQuestionData questionData)
        {
            LogDebug("�����ж���UI");

            // ����TrueButton��FalseButton
            Transform trueButtonTransform = torFUIInstance.transform.Find("TrueButton");
            Transform falseButtonTransform = torFUIInstance.transform.Find("FalseButton");

            if (trueButtonTransform != null)
            {
                var trueButton = trueButtonTransform.GetComponent<Button>();
                var trueText = trueButtonTransform.GetComponentInChildren<TextMeshProUGUI>();

                if (trueText != null)
                {
                    trueText.text = "A. ��ȷ";
                }

                if (trueButton != null)
                {
                    trueButton.onClick.RemoveAllListeners();
                    trueButton.onClick.AddListener(() => OnOptionSelected("A", torFUIInstance));
                    LogDebug("��TrueButton����¼�");
                }
            }
            else
            {
                LogDebug("δ�ҵ�TrueButton");
            }

            if (falseButtonTransform != null)
            {
                var falseButton = falseButtonTransform.GetComponent<Button>();
                var falseText = falseButtonTransform.GetComponentInChildren<TextMeshProUGUI>();

                if (falseText != null)
                {
                    falseText.text = "B. ����";
                }

                if (falseButton != null)
                {
                    falseButton.onClick.RemoveAllListeners();
                    falseButton.onClick.AddListener(() => OnOptionSelected("B", torFUIInstance));
                    LogDebug("��FalseButton����¼�");
                }
            }
            else
            {
                LogDebug("δ�ҵ�FalseButton");
            }
        }

        /// <summary>
        /// ���������UI
        /// </summary>
        private void SetupFillBlankUI(GameObject fillBlankUIInstance, NetworkQuestionData questionData)
        {
            LogDebug("���������UI");

            // ����answerinput�����
            Transform inputTransform = fillBlankUIInstance.transform.Find("AnswerInput");
            if (inputTransform != null)
            {
                var inputField = inputTransform.GetComponent<TMP_InputField>();
                if (inputField != null)
                {
                    inputField.text = "";
                    inputField.placeholder.GetComponent<TextMeshProUGUI>().text = "�������...";
                    LogDebug("���������ɹ�");
                }
            }
            else
            {
                LogDebug("δ�ҵ�answerinput�����");
            }

            // ����submitbutton�ύ��ť
            Transform submitTransform = fillBlankUIInstance.transform.Find("SubmitButton");
            if (submitTransform != null)
            {
                var submitButton = submitTransform.GetComponent<Button>();
                if (submitButton != null)
                {
                    submitButton.onClick.RemoveAllListeners();
                    submitButton.onClick.AddListener(() => OnFillBlankSubmit(fillBlankUIInstance));
                    LogDebug("��������ύ��ť");
                }
            }
            else
            {
                LogDebug("δ�ҵ�submitbutton�ύ��ť");
            }
        }

        /// <summary>
        /// ѡ�ѡ��ʱ�Ĵ���
        /// </summary>
        private void OnOptionSelected(string selectedOption, GameObject answerUIInstance)
        {
            LogDebug($"�û�ѡ����ѡ��: {selectedOption}");

            // �����ύ��
            OnAnswerSubmitted?.Invoke(selectedOption);
            HideAnswerUI();
        }

        /// <summary>
        /// ������ύ����
        /// </summary>
        private void OnFillBlankSubmit(GameObject fillBlankUIInstance)
        {
            LogDebug("������ύ��ť�����");

            // ��ȡ���������
            Transform inputTransform = fillBlankUIInstance.transform.Find("AnswerInput");
            if (inputTransform != null)
            {
                var inputField = inputTransform.GetComponent<TMP_InputField>();
                if (inputField != null)
                {
                    string answer = inputField.text.Trim();
                    LogDebug($"������: '{answer}'");
                    OnAnswerSubmitted?.Invoke(answer);
                    HideAnswerUI();
                }
            }
        }

        /// <summary>
        /// �󶨴��������ύ�¼�
        /// </summary>
        private void BindAnswerUIEvents(GameObject answerUIInstance)
        {
            LogDebug($"�󶨴�������¼�������: {currentQuestion.questionType}");

            // �¼����Ѿ���SetupXXXUI���������
            // ����ֻ��Ҫ��¼��־
            LogDebug("��������¼������");
        }


        /// <summary>
        /// ����ǰ�������
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
        /// ���������
        /// </summary>
        private void SetCameraControl(bool enabled)
        {
            if (playerCameraController != null)
            {
                playerCameraController.SetControlEnabled(enabled);
                LogDebug($"���������: {(enabled ? "����" : "����")}");
            }
            else
            {
                LogDebug("����������������ã��޷����������");
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

            // ����ǰ�������
            ClearCurrentAnswerUI();

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
            ClearCurrentAnswerUI();
            UnsubscribeFromClassroomManagerEvents();
        }

    }
}