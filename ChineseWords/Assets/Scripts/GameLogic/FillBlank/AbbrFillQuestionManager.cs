using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;
//题型过于简单，故搁置

namespace GameLogic.FillBlank
{
    /// <summary>
    /// 首字母补全题管理器：
    /// - 随机抽取 word/idiom/other_idiom 中一条记录 (Freq 范围内)
    /// - 从记录的 abbr 字段中随机显示 revealCount 个首字母，其余位置用 _
    /// - <char> 代表第 i 位首字母
    /// - 玩家输入完整词条 D
    /// - 校验：长度、库中存在性、D 的 abbr 与reveal相同
    /// - 结果通过 OnAnswerResult 通知外层切题
    /// </summary>
    public class AbbrFillQuestionManager : QuestionManagerBase
    {
        [Header("数据库路径 (StreamingAssets/dictionary.db)")]
        private string dbPath;

        [Header("Freq 范围")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        [Header("显示首字母数量 (0 < revealCount < abbr.Length)")]
        [SerializeField] private int revealCount = 1;

        [Header("UI 组件")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_InputField answerInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button surrenderButton;
        [SerializeField] private TMP_Text feedbackText;

        private string currentWord;
        private string currentAbbr;
        private int[] revealIndices;

        private void Awake()
        {
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/HardFillUI");
            var ui = Instantiate(prefab).transform.Find("UI");
            questionText = ui.Find("QuestionText").GetComponent<TMP_Text>();
            answerInput = ui.Find("AnswerInput").GetComponent<TMP_InputField>();
            submitButton = ui.Find("SubmitButton").GetComponent<Button>();
            surrenderButton = ui.Find("SurrenderButton").GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText").GetComponent<TMP_Text>();

            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);
            surrenderButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.AddListener(OnSurrender);

            feedbackText.text = string.Empty;
            LoadQuestion();
        }

        public override void LoadQuestion()
        {
            // 1. 随机抽取一条记录
            currentWord = null;
            currentAbbr = null;
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT word, abbr FROM (
  SELECT word, abbr, Freq FROM word
  UNION ALL
  SELECT word, abbr, Freq FROM idiom
  UNION ALL
  SELECT word, abbr, Freq FROM other_idiom
) WHERE Freq BETWEEN @min AND @max
ORDER BY RANDOM() LIMIT 1";
                    cmd.Parameters.AddWithValue("@min", freqMin);
                    cmd.Parameters.AddWithValue("@max", freqMax);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            currentWord = reader.GetString(0);
                            currentAbbr = reader.GetString(1);
                        }
                    }
                }
            }
            if (string.IsNullOrEmpty(currentWord) || string.IsNullOrEmpty(currentAbbr))
            {
                questionText.text = "没有符合条件的词条。";
                return;
            }
            // 在 LoadQuestion() 里，读取 currentAbbr 后：
            currentAbbr = currentAbbr.ToUpperInvariant();

            // 2. 随机选择 revealIndices
            int L = currentAbbr.Length;
            int count = Mathf.Clamp(revealCount, 1, L - 1);
            var idxs = Enumerable.Range(0, L).OrderBy(_ => Random.value).Take(count).OrderBy(i => i).ToArray();
            revealIndices = idxs;

            // 3. 构造题干字符串
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < L; i++)
            {
                sb.Append(revealIndices.Contains(i) ? currentAbbr[i] : '_');
            }
            questionText.text = $"题目：请输入一个首字母为<color=red>{sb}</color>的单词\nHint:_可以为任意首字母开头的字";

            // 重置输入与提示
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.ActivateInputField();
        }

        public override void CheckAnswer(string answer)
        {
            string D = answer.Trim();
            bool ok = true;

            // 长度校验
            if (D.Length != currentWord.Length)
                ok = false;

            // 库中存在性
            if (ok)
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
SELECT COUNT(*) FROM (
  SELECT word FROM word
  UNION ALL
  SELECT word FROM idiom
  UNION ALL
  SELECT word FROM other_idiom
) WHERE word = @d";
                        cmd.Parameters.AddWithValue("@d", D);
                        long cnt = (long)cmd.ExecuteScalar();
                        ok = cnt > 0;
                    }
                }
            }

            // abbr 校验
            if (ok)
            {
                string inputAbbr = null;
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
SELECT abbr FROM (
  SELECT word, abbr FROM word
  UNION ALL
  SELECT word, abbr FROM idiom
  UNION ALL
  SELECT word, abbr FROM other_idiom
) WHERE word = @d LIMIT 1";
                        cmd.Parameters.AddWithValue("@d", D);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                inputAbbr = reader.GetString(0);
                        }
                    }
                }
                if (string.IsNullOrEmpty(inputAbbr) || inputAbbr.Length != currentAbbr.Length)
                    ok = false;
                else
                {
                    foreach (int i in revealIndices)
                    {
                        if (inputAbbr[i] != currentAbbr[i])
                        {
                            ok = false;
                            break;
                        }
                    }
                }
            }

            StartCoroutine(ShowFeedbackThenNext(ok));
            OnAnswerResult?.Invoke(ok);
        }

        private void OnSubmit()
        {
            if (!string.IsNullOrWhiteSpace(answerInput.text))
            {
                StopAllCoroutines();
                CheckAnswer(answerInput.text);
            }
        }

        private void OnSurrender()
        {
            StopAllCoroutines();
            CheckAnswer(string.Empty);
        }

        private IEnumerator ShowFeedbackThenNext(bool isRight)
        {
            if (isRight)
            {
                feedbackText.text = "回答正确！";
                feedbackText.color = Color.green;
            }
            else
            {
                feedbackText.text = $"回答错误，示例答案：{currentWord}";
                feedbackText.color = Color.red;
            }
            yield return new WaitForSeconds(1f);
            // 下一题由 OnAnswerResult 统一触发
        }
    }
}
