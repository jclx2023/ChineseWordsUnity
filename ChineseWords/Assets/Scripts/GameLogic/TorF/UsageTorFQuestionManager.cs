using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;

namespace GameLogic.TorF
{
    /// <summary>
    /// 成语/词语使用判断题：
    /// - 从 simular_usage_questions 表随机取一条记录
    /// - 随机决定展示正确示例或错误示例
    /// - 将替换后文本插入到下划线位置并高亮
    /// - 玩家选择“正确”/“错误”判定
    /// </summary>
    public class UsageTorFQuestionManager : QuestionManagerBase
    {
        [Header("数据库路径 (StreamingAssets/dictionary.db)")]
        private string dbPath;

        [Header("UI 组件 (Prefabs/InGame/TorFUI)")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private Button trueButton;
        [SerializeField] private Button falseButton;
        [SerializeField] private TMP_Text feedbackText;

        private TMP_Text textTrue;
        private TMP_Text textFalse;
        private bool isInstanceCorrect;

        private void Awake()
        {
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            var ui = UIManager.Instance.LoadUI("Prefabs/InGame/TorFUI");


            questionText = ui.Find("QuestionText").GetComponent<TMP_Text>();
            trueButton = ui.Find("TrueButton").GetComponent<Button>();
            falseButton = ui.Find("FalseButton").GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText").GetComponent<TMP_Text>();

            textTrue = trueButton.GetComponentInChildren<TMP_Text>();
            textFalse = falseButton.GetComponentInChildren<TMP_Text>();

            trueButton.onClick.RemoveAllListeners();
            falseButton.onClick.RemoveAllListeners();
            trueButton.onClick.AddListener(() => OnSelect(true));
            falseButton.onClick.AddListener(() => OnSelect(false));

            feedbackText.text = string.Empty;
        }

        public override void LoadQuestion()
        {
            string stem = null;
            string correctFill = null;
            List<string> wrongFills = new List<string>(3);
            Debug.Log("加载一次Usage");
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT [stem], [True], [1], [2], [3]
  FROM simular_usage_questions
 ORDER BY RANDOM()
    LIMIT 1";
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stem = reader.GetString(0);
                            correctFill = reader.GetString(1);
                            wrongFills.Add(reader.GetString(2));
                            wrongFills.Add(reader.GetString(3));
                            wrongFills.Add(reader.GetString(4));
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(stem) || string.IsNullOrEmpty(correctFill))
            {
                questionText.text = "暂无题目数据";
                return;
            }

            // 决定展示正确或错误示例
            isInstanceCorrect = Random.value < 0.5f;
            string fill = isInstanceCorrect ? correctFill : wrongFills[Random.Range(0, wrongFills.Count)];

            // 构造题干：替换第一个下划线段
            int start = stem.IndexOf('_');
            if (start >= 0)
            {
                // 计算连续下划线长度
                int end = start;
                while (end < stem.Length && stem[end] == '_') end++;
                // 拼接替换
                string before = stem.Substring(0, start);
                string after = stem.Substring(end);
                questionText.text = before + $"<color=red>{fill}</color>" + after;
            }
            else
            {
                // 没有下划线则直接显示
                questionText.text = stem;
            }

            feedbackText.text = string.Empty;
            textTrue.text = "正确";
            textFalse.text = "错误";
        }

        public override void CheckAnswer(string answer)
        {
            Debug.Log("CheckAnswer 不适用于此题型");
        }

        private void OnSelect(bool selectedTrue)
        {
            bool isRight = (selectedTrue && isInstanceCorrect) || (!selectedTrue && !isInstanceCorrect);
            StartCoroutine(ShowFeedbackThenNext(isRight));
            OnAnswerResult?.Invoke(isRight);
        }

        private IEnumerator ShowFeedbackThenNext(bool isRight)
        {
            feedbackText.color = isRight ? Color.green : Color.red;
            feedbackText.text = isRight ? "回答正确！" : "回答错误！";
            yield return new WaitForSeconds(1f);
        }
    }
}
