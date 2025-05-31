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
    /// �޸����Ӳ�Ե��ʲ�ȫ�������
    /// 
    /// �޸����ݣ�
    /// 1. ������ɣ����������ո�ʽ����"Ͷ_"��"_��"��"��_��"�ȣ�
    /// 2. ����֤�������Ҵ��Ƿ������ɸ�ʽ�������ݿ��д���
    /// 3. ����������ұ�����д�����Ԥ����ض�����
    /// 4. Host��ֻ����������ɸ�ʽ����Ԥ��"��ȷ��"
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

        [Header("�������")]
        [SerializeField] private int minWordLength = 2;
        [SerializeField] private int maxWordLength = 4;
        [SerializeField, Range(1, 3)] private int maxBlankCount = 2; // ���ո�����

        [Header("UI�������")]
        private TMP_Text questionText;
        private TMP_InputField answerInput;
        private Button submitButton;
        private Button surrenderButton;
        private TMP_Text feedbackText;

        // ��ǰ��Ŀ��Ϣ
        private string currentPattern;        // ��ɸ�ʽ����"Ͷ_"��"_��"
        private int[] blankPositions;         // �ո�λ��
        private char[] knownCharacters;       // ��֪�ַ�
        private int wordLength;               // ���ﳤ��
        private bool hasAnswered = false;

        // IQuestionDataProvider�ӿ�ʵ��
        public QuestionType QuestionType => QuestionType.HardFill;

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            if (NeedsUI())
            {
                InitializeUI();
            }
            else
            {
                Debug.Log("[HardFill] Host����ģʽ������UI��ʼ��");
            }
        }

        private bool NeedsUI()
        {
            return transform.parent == null ||
                   (transform.parent.GetComponent<HostGameManager>() == null &&
                    transform.parent.GetComponent<QuestionDataService>() == null);
        }

        /// <summary>
        /// ��ȡ��Ŀ���ݣ�IQuestionDataProvider�ӿ�ʵ�֣�
        /// �޸���ֻ������ɸ�ʽ����Ԥ��̶���
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            Debug.Log("[HardFill] Host�����������");

            // 1. ���ѡ����ﳤ��
            wordLength = Random.Range(minWordLength, maxWordLength + 1);

            // 2. ���ѡ��ո�����������ȫ�ǿո�
            int blankCount = Random.Range(1, Mathf.Min(maxBlankCount + 1, wordLength));

            // 3. �����ݿ����ѡ��һ������Ϊ�ο�������������ɸ�ʽ��
            string referenceWord = GetRandomWordForPattern(wordLength);

            if (string.IsNullOrEmpty(referenceWord))
            {
                Debug.LogWarning($"[HardFill] �޷��ҵ�����Ϊ{wordLength}�Ĳο�����");
                return null;
            }

            // 4. ���ڲο���������ɸ�ʽ
            GenerateQuestionPattern(referenceWord, blankCount);

            // 5. �����������ݣ�������֤������Ϣ��
            var additionalData = new HardFillAdditionalData
            {
                pattern = currentPattern,
                blankPositions = blankPositions,
                knownCharacters = knownCharacters,
                wordLength = wordLength,
                minFreq = freqMin,
                maxFreq = freqMax
            };

            // 6. ����������Ŀ���ݣ��������̶��𰸣�
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.HardFill,
                questionText = $"����д���ϸ�ʽ�Ĵ��{currentPattern}",
                correctAnswer = "", // �����ù̶���
                options = new string[0],
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(additionalData)
            };

            Debug.Log($"[HardFill] ����������: {currentPattern} (����: {wordLength})");
            return questionData;
        }

        /// <summary>
        /// �����ݿ��ȡ���������Ϊ������ɵĲο�
        /// </summary>
        private string GetRandomWordForPattern(int targetLength)
        {
            string word = null;

            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT word FROM (
                                SELECT word, Freq, length FROM word
                                UNION ALL
                                SELECT word, Freq, length FROM idiom
                            ) WHERE length = @len AND Freq BETWEEN @min AND @max 
                            ORDER BY RANDOM() LIMIT 1";

                        cmd.Parameters.AddWithValue("@len", targetLength);
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
        /// ���ڲο���������ɸ�ʽ
        /// </summary>
        private void GenerateQuestionPattern(string referenceWord, int blankCount)
        {
            wordLength = referenceWord.Length;
            knownCharacters = new char[wordLength];

            // ���ѡ��Ҫ���ص�λ��
            var allPositions = Enumerable.Range(0, wordLength).ToList();
            blankPositions = new int[blankCount];

            for (int i = 0; i < blankCount; i++)
            {
                int randomIndex = Random.Range(0, allPositions.Count);
                blankPositions[i] = allPositions[randomIndex];
                allPositions.RemoveAt(randomIndex);
            }

            System.Array.Sort(blankPositions);

            // �������ģʽ����֪�ַ�����
            var patternBuilder = new System.Text.StringBuilder();

            for (int i = 0; i < wordLength; i++)
            {
                if (blankPositions.Contains(i))
                {
                    patternBuilder.Append('_');
                    knownCharacters[i] = '\0'; // ���Ϊδ֪
                }
                else
                {
                    char ch = referenceWord[i];
                    patternBuilder.Append(ch);
                    knownCharacters[i] = ch;
                }
            }

            currentPattern = patternBuilder.ToString();

            Debug.Log($"[HardFill] �������: {currentPattern} (���ڲο���: {referenceWord})");
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

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("�޷�������Ŀ");
                return;
            }

            ParseQuestionData(questionData);
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
                Debug.LogError($"[HardFill] ��Ŀ���Ͳ�ƥ��");
                DisplayErrorMessage("��Ŀ���ʹ���");
                return;
            }

            ParseQuestionData(networkData);
            DisplayQuestion();
        }

        /// <summary>
        /// ������Ŀ����
        /// </summary>
        private void ParseQuestionData(NetworkQuestionData questionData)
        {
            if (!string.IsNullOrEmpty(questionData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<HardFillAdditionalData>(questionData.additionalData);
                    currentPattern = additionalInfo.pattern;
                    blankPositions = additionalInfo.blankPositions;
                    knownCharacters = additionalInfo.knownCharacters;
                    wordLength = additionalInfo.wordLength;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"������������ʧ��: {e.Message}");
                    DisplayErrorMessage("��Ŀ���ݽ�������");
                    return;
                }
            }
            else
            {
                Debug.LogError("ȱ����Ŀ��������");
                DisplayErrorMessage("��Ŀ���ݲ�����");
            }
        }

        /// <summary>
        /// ��ʾ��Ŀ
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(currentPattern))
            {
                DisplayErrorMessage("��Ŀ���ݴ���");
                return;
            }

            questionText.text = $"����д���ϸ�ʽ�Ĵ��{currentPattern}";

            // ��������ͷ���
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.interactable = true;
            submitButton.interactable = true;
            surrenderButton.interactable = true;

            // ����������ַ�����
            answerInput.characterLimit = wordLength;
            answerInput.ActivateInputField();

            Debug.Log($"[HardFill] ��Ŀ��ʾ���: {currentPattern}");
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
            StartCoroutine(ShowFeedbackAndNotify(isCorrect, answer.Trim()));
        }

        /// <summary>
        /// �޸���Ĵ���֤�߼�
        /// </summary>
        private bool ValidateAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
            {
                Debug.Log("[HardFill] ��Ϊ��");
                return false;
            }

            // 1. ��鳤���Ƿ�ƥ��
            if (answer.Length != wordLength)
            {
                Debug.Log($"[HardFill] ���Ȳ�ƥ��: ����{wordLength}, ʵ��{answer.Length}");
                return false;
            }

            // 2. �����֪λ�õ��ַ��Ƿ�ƥ��
            for (int i = 0; i < wordLength; i++)
            {
                if (knownCharacters[i] != '\0' && answer[i] != knownCharacters[i])
                {
                    Debug.Log($"[HardFill] λ��{i}�ַ���ƥ��: ����'{knownCharacters[i]}', ʵ��'{answer[i]}'");
                    return false;
                }
            }

            // 3. �������Ƿ���������ݿ���
            bool existsInDB = IsWordInDatabase(answer);
            Debug.Log($"[HardFill] ����'{answer}'�����ݿ���: {existsInDB}");

            return existsInDB;
        }

        /// <summary>
        /// ��̬��֤���� - ��Host����
        /// ������Ŀ������֤�𰸣�������ʵ��״̬
        /// </summary>
        public static bool ValidateAnswerStatic(string answer, NetworkQuestionData questionData)
        {
            if (string.IsNullOrEmpty(answer))
            {
                Debug.Log("[HardFill] ��̬��֤����Ϊ��");
                return false;
            }

            if (questionData.questionType != QuestionType.HardFill)
            {
                Debug.LogError("[HardFill] ��̬��֤����Ŀ���Ͳ�ƥ��");
                return false;
            }

            // ������Ŀ��������
            if (string.IsNullOrEmpty(questionData.additionalData))
            {
                Debug.LogError("[HardFill] ��̬��֤��ȱ����Ŀ��������");
                return false;
            }

            try
            {
                var additionalData = JsonUtility.FromJson<HardFillAdditionalData>(questionData.additionalData);

                // 1. ��鳤��
                if (answer.Length != additionalData.wordLength)
                {
                    Debug.Log($"[HardFill] ��̬��֤�����Ȳ�ƥ�� ����{additionalData.wordLength}, ʵ��{answer.Length}");
                    return false;
                }

                // 2. �����֪λ�õ��ַ��Ƿ�ƥ��
                for (int i = 0; i < additionalData.wordLength; i++)
                {
                    if (additionalData.knownCharacters[i] != '\0' &&
                        answer[i] != additionalData.knownCharacters[i])
                    {
                        Debug.Log($"[HardFill] ��̬��֤��λ��{i}�ַ���ƥ�� ����'{additionalData.knownCharacters[i]}', ʵ��'{answer[i]}'");
                        return false;
                    }
                }

                // 3. �������Ƿ������ݿ���
                bool existsInDB = IsWordInDatabaseStatic(answer, additionalData.minFreq, additionalData.maxFreq);
                Debug.Log($"[HardFill] ��̬��֤������'{answer}'���ݿ���֤���: {existsInDB}");

                return existsInDB;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HardFill] ��̬��֤ʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ��̬���ݿ��ѯ���� - ��Host����
        /// </summary>
        public static bool IsWordInDatabaseStatic(string word, int minFreq, int maxFreq)
        {
            try
            {
                string dbPath = Application.streamingAssetsPath + "/dictionary.db";

                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM (
                                SELECT word FROM word WHERE Freq BETWEEN @min AND @max
                                UNION ALL
                                SELECT word FROM idiom WHERE Freq BETWEEN @min AND @max
                            ) WHERE word = @word";

                        cmd.Parameters.AddWithValue("@word", word);
                        cmd.Parameters.AddWithValue("@min", minFreq);
                        cmd.Parameters.AddWithValue("@max", maxFreq);

                        long count = (long)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HardFill] ��̬���ݿ��ѯʧ��: {e.Message}");
                return false;
            }
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
                                SELECT word FROM word WHERE Freq BETWEEN @min AND @max
                                UNION ALL
                                SELECT word FROM idiom WHERE Freq BETWEEN @min AND @max
                            ) WHERE word = @word";

                        cmd.Parameters.AddWithValue("@word", word);
                        cmd.Parameters.AddWithValue("@min", freqMin);
                        cmd.Parameters.AddWithValue("@max", freqMax);

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

            feedbackText.text = "���ύ�𰸣��ȴ���������֤...";
            feedbackText.color = Color.yellow;

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
            }
            else
            {
                Debug.LogError("[HardFill] NetworkManagerʵ��������");
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
        private IEnumerator ShowFeedbackAndNotify(bool isCorrect, string userAnswer)
        {
            if (feedbackText != null)
            {
                if (isCorrect)
                {
                    feedbackText.text = $"�ش���ȷ��'{userAnswer}' ����Ҫ��";
                    feedbackText.color = Color.green;
                }
                else
                {
                    if (string.IsNullOrEmpty(userAnswer))
                    {
                        feedbackText.text = "δ�ύ��";
                    }
                    else
                    {
                        // �ṩ����ϸ�Ĵ�����Ϣ
                        string errorReason = GetValidationErrorReason(userAnswer);
                        feedbackText.text = $"�ش����{errorReason}";
                    }
                    feedbackText.color = Color.red;
                }
            }

            yield return new WaitForSeconds(2f);

            OnAnswerResult?.Invoke(isCorrect);
        }

        /// <summary>
        /// ��ȡ��֤ʧ�ܵ���ϸԭ��
        /// </summary>
        private string GetValidationErrorReason(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "��Ϊ��";

            if (answer.Length != wordLength)
                return $"���Ȳ���ȷ��ӦΪ{wordLength}�֣�";

            // �����֪�ַ��Ƿ�ƥ��
            for (int i = 0; i < wordLength; i++)
            {
                if (knownCharacters[i] != '\0' && answer[i] != knownCharacters[i])
                    return $"��{i + 1}λ�ַ�ӦΪ'{knownCharacters[i]}'";
            }

            // �����ʽ��ȷ���������ݿ���
            if (!IsWordInDatabase(answer))
                return "�ô��ﲻ�ڴʿ���";

            return "δ֪����";
        }

        /// <summary>
        /// ��ʾ�����������������ϵͳ���ã�
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[HardFill] �յ�������: {(isCorrect ? "��ȷ" : "����")}");

            string userAnswer = answerInput.text.Trim();
            StartCoroutine(ShowFeedbackAndNotify(isCorrect, userAnswer));
        }

        /// <summary>
        /// ������Դ
        /// </summary>
        private void OnDestroy()
        {
            if (submitButton != null)
                submitButton.onClick.RemoveAllListeners();
            if (surrenderButton != null)
                surrenderButton.onClick.RemoveAllListeners();
            if (answerInput != null)
                answerInput.onSubmit.RemoveAllListeners();
        }
    }

    /// <summary>
    /// Ӳ����⸽�����ݽṹ���޸���
    /// </summary>
    [System.Serializable]
    public class HardFillAdditionalData
    {
        public string pattern;           // ��ɸ�ʽ����"Ͷ_"
        public int[] blankPositions;     // �ո�λ������
        public char[] knownCharacters;   // ��֪�ַ����飨'\0'��ʾ�ո�λ�ã�
        public int wordLength;           // ���ﳤ��
        public int minFreq;              // Ƶ�ʷ�Χ
        public int maxFreq;
    }
}