using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;
using Core.Network;
using Managers;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// ��������������
    /// - ֧�ֵ���������ģʽ
    /// - ʵ��IQuestionDataProvider�ӿڣ�֧��Host����
    /// - ����ģʽ����Ԥ���ص������ѡ�����ѡ����ҽ���
    /// - ����ģʽ��ʹ�÷������ṩ����Ŀ����
    /// - ������ʾ��Ҫ�������ַ������������һ������
    /// - ��֤��ͷ�ַ��ͳ��������
    /// - �����߼�����Ժ��ô���Ϊ��һ���������
    /// </summary>
    public class IdiomChainQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        private string dbPath;

        [Header("UI����")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/HardFillUI";

        [Header("����ģʽ����")]
        [Header("����Ƶ������")]
        [SerializeField] private int minFreq = 0;
        [SerializeField] private int maxFreq = 9;

        [Header("UI�������")]
        private TMP_Text questionText;
        private TMP_InputField answerInput;
        private Button submitButton;
        private Button surrenderButton;
        private TMP_Text feedbackText;
        private TimerManager timerManager;

        // ���������ѡ������ģʽ��
        private List<string> firstCandidates = new List<string>();

        // ��ǰ״̬
        private string currentIdiom;
        private bool hasAnswered = false;
        private bool isGameInProgress = false;

        // IQuestionDataProvider�ӿ�ʵ��
        public QuestionType QuestionType => QuestionType.IdiomChain;

        protected override void Awake()
        {
            base.Awake(); // ������������ʼ��
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            // Ԥ���������ѡ������ģʽ�ã�
            LoadFirstCandidates();
        }

        private void Start()
        {
            // ����Ƿ���ҪUI��Host����ģʽ���ܲ���ҪUI��
            if (NeedsUI())
            {
                InitializeUI();
            }
            else
            {
                Debug.Log("[IdiomChain] Host����ģʽ������UI��ʼ��");
            }
        }

        /// <summary>
        /// ����Ƿ���ҪUI
        /// </summary>
        private bool NeedsUI()
        {
            // �����HostGameManager���Ӷ���˵�������ڳ������ʱ������������ҪUI
            // ���������QuestionDataService���Ӷ���Ҳ����ҪUI
            return transform.parent == null ||
                   (transform.parent.GetComponent<HostGameManager>() == null &&
                    transform.parent.GetComponent<QuestionDataService>() == null);
        }

        /// <summary>
        /// ��ȡ��Ŀ���ݣ�IQuestionDataProvider�ӿ�ʵ�֣�
        /// ר��ΪHost����ʹ�ã�����ʾUI
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            Debug.Log("[IdiomChain] Host�����������");

            // ���ѡ��һ���������
            string selectedIdiom = GetRandomFirstIdiom();

            if (string.IsNullOrEmpty(selectedIdiom))
            {
                Debug.LogWarning("[IdiomChain] Host���⣺û�п��õĳ�����Ŀ");
                return null;
            }

            // ������ʾ�ı����������һ���֣�
            string displayText = CreateDisplayText(selectedIdiom);

            // ������������
            var additionalData = new IdiomChainAdditionalData
            {
                displayText = displayText,
                currentIdiom = selectedIdiom,
                targetChar = selectedIdiom[selectedIdiom.Length - 1]
            };

            // ����������Ŀ����
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.IdiomChain,
                questionText = displayText,
                correctAnswer = selectedIdiom, // ��ǰ������Ϊ��׼
                options = new string[0], // �����������Ҫѡ��
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(additionalData)
            };

            Debug.Log($"[IdiomChain] Host����ɹ�: {selectedIdiom} -> {displayText}");
            return questionData;
        }

        /// <summary>
        /// �������������������Ŀ�����ڴ�Ժ������������
        /// </summary>
        /// <param name="baseIdiom">��׼�����Ҹջش�ĳ��</param>
        /// <returns>�µ���Ŀ����</returns>
        public NetworkQuestionData CreateContinuationQuestion(string baseIdiom)
        {
            Debug.Log($"[IdiomChain] ��������������Ŀ������: {baseIdiom}");

            if (string.IsNullOrEmpty(baseIdiom))
                return null;

            // ������ʾ�ı����������һ���֣�
            string displayText = CreateDisplayText(baseIdiom);

            // ������������
            var additionalData = new IdiomChainAdditionalData
            {
                displayText = displayText,
                currentIdiom = baseIdiom,
                targetChar = baseIdiom[baseIdiom.Length - 1]
            };

            // ����������Ŀ����
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.IdiomChain,
                questionText = displayText,
                correctAnswer = baseIdiom, // ��ǰ������Ϊ��׼
                options = new string[0],
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(additionalData)
            };

            Debug.Log($"[IdiomChain] ����������Ŀ�����ɹ�: {baseIdiom} -> {displayText}");
            return questionData;
        }

        /// <summary>
        /// ������ʾ�ı����������һ���֣�
        /// </summary>
        private string CreateDisplayText(string idiom)
        {
            if (string.IsNullOrEmpty(idiom))
                return "";

            // �������һ����
            char lastChar = idiom[idiom.Length - 1];
            return idiom.Substring(0, idiom.Length - 1) + $"<color=red>{lastChar}</color>";
        }

        /// <summary>
        /// �����ȡ�������
        /// </summary>
        private string GetRandomFirstIdiom()
        {
            if (firstCandidates.Count == 0)
            {
                Debug.LogError("[IdiomChain] �����ѡ�б�Ϊ��");
                return null;
            }

            // ���ѡ�񣨲��Ƴ�����ΪHost�˿�����Ҫ��γ��⣩
            int index = Random.Range(0, firstCandidates.Count);
            return firstCandidates[index];
        }

        /// <summary>
        /// Ԥ���������ѡ����
        /// </summary>
        private void LoadFirstCandidates()
        {
            firstCandidates.Clear();

            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT i1.word 
                            FROM idiom AS i1
                            WHERE i1.Freq BETWEEN @min AND @max
                              AND EXISTS (
                                  SELECT 1 FROM idiom AS i2
                                  WHERE substr(i2.word,1,1)=substr(i1.word,4,1)
                                    AND i2.Freq BETWEEN @min AND @max
                                    AND i2.word<>i1.word
                              )";
                        cmd.Parameters.AddWithValue("@min", minFreq);
                        cmd.Parameters.AddWithValue("@max", maxFreq);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                firstCandidates.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"���������ѡʧ��: {e.Message}");
            }

            if (firstCandidates.Count == 0)
            {
                Debug.LogError("�����ѡ�б�Ϊ�գ�������������޷���������");
            }
            else
            {
                Debug.Log($"�Ѽ��� {firstCandidates.Count} �������ѡ����");
            }
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUI()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[IdiomChain] UIManagerʵ��������");
                return;
            }

            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"[IdiomChain] �޷�����UIԤ����: {uiPrefabPath}");
                return;
            }

            // ��ȡUI���
            questionText = ui.Find("QuestionText")?.GetComponent<TMP_Text>();
            answerInput = ui.Find("AnswerInput")?.GetComponent<TMP_InputField>();
            submitButton = ui.Find("SubmitButton")?.GetComponent<Button>();
            surrenderButton = ui.Find("SurrenderButton")?.GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || answerInput == null || submitButton == null ||
                surrenderButton == null || feedbackText == null)
            {
                Debug.LogError("[IdiomChain] UI�����ȡʧ�ܣ����Ԥ����ṹ");
                return;
            }

            // ��ȡ��ʱ��������
            timerManager = GetComponent<TimerManager>();

            // �󶨰�ť�¼�
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);

            surrenderButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.AddListener(OnSurrender);

            // �������س��¼�
            answerInput.onSubmit.RemoveAllListeners();
            answerInput.onSubmit.AddListener(OnInputSubmit);

            feedbackText.text = string.Empty;

            Debug.Log("[IdiomChain] UI��ʼ�����");
        }

        /// <summary>
        /// ���ر�����Ŀ������ģʽ��
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[IdiomChain] ���ر�����Ŀ");

            // ʹ��GetQuestionData()�������ó����߼�
            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("û�п��õĳ�����Ŀ");
                return;
            }

            // ������Ŀ����
            currentIdiom = questionData.correctAnswer;

            // �Ӹ��������л�ȡ��ʾ��Ϣ
            if (!string.IsNullOrEmpty(questionData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<IdiomChainAdditionalData>(questionData.additionalData);
                    DisplayQuestionDirect(additionalInfo.displayText);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"������������ʧ��: {e.Message}");
                    ShowQuestion(currentIdiom);
                }
            }
            else
            {
                ShowQuestion(currentIdiom);
            }

            isGameInProgress = true;
        }

        /// <summary>
        /// ����������Ŀ������ģʽ��
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[IdiomChain] ����������Ŀ");

            if (networkData == null)
            {
                Debug.LogError("[IdiomChain] ������Ŀ����Ϊ��");
                DisplayErrorMessage("������Ŀ���ݴ���");
                return;
            }

            if (networkData.questionType != QuestionType.IdiomChain)
            {
                Debug.LogError($"[IdiomChain] ��Ŀ���Ͳ�ƥ��: ����{QuestionType.IdiomChain}, ʵ��{networkData.questionType}");
                DisplayErrorMessage("��Ŀ���ʹ���");
                return;
            }

            // ʹ�������ṩ�ĳ���
            currentIdiom = networkData.correctAnswer;
            isGameInProgress = true;

            // �����������ʾ��ʽ����additionalData�л�ȡ
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<IdiomChainAdditionalData>(networkData.additionalData);
                    if (!string.IsNullOrEmpty(additionalInfo.displayText))
                    {
                        DisplayQuestionDirect(additionalInfo.displayText);
                        return;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"�������總������ʧ��: {e.Message}");
                }
            }

            ShowQuestion(currentIdiom);
        }

        /// <summary>
        /// ��ʾ��Ŀ���������һ���֣�
        /// </summary>
        private void ShowQuestion(string idiom)
        {
            if (string.IsNullOrEmpty(idiom))
            {
                DisplayErrorMessage("�������ݴ���");
                return;
            }

            hasAnswered = false;
            string displayText = CreateDisplayText(idiom);
            DisplayQuestionDirect(displayText);
        }

        /// <summary>
        /// ֱ����ʾ��Ŀ�ı�����������ģʽ��Ԥ��ʽ���ı���
        /// </summary>
        private void DisplayQuestionDirect(string displayText)
        {
            questionText.text = displayText;
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;

            // ���ý���
            answerInput.interactable = true;
            submitButton.interactable = true;
            surrenderButton.interactable = true;

            // ���������
            answerInput.ActivateInputField();

            Debug.Log($"[IdiomChain] ��Ŀ��ʾ���: {displayText}");
        }

        /// <summary>
        /// ��ʾ������Ϣ
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            Debug.LogWarning($"[IdiomChain] {message}");

            if (questionText != null)
                questionText.text = message;

            if (feedbackText != null)
                feedbackText.text = "";

            if (answerInput != null)
            {
                answerInput.text = "";
                answerInput.interactable = false;
            }

            if (submitButton != null)
                submitButton.interactable = false;

            if (surrenderButton != null)
                surrenderButton.interactable = false;

            isGameInProgress = false;
        }

        /// <summary>
        /// ��鱾�ش𰸣�����ģʽ��
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            Debug.Log($"[IdiomChain] ��鱾�ش�: {answer}");

            bool isCorrect = ValidateIdiomChain(answer);

            if (isCorrect)
            {
                // ����ģʽ�¼�������
                currentIdiom = answer;
                feedbackText.text = "�ش���ȷ����������...";
                feedbackText.color = Color.green;

                // ������ʱ������ʾ��һ��
                if (timerManager != null)
                {
                    timerManager.StopTimer();
                    timerManager.StartTimer();
                }

                Invoke(nameof(ShowNextQuestion), 0.5f);
            }
            else
            {
                // ����ˣ���Ϸ����
                feedbackText.text = GetValidationErrorMessage(answer);
                feedbackText.color = Color.red;

                StartCoroutine(ShowFeedbackAndNotify(false));
            }
        }

        /// <summary>
        /// ��֤���������
        /// </summary>
        private bool ValidateIdiomChain(string answer)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(currentIdiom))
                return false;

            // 1. ��鿪ͷ�ַ��Ƿ���ȷ
            if (answer[0] != currentIdiom[currentIdiom.Length - 1])
                return false;

            // 2. �������Ƿ�����ڴʿ���
            return IsIdiomInDatabase(answer);
        }

        /// <summary>
        /// �������Ƿ������ݿ���
        /// </summary>
        private bool IsIdiomInDatabase(string idiom)
        {
            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM (
                                SELECT word FROM idiom WHERE word=@idiom
                                UNION
                                SELECT word FROM other_idiom WHERE word=@idiom
                            )";
                        cmd.Parameters.AddWithValue("@idiom", idiom);

                        long count = (long)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"���ݿ��ѯʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ��ȡ��֤������Ϣ
        /// </summary>
        private string GetValidationErrorMessage(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "�𰸲���Ϊ�գ�";

            if (string.IsNullOrEmpty(currentIdiom))
                return "��Ŀ���ݴ���";

            if (answer[0] != currentIdiom[currentIdiom.Length - 1])
                return $"��ͷ����Ӧ��'{currentIdiom[currentIdiom.Length - 1]}'��ͷ";

            return "�ʿ����޴˳��";
        }

        /// <summary>
        /// ��ʾ��һ�⣨����ģʽ����������
        /// </summary>
        private void ShowNextQuestion()
        {
            if (isGameInProgress)
            {
                ShowQuestion(currentIdiom);
            }
        }

        /// <summary>
        /// �ύ��ť���
        /// </summary>
        private void OnSubmit()
        {
            if (hasAnswered || !isGameInProgress)
                return;

            string userAnswer = answerInput.text.Trim();
            if (string.IsNullOrEmpty(userAnswer))
                return;

            SubmitAnswer(userAnswer);
        }

        /// <summary>
        /// �����س��ύ
        /// </summary>
        private void OnInputSubmit(string value)
        {
            if (!hasAnswered && isGameInProgress && !string.IsNullOrEmpty(value.Trim()))
            {
                SubmitAnswer(value.Trim());
            }
        }

        /// <summary>
        /// Ͷ����ť���
        /// </summary>
        private void OnSurrender()
        {
            if (hasAnswered || !isGameInProgress)
                return;

            Debug.Log("[IdiomChain] ���Ͷ��");

            StopAllCoroutines();
            isGameInProgress = false;
            feedbackText.text = "��Ͷ����";
            feedbackText.color = Color.yellow;

            OnAnswerResult?.Invoke(false);
        }

        /// <summary>
        /// �ύ��
        /// </summary>
        private void SubmitAnswer(string answer)
        {
            if (hasAnswered || !isGameInProgress)
                return;

            hasAnswered = true;

            // ���ý���
            answerInput.interactable = false;
            submitButton.interactable = false;
            surrenderButton.interactable = false;

            Debug.Log($"[IdiomChain] �ύ��: '{answer}'");

            if (IsNetworkMode())
            {
                HandleNetworkAnswer(answer);
            }
            else
            {
                HandleLocalAnswer(answer);
            }
        }

        /// <summary>
        /// ��������ģʽ��
        /// </summary>
        private void HandleNetworkAnswer(string answer)
        {
            Debug.Log($"[IdiomChain] ����ģʽ�ύ��: {answer}");

            // ��ʾ�ύ״̬
            feedbackText.text = "���ύ�𰸣��ȴ����������...";
            feedbackText.color = Color.yellow;

            // �ύ�𰸵�������
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
            }
            else
            {
                Debug.LogError("[IdiomChain] NetworkManagerʵ�������ڣ��޷��ύ��");
            }
        }

        /// <summary>
        /// ������ģʽ��
        /// </summary>
        private void HandleLocalAnswer(string answer)
        {
            CheckLocalAnswer(answer);
        }

        /// <summary>
        /// ��ʾ������Ϣ��֪ͨ���
        /// </summary>
        private IEnumerator ShowFeedbackAndNotify(bool isCorrect)
        {
            // �ȴ�һ��ʱ����ʾ����
            yield return new WaitForSeconds(1.5f);

            // ֪ͨ������
            OnAnswerResult?.Invoke(isCorrect);

            isGameInProgress = false;
        }

        /// <summary>
        /// ��ʾ�����������������ϵͳ���ã�
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[IdiomChain] �յ�������: {(isCorrect ? "��ȷ" : "����")}");

            isGameInProgress = false;

            if (isCorrect)
            {
                feedbackText.text = "�ش���ȷ��";
                feedbackText.color = Color.green;
            }
            else
            {
                feedbackText.text = $"�ش������ȷ��ʾ����{correctAnswer}";
                feedbackText.color = Color.red;
            }

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// ������Դ
        /// </summary>
        private void OnDestroy()
        {
            // �����¼�����
            if (submitButton != null)
                submitButton.onClick.RemoveAllListeners();
            if (surrenderButton != null)
                surrenderButton.onClick.RemoveAllListeners();
            if (answerInput != null)
                answerInput.onSubmit.RemoveAllListeners();
        }
    }

    /// <summary>
    /// ��������������ݽṹ���������紫�䣩
    /// </summary>
    [System.Serializable]
    public class IdiomChainAdditionalData
    {
        public string displayText;      // Ԥ��ʽ������ʾ�ı�
        public string currentIdiom;     // ��ǰ����
        public char targetChar;         // ��Ҫ�������ַ�
    }
}