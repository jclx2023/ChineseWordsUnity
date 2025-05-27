using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.TorF
{
    /// <summary>
    /// ����/����������ж��������
    /// - ֧�ֵ���������ģʽ
    /// - ʵ��IQuestionDataProvider�ӿڣ�֧��Host����
    /// - ����ģʽ���� sentiment �����ȡһ����¼�����ɸ��Ŵ�
    /// - ����ģʽ��ʹ�÷������ṩ����Ŀ����
    /// - ӳ�� polarity Ϊ"����/����/����/����"
    /// - ����Ȩ�ؽϵ͵�"����"���Ż��������͸��Ŵ�
    /// - �������ȷ�𰸺͸��Ŵ𰸷��䵽���Ұ�ť
    /// </summary>
    public class SentimentTorFQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        private string dbPath;

        [Header("UI����")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/TorFUI";

        [Header("����ģʽ����")]
        [Header("Freq ��Χ (��ѡ)")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        [Header("UI�������")]
        private TMP_Text questionText;
        private Button buttonA;
        private Button buttonB;
        private TMP_Text feedbackText;

        private TMP_Text textA;
        private TMP_Text textB;

        private string currentWord;
        private int currentPolarity;
        private int choiceAPolarity;
        private int choiceBPolarity;
        private bool hasAnswered = false;

        // IQuestionDataProvider�ӿ�ʵ��
        public QuestionType QuestionType => QuestionType.SentimentTorF;

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
                Debug.Log("[SentimentTorF] Host����ģʽ������UI��ʼ��");
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
            Debug.Log("[SentimentTorF] Host�����������");

            // 1. ���ѡ�� sentiment ��¼
            var sentimentData = GetRandomSentimentData();
            if (sentimentData == null)
            {
                Debug.LogWarning("[SentimentTorF] Host���⣺������Ч��м�¼");
                return null;
            }

            // 2. ���� source �� word_id ��ȡ����
            string word = GetWordFromDatabase(sentimentData.source, sentimentData.wordId);
            if (string.IsNullOrEmpty(word))
            {
                Debug.LogWarning("[SentimentTorF] Host���⣺�Ҳ�����Ӧ����");
                return null;
            }

            // 3. ����ѡ��
            var choicesData = GenerateChoicesForHost(sentimentData.polarity);

            // 4. ������������
            var additionalData = new SentimentTorFAdditionalData
            {
                word = word,
                polarity = sentimentData.polarity,
                choices = choicesData.choices,
                correctChoiceIndex = choicesData.correctIndex,
                source = sentimentData.source,
                wordId = sentimentData.wordId
            };

            // 5. ����������Ŀ����
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.SentimentTorF,
                questionText = $"��Ŀ���ж����д�����������\n    \u300c<color=red>{word}</color>\u300d",
                correctAnswer = sentimentData.polarity.ToString(), // ʹ��polarityֵ��Ϊ��
                options = choicesData.choices, // ����ѡ��
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(additionalData)
            };

            Debug.Log($"[SentimentTorF] Host����ɹ�: {word} (polarity: {sentimentData.polarity} -> {MapPolarity(sentimentData.polarity)})");
            return questionData;
        }

        /// <summary>
        /// ΪHost��������ѡ��
        /// </summary>
        private (string[] choices, int correctIndex) GenerateChoicesForHost(int correctPolarity)
        {
            // ��ȡ��ȷ���ı�
            string correctText = MapPolarity(correctPolarity);

            // ���ɸ��Ŵ𰸣�Ȩ�ؽϵ͵�"����"���������ͣ�
            var candidates = new List<int> { 0, 1, 2, 3 }.Where(p => p != correctPolarity).ToList();
            var weights = candidates.Select(p => p == 3 ? 0.2f : 1f).ToList();
            int wrongPolarity = WeightedChoice(candidates, weights);
            string wrongText = MapPolarity(wrongPolarity);

            // ������䵽ѡ��A��B
            string[] choices = new string[2];
            int correctIndex;

            if (Random.value < 0.5f)
            {
                // ��ȷ����Aλ��
                choices[0] = correctText;
                choices[1] = wrongText;
                correctIndex = 0;
            }
            else
            {
                // ��ȷ����Bλ��
                choices[0] = wrongText;
                choices[1] = correctText;
                correctIndex = 1;
            }

            return (choices, correctIndex);
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUI()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[SentimentTorF] UIManagerʵ��������");
                return;
            }

            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"[SentimentTorF] �޷�����UIԤ����: {uiPrefabPath}");
                return;
            }

            // ��ȡUI���
            questionText = ui.Find("QuestionText")?.GetComponent<TMP_Text>();
            buttonA = ui.Find("TrueButton")?.GetComponent<Button>();
            buttonB = ui.Find("FalseButton")?.GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || buttonA == null || buttonB == null || feedbackText == null)
            {
                Debug.LogError("[SentimentTorF] UI�����ȡʧ�ܣ����Ԥ����ṹ");
                return;
            }

            // ��ȡ��ť�ı����
            textA = buttonA.GetComponentInChildren<TMP_Text>();
            textB = buttonB.GetComponentInChildren<TMP_Text>();

            if (textA == null || textB == null)
            {
                Debug.LogError("[SentimentTorF] ��ť�ı������ȡʧ��");
                return;
            }

            // �󶨰�ť�¼�
            buttonA.onClick.RemoveAllListeners();
            buttonB.onClick.RemoveAllListeners();
            buttonA.onClick.AddListener(() => OnSelectChoice(choiceAPolarity));
            buttonB.onClick.AddListener(() => OnSelectChoice(choiceBPolarity));

            feedbackText.text = string.Empty;

            Debug.Log("[SentimentTorF] UI��ʼ�����");
        }

        /// <summary>
        /// ���ر�����Ŀ������ģʽ��
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[SentimentTorF] ���ر�����Ŀ");

            // ʹ��GetQuestionData()�������ó����߼�
            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("������Ч��м�¼��");
                return;
            }

            // ������Ŀ����
            currentPolarity = int.Parse(questionData.correctAnswer);

            // �Ӹ��������н�����ϸ��Ϣ
            if (!string.IsNullOrEmpty(questionData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<SentimentTorFAdditionalData>(questionData.additionalData);
                    currentWord = additionalInfo.word;

                    // ����ѡ��
                    if (additionalInfo.choices != null && additionalInfo.choices.Length == 2)
                    {
                        SetupChoices(additionalInfo.choices, additionalInfo.correctChoiceIndex);
                    }
                    else
                    {
                        GenerateChoices();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"������������ʧ��: {e.Message}������������Ŀ");
                    LoadOriginalLocalQuestion();
                    return;
                }
            }
            else
            {
                LoadOriginalLocalQuestion();
                return;
            }

            // ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ԭʼ�ı�����Ŀ�����߼������ã�
        /// </summary>
        private void LoadOriginalLocalQuestion()
        {
            // 1. ���ѡ�� sentiment ��¼
            var sentimentData = GetRandomSentimentData();
            if (sentimentData == null)
            {
                DisplayErrorMessage("������Ч��м�¼��");
                return;
            }

            // 2. ���� source �� word_id ��ȡ����
            currentWord = GetWordFromDatabase(sentimentData.source, sentimentData.wordId);
            if (string.IsNullOrEmpty(currentWord))
            {
                Debug.Log("�Ҳ�����Ӧ���������¼�����Ŀ");
                LoadQuestion(); // �ݹ�����
                return;
            }

            currentPolarity = sentimentData.polarity;

            // 3. ����ѡ��
            GenerateChoices();

            // 4. ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ����������Ŀ������ģʽ��
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[SentimentTorF] ����������Ŀ");

            if (networkData == null)
            {
                Debug.LogError("[SentimentTorF] ������Ŀ����Ϊ��");
                DisplayErrorMessage("������Ŀ���ݴ���");
                return;
            }

            if (networkData.questionType != QuestionType.SentimentTorF)
            {
                Debug.LogError($"[SentimentTorF] ��Ŀ���Ͳ�ƥ��: ����{QuestionType.SentimentTorF}, ʵ��{networkData.questionType}");
                DisplayErrorMessage("��Ŀ���ʹ���");
                return;
            }

            // ���������ݽ�����Ŀ��Ϣ
            ParseNetworkQuestionData(networkData);

            // ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ����������Ŀ����
        /// </summary>
        private void ParseNetworkQuestionData(NetworkQuestionData networkData)
        {
            // ������ȷ�𰸣�polarityֵ��
            if (int.TryParse(networkData.correctAnswer, out int polarity))
            {
                currentPolarity = polarity;
            }
            else
            {
                // ������ı���ʽ��ת��Ϊpolarityֵ
                currentPolarity = ParsePolarityText(networkData.correctAnswer);
            }

            // ��additionalData�н���������Ϣ
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<SentimentTorFAdditionalData>(networkData.additionalData);
                    currentWord = additionalInfo.word;

                    // �����Ԥ���ѡ�ֱ��ʹ��
                    if (additionalInfo.choices != null && additionalInfo.choices.Length == 2)
                    {
                        SetupChoices(additionalInfo.choices, additionalInfo.correctChoiceIndex);
                        return;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"�������總������ʧ��: {e.Message}");
                }
            }

            // ���û�и������ݣ�����Ŀ�ı�����
            ExtractWordFromQuestionText(networkData.questionText);

            // ����ѡ��
            GenerateChoices();
        }

        /// <summary>
        /// ����ѡ�ͳһ������
        /// </summary>
        private void SetupChoices(string[] choices, int correctIndex)
        {
            if (correctIndex == 0)
            {
                choiceAPolarity = currentPolarity;
                choiceBPolarity = ParsePolarityText(choices[1]);
                textA.text = choices[0];
                textB.text = choices[1];
            }
            else
            {
                choiceAPolarity = ParsePolarityText(choices[0]);
                choiceBPolarity = currentPolarity;
                textA.text = choices[0];
                textB.text = choices[1];
            }
        }

        /// <summary>
        /// ����Ŀ�ı�����ȡ����
        /// </summary>
        private void ExtractWordFromQuestionText(string questionText)
        {
            // �򵥵��ı���������ȡ�����Ĵ���
            var startTag = "��<color=red>";
            var endTag = "</color>��";

            var startIndex = questionText.IndexOf(startTag);
            var endIndex = questionText.IndexOf(endTag);

            if (startIndex != -1 && endIndex != -1)
            {
                startIndex += startTag.Length;
                currentWord = questionText.Substring(startIndex, endIndex - startIndex);
            }
        }

        /// <summary>
        /// ��������ı�Ϊpolarityֵ
        /// </summary>
        private int ParsePolarityText(string text)
        {
            switch (text)
            {
                case "����": return 0;
                case "����": return 1;
                case "����": return 2;
                case "����": return 3;
                default: return 0;
            }
        }

        /// <summary>
        /// ��ȡ����������
        /// </summary>
        private SentimentData GetRandomSentimentData()
        {
            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT id,source,word_id,polarity FROM sentiment ORDER BY RANDOM() LIMIT 1";

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new SentimentData
                                {
                                    id = reader.GetInt32(0),
                                    source = reader.GetString(1),
                                    wordId = reader.GetInt32(2),
                                    polarity = reader.GetInt32(3)
                                };
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"��ȡ�������ʧ��: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// ����source��wordId��ȡ����
        /// </summary>
        private string GetWordFromDatabase(string source, int wordId)
        {
            string tableName = source.ToLower() == "word" ? "word" :
                              source.ToLower() == "idiom" ? "idiom" : "other_idiom";

            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"SELECT word FROM {tableName} WHERE id=@id LIMIT 1";
                        cmd.Parameters.AddWithValue("@id", wordId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                return reader.GetString(0);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"��ȡ����ʧ��: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// ����ѡ�����ԭ���߼����ڵ���ģʽ��
        /// </summary>
        private void GenerateChoices()
        {
            // ��ȡ��ȷ���ı�
            string correctText = MapPolarity(currentPolarity);

            // ���ɸ��Ŵ𰸣�Ȩ�ؽϵ͵�"����"���������ͣ�
            var candidates = new List<int> { 0, 1, 2, 3 }.Where(p => p != currentPolarity).ToList();
            var weights = candidates.Select(p => p == 3 ? 0.2f : 1f).ToList();
            int wrongPolarity = WeightedChoice(candidates, weights);
            string wrongText = MapPolarity(wrongPolarity);

            // ������䵽���Ұ�ť
            if (Random.value < 0.5f)
            {
                choiceAPolarity = currentPolarity;
                choiceBPolarity = wrongPolarity;
                textA.text = correctText;
                textB.text = wrongText;
            }
            else
            {
                choiceAPolarity = wrongPolarity;
                choiceBPolarity = currentPolarity;
                textA.text = wrongText;
                textB.text = correctText;
            }
        }

        /// <summary>
        /// ��ʾ��Ŀ
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(currentWord))
            {
                DisplayErrorMessage("�������ݴ���");
                return;
            }

            // ʹ��Unicodeת�����������
            questionText.text = $"��Ŀ���ж����д�����������\n    \u300c<color=red>{currentWord}</color>\u300d";
            feedbackText.text = string.Empty;

            // ���ð�ť
            buttonA.interactable = true;
            buttonB.interactable = true;

            Debug.Log($"[SentimentTorF] ��Ŀ��ʾ���: {currentWord} (��ȷ��: {MapPolarity(currentPolarity)})");
        }

        /// <summary>
        /// ��ʾ������Ϣ
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            Debug.LogWarning($"[SentimentTorF] {message}");

            if (questionText != null)
                questionText.text = message;

            if (feedbackText != null)
                feedbackText.text = "";

            if (buttonA != null)
                buttonA.interactable = false;

            if (buttonB != null)
                buttonB.interactable = false;
        }

        /// <summary>
        /// ��鱾�ش𰸣�����ģʽ��
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            // �ж���ͨ����ť��������˷�����ֱ��ʹ��
            Debug.Log("[SentimentTorF] CheckLocalAnswer �����ã����ж���ͨ����ť����");
        }

        /// <summary>
        /// ѡ��ѡ���
        /// </summary>
        private void OnSelectChoice(int selectedPolarity)
        {
            if (hasAnswered)
            {
                Debug.Log("[SentimentTorF] �Ѿ��ش���ˣ������ظ����");
                return;
            }

            hasAnswered = true;

            // ���ð�ť��ֹ�ظ����
            buttonA.interactable = false;
            buttonB.interactable = false;

            Debug.Log($"[SentimentTorF] ѡ�����������: {MapPolarity(selectedPolarity)}");

            if (IsNetworkMode())
            {
                HandleNetworkAnswer(selectedPolarity.ToString());
            }
            else
            {
                HandleLocalAnswer(selectedPolarity);
            }
        }

        /// <summary>
        /// ��������ģʽ��
        /// </summary>
        private void HandleNetworkAnswer(string answer)
        {
            Debug.Log($"[SentimentTorF] ����ģʽ�ύ��: {answer}");

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
                Debug.LogError("[SentimentTorF] NetworkManagerʵ�������ڣ��޷��ύ��");
            }
        }

        /// <summary>
        /// ������ģʽ��
        /// </summary>
        private void HandleLocalAnswer(int selectedPolarity)
        {
            bool isCorrect = selectedPolarity == currentPolarity;
            Debug.Log($"[SentimentTorF] ����ģʽ������: {(isCorrect ? "��ȷ" : "����")}");

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
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
                    feedbackText.text = $"�ش������ȷ���ǣ�{MapPolarity(currentPolarity)}";
                    feedbackText.color = Color.red;
                }
            }

            // �ȴ�һ��ʱ��
            yield return new WaitForSeconds(1.5f);

            // ֪ͨ������
            OnAnswerResult?.Invoke(isCorrect);

            // �������ð�ťΪ��һ��׼��
            if (buttonA != null)
                buttonA.interactable = true;
            if (buttonB != null)
                buttonB.interactable = true;
        }

        /// <summary>
        /// ��ʾ�����������������ϵͳ���ã�
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[SentimentTorF] �յ�������: {(isCorrect ? "��ȷ" : "����")}");

            // ������ȷ����ʾ
            if (int.TryParse(correctAnswer, out int polarity))
            {
                currentPolarity = polarity;
            }

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// ӳ��polarityֵ���ı�
        /// </summary>
        private static string MapPolarity(int polarity)
        {
            switch (polarity)
            {
                case 0: return "����";
                case 1: return "����";
                case 2: return "����";
                case 3: return "����";
                default: return "δ֪";
            }
        }

        /// <summary>
        /// Ȩ�����ѡ��
        /// </summary>
        private static int WeightedChoice(List<int> items, List<float> weights)
        {
            float total = weights.Sum();
            float random = Random.value * total;
            float accumulator = 0;

            for (int i = 0; i < items.Count; i++)
            {
                accumulator += weights[i];
                if (random <= accumulator)
                    return items[i];
            }

            return items.Last();
        }

        /// <summary>
        /// ������Դ
        /// </summary>
        private void OnDestroy()
        {
            // ����ť�¼�����
            if (buttonA != null)
                buttonA.onClick.RemoveAllListeners();
            if (buttonB != null)
                buttonB.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// ������ݽṹ
    /// </summary>
    public class SentimentData
    {
        public int id;
        public string source;
        public int wordId;
        public int polarity;
    }

    /// <summary>
    /// ����ж��⸽�����ݽṹ���������紫�䣩
    /// </summary>
    [System.Serializable]
    public class SentimentTorFAdditionalData
    {
        public string word;                 // Ŀ�����
        public int polarity;                // ��ȷ���������
        public string[] choices;            // ѡ���ı�����
        public int correctChoiceIndex;      // ��ȷѡ�������
        public string source;               // ������Դ��
        public int wordId;                  // ����ID
    }
}