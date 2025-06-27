using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.TorF
{
    /// <summary>
    /// 成语/词语褒贬义判断题管理器 - UI架构重构版
    /// 
    /// 功能说明：
    /// - 从sentiment表随机取一条记录，生成干扰答案
    /// - 映射polarity为"中性/褒义/贬义/兼有"
    /// - 生成权重较低的"兼有"干扰或其他类型干扰答案
    /// - 随机将正确答案和干扰答案分配到左右选项
    /// - Host端生成题目数据和选项验证信息
    /// </summary>
    public class SentimentTorFQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region 配置字段

        [Header("题目生成配置")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        #endregion

        #region 私有字段

        private string dbPath;

        // 当前题目数据
        private string currentWord;
        private int currentPolarity;

        #endregion

        #region IQuestionDataProvider接口实现

        public QuestionType QuestionType => QuestionType.SentimentTorF;

        #endregion

        #region Unity生命周期

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("SentimentTorF题型管理器初始化完成");
        }

        #endregion

        #region 题目数据生成

        /// <summary>
        /// 获取题目数据（IQuestionDataProvider接口实现）
        /// 为Host端抽题使用，生成褒贬义判断题目和选项数据
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("开始生成SentimentTorF题目数据");

            try
            {
                // 1. 随机选择sentiment记录
                var sentimentData = GetRandomSentimentData();
                if (sentimentData == null)
                {
                    LogError("无法获取有效情感记录");
                    return null;
                }

                // 2. 根据source和word_id获取词条
                string word = GetWordFromDatabase(sentimentData.source, sentimentData.wordId);
                if (string.IsNullOrEmpty(word))
                {
                    LogError("找不到对应词条");
                    return null;
                }

                // 3. 生成选项
                var choicesData = GenerateChoicesForHost(sentimentData.polarity);

                // 4. 构建附加数据
                var additionalData = new SentimentTorFAdditionalData
                {
                    word = word,
                    polarity = sentimentData.polarity,
                    choices = choicesData.choices,
                    correctChoiceIndex = choicesData.correctIndex,
                    source = sentimentData.source,
                    wordId = sentimentData.wordId
                };

                // 5. 创建题目数据
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.SentimentTorF,
                    questionText = FormatQuestionText(word),
                    correctAnswer = sentimentData.polarity.ToString(),
                    options = choicesData.choices,
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"SentimentTorF题目生成成功: {word} (polarity: {sentimentData.polarity} -> {MapPolarity(sentimentData.polarity)})");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"生成SentimentTorF题目失败: {e.Message}");
                return null;
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
                LogError($"获取情感数据失败: {e.Message}");
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
                LogError($"获取词条失败: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 为Host抽题生成选项
        /// </summary>
        private (string[] choices, int correctIndex) GenerateChoicesForHost(int correctPolarity)
        {
            // 获取正确答案文本
            string correctText = MapPolarity(correctPolarity);

            // 生成干扰答案（权重较低的"兼有"或其他类型）
            var candidates = new List<int> { 0, 1, 2, 3 }.Where(p => p != correctPolarity).ToList();
            var weights = candidates.Select(p => p == 3 ? 0.2f : 1f).ToList();
            int wrongPolarity = WeightedChoice(candidates, weights);
            string wrongText = MapPolarity(wrongPolarity);

            // 随机分配到选项A和B
            string[] choices = new string[2];
            int correctIndex;

            if (Random.value < 0.5f)
            {
                // 正确答案在A位置
                choices[0] = correctText;
                choices[1] = wrongText;
                correctIndex = 0;
            }
            else
            {
                // 正确答案在B位置
                choices[0] = wrongText;
                choices[1] = correctText;
                correctIndex = 1;
            }

            return (choices, correctIndex);
        }

        /// <summary>
        /// 格式化题目文本
        /// </summary>
        private string FormatQuestionText(string word)
        {
            return $"题目：判断下列词语的情感倾向\n    \u300c<color=red>{word}</color>\u300d";
        }

        #endregion

        #region 网络题目处理

        /// <summary>
        /// 加载网络题目数据
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("加载网络SentimentTorF题目");

            if (!ValidateNetworkData(networkData))
            {
                LogError("网络题目数据验证失败");
                return;
            }

            if (!ParseQuestionData(networkData))
            {
                LogError("解析网络题目数据失败");
                return;
            }

            LogDebug($"网络SentimentTorF题目加载完成: {currentWord}");
        }

        /// <summary>
        /// 验证网络数据
        /// </summary>
        private bool ValidateNetworkData(NetworkQuestionData networkData)
        {
            if (networkData == null)
            {
                LogError("网络题目数据为空");
                return false;
            }

            if (networkData.questionType != QuestionType.SentimentTorF)
            {
                LogError($"题目类型不匹配，期望: {QuestionType.SentimentTorF}, 实际: {networkData.questionType}");
                return false;
            }

            if (networkData.options == null || networkData.options.Length != 2)
            {
                LogError("选项数据错误，判断题需要2个选项");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 解析题目数据
        /// </summary>
        private bool ParseQuestionData(NetworkQuestionData questionData)
        {
            try
            {
                // 解析正确答案（polarity值）
                if (int.TryParse(questionData.correctAnswer, out int polarity))
                {
                    currentPolarity = polarity;
                }
                else
                {
                    // 如果是文本形式，转换为polarity值
                    currentPolarity = ParsePolarityText(questionData.correctAnswer);
                }

                // 从附加数据中解析详细信息
                if (!string.IsNullOrEmpty(questionData.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<SentimentTorFAdditionalData>(questionData.additionalData);
                    currentWord = additionalInfo.word;
                }
                else
                {
                    // 从题目文本中提取词语
                    ExtractWordFromQuestionText(questionData.questionText);
                }

                LogDebug($"题目数据解析成功: 词语={currentWord}, 正确情感={MapPolarity(currentPolarity)}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"解析题目数据失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从题目文本中提取词语
        /// </summary>
        private void ExtractWordFromQuestionText(string questionText)
        {
            try
            {
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
            catch (System.Exception e)
            {
                LogError($"从题目文本提取词语失败: {e.Message}");
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

        #endregion

        #region 答案验证

        /// <summary>
        /// 静态答案验证方法 - 供Host端调用
        /// 基于题目数据验证答案，不依赖实例状态
        /// </summary>
        public static bool ValidateAnswerStatic(string answer, NetworkQuestionData question)
        {
            Debug.Log($"[SentimentTorF] 静态验证答案: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[SentimentTorF] 静态验证: 答案为空或题目数据无效");
                return false;
            }

            if (question.questionType != QuestionType.SentimentTorF)
            {
                Debug.LogError($"[SentimentTorF] 静态验证: 题目类型不匹配，期望SentimentTorF，实际{question.questionType}");
                return false;
            }

            try
            {
                string correctAnswer = question.correctAnswer?.Trim();
                if (string.IsNullOrEmpty(correctAnswer))
                {
                    Debug.Log("[SentimentTorF] 静态验证: 题目数据中缺少正确答案");
                    return false;
                }

                // 支持两种验证方式：polarity值比较或文本比较
                bool isCorrect = false;

                // 尝试作为polarity值比较
                if (int.TryParse(answer.Trim(), out int answerPolarity) &&
                    int.TryParse(correctAnswer, out int correctPolarity))
                {
                    isCorrect = answerPolarity == correctPolarity;
                }
                else
                {
                    // 作为文本比较
                    isCorrect = answer.Trim().Equals(correctAnswer, System.StringComparison.Ordinal);
                }

                Debug.Log($"[SentimentTorF] 静态验证结果: 用户答案='{answer.Trim()}', 正确答案='{correctAnswer}', 结果={isCorrect}");
                return isCorrect;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SentimentTorF] 静态验证异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 本地答案验证（用于单机模式或网络模式的本地验证）
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            LogDebug($"验证本地答案: {answer}");

            bool isCorrect = ValidateLocalAnswer(answer);
            OnAnswerResult?.Invoke(isCorrect);
        }

        /// <summary>
        /// 验证本地答案
        /// </summary>
        private bool ValidateLocalAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
            {
                LogDebug("答案为空");
                return false;
            }

            // 支持polarity值验证
            if (int.TryParse(answer.Trim(), out int selectedPolarity))
            {
                bool isCorrect = selectedPolarity == currentPolarity;
                LogDebug($"本地验证结果: 选择情感={MapPolarity(selectedPolarity)}, 正确情感={MapPolarity(currentPolarity)}, 结果={isCorrect}");
                return isCorrect;
            }

            LogDebug("答案格式无效");
            return false;
        }

        #endregion

        #region 网络结果处理

        /// <summary>
        /// 显示网络答题结果（由网络系统调用）
        /// 重要：此方法必须保留，网络系统会通过反射调用
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            LogDebug($"收到网络答题结果: {(isCorrect ? "正确" : "错误")}");

            // 更新正确答案显示
            if (int.TryParse(correctAnswer, out int polarity))
            {
                currentPolarity = polarity;
            }

            OnAnswerResult?.Invoke(isCorrect);
        }

        #endregion

        #region 单机模式兼容（保留高耦合方法）

        /// <summary>
        /// 加载本地题目（单机模式）
        /// 保留此方法因为可能被其他系统调用
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            LogDebug("加载本地SentimentTorF题目");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("无法生成本地题目");
                return;
            }

            ParseQuestionData(questionData);
            LogDebug($"本地题目加载完成: {currentWord}");
        }

        #endregion

        #region 工具方法

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
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[SentimentTorF] {message}");
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[SentimentTorF] {message}");
        }

        #endregion
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
    /// 情感判断题附加数据结构
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