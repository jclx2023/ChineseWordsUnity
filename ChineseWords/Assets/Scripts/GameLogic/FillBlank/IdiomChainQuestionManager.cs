using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;

namespace GameLogic.FillBlank
{
    public class IdiomChainQuestionManager : QuestionManagerBase
    {
        [Header("数据库路径（StreamingAssets/dictionary.db）")]
        private string dbPath;

        [Header("UI Components")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_InputField answerInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private Button surrenderButton;
        [SerializeField] private TimerManager timerManager;

        [Header("成语频率区间")]
        [SerializeField] private int minFreq = 0;
        [SerializeField] private int maxFreq = 9;

        // 缓存首题候选
        private List<string> firstCandidates = new List<string>();

        private string currentIdiom;

        private void Awake()
        {
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            // 预加载所有符合“第4字可接龙”的首题成语
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT i1.word 
                        FROM idiom AS i1
                        WHERE i1.Freq BETWEEN @min AND @max
                          AND EXISTS (
                              SELECT 1 FROM idiom AS i2
                              WHERE substr(i2.word,1,1)=substr(i1.word,4,1)
                                AND i2.Freq BETWEEN @min AND @max
                                AND i2.word<>i1.word
                          )";
                    cmd.Parameters.AddWithValue("@min", minFreq);
                    cmd.Parameters.AddWithValue("@max", maxFreq);

                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            firstCandidates.Add(reader.GetString(0));
                }
            }

            if (firstCandidates.Count == 0)
                Debug.LogError("首题候选列表为空");
        }

        private void Start()
        {
            var uiTrans = UIManager.Instance.LoadUI("Prefabs/InGame/HardFillUI");

            questionText = uiTrans.Find("QuestionText").GetComponent<TMP_Text>();
            answerInput = uiTrans.Find("AnswerInput").GetComponent<TMP_InputField>();
            submitButton = uiTrans.Find("SubmitButton").GetComponent<Button>();
            feedbackText = uiTrans.Find("FeedbackText").GetComponent<TMP_Text>();
            surrenderButton = uiTrans.Find("SurrenderButton").GetComponent<Button>();
            surrenderButton.onClick.AddListener(() => {
                StopAllCoroutines();
                feedbackText.text = "超时！";
                OnAnswerResult?.Invoke(false);
            });
            submitButton.onClick.AddListener(OnSubmit);
            timerManager = GetComponent<TimerManager>();
            feedbackText.text = "";
        }
        public override void LoadQuestion()
        {
            Debug.Log("加载一次");
            // 随机选一个
            int idx = Random.Range(0, firstCandidates.Count);
            currentIdiom = firstCandidates[idx];
            firstCandidates.RemoveAt(idx);
            ShowQuestion(currentIdiom);
        }

        /// <summary>
        /// 高亮最后一个字并展示
        /// </summary>
        private void ShowQuestion(string idiom)
        {
            char last = idiom[^1];
            questionText.text =
                idiom.Substring(0, idiom.Length - 1)
                + $"<color=red>{last}</color>";
            answerInput.text = "";
            answerInput.ActivateInputField();
            feedbackText.text = "";
        }

        private void OnSubmit()
        {
            CheckAnswer(answerInput.text.Trim());
        }

        public override void CheckAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer) || answer[0] != currentIdiom[^1])
            {
                feedbackText.text = "开头错误！";
                return;
            }

            // 较快的单条 COUNT 查询检验成语是否存在
            bool exists;
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COUNT(*) FROM (
                          SELECT word FROM idiom WHERE word=@w
                          UNION
                          SELECT word FROM other_idiom WHERE word=@w
                        )";
                    cmd.Parameters.AddWithValue("@w", answer);
                    exists = (long)cmd.ExecuteScalar() > 0;
                }
            }

            if (exists)
            {
                feedbackText.text = "回答正确！";
                currentIdiom = answer;
                timerManager.StopTimer();
                timerManager.StartTimer();
                Invoke(nameof(ShowNext), 0.5f);
            }
            else
            {
                feedbackText.text = "词库中无此成语！";
            }
        }

        private void ShowNext()
        {
            ShowQuestion(currentIdiom);
        }
    }
}
