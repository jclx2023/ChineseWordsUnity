using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.TorF
{
    /// <summary>
    /// ����/����������ж�������� - UI�ܹ��ع���
    /// 
    /// ����˵����
    /// - ��sentiment�����ȡһ����¼�����ɸ��Ŵ�
    /// - ӳ��polarityΪ"����/����/����/����"
    /// - ����Ȩ�ؽϵ͵�"����"���Ż��������͸��Ŵ�
    /// - �������ȷ�𰸺͸��Ŵ𰸷��䵽����ѡ��
    /// - Host��������Ŀ���ݺ�ѡ����֤��Ϣ
    /// </summary>
    public class SentimentTorFQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region �����ֶ�

        [Header("��Ŀ��������")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        #endregion

        #region ˽���ֶ�

        private string dbPath;

        // ��ǰ��Ŀ����
        private string currentWord;
        private int currentPolarity;

        #endregion

        #region IQuestionDataProvider�ӿ�ʵ��

        public QuestionType QuestionType => QuestionType.SentimentTorF;

        #endregion

        #region Unity��������

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("SentimentTorF���͹�������ʼ�����");
        }

        #endregion

        #region ��Ŀ��������

        /// <summary>
        /// ��ȡ��Ŀ���ݣ�IQuestionDataProvider�ӿ�ʵ�֣�
        /// ΪHost�˳���ʹ�ã����ɰ������ж���Ŀ��ѡ������
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("��ʼ����SentimentTorF��Ŀ����");

            try
            {
                // 1. ���ѡ��sentiment��¼
                var sentimentData = GetRandomSentimentData();
                if (sentimentData == null)
                {
                    LogError("�޷���ȡ��Ч��м�¼");
                    return null;
                }

                // 2. ����source��word_id��ȡ����
                string word = GetWordFromDatabase(sentimentData.source, sentimentData.wordId);
                if (string.IsNullOrEmpty(word))
                {
                    LogError("�Ҳ�����Ӧ����");
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

                // 5. ������Ŀ����
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.SentimentTorF,
                    questionText = FormatQuestionText(word),
                    correctAnswer = sentimentData.polarity.ToString(),
                    options = choicesData.choices,
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"SentimentTorF��Ŀ���ɳɹ�: {word} (polarity: {sentimentData.polarity} -> {MapPolarity(sentimentData.polarity)})");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"����SentimentTorF��Ŀʧ��: {e.Message}");
                return null;
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
                LogError($"��ȡ�������ʧ��: {e.Message}");
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
                LogError($"��ȡ����ʧ��: {e.Message}");
            }

            return null;
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
        /// ��ʽ����Ŀ�ı�
        /// </summary>
        private string FormatQuestionText(string word)
        {
            return $"��Ŀ���ж����д�����������\n    \u300c<color=red>{word}</color>\u300d";
        }

        #endregion

        #region ������Ŀ����

        /// <summary>
        /// ����������Ŀ����
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("��������SentimentTorF��Ŀ");

            if (!ValidateNetworkData(networkData))
            {
                LogError("������Ŀ������֤ʧ��");
                return;
            }

            if (!ParseQuestionData(networkData))
            {
                LogError("����������Ŀ����ʧ��");
                return;
            }

            LogDebug($"����SentimentTorF��Ŀ�������: {currentWord}");
        }

        /// <summary>
        /// ��֤��������
        /// </summary>
        private bool ValidateNetworkData(NetworkQuestionData networkData)
        {
            if (networkData == null)
            {
                LogError("������Ŀ����Ϊ��");
                return false;
            }

            if (networkData.questionType != QuestionType.SentimentTorF)
            {
                LogError($"��Ŀ���Ͳ�ƥ�䣬����: {QuestionType.SentimentTorF}, ʵ��: {networkData.questionType}");
                return false;
            }

            if (networkData.options == null || networkData.options.Length != 2)
            {
                LogError("ѡ�����ݴ����ж�����Ҫ2��ѡ��");
                return false;
            }

            return true;
        }

        /// <summary>
        /// ������Ŀ����
        /// </summary>
        private bool ParseQuestionData(NetworkQuestionData questionData)
        {
            try
            {
                // ������ȷ�𰸣�polarityֵ��
                if (int.TryParse(questionData.correctAnswer, out int polarity))
                {
                    currentPolarity = polarity;
                }
                else
                {
                    // ������ı���ʽ��ת��Ϊpolarityֵ
                    currentPolarity = ParsePolarityText(questionData.correctAnswer);
                }

                // �Ӹ��������н�����ϸ��Ϣ
                if (!string.IsNullOrEmpty(questionData.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<SentimentTorFAdditionalData>(questionData.additionalData);
                    currentWord = additionalInfo.word;
                }
                else
                {
                    // ����Ŀ�ı�����ȡ����
                    ExtractWordFromQuestionText(questionData.questionText);
                }

                LogDebug($"��Ŀ���ݽ����ɹ�: ����={currentWord}, ��ȷ���={MapPolarity(currentPolarity)}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"������Ŀ����ʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ����Ŀ�ı�����ȡ����
        /// </summary>
        private void ExtractWordFromQuestionText(string questionText)
        {
            try
            {
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
            catch (System.Exception e)
            {
                LogError($"����Ŀ�ı���ȡ����ʧ��: {e.Message}");
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

        #endregion

        #region ����֤

        /// <summary>
        /// ��̬����֤���� - ��Host�˵���
        /// ������Ŀ������֤�𰸣�������ʵ��״̬
        /// </summary>
        public static bool ValidateAnswerStatic(string answer, NetworkQuestionData question)
        {
            Debug.Log($"[SentimentTorF] ��̬��֤��: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[SentimentTorF] ��̬��֤: ��Ϊ�ջ���Ŀ������Ч");
                return false;
            }

            if (question.questionType != QuestionType.SentimentTorF)
            {
                Debug.LogError($"[SentimentTorF] ��̬��֤: ��Ŀ���Ͳ�ƥ�䣬����SentimentTorF��ʵ��{question.questionType}");
                return false;
            }

            try
            {
                string correctAnswer = question.correctAnswer?.Trim();
                if (string.IsNullOrEmpty(correctAnswer))
                {
                    Debug.Log("[SentimentTorF] ��̬��֤: ��Ŀ������ȱ����ȷ��");
                    return false;
                }

                // ֧��������֤��ʽ��polarityֵ�Ƚϻ��ı��Ƚ�
                bool isCorrect = false;

                // ������Ϊpolarityֵ�Ƚ�
                if (int.TryParse(answer.Trim(), out int answerPolarity) &&
                    int.TryParse(correctAnswer, out int correctPolarity))
                {
                    isCorrect = answerPolarity == correctPolarity;
                }
                else
                {
                    // ��Ϊ�ı��Ƚ�
                    isCorrect = answer.Trim().Equals(correctAnswer, System.StringComparison.Ordinal);
                }

                Debug.Log($"[SentimentTorF] ��̬��֤���: �û���='{answer.Trim()}', ��ȷ��='{correctAnswer}', ���={isCorrect}");
                return isCorrect;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SentimentTorF] ��̬��֤�쳣: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ���ش���֤�����ڵ���ģʽ������ģʽ�ı�����֤��
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            LogDebug($"��֤���ش�: {answer}");

            bool isCorrect = ValidateLocalAnswer(answer);
            OnAnswerResult?.Invoke(isCorrect);
        }

        /// <summary>
        /// ��֤���ش�
        /// </summary>
        private bool ValidateLocalAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
            {
                LogDebug("��Ϊ��");
                return false;
            }

            // ֧��polarityֵ��֤
            if (int.TryParse(answer.Trim(), out int selectedPolarity))
            {
                bool isCorrect = selectedPolarity == currentPolarity;
                LogDebug($"������֤���: ѡ�����={MapPolarity(selectedPolarity)}, ��ȷ���={MapPolarity(currentPolarity)}, ���={isCorrect}");
                return isCorrect;
            }

            LogDebug("�𰸸�ʽ��Ч");
            return false;
        }

        #endregion

        #region ����������

        /// <summary>
        /// ��ʾ�����������������ϵͳ���ã�
        /// ��Ҫ���˷������뱣��������ϵͳ��ͨ���������
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            LogDebug($"�յ����������: {(isCorrect ? "��ȷ" : "����")}");

            // ������ȷ����ʾ
            if (int.TryParse(correctAnswer, out int polarity))
            {
                currentPolarity = polarity;
            }

            OnAnswerResult?.Invoke(isCorrect);
        }

        #endregion

        #region ����ģʽ���ݣ���������Ϸ�����

        /// <summary>
        /// ���ر�����Ŀ������ģʽ��
        /// �����˷�����Ϊ���ܱ�����ϵͳ����
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            LogDebug("���ر���SentimentTorF��Ŀ");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("�޷����ɱ�����Ŀ");
                return;
            }

            ParseQuestionData(questionData);
            LogDebug($"������Ŀ�������: {currentWord}");
        }

        #endregion

        #region ���߷���

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
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[SentimentTorF] {message}");
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[SentimentTorF] {message}");
        }

        #endregion
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
    /// ����ж��⸽�����ݽṹ
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