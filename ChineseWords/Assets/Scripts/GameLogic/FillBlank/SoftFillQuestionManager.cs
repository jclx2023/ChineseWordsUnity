using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// ���Ե��ʲ�ȫ������� - UI�ܹ��ع���
    /// 
    /// ����˵����
    /// - ����������ͨ���ģʽ��*��ʾ���ⳤ�ȣ�_��ʾ�����ַ���
    /// - ʹ��������ʽƥ��𰸣���֤�ʿ������
    /// - Host��������Ŀ���ݺ�ͨ���ģʽ
    /// - ֧������ģʽ�µ���Ŀ���ɺʹ���֤
    /// 
    /// �ع����ݣ�
    /// - �Ƴ�����UI��ش��룬UI������ת����AnswerUIManager
    /// - �����������ɡ�����ͨ�źʹ���֤���Ĺ���
    /// - �Ż�����ṹ��ͳһ���͹�����������
    /// </summary>
    public class SoftFillQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region �����ֶ�

        [Header("��Ŀ��������")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;
        [SerializeField] private int revealCount = 2; // ��֪�ַ�����

        #endregion

        #region ˽���ֶ�

        private string dbPath;

        // ��ǰ��Ŀ����
        private string currentWord;
        private string stemPattern;
        private Regex matchRegex;

        #endregion

        #region IQuestionDataProvider�ӿ�ʵ��

        public QuestionType QuestionType => QuestionType.SoftFill;

        #endregion

        #region Unity��������

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("SoftFill���͹�������ʼ�����");
        }

        #endregion

        #region ��Ŀ��������

        /// <summary>
        /// ��ȡ��Ŀ���ݣ�IQuestionDataProvider�ӿ�ʵ�֣�
        /// ΪHost�˳���ʹ�ã�����ͨ���ģʽ�ʹ�
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("��ʼ����SoftFill��Ŀ����");

            try
            {
                // 1. �����ȡ����
                string word = GetRandomWordFromDatabase();
                if (string.IsNullOrEmpty(word))
                {
                    LogError("�޷��ҵ����������Ĵ���");
                    return CreateFallbackQuestion();
                }

                // 2. ����ͨ���ģʽ
                string pattern = GenerateWildcardPattern(word);
                if (string.IsNullOrEmpty(pattern))
                {
                    LogError("ͨ���ģʽ����ʧ��");
                    return CreateFallbackQuestion();
                }

                // 3. ����������ʽģʽ
                string regexPattern = CreateRegexPattern(pattern);

                // 4. ������������
                var additionalData = new SoftFillAdditionalData
                {
                    stemPattern = pattern,
                    regexPattern = regexPattern,
                    revealIndices = ExtractRevealIndices(word, pattern)
                };

                // 5. ������Ŀ����
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.SoftFill,
                    questionText = FormatQuestionText(pattern),
                    correctAnswer = word,
                    options = new string[0],
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"SoftFill��Ŀ���ɳɹ�: {pattern} (ʾ����: {word})");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"����SoftFill��Ŀʧ��: {e.Message}");
                return CreateFallbackQuestion();
            }
        }

        /// <summary>
        /// �����ݿ������ȡ����
        /// </summary>
        private string GetRandomWordFromDatabase()
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
                                SELECT word FROM word WHERE Freq BETWEEN @min AND @max
                                UNION ALL
                                SELECT word FROM idiom WHERE Freq BETWEEN @min AND @max
                            ) ORDER BY RANDOM() LIMIT 1";

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
        /// Ϊָ����������ͨ���ģʽ
        /// </summary>
        private string GenerateWildcardPattern(string word)
        {
            if (string.IsNullOrEmpty(word))
                return "";

            int wordLength = word.Length;
            int revealedCount = Mathf.Clamp(revealCount, 1, wordLength - 1);

            // ���ѡ��Ҫ��ʾ���ַ�λ��
            var indices = Enumerable.Range(0, wordLength).ToArray();

            // Fisher-Yates ϴ���㷨
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }

            var selectedIndices = indices.Take(revealedCount).OrderBy(i => i).ToArray();

            // ����ͨ������
            var sb = new System.Text.StringBuilder();
            sb.Append("*");
            sb.Append(word[selectedIndices[0]]);

            for (int k = 1; k < selectedIndices.Length; k++)
            {
                int gap = selectedIndices[k] - selectedIndices[k - 1] - 1;
                sb.Append(new string('_', gap));
                sb.Append(word[selectedIndices[k]]);
            }
            sb.Append("*");

            LogDebug($"����ͨ���ģʽ: {sb} (���ڴ���: {word})");
            return sb.ToString();
        }

        /// <summary>
        /// ����������ʽģʽ
        /// </summary>
        private string CreateRegexPattern(string stemPattern)
        {
            if (string.IsNullOrEmpty(stemPattern))
                return "";

            try
            {
                var pattern = "^" + string.Concat(stemPattern.Select(c =>
                {
                    if (c == '*') return ".*";
                    if (c == '_') return ".";
                    return Regex.Escape(c.ToString());
                })) + "$";

                return pattern;
            }
            catch (System.Exception e)
            {
                LogError($"����������ʽʧ��: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// ��ͨ���ģʽ����ȡ��ʾ�ַ���λ��
        /// </summary>
        private int[] ExtractRevealIndices(string word, string pattern)
        {
            var indices = new List<int>();

            // �򻯵���ȡ�߼�����Ҫ����������Ϣ����
            for (int i = 0; i < word.Length && i < pattern.Length; i++)
            {
                if (pattern[i] != '*' && pattern[i] != '_')
                {
                    indices.Add(i);
                }
            }

            return indices.ToArray();
        }

        /// <summary>
        /// ��ʽ����Ŀ�ı�
        /// </summary>
        private string FormatQuestionText(string pattern)
        {
            return $"��Ŀ��������һ������<color=red>{pattern}</color>��ʽ�ĵ���\nHint��*Ϊ������֣�_Ϊ������";
        }

        /// <summary>
        /// ����������Ŀ
        /// </summary>
        private NetworkQuestionData CreateFallbackQuestion()
        {
            LogDebug("����SoftFill������Ŀ");

            return new NetworkQuestionData
            {
                questionType = QuestionType.SoftFill,
                questionText = "��Ŀ��������һ������<color=red>*��_��*</color>��ʽ�ĵ���\nHint��*Ϊ������֣�_Ϊ������",
                correctAnswer = "�л����񹲺͹�",
                options = new string[0],
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(new SoftFillAdditionalData
                {
                    stemPattern = "*��_��*",
                    regexPattern = "^.*��.��.*$",
                    revealIndices = new int[] { 0, 2 }
                })
            };
        }

        #endregion

        #region ������Ŀ����

        /// <summary>
        /// ����������Ŀ����
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("��������SoftFill��Ŀ");

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

            // ����������ʽ���ڱ�����֤
            CreateMatchRegex();

            LogDebug($"����SoftFill��Ŀ�������: {stemPattern}");
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

            if (networkData.questionType != QuestionType.SoftFill)
            {
                LogError($"��Ŀ���Ͳ�ƥ�䣬����: {QuestionType.SoftFill}, ʵ��: {networkData.questionType}");
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
                currentWord = questionData.correctAnswer;

                // �Ӹ��������н���ͨ���ģʽ
                if (!string.IsNullOrEmpty(questionData.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<SoftFillAdditionalData>(questionData.additionalData);
                    stemPattern = additionalInfo.stemPattern;
                }
                else
                {
                    // ����Ŀ�ı�����ȡģʽ
                    ExtractPatternFromQuestionText(questionData.questionText);
                }

                LogDebug($"��Ŀ���ݽ����ɹ�: ģʽ={stemPattern}, ��={currentWord}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"������Ŀ����ʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ����Ŀ�ı�����ȡͨ���ģʽ
        /// </summary>
        private void ExtractPatternFromQuestionText(string questionText)
        {
            try
            {
                // Ѱ�� <color=red>ģʽ</color> ��ʽ
                var colorMatch = Regex.Match(questionText, @"<color=red>([^<]+)</color>");
                if (colorMatch.Success)
                {
                    stemPattern = colorMatch.Groups[1].Value;
                    return;
                }

                // Ѱ��ͨ���ģʽ
                var wildcardMatch = Regex.Match(questionText, @"([*_\u4e00-\u9fa5]+)");
                if (wildcardMatch.Success)
                {
                    stemPattern = wildcardMatch.Groups[1].Value;
                    return;
                }

                LogError("�޷�����Ŀ�ı�����ģʽ��ʹ��Ĭ��ģʽ");
                stemPattern = GenerateWildcardPattern(currentWord);
            }
            catch (System.Exception e)
            {
                LogError($"����Ŀ�ı���ȡģʽʧ��: {e.Message}");
                stemPattern = GenerateWildcardPattern(currentWord);
            }
        }

        /// <summary>
        /// ����ƥ���������ʽ
        /// </summary>
        private void CreateMatchRegex()
        {
            if (string.IsNullOrEmpty(stemPattern))
                return;

            try
            {
                string regexPattern = CreateRegexPattern(stemPattern);
                matchRegex = new Regex(regexPattern);
                LogDebug($"����������ʽ: {regexPattern}");
            }
            catch (System.Exception e)
            {
                LogError($"����������ʽʧ��: {e.Message}");
                matchRegex = null;
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
            Debug.Log($"[SoftFill] ��̬��֤��: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[SoftFill] ��̬��֤: ��Ϊ�ջ���Ŀ������Ч");
                return false;
            }

            if (question.questionType != QuestionType.SoftFill)
            {
                Debug.LogError($"[SoftFill] ��̬��֤: ��Ŀ���Ͳ�ƥ�䣬����SoftFill��ʵ��{question.questionType}");
                return false;
            }

            try
            {
                // 1. ����ͨ���ģʽ
                string stemPattern = ExtractStemPatternStatic(question);

                if (string.IsNullOrEmpty(stemPattern))
                {
                    Debug.Log("[SoftFill] ��̬��֤: �޷���ȡͨ���ģʽ��ʹ��ֱ�ӱȽ�");
                    return answer.Trim().Equals(question.correctAnswer?.Trim(), System.StringComparison.OrdinalIgnoreCase);
                }

                Debug.Log($"[SoftFill] ��̬��֤ģʽ: {stemPattern}");

                // 2. ��֤ģʽƥ��
                bool patternMatches = ValidatePatternMatchStatic(answer.Trim(), stemPattern);

                if (!patternMatches)
                {
                    Debug.Log($"[SoftFill] ��̬��֤: �� '{answer}' ��ƥ��ģʽ '{stemPattern}'");
                    return false;
                }

                // 3. ��֤�ʿ������
                bool wordExists = IsWordInDatabaseStatic(answer.Trim());

                if (!wordExists)
                {
                    Debug.Log($"[SoftFill] ��̬��֤: �� '{answer}' ���ڴʿ���");
                    return false;
                }

                Debug.Log($"[SoftFill] ��̬��֤ͨ��: {answer}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SoftFill] ��̬��֤�쳣: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ��̬��ȡͨ���ģʽ
        /// </summary>
        private static string ExtractStemPatternStatic(NetworkQuestionData question)
        {
            try
            {
                // ����1���Ӹ��������н���
                if (!string.IsNullOrEmpty(question.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<SoftFillAdditionalData>(question.additionalData);
                    if (!string.IsNullOrEmpty(additionalInfo.stemPattern))
                    {
                        return additionalInfo.stemPattern;
                    }
                }

                // ����2������Ŀ�ı�����ȡ
                if (!string.IsNullOrEmpty(question.questionText))
                {
                    // Ѱ�� <color=red>ģʽ</color> ��ʽ
                    var colorMatch = Regex.Match(question.questionText, @"<color=red>([^<]+)</color>");
                    if (colorMatch.Success)
                    {
                        return colorMatch.Groups[1].Value;
                    }

                    // Ѱ��ͨ���ģʽ
                    var wildcardMatch = Regex.Match(question.questionText, @"([*_\u4e00-\u9fa5]+[*_][*_\u4e00-\u9fa5]*)");
                    if (wildcardMatch.Success)
                    {
                        return wildcardMatch.Groups[1].Value;
                    }
                }

                return "";
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SoftFill] ��̬����ģʽʧ��: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// ��̬ģʽƥ����֤
        /// </summary>
        private static bool ValidatePatternMatchStatic(string answer, string stemPattern)
        {
            try
            {
                // ����������ʽģʽ
                var regexPattern = "^" + string.Concat(stemPattern.Select(c =>
                {
                    if (c == '*') return ".*";
                    if (c == '_') return ".";
                    return Regex.Escape(c.ToString());
                })) + "$";

                var regex = new Regex(regexPattern);
                return regex.IsMatch(answer);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SoftFill] ��̬ģʽƥ��ʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ��̬�ʿ��� - ��Host�˵���
        /// </summary>
        public static bool IsWordInDatabaseStatic(string word)
        {
            if (string.IsNullOrEmpty(word))
                return false;

            string dbPath = Application.streamingAssetsPath + "/dictionary.db";

            try
            {
                if (!System.IO.File.Exists(dbPath))
                {
                    Debug.LogWarning($"[SoftFill] ��̬��֤: ���ݿ��ļ�������: {dbPath}");
                    return false;
                }

                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM (
                                SELECT word FROM word WHERE word = @word
                                UNION ALL
                                SELECT word FROM idiom WHERE word = @word
                            )";
                        cmd.Parameters.AddWithValue("@word", word);

                        long count = (long)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SoftFill] ��̬�ʿ��ѯʧ��: {e.Message}");
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

            // ����Ƿ�ƥ��ͨ���ģʽ
            if (matchRegex == null || !matchRegex.IsMatch(answer))
            {
                LogDebug($"�𰸲�ƥ��ģʽ: {stemPattern}");
                return false;
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
                                SELECT word FROM word WHERE word = @word
                                UNION ALL
                                SELECT word FROM idiom WHERE word = @word
                            )";
                        cmd.Parameters.AddWithValue("@word", word);

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

            currentWord = correctAnswer;
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
            LogDebug("���ر���SoftFill��Ŀ");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("�޷����ɱ�����Ŀ");
                return;
            }

            ParseQuestionData(questionData);
            CreateMatchRegex();
            LogDebug($"������Ŀ�������: {stemPattern}");
        }

        #endregion

        #region ���߷���

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[SoftFill] {message}");
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[SoftFill] {message}");
        }

        #endregion
    }

    /// <summary>
    /// ������⸽�����ݽṹ
    /// </summary>
    [System.Serializable]
    public class SoftFillAdditionalData
    {
        public string stemPattern;      // ͨ���ģʽ (�� "*��_��*")
        public int[] revealIndices;     // ��ʾ���ַ�λ��
        public string regexPattern;     // ������ʽģʽ
    }
}