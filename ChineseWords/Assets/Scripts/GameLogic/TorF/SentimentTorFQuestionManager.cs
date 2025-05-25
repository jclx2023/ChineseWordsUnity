using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.TorF
{
    /// <summary>
    /// 成语/词语褒贬义判断题管理器
    /// - 支持单机和网络模式
    /// - 单机模式：从 sentiment 表随机取一条记录，生成干扰答案
    /// - 网络模式：使用服务器提供的题目数据
    /// - 映射 polarity 为"中性/褒义/贬义/兼有"
    /// - 生成权重较低的"兼有"干扰或其他类型干扰答案
    /// - 随机将正确答案和干扰答案分配到左右按钮
    /// </summary>
    public class SentimentTorFQuestionManager : NetworkQuestionManagerBase
    {
        private string dbPath;

        [Header("UI设置")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/TorFUI";

        [Header("单机模式配置")]
        [Header("Freq 范围 (可选)")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        [Header("UI组件引用")]
        private TMP_Text questionText;
        private Button buttonA;
        private Button buttonB;
        private TMP_Text feedbackText;

        private TMP_Text textA;
        private TMP_Text textB;

        private string currentWord;
        private int currentPolarity;
        private int choiceAPolarity;
        private int choiceBPolarity; // 修正变量名
        private bool hasAnswered = false;

        protected override void Awake()
        {
            base.Awake(); // 调用网络基类初始化
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            InitializeUI();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"无法加载UI预制体: {uiPrefabPath}");
                return;
            }

            // 获取UI组件
            questionText = ui.Find("QuestionText")?.GetComponent<TMP_Text>();
            buttonA = ui.Find("TrueButton")?.GetComponent<Button>();
            buttonB = ui.Find("FalseButton")?.GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || buttonA == null || buttonB == null || feedbackText == null)
            {
                Debug.LogError("UI组件获取失败，检查预制体结构");
                return;
            }

            // 获取按钮文本组件
            textA = buttonA.GetComponentInChildren<TMP_Text>();
            textB = buttonB.GetComponentInChildren<TMP_Text>();

            if (textA == null || textB == null)
            {
                Debug.LogError("按钮文本组件获取失败");
                return;
            }

            // 绑定按钮事件
            buttonA.onClick.RemoveAllListeners();
            buttonB.onClick.RemoveAllListeners();
            buttonA.onClick.AddListener(() => OnSelectChoice(choiceAPolarity));
            buttonB.onClick.AddListener(() => OnSelectChoice(choiceBPolarity));

            feedbackText.text = string.Empty;
        }

        /// <summary>
        /// 加载本地题目（单机模式）
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[SentimentTorF] 加载本地题目");

            // 1. 随机选择 sentiment 记录
            var sentimentData = GetRandomSentimentData();
            if (sentimentData == null)
            {
                DisplayErrorMessage("暂无有效情感记录。");
                return;
            }

            // 2. 根据 source 和 word_id 获取词条
            currentWord = GetWordFromDatabase(sentimentData.source, sentimentData.wordId);
            if (string.IsNullOrEmpty(currentWord))
            {
                Debug.Log("找不到对应词条，重新加载题目");
                LoadQuestion(); // 递归重试
                return;
            }

            currentPolarity = sentimentData.polarity;

            // 3. 生成选项
            GenerateChoices();

            // 4. 显示题目
            DisplayQuestion();
        }

        /// <summary>
        /// 加载网络题目（网络模式）
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[SentimentTorF] 加载网络题目");

            if (networkData == null)
            {
                Debug.LogError("网络题目数据为空");
                DisplayErrorMessage("网络题目数据错误");
                return;
            }

            if (networkData.questionType != QuestionType.SentimentTorF)
            {
                Debug.LogError($"题目类型不匹配: 期望{QuestionType.SentimentTorF}, 实际{networkData.questionType}");
                LoadLocalQuestion(); // 降级到本地题目
                return;
            }

            // 从网络数据解析题目信息
            ParseNetworkQuestionData(networkData);

            // 显示题目
            DisplayQuestion();
        }

        /// <summary>
        /// 解析网络题目数据
        /// </summary>
        private void ParseNetworkQuestionData(NetworkQuestionData networkData)
        {
            // 解析正确答案（polarity值）
            if (int.TryParse(networkData.correctAnswer, out int polarity))
            {
                currentPolarity = polarity;
            }
            else
            {
                // 如果是文本形式，转换为polarity值
                currentPolarity = ParsePolarityText(networkData.correctAnswer);
            }

            // 从additionalData中解析额外信息
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<SentimentTorFAdditionalData>(networkData.additionalData);
                    currentWord = additionalInfo.word;

                    // 如果有预设的选项，直接使用
                    if (additionalInfo.choices != null && additionalInfo.choices.Length == 2)
                    {
                        SetupNetworkChoices(additionalInfo.choices, additionalInfo.correctChoiceIndex);
                        return;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"解析网络附加数据失败: {e.Message}");
                }
            }

            // 如果没有附加数据，从题目文本解析
            ExtractWordFromQuestionText(networkData.questionText);

            // 生成选项
            GenerateChoices();
        }

        /// <summary>
        /// 从题目文本中提取词语
        /// </summary>
        private void ExtractWordFromQuestionText(string questionText)
        {
            // 简单的文本解析，提取被标红的词语
            var startTag = "「<color=red>";
            var endTag = "</color>」";

            var startIndex = questionText.IndexOf(startTag);
            var endIndex = questionText.IndexOf(endTag);

            if (startIndex != -1 && endIndex != -1)
            {
                startIndex += startTag.Length;
                currentWord = questionText.Substring(startIndex, endIndex - startIndex);
            }
        }

        /// <summary>
        /// 设置网络模式的选项
        /// </summary>
        private void SetupNetworkChoices(string[] choices, int correctIndex)
        {
            if (correctIndex == 0)
            {
                choiceAPolarity = currentPolarity;
                choiceBPolarity = ParsePolarityText(choices[1]);
                textA.text = choices[0];
                textB.text = choices[1];
            }
            else
            {
                choiceAPolarity = ParsePolarityText(choices[0]);
                choiceBPolarity = currentPolarity;
                textA.text = choices[0];
                textB.text = choices[1];
            }
        }

        /// <summary>
        /// 解析情感文本为polarity值
        /// </summary>
        private int ParsePolarityText(string text)
        {
            switch (text)
            {
                case "中性": return 0;
                case "褒义": return 1;
                case "贬义": return 2;
                case "兼有": return 3;
                default: return 0;
            }
        }

        /// <summary>
        /// 获取随机情感数据
        /// </summary>
        private SentimentData GetRandomSentimentData()
        {
            try
            {
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
                                return new SentimentData
                                {
                                    id = reader.GetInt32(0),
                                    source = reader.GetString(1),
                                    wordId = reader.GetInt32(2),
                                    polarity = reader.GetInt32(3)
                                };
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"获取情感数据失败: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 根据source和wordId获取词条
        /// </summary>
        private string GetWordFromDatabase(string source, int wordId)
        {
            string tableName = source.ToLower() == "word" ? "word" :
                              source.ToLower() == "idiom" ? "idiom" : "other_idiom";

            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"SELECT word FROM {tableName} WHERE id=@id LIMIT 1";
                        cmd.Parameters.AddWithValue("@id", wordId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                return reader.GetString(0);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"获取词条失败: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 生成选项
        /// </summary>
        private void GenerateChoices()
        {
            // 获取正确答案文本
            string correctText = MapPolarity(currentPolarity);

            // 生成干扰答案（权重较低的"兼有"或其他类型）
            var candidates = new List<int> { 0, 1, 2, 3 }.Where(p => p != currentPolarity).ToList();
            var weights = candidates.Select(p => p == 3 ? 0.2f : 1f).ToList();
            int wrongPolarity = WeightedChoice(candidates, weights);
            string wrongText = MapPolarity(wrongPolarity);

            // 随机分配到左右按钮
            if (Random.value < 0.5f)
            {
                choiceAPolarity = currentPolarity;
                choiceBPolarity = wrongPolarity;
                textA.text = correctText;
                textB.text = wrongText;
            }
            else
            {
                choiceAPolarity = wrongPolarity;
                choiceBPolarity = currentPolarity;
                textA.text = wrongText;
                textB.text = correctText;
            }
        }

        /// <summary>
        /// 显示题目
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(currentWord))
            {
                DisplayErrorMessage("词语数据错误");
                return;
            }

            // 使用Unicode转义避免编译错误
            questionText.text = $"题目：判断下列词语的情感倾向\n    \u300c<color=red>{currentWord}</color>\u300d";
            feedbackText.text = string.Empty;

            // 启用按钮
            buttonA.interactable = true;
            buttonB.interactable = true;

            Debug.Log($"[SentimentTorF] 题目显示完成: {currentWord} (正确答案: {MapPolarity(currentPolarity)})");
        }

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            if (questionText != null)
                questionText.text = message;

            if (feedbackText != null)
                feedbackText.text = "";

            if (buttonA != null)
                buttonA.interactable = false;

            if (buttonB != null)
                buttonB.interactable = false;
        }

        /// <summary>
        /// 检查本地答案（单机模式）
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            // 判断题通过按钮点击处理，此方法不直接使用
            Debug.Log("[SentimentTorF] CheckLocalAnswer 被调用，但判断题通过按钮处理");
        }

        /// <summary>
        /// 选择选项处理
        /// </summary>
        private void OnSelectChoice(int selectedPolarity)
        {
            if (hasAnswered)
            {
                Debug.Log("已经回答过了，忽略重复点击");
                return;
            }

            hasAnswered = true;

            // 禁用按钮防止重复点击
            buttonA.interactable = false;
            buttonB.interactable = false;

            Debug.Log($"[SentimentTorF] 选择了情感倾向: {MapPolarity(selectedPolarity)}");

            if (IsNetworkMode())
            {
                HandleNetworkAnswer(selectedPolarity.ToString());
            }
            else
            {
                HandleLocalAnswer(selectedPolarity);
            }
        }

        /// <summary>
        /// 处理网络模式答案
        /// </summary>
        private void HandleNetworkAnswer(string answer)
        {
            Debug.Log($"[SentimentTorF] 网络模式提交答案: {answer}");

            // 显示提交状态
            feedbackText.text = "已提交答案，等待服务器结果...";
            feedbackText.color = Color.yellow;

            // 通过基类提交到服务器
            CheckAnswer(answer);
        }

        /// <summary>
        /// 处理单机模式答案
        /// </summary>
        private void HandleLocalAnswer(int selectedPolarity)
        {
            bool isCorrect = selectedPolarity == currentPolarity;
            Debug.Log($"[SentimentTorF] 单机模式答题结果: {(isCorrect ? "正确" : "错误")}");

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// 显示反馈信息并通知结果
        /// </summary>
        private IEnumerator ShowFeedbackAndNotify(bool isCorrect)
        {
            // 显示反馈
            if (isCorrect)
            {
                feedbackText.text = "回答正确！";
                feedbackText.color = Color.green;
            }
            else
            {
                feedbackText.text = $"回答错误，正确答案是：{MapPolarity(currentPolarity)}";
                feedbackText.color = Color.red;
            }

            // 等待一段时间
            yield return new WaitForSeconds(1.5f);

            // 通知答题结果
            OnAnswerResult?.Invoke(isCorrect);

            // 重新启用按钮为下一题准备
            buttonA.interactable = true;
            buttonB.interactable = true;
        }

        /// <summary>
        /// 显示网络答题结果（由网络系统调用）
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[SentimentTorF] 收到网络结果: {(isCorrect ? "正确" : "错误")}");

            // 更新正确答案显示
            if (int.TryParse(correctAnswer, out int polarity))
            {
                currentPolarity = polarity;
            }

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// 映射polarity值到文本
        /// </summary>
        private static string MapPolarity(int polarity)
        {
            switch (polarity)
            {
                case 0: return "中性";
                case 1: return "褒义";
                case 2: return "贬义";
                case 3: return "兼有";
                default: return "未知";
            }
        }

        /// <summary>
        /// 权重随机选择
        /// </summary>
        private static int WeightedChoice(List<int> items, List<float> weights)
        {
            float total = weights.Sum();
            float random = Random.value * total;
            float accumulator = 0;

            for (int i = 0; i < items.Count; i++)
            {
                accumulator += weights[i];
                if (random <= accumulator)
                    return items[i];
            }

            return items.Last();
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            // 清理按钮事件监听
            if (buttonA != null)
                buttonA.onClick.RemoveAllListeners();
            if (buttonB != null)
                buttonB.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// 情感数据结构
    /// </summary>
    public class SentimentData
    {
        public int id;
        public string source;
        public int wordId;
        public int polarity;
    }

    /// <summary>
    /// 情感判断题附加数据结构（用于网络传输）
    /// </summary>
    [System.Serializable]
    public class SentimentTorFAdditionalData
    {
        public string word;                 // 目标词语
        public int polarity;                // 正确的情感倾向
        public string[] choices;            // 选项文本数组
        public int correctChoiceIndex;      // 正确选项的索引
        public string source;               // 数据来源表
        public int wordId;                  // 词语ID
    }
}