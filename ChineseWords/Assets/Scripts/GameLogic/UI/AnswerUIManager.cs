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
                answerCanvas.sortingOrder = 100;
            }

            // ���ó�ʼ״̬
            if (answerCanvasGroup != null)
            {
                answerCanvasGroup.alpha = 0f;
                answerCanvasGroup.interactable = false;
                answerCanvasGroup.blocksRaycasts = false;
            }

            // ȷ��ContentArea����
            if (contentArea == null)
            {
                LogDebug("���棺ContentAreaδ���ã������Զ�����");
                contentArea = answerPanel?.Find("ContentArea")?.GetComponent<RectTransform>();
            }

            // ���ش������
            if (answerPanel != null)
            {
                answerPanel.gameObject.SetActive(false);
            }

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
                LogDebug($"�޷���ʾ����UI - ״̬: {currentState}, ����Ŀ: {HasValidQuestion()}, �ҵĻغ�: {IsMyTurn()}");
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
                try
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
                catch (System.Exception e)
                {
                    LogDebug($"ʵ�����������ʧ��: {e.Message}");
                }
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
                // ���������
                QuestionType.HardFill or QuestionType.SoftFill or
                QuestionType.IdiomChain or QuestionType.TextPinyin => fillBlankUIPrefab,

                // ѡ��������
                QuestionType.ExplanationChoice or QuestionType.SimularWordChoice => chooseUIPrefab,

                // �ж�������
                QuestionType.SentimentTorF or QuestionType.UsageTorF => torFUIPrefab,

                _ => null
            };
        }

        /// <summary>
        /// �������洫����Ŀ����
        /// </summary>
        private void SendQuestionDataToAnswerUI(GameObject answerUIInstance, NetworkQuestionData questionData)
        {
            // ��ʽ1��ͨ������ӿڴ�������
            var answerUIComponent = answerUIInstance.GetComponent<MonoBehaviour>();
            if (answerUIComponent != null)
            {
                // ���Ե���SetQuestionData������������ڣ�
                var setQuestionMethod = answerUIComponent.GetType().GetMethod("SetQuestionData");
                if (setQuestionMethod != null)
                {
                    setQuestionMethod.Invoke(answerUIComponent, new object[] { questionData });
                    LogDebug("ͨ��SetQuestionData����������Ŀ����");
                    return;
                }

                // ���Ե���Initialize������������ڣ�
                var initializeMethod = answerUIComponent.GetType().GetMethod("Initialize");
                if (initializeMethod != null)
                {
                    initializeMethod.Invoke(answerUIComponent, new object[] { questionData });
                    LogDebug("ͨ��Initialize����������Ŀ����");
                    return;
                }
            }

            // ��ʽ2��ͨ���¼��������ݣ��������UI����������ض��¼���
            LogDebug("ʹ��Ĭ�Ϸ�ʽ������Ŀ����");
        }

        /// <summary>
        /// �󶨴��������ύ�¼�
        /// </summary>
        private void BindAnswerUIEvents(GameObject answerUIInstance)
        {
            // ��������⣬�����ύ��ť�����¼�
            if (IsFillBlankType(currentQuestion.questionType))
            {
                Button submitButton = FindSubmitButton(answerUIInstance);
                if (submitButton != null)
                {
                    submitButton.onClick.AddListener(() => OnAnswerUISubmitted(answerUIInstance));
                    LogDebug($"��������ύ��ť�¼�: {submitButton.name}");
                }
            }
            // ����ѡ������ж��⣬����ֱ�ӵ��ѡ���ύ��Ҳ���Ա����ύ��ť��Ϊ��ѡ
            else
            {
                Button submitButton = FindSubmitButton(answerUIInstance);
                if (submitButton != null)
                {
                    submitButton.onClick.AddListener(() => OnAnswerUISubmitted(answerUIInstance));
                    LogDebug($"��ѡ��/�ж����ύ��ť�¼�: {submitButton.name}");
                }
            }
        }

        /// <summary>
        /// �����ύ��ť
        /// </summary>
        private Button FindSubmitButton(GameObject answerUIInstance)
        {
            Button[] buttons = answerUIInstance.GetComponentsInChildren<Button>();
            foreach (var btn in buttons)
            {
                if (btn.name.Contains("Submit") || btn.name.Contains("�ύ") || btn.name.Contains("ȷ��"))
                {
                    return btn;
                }
            }
            return null;
        }

        /// <summary>
        /// �ж��Ƿ������������
        /// </summary>
        private bool IsFillBlankType(QuestionType questionType)
        {
            return questionType == QuestionType.HardFill ||
                   questionType == QuestionType.SoftFill ||
                   questionType == QuestionType.IdiomChain ||
                   questionType == QuestionType.TextPinyin;
        }

        /// <summary>
        /// ��������ύ�¼�����
        /// </summary>
        private void OnAnswerUISubmitted(GameObject answerUIInstance)
        {
            string answer = GetAnswerFromUI(answerUIInstance);

            if (!string.IsNullOrEmpty(answer))
            {
                LogDebug($"�յ���������ύ�Ĵ�: {answer}");

                // �������ύ�¼�
                OnAnswerSubmitted?.Invoke(answer);

                // ���ش���UI
                HideAnswerUI();
            }
            else
            {
                LogDebug("������淵�صĴ�Ϊ��");
            }
        }

        /// <summary>
        /// �Ӵ�������ȡ��
        /// </summary>
        private string GetAnswerFromUI(GameObject answerUIInstance)
        {
            var answerUIComponent = answerUIInstance.GetComponent<MonoBehaviour>();
            if (answerUIComponent != null)
            {
                // ���Ե���GetAnswer����
                var getAnswerMethod = answerUIComponent.GetType().GetMethod("GetAnswer");
                if (getAnswerMethod != null)
                {
                    var result = getAnswerMethod.Invoke(answerUIComponent, null);
                    return result?.ToString() ?? "";
                }

                // ���Ի�ȡAnswer����
                var answerProperty = answerUIComponent.GetType().GetProperty("Answer");
                if (answerProperty != null)
                {
                    var result = answerProperty.GetValue(answerUIComponent);
                    return result?.ToString() ?? "";
                }
            }

            LogDebug("�޷��Ӵ�������ȡ��");
            return "";
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
        }
    }
}