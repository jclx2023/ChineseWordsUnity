using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Data;
using Mono.Data.Sqlite;
using Core;
using System.Runtime.InteropServices;
using System;



namespace GameLogic

{

    /// <summary>
    /// 填空题管理器，继承基类并复用原有的 DB 查询与题干生成/校验逻辑
    /// </summary>
    public class FillBlankQuestionManager : QuestionManagerBase
    {
        [Header("数据库路径（StreamingAssets/Temp.db）")]
        private string dbPath;

        private GameObject uiRoot;

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

        }

        void Start()
        {
            // 1. 构造 DB 路径
            dbPath = Application.streamingAssetsPath + "/Temp.db";
#if UNITY_STANDALONE_WIN
            // 检查数据库文件是否存在
            if (!System.IO.File.Exists(dbPath))
            {
                MessageBox(System.IntPtr.Zero, "找不到题库文件 Temp.db，请检查游戏目录下 StreamingAssets 是否包含该文件。", "无法启动游戏", 0);
                Application.Quit();
                return;
            }

            // 尝试连接数据库
            try
            {
                using (var conn = new Mono.Data.Sqlite.SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    Debug.Log("✅ 数据库连接成功！");
                }
            }
            catch (System.Exception ex)
            {
                MessageBox(System.IntPtr.Zero, "数据库连接失败：" + ex.Message, "数据库错误", 0);
                Application.Quit();
                return;
            }
#endif


            // 2. 加载 Prefab
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/FillBlankUI");

            // 3. 实例化
            uiRoot = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            if (uiRoot == null)
            {
                Debug.LogError("【FillBlank】Instantiate 失败，返回了 null");
                return;
            }

            // 4. 找到 UI 根节点（假设你的 Prefab 里有一个名为 “UI” 的子物体）
            var uiTrans = uiRoot.transform.Find("UI");
            if (uiTrans == null)
            {
                Debug.LogError($"【FillBlank】在 {uiRoot.name} 下找不到名为 UI 的子物体，子物体列表如下：");
                foreach (Transform t in uiRoot.transform)
                    Debug.Log($"    • {t.name}");
                return;
            }

            // 5. 在 UI 根节点下，再按名称查找各组件
            questionText = uiTrans.Find("QuestionText")?.GetComponent<TMP_Text>();
            answerInput = uiTrans.Find("AnswerInput")?.GetComponent<TMP_InputField>();
            submitButton = uiTrans.Find("SubmitButton")?.GetComponent<Button>();
            surrenderButton = uiTrans.Find("SurrenderButton")?.GetComponent<Button>();
            feedbackText = uiTrans.Find("Feedback")?.GetComponent<TMP_Text>();

            // 6. 检查每个组件是否都绑定到了
            if (questionText == null) Debug.LogError("找不到 QuestionText 或 没挂 TMP_Text 组件");
            if (answerInput == null) Debug.LogError("找不到 AnswerInput 或 没挂 TMP_InputField 组件");
            if (submitButton == null) Debug.LogError("找不到 SubmitButton 或 没挂 Button 组件");
            if (surrenderButton == null) Debug.LogError("找不到 SurrenderButton 或 没挂 Button 组件");
            if (feedbackText == null) Debug.LogError("找不到 FeedbackText 或 没挂 TMP_Text 组件");
            // 如果有任意一个 null，就直接 return，避免后续 OnClick 绑定也报错
            if (questionText == null || answerInput == null || submitButton == null || surrenderButton == null || feedbackText == null)
                return;

            // 7. 绑定按钮事件
            // 在 Start() 里，AddListener 之前：
            submitButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.RemoveAllListeners();

            // 再绑定
            submitButton.onClick.AddListener(OnSubmit);
            surrenderButton.onClick.AddListener(OnSurrender);


            // 8. 初始化显示
            feedbackText.text = "";
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
            int pos = UnityEngine.Random.Range(0, wordLen);
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
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);

    }
}
