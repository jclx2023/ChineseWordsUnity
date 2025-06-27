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

        public static BlackboardDisplayManager Instance { get; private set; }

        // ��̬������ѡ���ı�
        private TextMeshProUGUI[] optionTexts;
        private NetworkQuestionData currentQuestion;
        private bool isDisplaying = false;

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
            // �ӳٲ����������ȷ�����������Ѿ�����
            StartCoroutine(FindAndSetCamera());
        }

        /// <summary>
        /// ���Ҳ����ö�̬���ɵ���������
        /// </summary>
        private IEnumerator FindAndSetCamera()
        {
            Camera playerCamera = null;
            float timeoutCounter = 0f;
            float timeout = 5f; // 5�볬ʱ

            // ѭ��������������
            while (playerCamera == null && timeoutCounter < timeout)
            {
                // ���Զ��ֿ��ܵ���������ҷ�ʽ
                playerCamera = FindPlayerCamera();

                if (playerCamera == null)
                {
                    timeoutCounter += 0.1f;
                    yield return new WaitForSeconds(0.1f);
                }
            }

            // �����ҵ��������
            if (playerCamera != null)
            {
                SetBlackboardCamera(playerCamera);
            }
            else
            {
                Debug.LogWarning("[BlackboardDisplayManager] δ���ҵ�����������ʹ�����������Ϊ����");
                SetBlackboardCamera(Camera.main);
            }
        }

        /// <summary>
        /// �������������Ķ��ַ�ʽ������PlayerCameraController���߼��Ż���
        /// </summary>
        private Camera FindPlayerCamera()
        {
            // ��ʽ1: ͨ��PlayerCameraController������ң���ɿ���
            var playerCameraControllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in playerCameraControllers)
            {
                if (controller.IsLocalPlayer && controller.PlayerCamera != null)
                {
                    Debug.Log($"[BlackboardDisplayManager] ͨ��PlayerCameraController�ҵ������: {controller.PlayerCamera.name}");
                    return controller.PlayerCamera;
                }
            }

            // ��ʽ2: ����MainCamera��ǩ���������PlayerCameraController�����ô˱�ǩ��
            GameObject mainCameraObj = GameObject.FindWithTag("MainCamera");
            if (mainCameraObj != null)
            {
                var camera = mainCameraObj.GetComponent<Camera>();
                if (camera != null && camera.enabled)
                {
                    Debug.Log($"[BlackboardDisplayManager] ͨ��MainCamera��ǩ�ҵ������: {camera.name}");
                    return camera;
                }
            }

            // ��ʽ3: ��������Ϊ"PlayerCamera"�������
            Camera[] allCameras = FindObjectsOfType<Camera>();
            foreach (var cam in allCameras)
            {
                if (cam.gameObject.name == "PlayerCamera" && cam.enabled)
                {
                    Debug.Log($"[BlackboardDisplayManager] ͨ�������ҵ�PlayerCamera: {cam.name}");
                    return cam;
                }
            }

            // ��ʽ4: ���Ұ���"Player"��"Local"�ȹؼ��ʵ������
            foreach (var cam in allCameras)
            {
                if ((cam.gameObject.name.Contains("Player") ||
                     cam.gameObject.name.Contains("Local") ||
                     cam.gameObject.name.Contains("FPS")) &&
                     cam.enabled)
                {
                    Debug.Log($"[BlackboardDisplayManager] ͨ���ؼ����ҵ������: {cam.name}");
                    return cam;
                }
            }

            // ��ʽ5: ���ҳ���Ĭ���������֮��ĵ�һ�����������
            foreach (var cam in allCameras)
            {
                if (cam != Camera.main && cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    Debug.Log($"[BlackboardDisplayManager] �ҵ����������: {cam.name}");
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
                Debug.Log($"[BlackboardDisplayManager] �����������: {camera.name}");
            }
        }

        /// <summary>
        /// �ֶ���������������ⲿ���ã��ر���PlayerCameraController��
        /// </summary>
        public void SetCamera(Camera camera)
        {
            SetBlackboardCamera(camera);
        }

        /// <summary>
        /// ע�������������������Ƽ���ʽ��
        /// </summary>
        public void RegisterPlayerCameraController(PlayerCameraController cameraController)
        {
            if (cameraController != null && cameraController.IsLocalPlayer && cameraController.PlayerCamera != null)
            {
                SetBlackboardCamera(cameraController.PlayerCamera);
                Debug.Log($"[BlackboardDisplayManager] ��ע����������������: {cameraController.PlayerCamera.name}");
            }
        }

        /// <summary>
        /// ��ʼ���ڰ壨��֤������ã�
        /// </summary>
        private void InitializeBlackboard()
        {
            // ��֤��Ҫ���������
            if (blackboardCanvas == null)
            {
                Debug.LogError("[BlackboardDisplayManager] BlackboardCanvas����ȱʧ");
                return;
            }

            if (questionText == null)
            {
                Debug.LogError("[BlackboardDisplayManager] QuestionText����ȱʧ");
                return;
            }

            if (statusText == null)
            {
                Debug.LogError("[BlackboardDisplayManager] StatusText����ȱʧ");
                return;
            }

            if (optionTemplate == null)
            {
                Debug.LogError("[BlackboardDisplayManager] OptionTemplate����ȱʧ");
                return;
            }

            // ȷ��ģ�崦�ڷǼ���״̬
            optionTemplate.SetActive(false);

            // Ӧ����ɫ����
            ApplyColorSettings();

            // ��ʼ�����ʾ
            ClearDisplay();

            Debug.Log("[BlackboardDisplayManager] ��ʼ�����");
        }

        /// <summary>
        /// Ӧ����ɫ����
        /// </summary>
        private void ApplyColorSettings()
        {
            if (questionText != null)
            {
                questionText.color = questionColor;
            }

            if (statusText != null)
            {
                statusText.color = statusColor;
            }
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
        /// ��ʾ��Ŀ
        /// </summary>
        public void DisplayQuestion(NetworkQuestionData questionData)
        {
            if (questionData == null)
            {
                ClearDisplay();
                return;
            }

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
            UpdateDisplayStatus("������", statusColor);

            // ǿ�Ƹ��²���
            StartCoroutine(RefreshLayout());
        }

        /// <summary>
        /// ��ʾѡ����
        /// </summary>
        private void DisplayChoiceQuestion(NetworkQuestionData questionData)
        {
            if (questionData.options == null || questionData.options.Length == 0)
            {
                optionsArea.gameObject.SetActive(false);
                return;
            }

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
            // ����ѡ�����򼤻������ݣ�ά�ֲ��ֽṹ
            optionsArea.gameObject.SetActive(true);

            // ��������ѡ�����������
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
            currentQuestion = null;
            isDisplaying = false;

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
            questionArea.gameObject.SetActive(false);
            optionsArea.gameObject.SetActive(false);
            statusArea.gameObject.SetActive(false);
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
        }

        /// <summary>
        /// ����Ƿ�������ʾ��Ŀ
        /// </summary>
        public bool IsDisplaying => isDisplaying;

        /// <summary>
        /// ��ȡ��ǰ��ʾ����Ŀ
        /// </summary>
        public NetworkQuestionData CurrentQuestion => currentQuestion;

        #region ���Է���

        /// <summary>
        /// ���������õ�ѡ��������
        /// </summary>
        private NetworkQuestionData CreateTestChoiceQuestion()
        {
            return new NetworkQuestionData
            {
                questionType = QuestionType.ExplanationChoice,
                questionText = "�����ĸ������ʾ���ǳ����ˡ�����˼��",
                options = new string[]
                {
                    "�˸߲���",
                    "��ͷɥ��",
                    "��ƽ����",
                    "��������"
                },
                correctAnswer = "�˸߲���",
                timeLimit = 30f
            };
        }

        /// <summary>
        /// ���������õĳ�ѡ��ѡ����
        /// </summary>
        private NetworkQuestionData CreateTestLongChoiceQuestion()
        {
            return new NetworkQuestionData
            {
                questionType = QuestionType.ExplanationChoice,
                questionText = "�����ﾳ�������ĸ������г����ʹ������ȷ�ģ�",
                options = new string[]
                {
                    "����Ȼ����Ǯ������Ϊ�����ʣ���������ƶ������",
                    "���������ѵ���ѧ�⣬����Ҫӭ�Ѷ��ϣ�������������ǰ",
                    "��������Ѿ�ľ�ѳ��ۣ�����ֻ�ܽ�����ʵ�������ƶ��ƻ�",
                    "��˵�����ǵ������ģ��߼����������˺��������"
                },
                correctAnswer = "��������Ѿ�ľ�ѳ��ۣ�����ֻ�ܽ�����ʵ�������ƶ��ƻ�",
                timeLimit = 45f
            };
        }

        /// <summary>
        /// ���������õ��ж�������
        /// </summary>
        private NetworkQuestionData CreateTestTrueFalseQuestion()
        {
            return new NetworkQuestionData
            {
                questionType = QuestionType.SentimentTorF,
                questionText = "����÷ֹ�ʡ���������ǰ���ʡ�",
                correctAnswer = "A",
                timeLimit = 20f
            };
        }

        /// <summary>
        /// ���������õ����������
        /// </summary>
        private NetworkQuestionData CreateTestFillQuestion()
        {
            return new NetworkQuestionData
            {
                questionType = QuestionType.IdiomChain,
                questionText = "��������<color=yellow><b>��÷ֹ____</b></color>\n\n(������һ��������������)",
                correctAnswer = "��",
                timeLimit = 30f
            };
        }

        [ContextMenu("���� - ��ʾѡ����")]
        public void TestDisplayChoiceQuestion()
        {
            var testData = CreateTestChoiceQuestion();
            DisplayQuestion(testData);
        }

        [ContextMenu("���� - ��ʾ��ѡ��ѡ����")]
        public void TestDisplayLongChoiceQuestion()
        {
            var testData = CreateTestLongChoiceQuestion();
            DisplayQuestion(testData);
        }

        [ContextMenu("���� - ��ʾ�ж���")]
        public void TestDisplayTrueFalseQuestion()
        {
            var testData = CreateTestTrueFalseQuestion();
            DisplayQuestion(testData);
        }

        [ContextMenu("���� - ��ʾ�����")]
        public void TestDisplayFillQuestion()
        {
            var testData = CreateTestFillQuestion();
            DisplayQuestion(testData);
        }

        [ContextMenu("���� - ����״̬")]
        public void TestUpdateStatus()
        {
            UpdateDisplayStatus("���ύ�𰸣��ȴ����...", Color.yellow);
        }

        [ContextMenu("���� - ��ʾ��ȷ���")]
        public void TestShowCorrectResult()
        {
            UpdateDisplayStatus("�ش���ȷ��", Color.green);
        }

        [ContextMenu("���� - ��ʾ������")]
        public void TestShowWrongResult()
        {
            UpdateDisplayStatus("�ش������ȷ���ǣ��˸߲���", Color.red);
        }

        [ContextMenu("���� - �����ʾ")]
        public void TestClearDisplay()
        {
            ClearDisplay();
        }

        [ContextMenu("���� - ���񲼾�ѡ����")]
        public void TestGridChoiceQuestion()
        {
            var testData = new NetworkQuestionData
            {
                questionType = QuestionType.ExplanationChoice,
                questionText = "����2x2���񲼾���ʾЧ��",
                options = new string[]
                {
                    "ѡ��A",
                    "ѡ��B",
                    "ѡ��C",
                    "ѡ��D"
                },
                correctAnswer = "ѡ��A",
                timeLimit = 30f
            };
            DisplayQuestion(testData);
        }

        #endregion
    }
}