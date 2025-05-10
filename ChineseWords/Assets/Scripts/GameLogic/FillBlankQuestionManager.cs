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
    /// 填空题管理器，继承基类并复用原有的 DB 查询与题干生成/校验逻辑
    /// </summary>
    public class FillBlankQuestionManager : QuestionManagerBase
    {
        [Header("数据库路径（StreamingAssets/Temp.db）")]
        private string dbPath;

        [Header("UI Components")]
        [SerializeField] private TMP_Text questionText;    // “_公__”形式
        [SerializeField] private TMP_InputField answerInput;     // 玩家输入
        [SerializeField] private Button submitButton;    // 提交
        [SerializeField] private Button surrenderButton; // 投降
        [SerializeField] private TMP_Text feedbackText;    // 正误/投降提示

        // 当前题参数，用于生成题干和校验答案
        private string selectedChar;
        private int selectedPos;
        private int selectedLen;

        void Awake()
        {
            // 构造 DB 路径
            dbPath = Application.streamingAssetsPath + "/Temp.db";
        }

        void Start()
        {
            // 1. 绑定按钮事件
            submitButton.onClick.AddListener(OnSubmit);
            surrenderButton.onClick.AddListener(OnSurrender);
            // 2. 清空提示
            feedbackText.text = string.Empty;
            // 3. 第一道题
            LoadQuestion();
        }

        /// <summary>
        /// 重写：加载一道新题
        /// </summary>
        public override void LoadQuestion()
        {
            // 1. 随机选词
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

            // 2. 随机挑一个字的位置
            int pos = Random.Range(0, wordLen);
            string ch = wordText.Substring(pos, 1);

            selectedChar = ch;
            selectedPos = pos;
            selectedLen = wordLen;

            // 3. 生成并显示题干
            questionText.text = GeneratePattern(selectedChar, selectedPos, selectedLen);

            // 重置输入框与提示
            answerInput.text = string.Empty;
            answerInput.ActivateInputField();
            feedbackText.text = string.Empty;
        }

        /// <summary>
        /// 重写：校验玩家答案
        /// </summary>
        public override void CheckAnswer(string answer)
        {
            // 调用原有的 SQL 验证逻辑
            bool isRight = ValidateAnswer(answer.Trim());
            // 通知外部（如果有订阅）
            OnAnswerResult?.Invoke(isRight);
            // 显示反馈并延迟换题
            StartCoroutine(ShowFeedbackThenNext(isRight));
        }

        // 点击“提交”
        private void OnSubmit()
        {
            var ans = answerInput.text.Trim();
            if (string.IsNullOrEmpty(ans)) return;
            // 停止之前的切题协程
            StopAllCoroutines();
            CheckAnswer(ans);
        }

        // 点击“投降”
        private void OnSurrender()
        {
            StopAllCoroutines();
            // 投降当作错误处理
            OnAnswerResult?.Invoke(false);
            StartCoroutine(ShowSurrenderThenNext());
        }

        // 正误反馈协程
        private IEnumerator ShowFeedbackThenNext(bool isRight)
        {
            feedbackText.text = isRight ? "回答正确!" : "回答错误!";
            feedbackText.color = isRight ? Color.green : Color.red;
            yield return new WaitForSeconds(1f);
            LoadQuestion();
        }

        // 投降反馈协程
        private IEnumerator ShowSurrenderThenNext()
        {
            feedbackText.text = "已投降，进入下一题";
            feedbackText.color = Color.yellow;
            yield return new WaitForSeconds(0.5f);
            LoadQuestion();
        }

        /// <summary>
        /// 原始的 SQL 校验：检查 WordChar+Word 表中是否存在该词在该位置包含该字
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
        /// 原始的题干生成：把所有位置填“_”，只有选中位置显示该字
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
