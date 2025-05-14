using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;

namespace GameLogic.TorF
{
    /// <summary>
    /// 成语/词语褒贬义判断题管理器：
    /// - 从 sentiment 表随机取一条记录
    /// - 根据 source 和 word_id 获取对应词条
    /// - 映射 polarity 为“中性/褒义/贬义/兼有”
    /// - 生成一个权重较低的“兼有”干扰或其他类型干扰答案
    /// - 随机将正确答案和干扰答案分配到左右按钮
    /// - 玩家点击后判定是否正确
    /// </summary>
    public class SentimentTorFQuestionManager : QuestionManagerBase
    {
        [Header("数据库路径 (StreamingAssets/dictionary.db)")]
        private string dbPath;

        [Header("Freq 范围 (可选)")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        [Header("UI 组件")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private Button buttonA;
        [SerializeField] private Button buttonB;
        [SerializeField] private TMP_Text feedbackText;

        private TMP_Text textA;
        private TMP_Text textB;

        private string currentWord;
        private int currentPolarity;
        private int choiceAPolarity;
        private int choiceBPolarParty;

        private void Awake()
        {
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/TorFUI");
            var ui = Instantiate(prefab).transform.Find("UI");
            questionText = ui.Find("QuestionText").GetComponent<TMP_Text>();
            buttonA = ui.Find("TrueButton").GetComponent<Button>();
            buttonB = ui.Find("FalseButton").GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText").GetComponent<TMP_Text>();

            textA = buttonA.GetComponentInChildren<TMP_Text>();
            textB = buttonB.GetComponentInChildren<TMP_Text>();

            buttonA.onClick.RemoveAllListeners();
            buttonB.onClick.RemoveAllListeners();
            buttonA.onClick.AddListener(() => OnSelect(choiceAPolarity));
            buttonB.onClick.AddListener(() => OnSelect(choiceBPolarParty));

            feedbackText.text = string.Empty;
            LoadQuestion();
        }

        public override void LoadQuestion()
        {
            // 随机选 sentiment
            int sid = -1; string source = null; int wid = -1;
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id,source,word_id,polarity FROM sentiment ORDER BY RANDOM() LIMIT 1";
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            sid = reader.GetInt32(0);
                            source = reader.GetString(1);
                            wid = reader.GetInt32(2);
                            currentPolarity = reader.GetInt32(3);
                        }
                    }
                }
            }
            if (wid < 0 || string.IsNullOrEmpty(source))
            {
                questionText.text = "暂无有效情感记录。";
                return;
            }

            // 跨表取词
            string table = source.ToLower() == "word" ? "word" : source.ToLower() == "idiom" ? "idiom" : "other_idiom";
            currentWord = null;
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT word FROM {table} WHERE id=@id LIMIT 1";
                    cmd.Parameters.AddWithValue("@id", wid);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) currentWord = reader.GetString(0);
                    }
                }
            }
            if (string.IsNullOrEmpty(currentWord))
            {
                questionText.text = "找不到对应词条。";
                LoadQuestion();
            }

            // 映射答案文本
            string correctText = MapPolarity(currentPolarity);
            // 生成干扰
            var candidates = new List<int> { 0, 1, 2, 3 }.Where(p => p != currentPolarity).ToList();
            var weights = candidates.Select(p => p == 3 ? 0.2f : 1f).ToList();
            int wrongPol = WeightedChoice(candidates, weights);
            string wrongText = MapPolarity(wrongPol);

            // 分配到按钮
            if (Random.value < 0.5f)
            {
                choiceAPolarity = currentPolarity;
                choiceBPolarParty = wrongPol;
                textA.text = correctText;
                textB.text = wrongText;
            }
            else
            {
                choiceAPolarity = wrongPol;
                choiceBPolarParty = currentPolarity;
                textA.text = wrongText;
                textB.text = correctText;
            }

            questionText.text = $"题目：判断下列词语的情感倾向\n    「<color=red>{currentWord}</color>」";
            feedbackText.text = string.Empty;
        }

        public override void CheckAnswer(string answer)
        {
            // 本题使用按钮回调，不通过文本输入
            Debug.Log("CheckAnswer 不适用于此题型。");
        }

        private void OnSelect(int selectedPol)
        {
            bool isRight = selectedPol == currentPolarity;
            StartCoroutine(ShowFeedbackThenNext(isRight));
            OnAnswerResult?.Invoke(isRight);
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
                feedbackText.text = $"回答错误，正确答案是：{MapPolarity(currentPolarity)}";
                feedbackText.color = Color.red;
            }
            yield return new WaitForSeconds(1f);
        }

        private static string MapPolarity(int p)
        {
            switch (p)
            {
                case 0: return "中性";
                case 1: return "褒义";
                case 2: return "贬义";
                case 3: return "兼有";
                default: return string.Empty;
            }
        }

        private static int WeightedChoice(List<int> items, List<float> weights)
        {
            float total = weights.Sum();
            float r = Random.value * total;
            float acc = 0;
            for (int i = 0; i < items.Count; i++)
            {
                acc += weights[i];
                if (r <= acc) return items[i];
            }
            return items.Last();
        }
    }
}
