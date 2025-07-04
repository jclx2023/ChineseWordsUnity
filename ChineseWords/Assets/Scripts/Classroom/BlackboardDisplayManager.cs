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
    /// �ڰ���ʾ������ - �����ֶ����õ�LayoutGroupϵͳ
    /// רע����ʾ�߼������в��������Unity�༭����Ԥ������
    /// �޸�Ϊ����ClassroomManager���������������¼���ȷ����������ú�����ʾ��Ŀ
    /// </summary>
    public class BlackboardDisplayManager : MonoBehaviour
    {
        [Header("Ԥ���õ�UI�������")]
        [SerializeField] private Canvas blackboardCanvas;
        [SerializeField] private RectTransform blackboardArea;
        [SerializeField] private RectTransform questionArea;
        [SerializeField] private RectTransform optionsArea;
        [SerializeField] private RectTransform statusArea;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject optionTemplate;

        [Header("��ʾ����")]
        [SerializeField] private Color questionColor = Color.black;
        [SerializeField] private Color optionColor = Color.black;
        [SerializeField] private Color statusColor = Color.blue;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static BlackboardDisplayManager Instance { get; private set; }

        // ��̬������ѡ���ı�
        private TextMeshProUGUI[] optionTexts;
        private NetworkQuestionData currentQuestion;
        private bool isDisplaying = false;
        private bool cameraSetupCompleted = false;
        private bool blackboardInitialized = false;

        // ����ʾ����Ŀ�����������δ����ʱ��
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
            // ����ClassroomManager���¼�
            SubscribeToClassroomManagerEvents();

            // �ӳٲ����������ȷ�����������Ѿ�����
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
        /// ������Ŀ�������¼�
        /// </summary>
        private void SubscribeToQuestionManagerEvents()
        {
            if (NetworkQuestionManagerController.Instance != null)
            {
                LogDebug("�����ӵ�NetworkQuestionManagerController����ʼ�����Ŀ�仯");

                // ����NQMC��Ŀ���Э��
                StartCoroutine(MonitorNQMCQuestionChanges());
            }
            else
            {
                LogDebug("NQMC�����ã����Զ���NetworkManager�¼���Ϊ����");
                if (NetworkManager.Instance != null)
                {
                    NetworkManager.OnQuestionReceived += OnQuestionReceivedFromNetwork;
                    LogDebug("�Ѷ���NetworkManager��OnQuestionReceived�¼�");
                }
            }
        }

        /// <summary>
        /// ȡ��������Ŀ�������¼�
        /// </summary>
        private void UnsubscribeFromQuestionManagerEvents()
        {
            // ȡ������NetworkManager����Ŀ�¼�
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnQuestionReceived -= OnQuestionReceivedFromNetwork;
                LogDebug("��ȡ������NetworkManager��OnQuestionReceived�¼�");
            }
        }

        /// <summary>
        /// ���NQMC����Ŀ�仯����Ҫ������
        /// </summary>
        private IEnumerator MonitorNQMCQuestionChanges()
        {
            NetworkQuestionData lastQuestion = null;

            while (true)
            {
                yield return new WaitForSeconds(0.3f); // ÿ0.3����һ��

                if (NetworkQuestionManagerController.Instance != null)
                {
                    var currentQuestion = NetworkQuestionManagerController.Instance.GetCurrentNetworkQuestion();

                    // �����������Ŀ�Ҳ�ͬ���ϴε���Ŀ
                    if (currentQuestion != null && !AreQuestionsEqual(currentQuestion, lastQuestion))
                    {
                        LogDebug($"NQMC��Ŀ�仯��⵽����Ŀ: {currentQuestion.questionText}");
                        DisplayQuestion(currentQuestion);
                        lastQuestion = currentQuestion;
                    }
                    // �����Ŀ�������
                    else if (currentQuestion == null && lastQuestion != null)
                    {
                        LogDebug("NQMC��Ŀ����գ���պڰ���ʾ");
                        ClearDisplay();
                        lastQuestion = null;
                    }
                }
            }
        }

        /// <summary>
        /// �Ƚ�������Ŀ�Ƿ����
        /// </summary>
        private bool AreQuestionsEqual(NetworkQuestionData q1, NetworkQuestionData q2)
        {
            if (q1 == null && q2 == null) return true;
            if (q1 == null || q2 == null) return false;

            return q1.questionText == q2.questionText && q1.questionType == q2.questionType;
        }

        /// <summary>
        /// ��ӦNetworkManager����Ŀ�����¼������÷�����
        /// </summary>
        private void OnQuestionReceivedFromNetwork(NetworkQuestionData questionData)
        {
            LogDebug($"��NetworkManager�յ���Ŀ�¼�: {questionData?.questionText}");

            if (questionData != null)
            {
                // ��ʾ��Ŀ���ڰ�
                DisplayQuestion(questionData);
            }
        }

        /// <summary>
        /// ��Ӧ�������������¼�
        /// </summary>
        private void OnCameraSetupCompleted()
        {
            LogDebug("�յ��������������¼�");
            cameraSetupCompleted = true;

            // ����д���ʾ����Ŀ��������ʾ��
            if (pendingQuestion != null)
            {
                DisplayQuestion(pendingQuestion);
                pendingQuestion = null;
            }
        }

        /// <summary>
        /// ��Ӧ���ҳ�ʼ������¼�
        /// </summary>
        private void OnClassroomInitialized()
        {
            LogDebug("�յ����ҳ�ʼ������¼�");
            if (!cameraSetupCompleted)
            {
                StartCoroutine(FindAndSetCamera());
            }
        }

        #endregion

        /// <summary>
        /// ���Ҳ����ö�̬���ɵ���������
        /// </summary>
        private IEnumerator FindAndSetCamera()
        {
            Camera playerCamera = null;
            float timeoutCounter = 0f;
            float timeout = 10f; // ���ӵ�10�볬ʱ

            LogDebug("��ʼ������������");

            // ѭ��������������
            while (playerCamera == null && timeoutCounter < timeout)
            {
                // ���Զ��ֿ��ܵ���������ҷ�ʽ
                playerCamera = FindPlayerCamera();

                if (playerCamera == null)
                {
                    timeoutCounter += 0.2f;
                    yield return new WaitForSeconds(0.2f);
                }
            }

            // �����ҵ��������
            if (playerCamera != null)
            {
                SetBlackboardCamera(playerCamera);
                cameraSetupCompleted = true;

                // ����д���ʾ����Ŀ��������ʾ��
                if (pendingQuestion != null)
                {
                    LogDebug("�����������ɣ���ʾ���������Ŀ");
                    DisplayQuestion(pendingQuestion);
                    pendingQuestion = null;
                }
            }
            else
            {
                LogDebug("δ���ҵ�����������ʹ�����������Ϊ����");
                SetBlackboardCamera(Camera.main);
                cameraSetupCompleted = true;
            }
        }

        /// <summary>
        /// �������������Ķ��ַ�ʽ������PlayerCameraController���߼��Ż���
        /// </summary>
        private Camera FindPlayerCamera()
        {
            // ��ʽ1: ͨ��ClassroomManager��ȡ����ɿ���
            var classroomManager = FindObjectOfType<Classroom.ClassroomManager>();
            if (classroomManager != null && classroomManager.IsInitialized)
            {
                var cameraController = classroomManager.GetLocalPlayerCameraController();
                if (cameraController != null && cameraController.PlayerCamera != null)
                {
                    LogDebug($"ͨ��ClassroomManager�ҵ������: {cameraController.PlayerCamera.name}");
                    return cameraController.PlayerCamera;
                }
            }

            // ��ʽ2: ͨ��PlayerCameraController������ң���ɿ���
            var playerCameraControllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in playerCameraControllers)
            {
                if (controller.IsLocalPlayer && controller.PlayerCamera != null)
                {
                    LogDebug($"ͨ��PlayerCameraController�ҵ������: {controller.PlayerCamera.name}");
                    return controller.PlayerCamera;
                }
            }

            // ��ʽ3: ��������Ϊ"PlayerCamera"�������
            Camera[] allCameras = FindObjectsOfType<Camera>();
            foreach (var cam in allCameras)
            {
                if (cam.gameObject.name == "PlayerCamera" && cam.enabled)
                {
                    LogDebug($"ͨ�������ҵ�PlayerCamera: {cam.name}");
                    return cam;
                }
            }

            return null;
        }

        /// <summary>
        /// ���úڰ�Canvas�������
        /// </summary>
        private void SetBlackboardCamera(Camera camera)
        {
            if (blackboardCanvas != null && camera != null)
            {
                blackboardCanvas.worldCamera = camera;
                LogDebug($"�����úڰ������: {camera.name}");

                // ǿ��ˢ��Canvas
                StartCoroutine(RefreshCanvasLayout());
            }
        }

        /// <summary>
        /// ˢ��Canvas����
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
        /// ��ʼ���ڰ壨��֤������ã�
        /// </summary>
        private void InitializeBlackboard()
        {
            // ȷ��ģ�崦�ڷǼ���״̬
            optionTemplate.SetActive(false);

            // Ӧ����ɫ����
            ApplyColorSettings();

            // ��ʼ�����ʾ
            ClearDisplay();

            blackboardInitialized = true;
            LogDebug("BlackboardDisplayManager ��ʼ�����");
        }

        /// <summary>
        /// Ӧ����ɫ����
        /// </summary>
        private void ApplyColorSettings()
        {
            questionText.color = questionColor;
            statusText.color = statusColor;
        }

        /// <summary>
        /// ����ѡ���ı����
        /// </summary>
        private void CreateOptionTexts(int count)
        {
            // ����ɵ�ѡ���ı�
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

            // �����µ�ѡ���ı�
            optionTexts = new TextMeshProUGUI[count];

            for (int i = 0; i < count; i++)
            {
                // ��ģ��ʵ����
                GameObject optionObj = Instantiate(optionTemplate, optionsArea);
                optionObj.name = $"Option{i + 1}";
                optionObj.SetActive(true);

                var optionText = optionObj.GetComponent<TextMeshProUGUI>();
                optionTexts[i] = optionText;

                // Ӧ����ɫ����
                if (optionText != null)
                {
                    optionText.color = optionColor;
                }
            }
        }

        /// <summary>
        /// ��ʾ��Ŀ���޸�Ϊ��������״̬��
        /// </summary>
        public void DisplayQuestion(NetworkQuestionData questionData)
        {
            LogDebug($"�յ���ʾ��Ŀ����: {questionData.questionText}");

            ExecuteDisplayQuestion(questionData);
        }

        /// <summary>
        /// ִ��ʵ�ʵ���Ŀ��ʾ�߼�
        /// </summary>
        private void ExecuteDisplayQuestion(NetworkQuestionData questionData)
        {
            LogDebug($"��ʼ��ʾ��Ŀ: {questionData.questionText}");

            currentQuestion = questionData;
            isDisplaying = true;

            // ��ʾ���
            if (questionText != null)
            {
                questionText.text = questionData.questionText;
                questionArea.gameObject.SetActive(true);
            }

            // ����������ʾ����
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

            // ��ʾ״̬
            UpdateDisplayStatus("�밴������Ҽ�����", statusColor);

            // ǿ�Ƹ��²���
            StartCoroutine(RefreshLayout());
        }

        /// <summary>
        /// ��ʾѡ����
        /// </summary>
        private void DisplayChoiceQuestion(NetworkQuestionData questionData)
        {
            // ����ѡ���ı�
            CreateOptionTexts(questionData.options.Length);

            // ����ѡ������
            for (int i = 0; i < optionTexts.Length; i++)
            {
                optionTexts[i].text = $"{(char)('A' + i)}. {questionData.options[i]}";
            }

            optionsArea.gameObject.SetActive(true);
        }

        /// <summary>
        /// ��ʾ�ж���
        /// </summary>
        private void DisplayTrueFalseQuestion(NetworkQuestionData questionData)
        {
            CreateOptionTexts(2);

            optionTexts[0].text = "A. ��ȷ";
            optionTexts[1].text = "B. ����";

            optionsArea.gameObject.SetActive(true);
        }

        /// <summary>
        /// ��ʾ�����
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
        /// ������ʾ״̬
        /// </summary>
        public void UpdateDisplayStatus(string status, Color color)
        {
            LogDebug($"����״̬: {status}");

            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = color;
                statusArea.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// �����ʾ����
        /// </summary>
        public void ClearDisplay()
        {
            LogDebug("��պڰ���ʾ");

            currentQuestion = null;
            isDisplaying = false;
            pendingQuestion = null; // ��մ���ʾ����Ŀ

            if (questionText != null)
            {
                questionText.text = "";
            }

            if (statusText != null)
            {
                statusText.text = "";
            }

            // ����ѡ���ı�
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

            // ������������
            if (questionArea != null) questionArea.gameObject.SetActive(false);
            if (optionsArea != null) optionsArea.gameObject.SetActive(false);
            if (statusArea != null) statusArea.gameObject.SetActive(false);
        }

        /// <summary>
        /// ǿ��ˢ�²���
        /// </summary>
        private IEnumerator RefreshLayout()
        {
            yield return new WaitForEndOfFrame();

            // ǿ�Ƹ���Canvas����
            Canvas.ForceUpdateCanvases();

            // �ؽ����в���
            if (blackboardArea != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(blackboardArea);
            }

            if (optionsArea != null && optionsArea.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(optionsArea);
            }

            LogDebug("����ˢ�����");
        }

        /// <summary>
        /// ��ȡ��ǰ��ʾ����Ŀ
        /// </summary>
        public NetworkQuestionData CurrentQuestion => currentQuestion;

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[BlackboardDisplayManager] {message}");
            }
        }

        #region ������Դ

        private void OnDestroy()
        {
            UnsubscribeFromClassroomManagerEvents();
            UnsubscribeFromQuestionManagerEvents();
        }

        #endregion
    }
}