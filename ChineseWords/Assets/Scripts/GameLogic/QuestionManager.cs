using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Data;
using Mono.Data.Sqlite;

/// <summary>
/// 负责出题、校验答案，并在 UI 上显示反馈和“投降”功能
/// </summary>
public class QuestionManager : MonoBehaviour
{
    [Header("数据库路径（StreamingAssets/Temp.db）")]
    private string dbPath;

    [Header("UI Components")]
    public TMP_Text questionText;    // 用于显示 _公__ 形式题目
    public TMP_InputField answerInput;     // 玩家输入完整词语
    public Button submitButton;    // 提交按钮
    public Button surrenderButton; // 投降按钮，直接下一题
    public TMP_Text feedbackText;    // 答案正确/错误反馈

    // 当前题参数
    private string selectedChar;
    private int selectedPos;
    private int selectedLen;

    void Awake()
    {
        // 数据库路径：.../Assets/StreamingAssets/Temp.db
        dbPath = Application.streamingAssetsPath + "/Temp.db";
    }

    void Start()
    {
        // 绑定按钮事件
        submitButton.onClick.AddListener(OnSubmit);
        surrenderButton.onClick.AddListener(OnSurrender);
        // 初始清空反馈
        feedbackText.text = string.Empty;
        // 载入首题
        LoadRandomQuestion();
    }

    /// <summary>
    /// 随机从数据库加载一道填空题 (_公__) 并显示
    /// </summary>
    void LoadRandomQuestion()
    {
        string connStr = "URI=file:" + dbPath;
        using (var conn = new SqliteConnection(connStr))
        {
            conn.Open();

            // 随机选字及其位置和词长
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT wc.char, wc.pos, w.length
                      FROM WordChar wc
                      JOIN Word     w ON w.id = wc.word_id
                     GROUP BY wc.word_id
                     ORDER BY RANDOM()
                     LIMIT 1";
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    selectedChar = reader.GetString(0);
                    selectedPos = reader.GetInt32(1);
                    selectedLen = reader.GetInt32(2);
                }
            }
            conn.Close();
        }

        // 显示题目，并重置输入
        questionText.text = GeneratePattern(selectedChar, selectedPos, selectedLen);
        answerInput.text = string.Empty;
        answerInput.ActivateInputField();
        // 不主动清空 feedbackText，保留上次提示直到下一题
    }

    /// <summary>
    /// 处理提交答案的点击
    /// </summary>
    void OnSubmit()
    {
        string ans = answerInput.text.Trim();
        if (string.IsNullOrEmpty(ans)) return;
        // 如果已有协程在执行，先停止
        StopAllCoroutines();
        StartCoroutine(HandleAnswer(ans));
    }

    IEnumerator HandleAnswer(string answer)
    {
        // 校验答案
        bool ok = ValidateAnswer(answer);
        if (ok)
        {
            feedbackText.text = "回答正确!";
            feedbackText.color = Color.green;
        }
        else
        {
            feedbackText.text = "回答错误!";
            feedbackText.color = Color.red;
        }
        // 等待一秒再换题，让玩家看到反馈
        yield return new WaitForSeconds(1f);
        LoadRandomQuestion();
    }

    /// <summary>
    /// 处理投降按钮点击
    /// </summary>
    void OnSurrender()
    {
        StopAllCoroutines();
        StartCoroutine(HandleSurrender());
    }

    IEnumerator HandleSurrender()
    {
        feedbackText.text = "已投降,进入下一题";
        feedbackText.color = Color.yellow;
        // 等待半秒后换题
        yield return new WaitForSeconds(0.5f);
        LoadRandomQuestion();
    }

    /// <summary>
    /// 验证玩家答案是否正确
    /// </summary>
    bool ValidateAnswer(string answer)
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
    /// 根据词长和选中字生成 _公__ 样式的填空串
    /// </summary>
    string GeneratePattern(string ch, int pos, int len)
    {
        char[] arr = new char[len];
        for (int i = 0; i < len; i++) arr[i] = '_';
        arr[pos] = ch[0];
        return new string(arr);
    }
}