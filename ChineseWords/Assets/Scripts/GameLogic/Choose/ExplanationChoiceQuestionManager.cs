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
    /// 词语解释选择题管理器：
    /// - 从 WordExplanationChoice 表随机取一条记录
    /// - 显示 stem
    /// - 将 true、false1、false2、false3 四个选项随机打乱后分配给 OptionButton1-4
    /// - 玩家选择后判定是否等于正确选项
    /// - 通过 OnAnswerResult 通知外层统一处理
    /// </summary>
    public class ExplanationChoiceQuestionManager : QuestionManagerBase
    {
        private string dbPath;

        [Header("UI Prefab (Resources/Prefabs/InGame/ExplanationChoiceUI)")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/ChooseUI";

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
            Debug.Log("加载一题");
            string stem = null;
            List<string> choices = new List<string>(4);

            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT stem, [true], false1, false2, false3
  FROM WordExplanationChoice
 ORDER BY RANDOM()
    LIMIT 1";
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stem = reader.GetString(0);
                            correctOption = reader.GetString(1);
                            choices.Add(reader.GetString(1));
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

            // 显示题干
            questionText.text = stem;
            feedbackText.text = string.Empty;

            // 随机打乱选项
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
            // 本题通过按钮回调处理，不使用文本输入
            Debug.Log("CheckAnswer 未被使用");
        }

        private void OnOptionClicked(int index)
        {
            var selected = optionButtons[index].GetComponentInChildren<TMP_Text>().text;
            bool isRight = selected == correctOption;
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
