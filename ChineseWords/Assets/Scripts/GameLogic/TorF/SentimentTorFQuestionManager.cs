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
    /// - ����ģʽ���� sentiment �����ȡһ����¼�����ɸ��Ŵ�
    /// - ����ģʽ��ʹ�÷������ṩ����Ŀ����
    /// - ӳ�� polarity Ϊ"����/����/����/����"
    /// - ����Ȩ�ؽϵ͵�"����"���Ż��������͸��Ŵ�
    /// - �������ȷ�𰸺͸��Ŵ𰸷��䵽���Ұ�ť
    /// </summary>
    public class SentimentTorFQuestionManager : NetworkQuestionManagerBase
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
        private int choiceBPolarity; // ����������
        private bool hasAnswered = false;

        protected override void Awake()
        {
            base.Awake(); // ������������ʼ��
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            InitializeUI();
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUI()
        {
            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"�޷�����UIԤ����: {uiPrefabPath}");
                return;
            }

            // ��ȡUI���
            questionText = ui.Find("QuestionText")?.GetComponent<TMP_Text>();
            buttonA = ui.Find("TrueButton")?.GetComponent<Button>();
            buttonB = ui.Find("FalseButton")?.GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || buttonA == null || buttonB == null || feedbackText == null)
            {
                Debug.LogError("UI�����ȡʧ�ܣ����Ԥ����ṹ");
                return;
            }

            // ��ȡ��ť�ı����
            textA = buttonA.GetComponentInChildren<TMP_Text>();
            textB = buttonB.GetComponentInChildren<TMP_Text>();

            if (textA == null || textB == null)
            {
                Debug.LogError("��ť�ı������ȡʧ��");
                return;
            }

            // �󶨰�ť�¼�
            buttonA.onClick.RemoveAllListeners();
            buttonB.onClick.RemoveAllListeners();
            buttonA.onClick.AddListener(() => OnSelectChoice(choiceAPolarity));
            buttonB.onClick.AddListener(() => OnSelectChoice(choiceBPolarity));

            feedbackText.text = string.Empty;
        }

        /// <summary>
        /// ���ر�����Ŀ������ģʽ��
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[SentimentTorF] ���ر�����Ŀ");

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
                Debug.LogError("������Ŀ����Ϊ��");
                DisplayErrorMessage("������Ŀ���ݴ���");
                return;
            }

            if (networkData.questionType != QuestionType.SentimentTorF)
            {
                Debug.LogError($"��Ŀ���Ͳ�ƥ��: ����{QuestionType.SentimentTorF}, ʵ��{networkData.questionType}");
                LoadLocalQuestion(); // ������������Ŀ
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
                        SetupNetworkChoices(additionalInfo.choices, additionalInfo.correctChoiceIndex);
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
        /// ��������ģʽ��ѡ��
        /// </summary>
        private void SetupNetworkChoices(string[] choices, int correctIndex)
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
        /// ����ѡ��
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
                Debug.Log("�Ѿ��ش���ˣ������ظ����");
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

            // ͨ�������ύ��������
            CheckAnswer(answer);
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

            // �ȴ�һ��ʱ��
            yield return new WaitForSeconds(1.5f);

            // ֪ͨ������
            OnAnswerResult?.Invoke(isCorrect);

            // �������ð�ťΪ��һ��׼��
            buttonA.interactable = true;
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