using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// 软性单词补全题管理器 - UI架构重构版
    /// 
    /// 功能说明：
    /// - 随机抽词生成通配符模式（*表示任意长度，_表示单个字符）
    /// - 使用正则表达式匹配答案，验证词库存在性
    /// - Host端生成题目数据和通配符模式
    /// - 支持网络模式下的题目生成和答案验证
    /// 
    /// 重构内容：
    /// - 移除所有UI相关代码，UI功能已转移至AnswerUIManager
    /// - 保留数据生成、网络通信和答案验证核心功能
    /// - 优化代码结构，统一题型管理器代码风格
    /// </summary>
    public class SoftFillQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region 配置字段

        [Header("题目生成配置")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;
        [SerializeField] private int revealCount = 2; // 已知字符数量

        #endregion

        #region 私有字段

        private string dbPath;

        // 当前题目数据
        private string currentWord;
        private string stemPattern;
        private Regex matchRegex;

        #endregion

        #region IQuestionDataProvider接口实现

        public QuestionType QuestionType => QuestionType.SoftFill;

        #endregion

        #region Unity生命周期

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("SoftFill题型管理器初始化完成");
        }

        #endregion

        #region 题目数据生成

        /// <summary>
        /// 获取题目数据（IQuestionDataProvider接口实现）
        /// 为Host端抽题使用，生成通配符模式和答案
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("开始生成SoftFill题目数据");

            try
            {
                // 1. 随机获取单词
                string word = GetRandomWordFromDatabase();
                if (string.IsNullOrEmpty(word))
                {
                    LogError("无法找到符合条件的词语");
                    return CreateFallbackQuestion();
                }

                // 2. 生成通配符模式
                string pattern = GenerateWildcardPattern(word);
                if (string.IsNullOrEmpty(pattern))
                {
                    LogError("通配符模式生成失败");
                    return CreateFallbackQuestion();
                }

                // 3. 创建正则表达式模式
                string regexPattern = CreateRegexPattern(pattern);

                // 4. 构建附加数据
                var additionalData = new SoftFillAdditionalData
                {
                    stemPattern = pattern,
                    regexPattern = regexPattern,
                    revealIndices = ExtractRevealIndices(word, pattern)
                };

                // 5. 创建题目数据
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.SoftFill,
                    questionText = FormatQuestionText(pattern),
                    correctAnswer = word,
                    options = new string[0],
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"SoftFill题目生成成功: {pattern} (示例答案: {word})");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"生成SoftFill题目失败: {e.Message}");
                return CreateFallbackQuestion();
            }
        }

        /// <summary>
        /// 从数据库随机获取单词
        /// </summary>
        private string GetRandomWordFromDatabase()
        {
            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT word FROM (
                                SELECT word FROM word WHERE Freq BETWEEN @min AND @max
                                UNION ALL
                                SELECT word FROM idiom WHERE Freq BETWEEN @min AND @max
                            ) ORDER BY RANDOM() LIMIT 1";

                        cmd.Parameters.AddWithValue("@min", freqMin);
                        cmd.Parameters.AddWithValue("@max", freqMax);

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
                LogError($"数据库查询失败: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 为指定单词生成通配符模式
        /// </summary>
        private string GenerateWildcardPattern(string word)
        {
            if (string.IsNullOrEmpty(word))
                return "";

            int wordLength = word.Length;
            int revealedCount = Mathf.Clamp(revealCount, 1, wordLength - 1);

            // 随机选择要显示的字符位置
            var indices = Enumerable.Range(0, wordLength).ToArray();

            // Fisher-Yates 洗牌算法
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }

            var selectedIndices = indices.Take(revealedCount).OrderBy(i => i).ToArray();

            // 构造通配符题干
            var sb = new System.Text.StringBuilder();
            sb.Append("*");
            sb.Append(word[selectedIndices[0]]);

            for (int k = 1; k < selectedIndices.Length; k++)
            {
                int gap = selectedIndices[k] - selectedIndices[k - 1] - 1;
                sb.Append(new string('_', gap));
                sb.Append(word[selectedIndices[k]]);
            }
            sb.Append("*");

            LogDebug($"生成通配符模式: {sb} (基于词语: {word})");
            return sb.ToString();
        }

        /// <summary>
        /// 创建正则表达式模式
        /// </summary>
        private string CreateRegexPattern(string stemPattern)
        {
            if (string.IsNullOrEmpty(stemPattern))
                return "";

            try
            {
                var pattern = "^" + string.Concat(stemPattern.Select(c =>
                {
                    if (c == '*') return ".*";
                    if (c == '_') return ".";
                    return Regex.Escape(c.ToString());
                })) + "$";

                return pattern;
            }
            catch (System.Exception e)
            {
                LogError($"创建正则表达式失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 从通配符模式中提取显示字符的位置
        /// </summary>
        private int[] ExtractRevealIndices(string word, string pattern)
        {
            var indices = new List<int>();

            // 简化的提取逻辑，主要用于完整信息传输
            for (int i = 0; i < word.Length && i < pattern.Length; i++)
            {
                if (pattern[i] != '*' && pattern[i] != '_')
                {
                    indices.Add(i);
                }
            }

            return indices.ToArray();
        }

        /// <summary>
        /// 格式化题目文本
        /// </summary>
        private string FormatQuestionText(string pattern)
        {
            return $"题目：请输入一个符合<color=red>{pattern}</color>格式的单词\nHint：*为任意个字，_为单个字";
        }

        /// <summary>
        /// 创建备用题目
        /// </summary>
        private NetworkQuestionData CreateFallbackQuestion()
        {
            LogDebug("创建SoftFill备用题目");

            return new NetworkQuestionData
            {
                questionType = QuestionType.SoftFill,
                questionText = "题目：请输入一个符合<color=red>*中_国*</color>格式的单词\nHint：*为任意个字，_为单个字",
                correctAnswer = "中华人民共和国",
                options = new string[0],
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(new SoftFillAdditionalData
                {
                    stemPattern = "*中_国*",
                    regexPattern = "^.*中.国.*$",
                    revealIndices = new int[] { 0, 2 }
                })
            };
        }

        #endregion

        #region 网络题目处理

        /// <summary>
        /// 加载网络题目数据
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("加载网络SoftFill题目");

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

            // 创建正则表达式用于本地验证
            CreateMatchRegex();

            LogDebug($"网络SoftFill题目加载完成: {stemPattern}");
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

            if (networkData.questionType != QuestionType.SoftFill)
            {
                LogError($"题目类型不匹配，期望: {QuestionType.SoftFill}, 实际: {networkData.questionType}");
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
                currentWord = questionData.correctAnswer;

                // 从附加数据中解析通配符模式
                if (!string.IsNullOrEmpty(questionData.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<SoftFillAdditionalData>(questionData.additionalData);
                    stemPattern = additionalInfo.stemPattern;
                }
                else
                {
                    // 从题目文本中提取模式
                    ExtractPatternFromQuestionText(questionData.questionText);
                }

                LogDebug($"题目数据解析成功: 模式={stemPattern}, 答案={currentWord}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"解析题目数据失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从题目文本中提取通配符模式
        /// </summary>
        private void ExtractPatternFromQuestionText(string questionText)
        {
            try
            {
                // 寻找 <color=red>模式</color> 格式
                var colorMatch = Regex.Match(questionText, @"<color=red>([^<]+)</color>");
                if (colorMatch.Success)
                {
                    stemPattern = colorMatch.Groups[1].Value;
                    return;
                }

                // 寻找通配符模式
                var wildcardMatch = Regex.Match(questionText, @"([*_\u4e00-\u9fa5]+)");
                if (wildcardMatch.Success)
                {
                    stemPattern = wildcardMatch.Groups[1].Value;
                    return;
                }

                LogError("无法从题目文本解析模式，使用默认模式");
                stemPattern = GenerateWildcardPattern(currentWord);
            }
            catch (System.Exception e)
            {
                LogError($"从题目文本提取模式失败: {e.Message}");
                stemPattern = GenerateWildcardPattern(currentWord);
            }
        }

        /// <summary>
        /// 创建匹配的正则表达式
        /// </summary>
        private void CreateMatchRegex()
        {
            if (string.IsNullOrEmpty(stemPattern))
                return;

            try
            {
                string regexPattern = CreateRegexPattern(stemPattern);
                matchRegex = new Regex(regexPattern);
                LogDebug($"创建正则表达式: {regexPattern}");
            }
            catch (System.Exception e)
            {
                LogError($"创建正则表达式失败: {e.Message}");
                matchRegex = null;
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
            Debug.Log($"[SoftFill] 静态验证答案: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[SoftFill] 静态验证: 答案为空或题目数据无效");
                return false;
            }

            if (question.questionType != QuestionType.SoftFill)
            {
                Debug.LogError($"[SoftFill] 静态验证: 题目类型不匹配，期望SoftFill，实际{question.questionType}");
                return false;
            }

            try
            {
                // 1. 解析通配符模式
                string stemPattern = ExtractStemPatternStatic(question);

                if (string.IsNullOrEmpty(stemPattern))
                {
                    Debug.Log("[SoftFill] 静态验证: 无法获取通配符模式，使用直接比较");
                    return answer.Trim().Equals(question.correctAnswer?.Trim(), System.StringComparison.OrdinalIgnoreCase);
                }

                Debug.Log($"[SoftFill] 静态验证模式: {stemPattern}");

                // 2. 验证模式匹配
                bool patternMatches = ValidatePatternMatchStatic(answer.Trim(), stemPattern);

                if (!patternMatches)
                {
                    Debug.Log($"[SoftFill] 静态验证: 答案 '{answer}' 不匹配模式 '{stemPattern}'");
                    return false;
                }

                // 3. 验证词库存在性
                bool wordExists = IsWordInDatabaseStatic(answer.Trim());

                if (!wordExists)
                {
                    Debug.Log($"[SoftFill] 静态验证: 答案 '{answer}' 不在词库中");
                    return false;
                }

                Debug.Log($"[SoftFill] 静态验证通过: {answer}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SoftFill] 静态验证异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 静态提取通配符模式
        /// </summary>
        private static string ExtractStemPatternStatic(NetworkQuestionData question)
        {
            try
            {
                // 方法1：从附加数据中解析
                if (!string.IsNullOrEmpty(question.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<SoftFillAdditionalData>(question.additionalData);
                    if (!string.IsNullOrEmpty(additionalInfo.stemPattern))
                    {
                        return additionalInfo.stemPattern;
                    }
                }

                // 方法2：从题目文本中提取
                if (!string.IsNullOrEmpty(question.questionText))
                {
                    // 寻找 <color=red>模式</color> 格式
                    var colorMatch = Regex.Match(question.questionText, @"<color=red>([^<]+)</color>");
                    if (colorMatch.Success)
                    {
                        return colorMatch.Groups[1].Value;
                    }

                    // 寻找通配符模式
                    var wildcardMatch = Regex.Match(question.questionText, @"([*_\u4e00-\u9fa5]+[*_][*_\u4e00-\u9fa5]*)");
                    if (wildcardMatch.Success)
                    {
                        return wildcardMatch.Groups[1].Value;
                    }
                }

                return "";
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SoftFill] 静态解析模式失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 静态模式匹配验证
        /// </summary>
        private static bool ValidatePatternMatchStatic(string answer, string stemPattern)
        {
            try
            {
                // 创建正则表达式模式
                var regexPattern = "^" + string.Concat(stemPattern.Select(c =>
                {
                    if (c == '*') return ".*";
                    if (c == '_') return ".";
                    return Regex.Escape(c.ToString());
                })) + "$";

                var regex = new Regex(regexPattern);
                return regex.IsMatch(answer);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SoftFill] 静态模式匹配失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 静态词库检查 - 供Host端调用
        /// </summary>
        public static bool IsWordInDatabaseStatic(string word)
        {
            if (string.IsNullOrEmpty(word))
                return false;

            string dbPath = Application.streamingAssetsPath + "/dictionary.db";

            try
            {
                if (!System.IO.File.Exists(dbPath))
                {
                    Debug.LogWarning($"[SoftFill] 静态验证: 数据库文件不存在: {dbPath}");
                    return false;
                }

                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM (
                                SELECT word FROM word WHERE word = @word
                                UNION ALL
                                SELECT word FROM idiom WHERE word = @word
                            )";
                        cmd.Parameters.AddWithValue("@word", word);

                        long count = (long)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SoftFill] 静态词库查询失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 本地答案验证（用于单机模式或网络模式的本地验证）
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            LogDebug($"验证本地答案: {answer}");

            bool isCorrect = ValidateLocalAnswer(answer.Trim());
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

            // 检查是否匹配通配符模式
            if (matchRegex == null || !matchRegex.IsMatch(answer))
            {
                LogDebug($"答案不匹配模式: {stemPattern}");
                return false;
            }

            // 检查词语是否在数据库中
            bool existsInDB = IsWordInLocalDatabase(answer);
            LogDebug($"词语'{answer}'在数据库中: {existsInDB}");

            return existsInDB;
        }

        /// <summary>
        /// 检查词语是否在本地数据库中
        /// </summary>
        private bool IsWordInLocalDatabase(string word)
        {
            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM (
                                SELECT word FROM word WHERE word = @word
                                UNION ALL
                                SELECT word FROM idiom WHERE word = @word
                            )";
                        cmd.Parameters.AddWithValue("@word", word);

                        long count = (long)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (System.Exception e)
            {
                LogError($"本地数据库查询失败: {e.Message}");
                return false;
            }
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

            currentWord = correctAnswer;
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
            LogDebug("加载本地SoftFill题目");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("无法生成本地题目");
                return;
            }

            ParseQuestionData(questionData);
            CreateMatchRegex();
            LogDebug($"本地题目加载完成: {stemPattern}");
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[SoftFill] {message}");
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[SoftFill] {message}");
        }

        #endregion
    }

    /// <summary>
    /// 软填空题附加数据结构
    /// </summary>
    [System.Serializable]
    public class SoftFillAdditionalData
    {
        public string stemPattern;      // 通配符模式 (如 "*中_国*")
        public int[] revealIndices;     // 显示的字符位置
        public string regexPattern;     // 正则表达式模式
    }
}