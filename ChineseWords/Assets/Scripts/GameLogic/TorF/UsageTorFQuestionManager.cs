using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.TorF
{
    /// <summary>
    /// ����/����ʹ���ж�������� - UI�ܹ��ع���
    /// 
    /// ����˵����
    /// - ��simular_usage_questions�����ȡһ����¼
    /// - �������չʾ��ȷʾ�������ʾ��
    /// - ���滻���ı����뵽�»���λ�ò�����
    /// - ���ѡ��"��ȷ"/"����"�ж�
    /// - Host��������Ŀ���ݺ�ʹ����֤��Ϣ
    public class UsageTorFQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region ˽���ֶ�

        private string dbPath;

        // ��ǰ��Ŀ����
        private bool isInstanceCorrect;     // ��ǰʵ���Ƿ���ȷ
        private string currentStem;         // ��ǰ���
        private string correctFill;         // ��ȷ���
        private string currentFill;         // ��ǰʹ�õ����

        #endregion

        #region IQuestionDataProvider�ӿ�ʵ��

        public QuestionType QuestionType => QuestionType.UsageTorF;

        #endregion

        #region Unity��������

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("UsageTorF���͹�������ʼ�����");
        }

        #endregion

        #region ��Ŀ��������

        /// <summary>
        /// ��ȡ��Ŀ���ݣ�IQuestionDataProvider�ӿ�ʵ�֣�
        /// ΪHost�˳���ʹ�ã�����ʹ���ж���Ŀ��ѡ������
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("��ʼ����UsageTorF��Ŀ����");

            try
            {
                // 1. �����ݿ��ȡʹ���ж�������
                var usageData = GetRandomUsageData();
                if (usageData == null)
                {
                    LogError("�޷���ȡʹ���ж�������");
                    return null;
                }

                // 2. �������չʾ��ȷ���Ǵ���ʾ��
                bool isInstanceCorrect = Random.value < 0.5f;
                string currentFill = isInstanceCorrect ?
                    usageData.correctFill :
                    usageData.wrongFills[Random.Range(0, usageData.wrongFills.Count)];

                // 3. ������ʾ�ı�
                string questionText = ReplaceUnderscoreWithHighlight(usageData.stem, currentFill);

                // 4. ������������
                var additionalData = new UsageTorFAdditionalData
                {
                    stem = usageData.stem,
                    correctFill = usageData.correctFill,
                    currentFill = currentFill,
                    isInstanceCorrect = isInstanceCorrect,
                    wrongFills = usageData.wrongFills.ToArray()
                };

                // 5. ������Ŀ����
                var networkQuestionData = new NetworkQuestionData
                {
                    questionType = QuestionType.UsageTorF,
                    questionText = questionText,
                    correctAnswer = isInstanceCorrect.ToString().ToLower(), // "true" �� "false"
                    options = new string[] { "��ȷ", "����" }, // �̶�������ѡ��
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"UsageTorF��Ŀ���ɳɹ�: {currentFill} (ʵ��{(isInstanceCorrect ? "��ȷ" : "����")})");
                return networkQuestionData;
            }
            catch (System.Exception e)
            {
                LogError($"����UsageTorF��Ŀʧ��: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// ��ȡ���ʹ���ж�������
        /// </summary>
        private UsageData GetRandomUsageData()
        {
            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT [stem], [True], [1], [2], [3]
                            FROM simular_usage_questions
                            ORDER BY RANDOM()
                            LIMIT 1";

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var usageData = new UsageData
                                {
                                    stem = reader.GetString(0),
                                    correctFill = reader.GetString(1),
                                    wrongFills = new List<string>
                                    {
                                        reader.GetString(2),
                                        reader.GetString(3),
                                        reader.GetString(4)
                                    }
                                };
                                return usageData;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                LogError($"��ȡʹ���ж�������ʧ��: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// ������е��»����滻Ϊ�������������
        /// </summary>
        private string ReplaceUnderscoreWithHighlight(string stem, string fill)
        {
            int start = stem.IndexOf('_');
            if (start >= 0)
            {
                // ���������»��߳���
                int end = start;
                while (end < stem.Length && stem[end] == '_')
                    end++;

                // ƴ���滻��ǰ���� + ������� + �󲿷�
                string before = stem.Substring(0, start);
                string after = stem.Substring(end);
                return before + $"<color=red>{fill}</color>" + after;
            }
            else
            {
                // û���»�����ֱ����ʾԭ��
                return stem;
            }
        }

        #endregion

        #region ������Ŀ����

        /// <summary>
        /// ����������Ŀ����
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("��������UsageTorF��Ŀ");

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

            LogDebug($"����UsageTorF��Ŀ�������: {currentFill}");
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

            if (networkData.questionType != QuestionType.UsageTorF)
            {
                LogError($"��Ŀ���Ͳ�ƥ�䣬����: {QuestionType.UsageTorF}, ʵ��: {networkData.questionType}");
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
                // ������ȷ�𰸣�true/false��
                isInstanceCorrect = questionData.correctAnswer.ToLower() == "true";

                // �Ӹ��������н�����ϸ��Ϣ
                if (!string.IsNullOrEmpty(questionData.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<UsageTorFAdditionalData>(questionData.additionalData);
                    currentStem = additionalInfo.stem;
                    correctFill = additionalInfo.correctFill;
                    currentFill = additionalInfo.currentFill;
                }
                else
                {
                    // ����Ŀ�ı�����
                    ExtractInfoFromQuestionText(questionData.questionText);
                }

                LogDebug($"��Ŀ���ݽ����ɹ�: ʵ����ȷ={isInstanceCorrect}, ��ǰ���={currentFill}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"������Ŀ����ʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ����Ŀ�ı�����ȡ��Ϣ
        /// </summary>
        private void ExtractInfoFromQuestionText(string questionText)
        {
            try
            {
                // ���ԴӸ�����ǩ����ȡ��ǰ�������
                var colorTagStart = questionText.IndexOf("<color=red>");
                var colorTagEnd = questionText.IndexOf("</color>");

                if (colorTagStart != -1 && colorTagEnd != -1)
                {
                    colorTagStart += "<color=red>".Length;
                    currentFill = questionText.Substring(colorTagStart, colorTagEnd - colorTagStart);

                    // �ع�ԭʼ��ɣ������������滻Ϊ�»��ߣ�
                    var before = questionText.Substring(0, colorTagStart - "<color=red>".Length);
                    var after = questionText.Substring(colorTagEnd + "</color>".Length);
                    currentStem = before + new string('_', currentFill.Length) + after;
                }
                else
                {
                    // ���û�и�����ǩ��ֱ��ʹ����Ŀ�ı�
                    currentStem = questionText;
                    currentFill = "δ֪";
                }

                // ������ȷ���Ϊ��ǰ��գ�����ģʽ���޷�ȷ����
                correctFill = currentFill;
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
            Debug.Log($"[UsageTorF] ��̬��֤��: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[UsageTorF] ��̬��֤: ��Ϊ�ջ���Ŀ������Ч");
                return false;
            }

            if (question.questionType != QuestionType.UsageTorF)
            {
                Debug.LogError($"[UsageTorF] ��̬��֤: ��Ŀ���Ͳ�ƥ�䣬����UsageTorF��ʵ��{question.questionType}");
                return false;
            }

            try
            {
                string correctAnswer = question.correctAnswer?.Trim().ToLower();
                if (string.IsNullOrEmpty(correctAnswer))
                {
                    Debug.Log("[UsageTorF] ��̬��֤: ��Ŀ������ȱ����ȷ��");
                    return false;
                }

                // ��׼���û���
                string normalizedAnswer = NormalizeAnswer(answer.Trim());

                bool isCorrect = normalizedAnswer == correctAnswer;
                Debug.Log($"[UsageTorF] ��̬��֤���: �û���='{answer.Trim()}', ��׼��='{normalizedAnswer}', ��ȷ��='{correctAnswer}', ���={isCorrect}");

                return isCorrect;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UsageTorF] ��̬��֤�쳣: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ��׼���𰸸�ʽ
        /// </summary>
        private static string NormalizeAnswer(string answer)
        {
            // ������ֿ��ܵĴ𰸸�ʽ
            switch (answer.ToLower())
            {
                case "true":
                case "��ȷ":
                case "��":
                case "��":
                    return "true";
                case "false":
                case "����":
                case "��":
                case "��":
                    return "false";
                default:
                    return answer.ToLower();
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

            // ��׼����
            string normalizedAnswer = NormalizeAnswer(answer.Trim());
            string expectedAnswer = isInstanceCorrect ? "true" : "false";

            bool isCorrect = normalizedAnswer == expectedAnswer;
            LogDebug($"������֤���: ��׼����='{normalizedAnswer}', ������='{expectedAnswer}', ���={isCorrect}");

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
            LogDebug("���ر���UsageTorF��Ŀ");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("�޷����ɱ�����Ŀ");
                return;
            }

            ParseQuestionData(questionData);
            LogDebug($"������Ŀ�������: {currentFill}");
        }

        #endregion

        #region ���߷���

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[UsageTorF] {message}");
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[UsageTorF] {message}");
        }

        #endregion
    }

    /// <summary>
    /// ʹ���ж������ݽṹ
    /// </summary>
    public class UsageData
    {
        public string stem;                 // ��ɣ������»��ߣ�
        public string correctFill;          // ��ȷ���
        public List<string> wrongFills;     // �������ѡ��
    }

    /// <summary>
    /// ʹ���ж��⸽�����ݽṹ
    /// </summary>
    [System.Serializable]
    public class UsageTorFAdditionalData
    {
        public string stem;                 // ԭʼ��ɣ����»��ߣ�
        public string correctFill;          // ��ȷ�������
        public string currentFill;          // ��ǰʹ�õ��������
        public bool isInstanceCorrect;      // ��ǰʵ���Ƿ���ȷ
        public string[] wrongFills;         // ����ѡ���б�
    }
}