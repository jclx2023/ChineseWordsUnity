using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;

namespace GameLogic.Choice
{
    /// <summary>
    /// 词语选择填空（近义词/同义/造句等通用）题管理器：
    /// - 从自定义表随机读取一条记录
    /// - 将 stem 显示在題干
    /// - 将 True, 1, 2, 3 四个选项随机打乱，分别赋给四个按钮
    /// - 玩家点击按钮判定正误，通过 OnAnswerResult 通知外层
    /// </summary>
    public class SimularWordChoiceQuestionManager : QuestionManagerBase
    {
        private string dbPath;

        private TMP_Text questionText;
        private Button[] optionButtons;
        private TMP_Text feedbackText;

        private string correctOption;

        private void Awake()
        {
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            var ui = UIManager.Instance.LoadUI("Prefabs/InGame/ChooseUI");


            questionText = ui.Find("QuestionText").GetComponent<TMP_Text>();
            feedbackText = ui.Find("FeedbackText").GetComponent<TMP_Text>();

            optionButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var btn = ui.Find($"OptionButton{i + 1}").GetComponent<Button>();
                optionButtons[i] = btn;
                int idx = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnOptionClicked(idx));
            }
        }

        public override void LoadQuestion()
        {
            string stem = null;
            var choices = new List<string>(4);
            Debug.Log("加载一次");

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
                            correctOption = reader.GetString(1);
                            choices.Add(reader.GetString(2));
                            choices.Add(reader.GetString(3));
                            choices.Add(reader.GetString(4));
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(stem))
            {
                questionText.text = "暂无题目数据";
                return;
            }

            questionText.text = stem;
            feedbackText.text = string.Empty;

            // 添加正确选项并打乱
            choices.Add(correctOption);
            for (int i = choices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = choices[i]; choices[i] = choices[j]; choices[j] = tmp;
            }

            // 赋值按钮文本
            for (int i = 0; i < optionButtons.Length; i++)
            {
                var txt = optionButtons[i].GetComponentInChildren<TMP_Text>();
                txt.text = choices[i];
            }
        }

        public override void CheckAnswer(string answer)
        {
            // 通过按钮回调，不使用此方法
        }

        private void OnOptionClicked(int index)
        {
            var txt = optionButtons[index].GetComponentInChildren<TMP_Text>();
            bool isRight = txt.text == correctOption;
            StartCoroutine(ShowFeedbackThenNext(isRight));
            OnAnswerResult?.Invoke(isRight);
        }

        private IEnumerator ShowFeedbackThenNext(bool isRight)
        {
            feedbackText.color = isRight ? Color.green : Color.red;
            feedbackText.text = isRight ? "回答正确！" : $"回答错误，正确答案是：{correctOption}";
            yield return new WaitForSeconds(1f);
        }
    }
}
