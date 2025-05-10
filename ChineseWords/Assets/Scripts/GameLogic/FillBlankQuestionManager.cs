using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Data;
using Mono.Data.Sqlite;
using Core;

namespace GameLogic
{
    /// <summary>
    /// �������������̳л��ಢ����ԭ�е� DB ��ѯ���������/У���߼�
    /// </summary>
    public class FillBlankQuestionManager : QuestionManagerBase
    {
        [Header("���ݿ�·����StreamingAssets/Temp.db��")]
        private string dbPath;

        [Header("UI Components")]
        [SerializeField] private TMP_Text questionText;    // ��_��__����ʽ
        [SerializeField] private TMP_InputField answerInput;     // �������
        [SerializeField] private Button submitButton;    // �ύ
        [SerializeField] private Button surrenderButton; // Ͷ��
        [SerializeField] private TMP_Text feedbackText;    // ����/Ͷ����ʾ

        // ��ǰ�����������������ɺ�У���
        private string selectedChar;
        private int selectedPos;
        private int selectedLen;

        void Awake()
        {
            // ���� DB ·��
            dbPath = Application.streamingAssetsPath + "/Temp.db";
        }

        void Start()
        {
            // 1. �󶨰�ť�¼�
            submitButton.onClick.AddListener(OnSubmit);
            surrenderButton.onClick.AddListener(OnSurrender);
            // 2. �����ʾ
            feedbackText.text = string.Empty;
            // 3. ��һ����
            LoadQuestion();
        }

        /// <summary>
        /// ��д������һ������
        /// </summary>
        public override void LoadQuestion()
        {
            // 1. ���ѡ��
            string connStr = "URI=file:" + dbPath;
            string wordText = null;
            int wordLen = 0;

            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT text, length
                          FROM Word
                         ORDER BY RANDOM()
                         LIMIT 1";
                    using (var reader = cmd.ExecuteReader())
                    {
                        reader.Read();
                        wordText = reader.GetString(0);
                        wordLen = reader.GetInt32(1);
                    }
                }
                conn.Close();
            }

            // 2. �����һ���ֵ�λ��
            int pos = Random.Range(0, wordLen);
            string ch = wordText.Substring(pos, 1);

            selectedChar = ch;
            selectedPos = pos;
            selectedLen = wordLen;

            // 3. ���ɲ���ʾ���
            questionText.text = GeneratePattern(selectedChar, selectedPos, selectedLen);

            // �������������ʾ
            answerInput.text = string.Empty;
            answerInput.ActivateInputField();
            feedbackText.text = string.Empty;
        }

        /// <summary>
        /// ��д��У����Ҵ�
        /// </summary>
        public override void CheckAnswer(string answer)
        {
            // ����ԭ�е� SQL ��֤�߼�
            bool isRight = ValidateAnswer(answer.Trim());
            // ֪ͨ�ⲿ������ж��ģ�
            OnAnswerResult?.Invoke(isRight);
            // ��ʾ�������ӳٻ���
            StartCoroutine(ShowFeedbackThenNext(isRight));
        }

        // ������ύ��
        private void OnSubmit()
        {
            var ans = answerInput.text.Trim();
            if (string.IsNullOrEmpty(ans)) return;
            // ֹ֮ͣǰ������Э��
            StopAllCoroutines();
            CheckAnswer(ans);
        }

        // �����Ͷ����
        private void OnSurrender()
        {
            StopAllCoroutines();
            // Ͷ������������
            OnAnswerResult?.Invoke(false);
            StartCoroutine(ShowSurrenderThenNext());
        }

        // ������Э��
        private IEnumerator ShowFeedbackThenNext(bool isRight)
        {
            feedbackText.text = isRight ? "�ش���ȷ!" : "�ش����!";
            feedbackText.color = isRight ? Color.green : Color.red;
            yield return new WaitForSeconds(1f);
            LoadQuestion();
        }

        // Ͷ������Э��
        private IEnumerator ShowSurrenderThenNext()
        {
            feedbackText.text = "��Ͷ����������һ��";
            feedbackText.color = Color.yellow;
            yield return new WaitForSeconds(0.5f);
            LoadQuestion();
        }

        /// <summary>
        /// ԭʼ�� SQL У�飺��� WordChar+Word �����Ƿ���ڸô��ڸ�λ�ð�������
        /// </summary>
        private bool ValidateAnswer(string answer)
        {
            bool result = false;
            string connStr = "URI=file:" + dbPath;

            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COUNT(*)
                          FROM WordChar wc
                          JOIN Word     w  ON w.id = wc.word_id
                         WHERE w.length = @len
                           AND wc.char   = @ch
                           AND wc.pos    = @pos
                           AND w.text    = @ans";
                    cmd.Parameters.AddWithValue("@len", selectedLen);
                    cmd.Parameters.AddWithValue("@ch", selectedChar);
                    cmd.Parameters.AddWithValue("@pos", selectedPos);
                    cmd.Parameters.AddWithValue("@ans", answer);

                    long cnt = (long)cmd.ExecuteScalar();
                    result = cnt > 0;
                }
                conn.Close();
            }

            return result;
        }

        /// <summary>
        /// ԭʼ��������ɣ�������λ���_����ֻ��ѡ��λ����ʾ����
        /// </summary>
        private string GeneratePattern(string ch, int pos, int len)
        {
            char[] arr = new char[len];
            for (int i = 0; i < len; i++) arr[i] = '_';
            arr[pos] = ch[0];
            return new string(arr);
        }
    }
}
