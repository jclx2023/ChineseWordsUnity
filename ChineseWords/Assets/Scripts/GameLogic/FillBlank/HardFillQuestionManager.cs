using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// Ӳ�Ե��ʲ�ȫ�������
    /// - ֧�ֵ���������ģʽ
    /// - ʵ��IQuestionDataProvider�ӿڣ�֧��Host����
    /// - ����ģʽ�����ʳ���Ȩ�����ʣ����չʾ�����ַ�
    /// - ����ģʽ��ʹ�÷������ṩ����Ŀ����
    /// - ������������ʣ�У�鳤�ȡ������֡��ʿ������
    /// - "Ͷ��"��Ϊ�ύ�մ𰸣���������
    /// </summary>
    public class HardFillQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        private string dbPath;

        [Header("UI����")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/HardFillUI";

        [Header("����ģʽ����")]
        [Header("Ƶ�ʷ�Χ��Freq��")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        [Header("���ʳ���Ȩ��ʱ������ܺ�=1��")]
        [SerializeField, Range(0f, 1f)] private float weight2 = 0.32f;
        [SerializeField, Range(0f, 1f)] private float weight3 = 0.32f;
        [SerializeField, Range(0f, 1f)] private float weight4 = 0.32f;
        [SerializeField, Range(0f, 1f)] private float weightOther = 0.04f;

        [Header("��֪��������С�ڴʳ���")]
        [SerializeField] private int revealCount = 2;

        [Header("UI�������")]
        private TMP_Text questionText;
        private TMP_InputField answerInput;
        private Button submitButton;
        private Button surrenderButton;
        private TMP_Text feedbackText;

        private string currentWord;
        private int[] revealIndices;
        private bool hasAnswered = false;

        // IQuestionDataProvider�ӿ�ʵ��
        public QuestionType QuestionType => QuestionType.HardFill;

        protected override void Awake()
        {
            base.Awake(); // ������������ʼ��
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
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
                Debug.Log("[HardFill] Host����ģʽ������UI��ʼ��");
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
            Debug.Log("[HardFill] Host�����������");

            // 1. ����Ȩ��ȷ��Ŀ��ʳ�
            int targetLength = GetWeightedWordLength();

            // 2. ������
            string word = GetRandomWord(targetLength);

            if (string.IsNullOrEmpty(word))
            {
                Debug.LogWarning("[HardFill] Host���⣺���޷��������Ĵ���");
                return null;
            }

            // 3. ������ʾģʽ
            int[] revealPattern = GenerateRevealPattern(word);

            // 4. ������ʾ�ı�
            string displayText = CreateDisplayText(word, revealPattern);

            // 5. ������������
            var additionalData = new HardFillAdditionalData
            {
                revealIndices = revealPattern,
                revealCount = revealPattern.Length,
                displayPattern = displayText
            };

            // 6. ����������Ŀ����
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.HardFill,
                questionText = $"��Ŀ��{displayText}",
                correctAnswer = word,
                options = new string[0], // ����ⲻ��Ҫѡ��
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(additionalData)
            };

            Debug.Log($"[HardFill] Host����ɹ�: {displayText} (��: {word})");
            return questionData;
        }

        /// <summary>
        /// Ϊָ������������ʾģʽ
        /// </summary>
        private int[] GenerateRevealPattern(string word)
        {
            if (string.IsNullOrEmpty(word))
                return new int[0];

            int wordLength = word.Length;
            int revealNum = Mathf.Clamp(revealCount, 1, wordLength - 1);

            // ���ѡ��Ҫ��ʾ��λ��
            var allIndices = Enumerable.Range(0, wordLength).ToArray();

            // Fisher-Yates ϴ���㷨
            for (int i = allIndices.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = allIndices[i];
                allIndices[i] = allIndices[j];
                allIndices[j] = temp;
            }

            return allIndices.Take(revealNum).OrderBy(x => x).ToArray();
        }

        /// <summary>
        /// ������ʾ�ı�
        /// </summary>
        private string CreateDisplayText(string word, int[] revealPattern)
        {
            if (string.IsNullOrEmpty(word) || revealPattern == null)
                return "";

            var displayText = new System.Text.StringBuilder();
            for (int i = 0; i < word.Length; i++)
            {
                if (revealPattern.Contains(i))
                    displayText.Append(word[i]);
                else
                    displayText.Append('_');
            }

            return displayText.ToString();
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUI()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[HardFill] UIManagerʵ��������");
                return;
            }

            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"[HardFill] �޷�����UIԤ����: {uiPrefabPath}");
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
                Debug.LogError("[HardFill] UI�����ȡʧ�ܣ����Ԥ����ṹ");
                return;
            }

            // �󶨰�ť�¼�
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);

            surrenderButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.AddListener(OnSurrender);

            // �������س��¼�
            answerInput.onSubmit.RemoveAllListeners();
            answerInput.onSubmit.AddListener(OnInputSubmit);

            feedbackText.text = string.Empty;

            Debug.Log("[HardFill] UI��ʼ�����");
        }

        /// <summary>
        /// ���ر�����Ŀ������ģʽ��
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[HardFill] ���ر�����Ŀ");

            // ʹ��GetQuestionData()�������ó����߼�
            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("û�з��������Ĵ�����");
                return;
            }

            // ������Ŀ����
            currentWord = questionData.correctAnswer;

            // �Ӹ��������н�����ʾģʽ
            if (!string.IsNullOrEmpty(questionData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<HardFillAdditionalData>(questionData.additionalData);
                    revealIndices = additionalInfo.revealIndices;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"������������ʧ��: {e.Message}������������ʾģʽ");
                    revealIndices = GenerateRevealPattern(currentWord);
                }
            }
            else
            {
                revealIndices = GenerateRevealPattern(currentWord);
            }

            // ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ����������Ŀ������ģʽ��
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[HardFill] ����������Ŀ");

            if (networkData == null)
            {
                Debug.LogError("[HardFill] ������Ŀ����Ϊ��");
                DisplayErrorMessage("������Ŀ���ݴ���");
                return;
            }

            if (networkData.questionType != QuestionType.HardFill)
            {
                Debug.LogError($"[HardFill] ��Ŀ���Ͳ�ƥ��: ����{QuestionType.HardFill}, ʵ��{networkData.questionType}");
                DisplayErrorMessage("��Ŀ���ʹ���");
                return;
            }

            // �����������н�����Ŀ��Ϣ
            ParseNetworkQuestionData(networkData);

            // ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ����������Ŀ����
        /// </summary>
        private void ParseNetworkQuestionData(NetworkQuestionData networkData)
        {
            currentWord = networkData.correctAnswer;

            // ��additionalData�н�����ʾģʽ��JSON��ʽ��
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<HardFillAdditionalData>(networkData.additionalData);
                    revealIndices = additionalInfo.revealIndices;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"�������總������ʧ��: {e.Message}��ʹ��Ĭ����ʾģʽ");
                    revealIndices = GenerateRevealPattern(currentWord);
                }
            }
            else
            {
                // ���û�и������ݣ�ʹ��Ĭ��ģʽ
                revealIndices = GenerateRevealPattern(currentWord);
            }
        }

        /// <summary>
        /// ����Ȩ��ȷ���ʳ�
        /// </summary>
        private int GetWeightedWordLength()
        {
            float r = Random.value;
            if (r < weight2) return 2;
            else if (r < weight2 + weight3) return 3;
            else if (r < weight2 + weight3 + weight4) return 4;
            else return -1; // ��������
        }

        /// <summary>
        /// �����ݿ������ȡ����
        /// </summary>
        private string GetRandomWord(int targetLength)
        {
            string word = null;

            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        if (targetLength > 0)
                        {
                            cmd.CommandText = @"
                                SELECT word FROM (
                                    SELECT word, Freq, length FROM word
                                    UNION ALL
                                    SELECT word, Freq, length FROM idiom
                                ) WHERE length = @len AND Freq BETWEEN @min AND @max 
                                ORDER BY RANDOM() LIMIT 1";
                            cmd.Parameters.AddWithValue("@len", targetLength);
                        }
                        else
                        {
                            cmd.CommandText = @"
                                SELECT word FROM (
                                    SELECT word, Freq, length FROM word
                                    UNION ALL
                                    SELECT word, Freq, length FROM idiom
                                ) WHERE length NOT IN (2,3,4) AND Freq BETWEEN @min AND @max 
                                ORDER BY RANDOM() LIMIT 1";
                        }
                        cmd.Parameters.AddWithValue("@min", freqMin);
                        cmd.Parameters.AddWithValue("@max", freqMax);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                word = reader.GetString(0);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"���ݿ��ѯʧ��: {e.Message}");
            }

            return word;
        }

        /// <summary>
        /// �����ַ���ʾģʽ������ԭ���߼����ڵ���ģʽ��
        /// </summary>
        private void GenerateRevealPattern()
        {
            revealIndices = GenerateRevealPattern(currentWord);
        }

        /// <summary>
        /// ��ʾ��Ŀ
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(currentWord) || revealIndices == null)
            {
                DisplayErrorMessage("��Ŀ���ݴ���");
                return;
            }

            // ������ʾ�ı�
            string displayText = CreateDisplayText(currentWord, revealIndices);

            questionText.text = $"��Ŀ��{displayText}";

            // ��������ͷ���
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.interactable = true;
            submitButton.interactable = true;
            surrenderButton.interactable = true;

            // ���������
            answerInput.ActivateInputField();

            Debug.Log($"[HardFill] ��Ŀ��ʾ���: {displayText} (��: {currentWord})");
        }

        /// <summary>
        /// ��ʾ������Ϣ
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            Debug.LogWarning($"[HardFill] {message}");

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
        }

        /// <summary>
        /// ��鱾�ش𰸣�����ģʽ��
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            Debug.Log($"[HardFill] ��鱾�ش�: {answer}");

            bool isCorrect = ValidateAnswer(answer.Trim());
            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// ��֤��
        /// </summary>
        private bool ValidateAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(currentWord))
                return false;

            // 1. ��鳤��
            if (answer.Length != currentWord.Length)
                return false;

            // 2. �������ʾ���ַ��Ƿ���ȷ
            foreach (int idx in revealIndices)
            {
                if (answer[idx] != currentWord[idx])
                    return false;
            }

            // 3. �����Ƿ�����ڴʿ���
            return IsWordInDatabase(answer);
        }

        /// <summary>
        /// �����Ƿ������ݿ���
        /// </summary>
        private bool IsWordInDatabase(string word)
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
                                SELECT word FROM word
                                UNION ALL
                                SELECT word FROM idiom
                            ) WHERE word = @word";
                        cmd.Parameters.AddWithValue("@word", word);

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
        /// �ύ��ť���
        /// </summary>
        private void OnSubmit()
        {
            if (hasAnswered)
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
            if (!hasAnswered && !string.IsNullOrEmpty(value.Trim()))
            {
                SubmitAnswer(value.Trim());
            }
        }

        /// <summary>
        /// Ͷ����ť���
        /// </summary>
        private void OnSurrender()
        {
            if (hasAnswered)
                return;

            Debug.Log("[HardFill] ���Ͷ��");
            SubmitAnswer(""); // �ύ�մ𰸱�ʾͶ��
        }

        /// <summary>
        /// �ύ��
        /// </summary>
        private void SubmitAnswer(string answer)
        {
            if (hasAnswered)
                return;

            hasAnswered = true;
            StopAllCoroutines();

            // ���ý���
            answerInput.interactable = false;
            submitButton.interactable = false;
            surrenderButton.interactable = false;

            Debug.Log($"[HardFill] �ύ��: '{answer}'");

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
            Debug.Log($"[HardFill] ����ģʽ�ύ��: {answer}");

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
                Debug.LogError("[HardFill] NetworkManagerʵ�������ڣ��޷��ύ��");
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
            // ��ʾ����
            if (feedbackText != null)
            {
                if (isCorrect)
                {
                    feedbackText.text = "�ش���ȷ��";
                    feedbackText.color = Color.green;
                }
                else
                {
                    feedbackText.text = $"�ش������ȷʾ����{currentWord}";
                    feedbackText.color = Color.red;
                }
            }

            // �ȴ�һ��ʱ��
            yield return new WaitForSeconds(1.5f);

            // ֪ͨ������
            OnAnswerResult?.Invoke(isCorrect);
        }

        /// <summary>
        /// ��ʾ�����������������ϵͳ���ã�
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[HardFill] �յ�������: {(isCorrect ? "��ȷ" : "����")}");

            currentWord = correctAnswer;
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
    /// Ӳ����⸽�����ݽṹ���������紫�䣩
    /// </summary>
    [System.Serializable]
    public class HardFillAdditionalData
    {
        public int[] revealIndices;
        public int revealCount;
        public string displayPattern;
    }
}