using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// Ӳ�Ե��ʲ�ȫ������� - UI�ܹ��ع���
    /// 
    /// ����˵����
    /// - ���������ո�ʽ��ɣ���"Ͷ_"��"_��"��"��_��"�ȣ�
    /// - ��֤��Ҵ��Ƿ������ɸ�ʽ���ڴʿ��д���
    /// - Host�˸���������ɸ�ʽ����Ԥ��̶���
    /// - ֧������ģʽ�µ���Ŀ���ɺʹ���֤
    /// </summary>
    public class HardFillQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region �����ֶ�

        [Header("��Ŀ��������")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;
        [SerializeField] private int minWordLength = 2;
        [SerializeField] private int maxWordLength = 4;
        [SerializeField, Range(1, 3)] private int maxBlankCount = 2;

        #endregion

        #region ˽���ֶ�

        private string dbPath;

        // ��ǰ��Ŀ����
        private string currentPattern;
        private int[] blankPositions;
        private char[] knownCharacters;
        private int wordLength;

        #endregion

        #region IQuestionDataProvider�ӿ�ʵ��

        public QuestionType QuestionType => QuestionType.HardFill;

        #endregion

        #region Unity��������

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("HardFill���͹�������ʼ�����");
        }

        #endregion

        #region ��Ŀ��������

        /// <summary>
        /// ��ȡ��Ŀ���ݣ�IQuestionDataProvider�ӿ�ʵ�֣�
        /// ΪHost�˳���ʹ�ã�������ɸ�ʽ����Ԥ��̶���
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("��ʼ����HardFill��Ŀ����");

            try
            {
                // 1. ȷ�����ﳤ��
                wordLength = Random.Range(minWordLength, maxWordLength + 1);

                // 2. ȷ���ո�����������ȫ�ǿո�
                int blankCount = Random.Range(1, Mathf.Min(maxBlankCount + 1, wordLength));

                // 3. ��ȡ�ο����������������
                string referenceWord = GetRandomWordFromDatabase(wordLength);

                // 4. ������ɸ�ʽ
                GenerateQuestionPattern(referenceWord, blankCount);

                // 5. ������������
                var additionalData = new HardFillAdditionalData
                {
                    pattern = currentPattern,
                    blankPositions = blankPositions,
                    knownCharacters = knownCharacters,
                    wordLength = wordLength,
                    minFreq = freqMin,
                    maxFreq = freqMax
                };

                // 6. ������Ŀ����
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.HardFill,
                    questionText = FormatQuestionText(currentPattern),
                    correctAnswer = "", // Ӳ��ղ����ù̶���
                    options = new string[0],
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"HardFill��Ŀ���ɳɹ�: {currentPattern} (����: {wordLength}, �ո���: {blankCount})");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"����HardFill��Ŀʧ��: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// �����ݿ��ȡָ�����ȵ��������
        /// </summary>
        private string GetRandomWordFromDatabase(int targetLength)
        {
            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT word FROM (
                                SELECT word FROM word WHERE length = @len AND Freq BETWEEN @min AND @max
                                UNION ALL
                                SELECT word FROM idiom WHERE length = @len AND Freq BETWEEN @min AND @max
                            ) ORDER BY RANDOM() LIMIT 1";

                        cmd.Parameters.AddWithValue("@len", targetLength);
                        cmd.Parameters.AddWithValue("@min", freqMin);
                        cmd.Parameters.AddWithValue("@max", freqMax);

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
                LogError($"���ݿ��ѯʧ��: {e.Message}");
            }

            return null;
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
            LogDebug($"������ɸ�ʽ: {currentPattern} (���ڲο���: {referenceWord})");
        }

        /// <summary>
        /// ��ʽ����Ŀ�ı�
        /// </summary>
        private string FormatQuestionText(string pattern)
        {
            return $"����д���ϸ�ʽ�Ĵ��{pattern}";
        }


        #endregion

        #region ������Ŀ����

        /// <summary>
        /// ����������Ŀ����
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("��������HardFill��Ŀ");

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

            LogDebug($"����HardFill��Ŀ�������: {currentPattern}");
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

            if (networkData.questionType != QuestionType.HardFill)
            {
                LogError($"��Ŀ���Ͳ�ƥ�䣬����: {QuestionType.HardFill}, ʵ��: {networkData.questionType}");
                return false;
            }

            if (string.IsNullOrEmpty(networkData.additionalData))
            {
                LogError("ȱ����Ŀ��������");
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
                var additionalInfo = JsonUtility.FromJson<HardFillAdditionalData>(questionData.additionalData);

                currentPattern = additionalInfo.pattern;
                blankPositions = additionalInfo.blankPositions;
                knownCharacters = additionalInfo.knownCharacters;
                wordLength = additionalInfo.wordLength;

                LogDebug($"��Ŀ���ݽ����ɹ�: ��ʽ={currentPattern}, ����={wordLength}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"������������ʧ��: {e.Message}");
                return false;
            }
        }

        #endregion

        #region ����֤

        /// <summary>
        /// ��̬����֤���� - ��Host�˵���
        /// ������Ŀ������֤�𰸣�������ʵ��״̬
        /// </summary>
        public static bool ValidateAnswerStatic(string answer, NetworkQuestionData questionData)
        {
            if (string.IsNullOrEmpty(answer))
            {
                Debug.Log("[HardFill] ��̬��֤: ��Ϊ��");
                return false;
            }

            if (questionData?.questionType != QuestionType.HardFill)
            {
                Debug.LogError("[HardFill] ��̬��֤: ��Ŀ���Ͳ�ƥ��");
                return false;
            }

            if (string.IsNullOrEmpty(questionData.additionalData))
            {
                Debug.LogError("[HardFill] ��̬��֤: ȱ����Ŀ��������");
                return false;
            }

            try
            {
                var additionalData = JsonUtility.FromJson<HardFillAdditionalData>(questionData.additionalData);

                // ��֤����
                if (answer.Length != additionalData.wordLength)
                {
                    Debug.Log($"[HardFill] ��̬��֤: ���Ȳ�ƥ�䣬����{additionalData.wordLength}��ʵ��{answer.Length}");
                    return false;
                }

                // ��֤��֪λ�õ��ַ�
                for (int i = 0; i < additionalData.wordLength; i++)
                {
                    if (additionalData.knownCharacters[i] != '\0' &&
                        answer[i] != additionalData.knownCharacters[i])
                    {
                        Debug.Log($"[HardFill] ��̬��֤: λ��{i}�ַ���ƥ�䣬����'{additionalData.knownCharacters[i]}'��ʵ��'{answer[i]}'");
                        return false;
                    }
                }

                // ��֤�����Ƿ������ݿ���
                bool existsInDB = IsWordInDatabaseStatic(answer, additionalData.minFreq, additionalData.maxFreq);
                Debug.Log($"[HardFill] ��̬��֤: ����'{answer}'���ݿ���֤���={existsInDB}");

                return existsInDB;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HardFill] ��̬��֤�쳣: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ��̬���ݿ��ѯ���� - ��Host�˵���
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
        /// ���ش���֤�����ڵ���ģʽ������ģʽ�ı�����֤��
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            LogDebug($"��֤���ش�: {answer}");

            bool isCorrect = ValidateLocalAnswer(answer.Trim());
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

            if (answer.Length != wordLength)
            {
                LogDebug($"���Ȳ�ƥ��: ����{wordLength}, ʵ��{answer.Length}");
                return false;
            }

            // �����֪λ�õ��ַ�
            for (int i = 0; i < wordLength; i++)
            {
                if (knownCharacters[i] != '\0' && answer[i] != knownCharacters[i])
                {
                    LogDebug($"λ��{i}�ַ���ƥ��: ����'{knownCharacters[i]}', ʵ��'{answer[i]}'");
                    return false;
                }
            }

            // �������Ƿ������ݿ���
            bool existsInDB = IsWordInLocalDatabase(answer);
            LogDebug($"����'{answer}'�����ݿ���: {existsInDB}");

            return existsInDB;
        }

        /// <summary>
        /// �������Ƿ��ڱ������ݿ���
        /// </summary>
        private bool IsWordInLocalDatabase(string word)
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
                LogError($"�������ݿ��ѯʧ��: {e.Message}");
                return false;
            }
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

            // ֪ͨ��������������һ��������߼�
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
            LogDebug("���ر���HardFill��Ŀ");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("�޷����ɱ�����Ŀ");
                return;
            }

            ParseQuestionData(questionData);
            LogDebug($"������Ŀ�������: {currentPattern}");
        }

        #endregion

        #region ���߷���

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[HardFill] {message}");
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[HardFill] {message}");
        }

        #endregion
    }

    /// <summary>
    /// Ӳ����⸽�����ݽṹ
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