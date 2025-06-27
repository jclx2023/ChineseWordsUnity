using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.Choice
{
    /// <summary>
    /// �����ѡ��������� - UI�ܹ��ع���
    /// 
    /// ����˵����
    /// - ��simular_usage_questions�����ȡһ����¼
    /// - ��stem��ʾ����ɣ�True/1/2/3�ĸ�ѡ��������ҷ������ť
    /// - ��ҵ����ť�ж�������֤�����ѡ���׼ȷ��
    /// - Host��������Ŀ���ݺ�ѡ����֤��Ϣ
    /// </summary>
    public class SimularWordChoiceQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region ˽���ֶ�

        private string dbPath;

        // ��ǰ��Ŀ����
        private string correctOption;

        #endregion

        #region IQuestionDataProvider�ӿ�ʵ��

        public QuestionType QuestionType => QuestionType.SimularWordChoice;

        #endregion

        #region Unity��������

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("SimularWordChoice���͹�������ʼ�����");
        }

        #endregion

        #region ��Ŀ��������

        /// <summary>
        /// ��ȡ��Ŀ���ݣ�IQuestionDataProvider�ӿ�ʵ�֣�
        /// ΪHost�˳���ʹ�ã����ɽ����ѡ����Ŀ��ѡ������
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("��ʼ����SimularWordChoice��Ŀ����");

            try
            {
                string stem = null;
                List<string> choices = new List<string>(4);
                string correctAnswer = null;

                // �����ݿ��ȡ��Ŀ����
                if (!GetQuestionFromDatabase(out stem, out correctAnswer, out choices))
                {
                    LogError("�޷������ݿ��ȡ��Ŀ����");
                    return null;
                }

                // �������ѡ��
                ShuffleChoices(choices);

                // ������Ŀ����
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.SimularWordChoice,
                    questionText = stem,
                    correctAnswer = correctAnswer,
                    options = choices.ToArray(),
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(new SimularWordChoiceAdditionalData
                    {
                        source = "SimularWordChoiceQuestionManager"
                    })
                };

                LogDebug($"SimularWordChoice��Ŀ���ɳɹ�: {stem}");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"����SimularWordChoice��Ŀʧ��: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// �����ݿ��ȡ��Ŀ����
        /// </summary>
        private bool GetQuestionFromDatabase(out string stem, out string correctAnswer, out List<string> choices)
        {
            stem = null;
            correctAnswer = null;
            choices = new List<string>(4);

            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT [stem], [True] AS correct, [1] AS opt1, [2] AS opt2, [3] AS opt3
                            FROM simular_usage_questions
                            ORDER BY RANDOM()
                            LIMIT 1";

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                stem = reader.GetString(0);
                                correctAnswer = reader.GetString(1);

                                choices.Add(reader.GetString(1)); // correct
                                choices.Add(reader.GetString(2)); // opt1
                                choices.Add(reader.GetString(3)); // opt2
                                choices.Add(reader.GetString(4)); // opt3

                                return true;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                LogError($"���ݿ��ѯʧ��: {e.Message}");
            }

            return false;
        }

        /// <summary>
        /// �������ѡ��
        /// </summary>
        private void ShuffleChoices(List<string> choices)
        {
            for (int i = choices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                string temp = choices[i];
                choices[i] = choices[j];
                choices[j] = temp;
            }
        }

        #endregion

        #region ������Ŀ����

        /// <summary>
        /// ����������Ŀ����
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("��������SimularWordChoice��Ŀ");

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

            LogDebug($"����SimularWordChoice��Ŀ�������: {networkData.questionText}");
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

            if (networkData.questionType != QuestionType.SimularWordChoice)
            {
                LogError($"��Ŀ���Ͳ�ƥ�䣬����: {QuestionType.SimularWordChoice}, ʵ��: {networkData.questionType}");
                return false;
            }

            if (networkData.options == null || networkData.options.Length == 0)
            {
                LogError("ѡ������Ϊ��");
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
                correctOption = questionData.correctAnswer;

                LogDebug($"��Ŀ���ݽ����ɹ�: ��ȷ��={correctOption}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"������Ŀ����ʧ��: {e.Message}");
                return false;
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
            Debug.Log($"[SimularWordChoice] ��̬��֤��: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[SimularWordChoice] ��̬��֤: ��Ϊ�ջ���Ŀ������Ч");
                return false;
            }

            if (question.questionType != QuestionType.SimularWordChoice)
            {
                Debug.LogError($"[SimularWordChoice] ��̬��֤: ��Ŀ���Ͳ�ƥ�䣬����SimularWordChoice��ʵ��{question.questionType}");
                return false;
            }

            try
            {
                string correctAnswer = question.correctAnswer?.Trim();
                if (string.IsNullOrEmpty(correctAnswer))
                {
                    Debug.Log("[SimularWordChoice] ��̬��֤: ��Ŀ������ȱ����ȷ��");
                    return false;
                }

                bool isCorrect = answer.Trim().Equals(correctAnswer, System.StringComparison.Ordinal);
                Debug.Log($"[SimularWordChoice] ��̬��֤���: �û���='{answer.Trim()}', ��ȷ��='{correctAnswer}', ���={isCorrect}");

                return isCorrect;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SimularWordChoice] ��̬��֤�쳣: {e.Message}");
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
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(correctOption))
            {
                LogDebug("�𰸻���ȷѡ��Ϊ��");
                return false;
            }

            bool isCorrect = answer.Trim().Equals(correctOption.Trim(), System.StringComparison.Ordinal);
            LogDebug($"������֤���: �û���='{answer.Trim()}', ��ȷ��='{correctOption.Trim()}', ���={isCorrect}");

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

            correctOption = correctAnswer;
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
            LogDebug("���ر���SimularWordChoice��Ŀ");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("�޷����ɱ�����Ŀ");
                return;
            }

            ParseQuestionData(questionData);
            LogDebug($"������Ŀ�������: {questionData.questionText}");
        }

        #endregion

        #region ���߷���

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[SimularWordChoice] {message}");
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[SimularWordChoice] {message}");
        }

        #endregion
    }

    /// <summary>
    /// �����ѡ���⸽�����ݽṹ
    /// </summary>
    [System.Serializable]
    public class SimularWordChoiceAdditionalData
    {
        public string source; // ������Դ��ʶ
    }
}