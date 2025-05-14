using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Mono.Data.Sqlite;
using System.Data;
using Core;

namespace GameLogic
{
    /// <summary>
    /// 文字拼音题管理器：
    /// - 随机抽词
    /// - 随机定位字
    /// - 解析 character.Tpinyin JSON 并比对（无调、小写）
    /// - 反馈展示 word.pinyin（带调）
    /// - 投降视为错误答案，通过 OnAnswerResult 触发下一题
    /// - Debug.Log 调试
    /// </summary>
    public class TextPinyinQuestionManager : QuestionManagerBase
    {
        [Header("数据库路径（StreamingAssets/dictionary.db)")]
        private string dbPath;

        [Header("频率范围")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;

        [Header("UI 组件")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_InputField answerInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private Button surrenderButton;

        private string correctPinyinNoTone;
        private string correctPinyinTone;

        private void Awake()
        {
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
            Debug.Log($"[TextPinyin] Awake: dbPath={dbPath}");
        }

        private void Start()
        {
            Debug.Log("[TextPinyin] Start: 初始化 UI");
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/HardFillUI");
            var uiRoot = Instantiate(prefab);
            var uiTrans = uiRoot.transform.Find("UI");

            questionText = uiTrans.Find("QuestionText").GetComponent<TMP_Text>();
            answerInput = uiTrans.Find("AnswerInput").GetComponent<TMP_InputField>();
            submitButton = uiTrans.Find("SubmitButton").GetComponent<Button>();
            feedbackText = uiTrans.Find("FeedbackText").GetComponent<TMP_Text>();
            surrenderButton = uiTrans.Find("SurrenderButton").GetComponent<Button>();

            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);

            surrenderButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.AddListener(OnSurrender);

            feedbackText.text = "";
            LoadQuestion();
        }

        private void OnSurrender()
        {
            Debug.Log("[TextPinyin] Surrender clicked, treat as wrong answer");
            StopAllCoroutines();
            // 触发错误判定
            CheckAnswer("");
        }

        public override void LoadQuestion()
        {
            Debug.Log("[TextPinyin] LoadQuestion: 开始加载新题");
            // 1. 随机选词
            string word = null;
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
                        if (reader.Read()) word = reader.GetString(0);
                    }
                }
            }
            Debug.Log($"[TextPinyin] Selected word={word}");
            if (string.IsNullOrEmpty(word))
            {
                questionText.text = "没有符合频率范围的词！";
                return;
            }

            // 2. 定位字
            int pos = Random.Range(0, word.Length);
            string ch = word.Substring(pos, 1);
            Debug.Log($"[TextPinyin] Target char='{ch}', index={pos}");

            // 3. 解析 Tpinyin
            string rawTpinyin = null;
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT Tpinyin FROM character
                         WHERE char=@ch LIMIT 1";
                    cmd.Parameters.AddWithValue("@ch", ch);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) rawTpinyin = reader.GetString(0);
                    }
                }
            }
            Debug.Log($"[TextPinyin] Raw Tpinyin JSON={rawTpinyin}");
            string parsed = rawTpinyin;
            if (!string.IsNullOrEmpty(rawTpinyin) && rawTpinyin.StartsWith("["))
            {
                var inner = rawTpinyin.Substring(1, rawTpinyin.Length - 2);
                var parts = inner.Split(',');
                parsed = parts[0].Trim().Trim('"');
            }
            correctPinyinNoTone = (parsed ?? "").ToLower();
            Debug.Log($"[TextPinyin] Parsed TpinyinNoTone={correctPinyinNoTone}");
            if (string.IsNullOrEmpty(correctPinyinNoTone))
            {
                questionText.text = $"词库中找不到 '{ch}' 的拼音！";
                return;
            }

            // 4. 带调拼音
            string fullTone = null;
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
                        if (reader.Read()) fullTone = reader.GetString(0);
                    }
                }
            }
            Debug.Log($"[TextPinyin] Full word pinyin tone={fullTone}");
            var toneParts = fullTone?.Trim().Split(' ');
            if (toneParts == null || pos < 0 || pos >= toneParts.Length)
            {
                questionText.text = "拼音长度与文字长度不匹配！";
                Debug.LogError($"[TextPinyin] toneParts.Length={toneParts?.Length}, pos={pos}");
                return;
            }
            correctPinyinTone = toneParts[pos];
            Debug.Log($"[TextPinyin] CorrectTone={correctPinyinTone}");

            // 构造题干
            questionText.text = $"题目：{word}\n“<color=red>{ch}</color>” 的读音是？";
            answerInput.text = "";
            feedbackText.text = "";
            answerInput.ActivateInputField();
        }

        public override void CheckAnswer(string answer)
        {
            Debug.Log($"[TextPinyin] CheckAnswer: rawAnswer=\"{answer}\"");
            var processed = answer.Replace("\"", "").Replace("“", "").Replace("”", "").Trim().ToLower();
            Debug.Log($"[TextPinyin] CheckAnswer: processed=\"{processed}\"");
            bool isRight = processed == correctPinyinNoTone;
            Debug.Log($"[TextPinyin] CheckAnswer: isRight={isRight}");
            // 先反馈，再通过事件触发下一题
            StartCoroutine(ShowFeedbackThenNext(isRight));
            OnAnswerResult?.Invoke(isRight);
        }

        private void OnSubmit()
        {
            if (string.IsNullOrWhiteSpace(answerInput.text)) return;
            StopAllCoroutines();
            CheckAnswer(answerInput.text);
        }

        private IEnumerator ShowFeedbackThenNext(bool isRight)
        {
            if (isRight)
            {
                feedbackText.text = "回答正确！";
                feedbackText.color = Color.green;
                Debug.Log("[TextPinyin] ShowFeedback: 正确");
            }
            else
            {
                feedbackText.text = $"回答错误，正确拼音是：{correctPinyinTone}";
                feedbackText.color = Color.red;
                Debug.Log($"[TextPinyin] ShowFeedback: 错误，正确拼音={correctPinyinTone}");
            }
            yield return new WaitForSeconds(1f);
            // 不再直接 LoadQuestion，由监听 OnAnswerResult 的逻辑处理换题
        }
    }
}
