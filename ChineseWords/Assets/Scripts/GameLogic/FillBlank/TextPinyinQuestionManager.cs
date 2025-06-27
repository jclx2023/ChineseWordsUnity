using UnityEngine;
using System.Collections;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// ����ƴ��������� - UI�ܹ��ع���
    /// 
    /// ����˵����
    /// - �����ʣ������λ�ַ�������ƴ������
    /// - ����character.Tpinyin JSON���ݽ����޵��ȶ�
    /// - ����չʾ����ƴ���������û�����
    /// - Host��������Ŀ���ݺ�ƴ����֤��Ϣ
    /// 
    /// �ع����ݣ�
    /// - �Ƴ�����UI��ش��룬UI������ת����AnswerUIManager
    /// - �����������ɡ�����ͨ�źʹ���֤���Ĺ���
    /// - �Ż�����ṹ��ͳһ���͹�����������
    /// - ����֤�߼�������ӱ�����Ŀ����
    /// </summary>
    public class TextPinyinQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region �����ֶ�

        [Header("��Ŀ��������")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;

        #endregion

        #region ˽���ֶ�

        private string dbPath;

        // ��ǰ��Ŀ����
        private string currentWord;
        private string targetCharacter;
        private int characterIndex;
        private string correctPinyinNoTone;     // �޵�ƴ�������ڱȶԣ�
        private string correctPinyinTone;       // ����ƴ�������ڷ�����

        #endregion

        #region IQuestionDataProvider�ӿ�ʵ��

        public QuestionType QuestionType => QuestionType.TextPinyin;

        #endregion

        #region Unity��������

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("TextPinyin���͹�������ʼ�����");
        }

        #endregion

        #region ��Ŀ��������

        /// <summary>
        /// ��ȡ��Ŀ���ݣ�IQuestionDataProvider�ӿ�ʵ�֣�
        /// ΪHost�˳���ʹ�ã�����ƴ����Ŀ����֤����
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("��ʼ����TextPinyin��Ŀ����");

            try
            {
                // 1. ���ѡ�����
                string word = GetRandomWordFromDatabase();
                if (string.IsNullOrEmpty(word))
                {
                    LogError("�޷��ҵ�����Ƶ�ʷ�Χ�Ĵ���");
                    return null;
                }

                // 2. �����λĿ���ַ�
                int charIndex = Random.Range(0, word.Length);
                string targetChar = word.Substring(charIndex, 1);

                // 3. ��ȡƴ������
                string pinyinNoTone, pinyinTone;
                if (!GetPinyinDataForGeneration(targetChar, word, charIndex, out pinyinNoTone, out pinyinTone))
                {
                    LogError($"�޷���ȡ�ַ�'{targetChar}'��ƴ������");
                    return null;
                }

                // 4. ������������
                var additionalData = new TextPinyinAdditionalData
                {
                    word = word,
                    targetCharacter = targetChar,
                    characterIndex = charIndex,
                    correctPinyinTone = pinyinTone,
                    correctPinyinNoTone = pinyinNoTone
                };

                // 5. ������Ŀ����
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.TextPinyin,
                    questionText = FormatQuestionText(word, targetChar),
                    correctAnswer = pinyinNoTone, // �޵�ƴ��������֤
                    options = new string[0],
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"TextPinyin��Ŀ���ɳɹ�: {word} -> {targetChar} ({pinyinNoTone})");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"����TextPinyin��Ŀʧ��: {e.Message}");
                return null;
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
                            SELECT word FROM word
                            WHERE Freq BETWEEN @min AND @max
                            ORDER BY RANDOM()
                            LIMIT 1";

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
        /// ��ȡƴ������������Ŀ����
        /// </summary>
        private bool GetPinyinDataForGeneration(string character, string word, int charIndex, out string pinyinNoTone, out string pinyinTone)
        {
            pinyinNoTone = "";
            pinyinTone = "";

            try
            {
                // 1. ��ȡ�ַ����޵�ƴ��
                string rawTpinyin = null;
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT Tpinyin FROM character
                            WHERE char=@ch LIMIT 1";
                        cmd.Parameters.AddWithValue("@ch", character);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                rawTpinyin = reader.GetString(0);
                        }
                    }
                }

                // ����JSON��ʽ��ƴ������
                pinyinNoTone = ParseTpinyinJson(rawTpinyin);
                if (string.IsNullOrEmpty(pinyinNoTone))
                {
                    LogError($"�޷������ַ�'{character}'���޵�ƴ��");
                    return false;
                }

                // 2. ��ȡ����Ĵ���ƴ��
                string fullTonePinyin = null;
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT pinyin FROM word
                            WHERE word=@w LIMIT 1";
                        cmd.Parameters.AddWithValue("@w", word);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                fullTonePinyin = reader.GetString(0);
                        }
                    }
                }

                // ��ȡĿ���ַ��Ĵ���ƴ��
                pinyinTone = ExtractTonePinyinAtIndex(fullTonePinyin, charIndex, pinyinNoTone);

                LogDebug($"ƴ�����ݻ�ȡ�ɹ�: {character} -> �޵�:{pinyinNoTone}, ����:{pinyinTone}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"��ȡƴ������ʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ����Tpinyin JSON����
        /// </summary>
        private string ParseTpinyinJson(string rawTpinyin)
        {
            if (string.IsNullOrEmpty(rawTpinyin))
                return "";

            try
            {
                string parsed = rawTpinyin;
                if (rawTpinyin.StartsWith("["))
                {
                    var inner = rawTpinyin.Substring(1, rawTpinyin.Length - 2);
                    var parts = inner.Split(',');
                    parsed = parts[0].Trim().Trim('"');
                }
                return (parsed ?? "").ToLower();
            }
            catch (System.Exception e)
            {
                LogError($"����Tpinyin JSONʧ��: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// ��ȡָ��λ�õĴ���ƴ��
        /// </summary>
        private string ExtractTonePinyinAtIndex(string fullTonePinyin, int charIndex, string fallbackPinyin)
        {
            try
            {
                var toneParts = fullTonePinyin?.Trim().Split(' ');
                if (toneParts == null || charIndex < 0 || charIndex >= toneParts.Length)
                {
                    LogDebug($"����ƴ����ȡʧ�ܣ�ʹ���޵�ƴ����Ϊ����");
                    return fallbackPinyin;
                }
                return toneParts[charIndex];
            }
            catch (System.Exception e)
            {
                LogError($"��ȡ����ƴ��ʧ��: {e.Message}");
                return fallbackPinyin;
            }
        }

        /// <summary>
        /// ��ʽ����Ŀ�ı�
        /// </summary>
        private string FormatQuestionText(string word, string targetChar)
        {
            return $"��Ŀ��{word}\n\"<color=red>{targetChar}</color>\" �Ķ����ǣ�";
        }

        #endregion

        #region ������Ŀ����

        /// <summary>
        /// ����������Ŀ����
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("��������TextPinyin��Ŀ");

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

            LogDebug($"����TextPinyin��Ŀ�������: {currentWord} -> {targetCharacter}");
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

            if (networkData.questionType != QuestionType.TextPinyin)
            {
                LogError($"��Ŀ���Ͳ�ƥ�䣬����: {QuestionType.TextPinyin}, ʵ��: {networkData.questionType}");
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
                correctPinyinNoTone = questionData.correctAnswer.ToLower();

                // �Ӹ��������н�����ϸ��Ϣ
                if (!string.IsNullOrEmpty(questionData.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<TextPinyinAdditionalData>(questionData.additionalData);
                    currentWord = additionalInfo.word;
                    targetCharacter = additionalInfo.targetCharacter;
                    characterIndex = additionalInfo.characterIndex;
                    correctPinyinTone = additionalInfo.correctPinyinTone;
                    correctPinyinNoTone = additionalInfo.correctPinyinNoTone;
                }
                else
                {
                    // ����Ŀ�ı�����ȡ��Ϣ�����÷�����
                    ExtractInfoFromQuestionText(questionData.questionText);
                }

                LogDebug($"��Ŀ���ݽ����ɹ�: ����={currentWord}, Ŀ���ַ�={targetCharacter}, ��ȷƴ��={correctPinyinNoTone}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"������Ŀ����ʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ����Ŀ�ı�����ȡ��Ϣ�����÷�����
        /// </summary>
        private void ExtractInfoFromQuestionText(string questionText)
        {
            try
            {
                var lines = questionText.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("��Ŀ��"))
                    {
                        var wordStart = line.IndexOf("��Ŀ��") + 3;
                        var wordEnd = line.IndexOf("\n");
                        if (wordEnd == -1) wordEnd = line.Length;
                        currentWord = line.Substring(wordStart, wordEnd - wordStart).Trim();
                        break;
                    }
                }

                // ��HTML�������ȡĿ���ַ�
                var colorTagStart = questionText.IndexOf("<color=red>");
                var colorTagEnd = questionText.IndexOf("</color>");
                if (colorTagStart != -1 && colorTagEnd != -1)
                {
                    targetCharacter = questionText.Substring(colorTagStart + 11, colorTagEnd - colorTagStart - 11);
                }

                // ���û�д���ƴ��������Ϊ�𰸱���
                if (string.IsNullOrEmpty(correctPinyinTone))
                    correctPinyinTone = correctPinyinNoTone;

                LogDebug("����Ŀ�ı���ȡ��Ϣ���");
            }
            catch (System.Exception e)
            {
                LogError($"����Ŀ�ı���ȡ��Ϣʧ��: {e.Message}");
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
            Debug.Log($"[TextPinyin] ��̬��֤��: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[TextPinyin] ��̬��֤: ��Ϊ�ջ���Ŀ������Ч");
                return false;
            }

            if (question.questionType != QuestionType.TextPinyin)
            {
                Debug.LogError($"[TextPinyin] ��̬��֤: ��Ŀ���Ͳ�ƥ�䣬����TextPinyin��ʵ��{question.questionType}");
                return false;
            }

            try
            {
                // ��ȡ��ȷ���޵�ƴ��
                string correctPinyin = question.correctAnswer?.ToLower();
                if (string.IsNullOrEmpty(correctPinyin))
                {
                    Debug.Log("[TextPinyin] ��̬��֤: ��Ŀ������ȱ����ȷ��");
                    return false;
                }

                // �����û��𰸣�ȥ�����š�תСд
                string processedAnswer = ProcessPinyinAnswer(answer.Trim());

                bool isCorrect = processedAnswer == correctPinyin;
                Debug.Log($"[TextPinyin] ��̬��֤���: ������='{processedAnswer}', ��ȷ��='{correctPinyin}', ���={isCorrect}");

                return isCorrect;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TextPinyin] ��̬��֤�쳣: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ����ƴ���𰸸�ʽ
        /// </summary>
        private static string ProcessPinyinAnswer(string answer)
        {
            return answer.Replace("\"", "")
                         .Replace("\u201c", "") // ��˫���� "
                         .Replace("\u201d", "") // ��˫���� "
                         .Trim()
                         .ToLower();
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
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(correctPinyinNoTone))
            {
                LogDebug("�𰸻���ȷƴ��Ϊ��");
                return false;
            }

            string processedAnswer = ProcessPinyinAnswer(answer);
            bool isCorrect = processedAnswer == correctPinyinNoTone;

            LogDebug($"������֤���: ������='{processedAnswer}', ��ȷ��='{correctPinyinNoTone}', ���={isCorrect}");
            return isCorrect;
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

            // ������ȷ�𰸣������������ṩ����ƴ����
            if (!string.IsNullOrEmpty(correctAnswer))
                correctPinyinTone = correctAnswer;

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
            LogDebug("���ر���TextPinyin��Ŀ");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("�޷����ɱ�����Ŀ");
                return;
            }

            ParseQuestionData(questionData);
            LogDebug($"������Ŀ�������: {currentWord} -> {targetCharacter}");
        }

        #endregion

        #region ���߷���

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[TextPinyin] {message}");
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[TextPinyin] {message}");
        }

        #endregion
    }

    /// <summary>
    /// ����ƴ���⸽�����ݽṹ
    /// </summary>
    [System.Serializable]
    public class TextPinyinAdditionalData
    {
        public string word;                 // ��������
        public string targetCharacter;      // Ŀ���ַ�
        public int characterIndex;          // �ַ�λ��
        public string correctPinyinTone;    // ����ƴ�������ڷ�����
        public string correctPinyinNoTone;  // �޵�ƴ�������ڱȶԣ�
    }
}