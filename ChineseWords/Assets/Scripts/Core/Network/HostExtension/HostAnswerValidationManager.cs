using UnityEngine;
using System.Collections.Generic;
using Core;
using Core.Network;
using GameLogic.FillBlank;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Data.Sqlite;

namespace Core.Network
{
    /// <summary>
    /// 答案验证管理器 - 修复版
    /// 专门负责各种题型的答案验证逻辑，完整支持SoftFill验证
    /// </summary>
    public class AnswerValidationManager
    {
        [Header("调试设置")]
        private bool enableDebugLogs = true;

        [Header("验证设置")]
        private bool strictValidation = false; // 是否启用严格验证模式
        private bool caseSensitive = false;    // 是否大小写敏感

        // 验证结果缓存（可选优化）
        private Dictionary<string, bool> validationCache;
        private int maxCacheSize = 3000;

        // 特殊题型管理器缓存
        private IdiomChainQuestionManager idiomChainManager;
        private QuestionDataService questionDataService;

        // SoftFill数据库路径（用于验证词库存在性）
        private string dbPath;

        // 事件定义
        public System.Action<QuestionType, string, bool> OnAnswerValidated; // questionType, answer, isCorrect
        public System.Action<string> OnValidationError; // error message

        /// <summary>
        /// 验证结果数据结构
        /// </summary>
        public class ValidationResult
        {
            public bool isCorrect;
            public string providedAnswer;
            public string correctAnswer;
            public QuestionType questionType;
            public string errorMessage;
            public float validationTime;

            public ValidationResult(bool correct, string provided, string expected, QuestionType type)
            {
                isCorrect = correct;
                providedAnswer = provided;
                correctAnswer = expected;
                questionType = type;
                errorMessage = "";
                validationTime = Time.time;
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public AnswerValidationManager()
        {
            validationCache = new Dictionary<string, bool>();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
            LogDebug("AnswerValidationManager 实例已创建");
        }

        #region 初始化

        /// <summary>
        /// 初始化答案验证管理器
        /// </summary>
        /// <param name="dataService">题目数据服务</param>
        /// <param name="enableCache">是否启用验证缓存</param>
        public void Initialize(QuestionDataService dataService = null, bool enableCache = true)
        {
            LogDebug("初始化AnswerValidationManager...");

            questionDataService = dataService ?? QuestionDataService.Instance;

            if (enableCache)
            {
                validationCache = new Dictionary<string, bool>();
            }
            else
            {
                validationCache = null;
            }

            // 检查数据库路径
            if (!System.IO.File.Exists(dbPath))
            {
                Debug.LogWarning($"[AnswerValidationManager] 数据库文件不存在: {dbPath}");
            }

            LogDebug($"AnswerValidationManager初始化完成 - 缓存: {(enableCache ? "启用" : "禁用")}");
        }

        #endregion

        #region 主验证方法

        /// <summary>
        /// 验证答案（主入口方法）
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>验证结果</returns>
        public ValidationResult ValidateAnswer(string answer, NetworkQuestionData question)
        {
            if (question == null)
            {
                var error = "题目数据为空";
                LogDebug($"验证失败: {error}");
                OnValidationError?.Invoke(error);
                return new ValidationResult(false, answer, "", QuestionType.HardFill) { errorMessage = error };
            }

            LogDebug($"开始验证答案: [{answer}] 题型: {question.questionType}");

            ValidationResult result;

            try
            {
                // 根据题目类型选择验证方式
                switch (question.questionType)
                {
                    case QuestionType.IdiomChain:
                        result = ValidateIdiomChainAnswer(answer, question);
                        break;

                    case QuestionType.HardFill:
                        result = ValidateHardFillAnswer(answer, question);
                        break;

                    case QuestionType.SoftFill:
                        result = ValidateSoftFillAnswer(answer, question);
                        break;

                    case QuestionType.TextPinyin:
                        result = ValidatePinyinAnswer(answer, question);
                        break;

                    case QuestionType.ExplanationChoice:
                    case QuestionType.SimularWordChoice:
                        result = ValidateChoiceAnswer(answer, question);
                        break;

                    case QuestionType.SentimentTorF:
                    case QuestionType.UsageTorF:
                        result = ValidateTrueFalseAnswer(answer, question);
                        break;

                    case QuestionType.HandWriting:
                        result = ValidateHandwritingAnswer(answer, question);
                        break;

                    default:
                        result = ValidateGenericAnswer(answer, question);
                        break;
                }

                LogDebug($"验证完成: {question.questionType} - {(result.isCorrect ? "正确" : "错误")}");

                // 触发验证事件
                OnAnswerValidated?.Invoke(question.questionType, answer, result.isCorrect);

                return result;
            }
            catch (System.Exception e)
            {
                var error = $"验证过程中发生异常: {e.Message}";
                Debug.LogError($"[AnswerValidationManager] {error}");
                OnValidationError?.Invoke(error);
                return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = error };
            }
        }

        /// <summary>
        /// 简化的答案验证方法（向后兼容）
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>是否正确</returns>
        public bool ValidateAnswerSimple(string answer, NetworkQuestionData question)
        {
            var result = ValidateAnswer(answer, question);
            return result.isCorrect;
        }

        #endregion

        #region 具体题型验证

        /// <summary>
        /// 验证成语接龙答案
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>验证结果</returns>
        private ValidationResult ValidateIdiomChainAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"验证成语接龙答案: {answer}");

            try
            {
                // 从题目数据中获取题干成语
                string baseIdiom = GetBaseIdiomFromQuestion(question);

                if (string.IsNullOrEmpty(baseIdiom))
                {
                    var error = "无法获取成语接龙的题干成语";
                    LogDebug(error);
                    return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = error };
                }

                // 获取成语接龙管理器
                var idiomManager = GetIdiomChainManager();
                if (idiomManager == null)
                {
                    var error = "无法找到成语接龙管理器";
                    LogDebug(error);
                    return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = error };
                }

                // 调用专业验证方法
                bool isValid = idiomManager.ValidateIdiomChain(answer, baseIdiom);
                LogDebug($"成语接龙验证结果: {answer} (基于: {baseIdiom}) -> {isValid}");

                return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
            }
            catch (System.Exception e)
            {
                var error = $"成语接龙验证失败: {e.Message}";
                LogDebug(error);
                return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = error };
            }
        }

        /// <summary>
        /// 验证硬性填空题答案
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>验证结果</returns>
        private ValidationResult ValidateHardFillAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"验证硬性填空答案: {answer}");

            try
            {
                // 调用HardFill管理器的静态验证方法
                bool isValid = HardFillQuestionManager.ValidateAnswerStatic(answer, question);
                LogDebug($"硬性填空验证结果: {isValid}");

                return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
            }
            catch (System.Exception e)
            {
                var error = $"硬性填空验证失败: {e.Message}";
                LogDebug(error);

                // 回退到通用验证
                bool fallbackResult = ValidateGenericAnswerInternal(answer, question.correctAnswer);
                return new ValidationResult(fallbackResult, answer, question.correctAnswer, question.questionType) { errorMessage = error };
            }
        }

        /// <summary>
        /// 验证软性填空题答案 - 修复版本
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>验证结果</returns>
        private ValidationResult ValidateSoftFillAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"验证软性填空答案: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()))
            {
                LogDebug("软性填空答案为空");
                return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = "答案为空" };
            }

            try
            {
                // 1. 从题目数据解析通配符模式
                string stemPattern = ExtractStemPatternFromQuestion(question);

                if (string.IsNullOrEmpty(stemPattern))
                {
                    var error = "无法获取软性填空的通配符模式";
                    LogDebug(error);
                    // 回退到直接比较
                    bool fallbackResult = ValidateGenericAnswerInternal(answer, question.correctAnswer);
                    return new ValidationResult(fallbackResult, answer, question.correctAnswer, question.questionType) { errorMessage = error };
                }

                LogDebug($"软性填空模式: {stemPattern}");

                // 2. 创建正则表达式进行模式匹配
                bool patternMatches = ValidatePatternMatch(answer.Trim(), stemPattern);

                if (!patternMatches)
                {
                    LogDebug($"答案 '{answer}' 不匹配模式 '{stemPattern}'");
                    return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = "不匹配题目模式" };
                }

                LogDebug($"答案 '{answer}' 匹配模式 '{stemPattern}' ✓");

                // 3. 检查词是否存在于词库中
                bool wordExists = IsWordInDatabase(answer.Trim());

                if (!wordExists)
                {
                    LogDebug($"答案 '{answer}' 不在词库中");
                    return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = "词汇不在词库中" };
                }

                LogDebug($"答案 '{answer}' 在词库中 ✓");

                // 两个条件都满足，答案正确
                LogDebug($"软性填空验证通过: {answer}");
                return new ValidationResult(true, answer, question.correctAnswer, question.questionType);
            }
            catch (System.Exception e)
            {
                var error = $"软性填空验证失败: {e.Message}";
                LogDebug(error);

                // 回退到通用验证
                bool fallbackResult = ValidateGenericAnswerInternal(answer, question.correctAnswer);
                return new ValidationResult(fallbackResult, answer, question.correctAnswer, question.questionType) { errorMessage = error };
            }
        }

        /// <summary>
        /// 从题目数据中提取通配符模式
        /// </summary>
        /// <param name="question">题目数据</param>
        /// <returns>通配符模式</returns>
        private string ExtractStemPatternFromQuestion(NetworkQuestionData question)
        {
            try
            {
                // 方法1：从附加数据中解析
                if (!string.IsNullOrEmpty(question.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<SoftFillAdditionalData>(question.additionalData);
                    if (!string.IsNullOrEmpty(additionalInfo.stemPattern))
                    {
                        LogDebug($"从附加数据解析到模式: {additionalInfo.stemPattern}");
                        return additionalInfo.stemPattern;
                    }
                }

                // 方法2：从题目文本中提取（备用方案）
                if (!string.IsNullOrEmpty(question.questionText))
                {
                    // 寻找 <color=red>模式</color> 格式
                    var colorMatch = Regex.Match(question.questionText, @"<color=red>([^<]+)</color>");
                    if (colorMatch.Success)
                    {
                        string pattern = colorMatch.Groups[1].Value;
                        LogDebug($"从题目文本解析到模式: {pattern}");
                        return pattern;
                    }

                    // 寻找通配符模式（包含*或_的字符串）
                    var wildcardMatch = Regex.Match(question.questionText, @"([*_\u4e00-\u9fa5]+[*_][*_\u4e00-\u9fa5]*)");
                    if (wildcardMatch.Success)
                    {
                        string pattern = wildcardMatch.Groups[1].Value;
                        LogDebug($"从题目文本通配符解析到模式: {pattern}");
                        return pattern;
                    }
                }

                LogDebug("无法解析通配符模式");
                return "";
            }
            catch (System.Exception e)
            {
                LogDebug($"解析通配符模式失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 验证答案是否匹配通配符模式
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="stemPattern">通配符模式</param>
        /// <returns>是否匹配</returns>
        private bool ValidatePatternMatch(string answer, string stemPattern)
        {
            try
            {
                // 创建正则表达式模式
                // * -> .* (任意个字符)
                // _ -> . (单个字符)
                // 其他字符 -> 转义
                var regexPattern = "^" + string.Concat(stemPattern.Select(c =>
                {
                    if (c == '*') return ".*";
                    if (c == '_') return ".";
                    return Regex.Escape(c.ToString());
                })) + "$";

                LogDebug($"正则表达式模式: {regexPattern}");

                var regex = new Regex(regexPattern);
                bool matches = regex.IsMatch(answer);

                LogDebug($"模式匹配结果: '{answer}' 匹配 '{regexPattern}' = {matches}");
                return matches;
            }
            catch (System.Exception e)
            {
                LogDebug($"模式匹配失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查词是否在数据库中
        /// </summary>
        /// <param name="word">要检查的词</param>
        /// <returns>是否存在</returns>
        private bool IsWordInDatabase(string word)
        {
            if (string.IsNullOrEmpty(word))
                return false;

            // 检查缓存
            string cacheKey = $"word_exists_{word}";
            if (validationCache != null && validationCache.ContainsKey(cacheKey))
            {
                return validationCache[cacheKey];
            }

            bool exists = false;

            try
            {
                if (!System.IO.File.Exists(dbPath))
                {
                    LogDebug($"数据库文件不存在: {dbPath}");
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
                        exists = count > 0;
                    }
                }

                LogDebug($"词库查询结果: '{word}' 存在 = {exists}");

                // 缓存结果
                if (validationCache != null)
                {
                    if (validationCache.Count >= maxCacheSize)
                    {
                        validationCache.Clear();
                    }
                    validationCache[cacheKey] = exists;
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"数据库查询失败: {e.Message}");
                exists = false;
            }

            return exists;
        }

        /// <summary>
        /// 验证拼音答案
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>验证结果</returns>
        private ValidationResult ValidatePinyinAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"验证拼音答案: {answer}");

            // 拼音验证的特殊处理
            string normalizedAnswer = NormalizePinyinAnswer(answer);
            string normalizedCorrect = NormalizePinyinAnswer(question.correctAnswer);

            bool isValid = normalizedAnswer.Equals(normalizedCorrect, System.StringComparison.OrdinalIgnoreCase);

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        /// <summary>
        /// 验证选择题答案
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>验证结果</returns>
        private ValidationResult ValidateChoiceAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"验证选择题答案: {answer}");

            // 选择题通常是精确匹配
            bool isValid = answer.Trim().Equals(question.correctAnswer.Trim(),
                caseSensitive ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase);

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        /// <summary>
        /// 验证判断题答案
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>验证结果</returns>
        private ValidationResult ValidateTrueFalseAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"验证判断题答案: {answer}");

            // 标准化判断题答案
            string normalizedAnswer = NormalizeTrueFalseAnswer(answer);
            string normalizedCorrect = NormalizeTrueFalseAnswer(question.correctAnswer);

            bool isValid = normalizedAnswer.Equals(normalizedCorrect, System.StringComparison.OrdinalIgnoreCase);

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        /// <summary>
        /// 验证手写题答案
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>验证结果</returns>
        private ValidationResult ValidateHandwritingAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"验证手写题答案: {answer}");

            // 手写题可能需要特殊的识别和验证逻辑
            // 这里暂时使用通用验证，实际可能需要OCR或图像识别

            bool isValid = ValidateGenericAnswerInternal(answer, question.correctAnswer);

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        /// <summary>
        /// 通用答案验证
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>验证结果</returns>
        private ValidationResult ValidateGenericAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"验证通用答案: {answer}");

            bool isValid = ValidateGenericAnswerInternal(answer, question.correctAnswer);

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        #endregion

        #region 答案标准化方法

        /// <summary>
        /// 标准化拼音答案
        /// </summary>
        /// <param name="answer">原始答案</param>
        /// <returns>标准化后的答案</returns>
        private string NormalizePinyinAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "";

            // 移除空格、声调符号等
            string normalized = answer.Trim()
                .Replace(" ", "")
                .Replace("ā", "a").Replace("á", "a").Replace("ǎ", "a").Replace("à", "a")
                .Replace("ē", "e").Replace("é", "e").Replace("ě", "e").Replace("è", "e")
                .Replace("ī", "i").Replace("í", "i").Replace("ǐ", "i").Replace("ì", "i")
                .Replace("ō", "o").Replace("ó", "o").Replace("ǒ", "o").Replace("ò", "o")
                .Replace("ū", "u").Replace("ú", "u").Replace("ǔ", "u").Replace("ù", "u")
                .Replace("ǖ", "v").Replace("ǘ", "v").Replace("ǚ", "v").Replace("ǜ", "v");

            return normalized.ToLowerInvariant();
        }

        /// <summary>
        /// 标准化判断题答案
        /// </summary>
        /// <param name="answer">原始答案</param>
        /// <returns>标准化后的答案</returns>
        private string NormalizeTrueFalseAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "";

            string normalized = answer.Trim().ToLowerInvariant();

            // 统一化各种"是"的表示
            if (normalized == "true" || normalized == "是" || normalized == "对" ||
                normalized == "正确" || normalized == "√" || normalized == "t" || normalized == "1")
            {
                return "true";
            }

            // 统一化各种"否"的表示
            if (normalized == "false" || normalized == "否" || normalized == "错" ||
                normalized == "错误" || normalized == "×" || normalized == "f" || normalized == "0")
            {
                return "false";
            }

            return normalized;
        }

        /// <summary>
        /// 通用答案标准化
        /// </summary>
        /// <param name="answer">原始答案</param>
        /// <returns>标准化后的答案</returns>
        private string NormalizeGenericAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "";

            string normalized = answer.Trim();

            // 移除多余的空格
            while (normalized.Contains("  "))
            {
                normalized = normalized.Replace("  ", " ");
            }

            // 统一中英文标点符号
            normalized = normalized
                .Replace("，", ",")
                .Replace("。", ".")
                .Replace("？", "?")
                .Replace("！", "!")
                .Replace("；", ";")
                .Replace("：", ":");

            return caseSensitive ? normalized : normalized.ToLowerInvariant();
        }

        #endregion

        #region 辅助验证方法

        /// <summary>
        /// 通用答案验证内部实现
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="correctAnswer">正确答案</param>
        /// <returns>是否正确</returns>
        private bool ValidateGenericAnswerInternal(string answer, string correctAnswer)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(correctAnswer))
                return false;

            // 检查缓存
            string cacheKey = $"{answer}|{correctAnswer}";
            if (validationCache != null && validationCache.ContainsKey(cacheKey))
            {
                return validationCache[cacheKey];
            }

            // 标准化答案
            string normalizedAnswer = NormalizeGenericAnswer(answer);
            string normalizedCorrect = NormalizeGenericAnswer(correctAnswer);

            bool isCorrect;

            if (strictValidation)
            {
                // 严格模式：必须完全匹配
                isCorrect = normalizedAnswer.Equals(normalizedCorrect);
            }
            else
            {
                // 宽松模式：允许部分匹配
                isCorrect = normalizedAnswer.Equals(normalizedCorrect) ||
                           normalizedCorrect.Contains(normalizedAnswer) ||
                           normalizedAnswer.Contains(normalizedCorrect);
            }

            // 缓存结果
            if (validationCache != null)
            {
                // 限制缓存大小
                if (validationCache.Count >= maxCacheSize)
                {
                    validationCache.Clear();
                }
                validationCache[cacheKey] = isCorrect;
            }

            return isCorrect;
        }

        /// <summary>
        /// 从题目数据中获取题干成语
        /// </summary>
        /// <param name="question">题目数据</param>
        /// <returns>题干成语</returns>
        private string GetBaseIdiomFromQuestion(NetworkQuestionData question)
        {
            try
            {
                if (!string.IsNullOrEmpty(question.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<IdiomChainAdditionalData>(question.additionalData);
                    return additionalInfo.currentIdiom;
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"获取题干成语失败: {e.Message}");
            }

            return "";
        }

        /// <summary>
        /// 获取成语接龙管理器实例
        /// </summary>
        /// <returns>成语接龙管理器</returns>
        private IdiomChainQuestionManager GetIdiomChainManager()
        {
            // 如果已缓存，直接返回
            if (idiomChainManager != null)
                return idiomChainManager;

            try
            {
                // 从题目数据服务获取
                if (questionDataService != null)
                {
                    var getProviderMethod = questionDataService.GetType()
                        .GetMethod("GetOrCreateProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (getProviderMethod != null)
                    {
                        var provider = getProviderMethod.Invoke(questionDataService, new object[] { QuestionType.IdiomChain });

                        if (provider is IdiomChainQuestionManager manager)
                        {
                            idiomChainManager = manager;
                            return manager;
                        }
                    }
                }

                // 直接在场景中查找
                idiomChainManager = Object.FindObjectOfType<IdiomChainQuestionManager>();
                return idiomChainManager;
            }
            catch (System.Exception e)
            {
                LogDebug($"获取IdiomChainQuestionManager失败: {e.Message}");
                return null;
            }
        }

        #endregion

        #region 批量验证

        /// <summary>
        /// 批量验证答案
        /// </summary>
        public List<ValidationResult> ValidateAnswersBatch(List<string> answers, List<NetworkQuestionData> questions)
        {
            if (answers == null || questions == null || answers.Count != questions.Count)
            {
                LogDebug("批量验证参数无效");
                return new List<ValidationResult>();
            }

            LogDebug($"开始批量验证 {answers.Count} 个答案");

            var results = new List<ValidationResult>();

            for (int i = 0; i < answers.Count; i++)
            {
                var result = ValidateAnswer(answers[i], questions[i]);
                results.Add(result);
            }

            LogDebug($"批量验证完成，正确率: {results.Count(r => r.isCorrect)}/{results.Count}");

            return results;
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 设置缓存大小
        /// </summary>
        /// <param name="size">最大缓存条目数</param>
        public void SetCacheSize(int size)
        {
            maxCacheSize = Mathf.Max(0, size);

            if (validationCache != null && validationCache.Count > maxCacheSize)
            {
                validationCache.Clear();
            }

            LogDebug($"验证缓存大小已设置为: {maxCacheSize}");
        }

        /// <summary>
        /// 清空验证缓存
        /// </summary>
        public void ClearValidationCache()
        {
            validationCache?.Clear();
            LogDebug("验证缓存已清空");
        }

        /// <summary>
        /// 设置严格验证模式
        /// </summary>
        /// <param name="strict">是否启用严格验证</param>
        public void SetStrictValidation(bool strict)
        {
            strictValidation = strict;
            LogDebug($"严格验证模式: {(strict ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 设置大小写敏感
        /// </summary>
        /// <param name="sensitive">是否大小写敏感</param>
        public void SetCaseSensitive(bool sensitive)
        {
            caseSensitive = sensitive;
            LogDebug($"大小写敏感: {(sensitive ? "启用" : "禁用")}");
        }

        #endregion

        #region 统计信息

        /// <summary>
        /// 获取验证统计信息
        /// </summary>
        /// <returns>统计信息字符串</returns>
        public string GetValidationStats()
        {
            var stats = "=== AnswerValidationManager统计 ===\n";
            stats += $"验证模式: {(strictValidation ? "严格" : "宽松")}\n";
            stats += $"大小写敏感: {(caseSensitive ? "是" : "否")}\n";
            stats += $"缓存启用: {(validationCache != null ? "是" : "否")}\n";

            if (validationCache != null)
            {
                stats += $"缓存条目数: {validationCache.Count}/{maxCacheSize}\n";
                stats += $"缓存使用率: {(float)validationCache.Count / maxCacheSize:P1}\n";
            }

            stats += $"成语接龙管理器: {(idiomChainManager != null ? "已缓存" : "未缓存")}\n";
            stats += $"题目数据服务: {(questionDataService != null ? "已连接" : "未连接")}\n";
            stats += $"数据库路径: {dbPath}\n";
            stats += $"数据库可用: {(System.IO.File.Exists(dbPath) ? "是" : "否")}";

            return stats;
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 设置调试日志开关
        /// </summary>
        /// <param name="enabled">是否启用调试日志</param>
        public void SetDebugLogs(bool enabled)
        {
            enableDebugLogs = enabled;
            LogDebug($"调试日志已{(enabled ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 调试日志输出
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[AnswerValidationManager] {message}");
            }
        }

        #endregion

        #region 销毁和清理

        /// <summary>
        /// 销毁答案验证管理器
        /// </summary>
        public void Dispose()
        {
            // 清理事件
            OnAnswerValidated = null;
            OnValidationError = null;

            // 清理缓存
            validationCache?.Clear();
            validationCache = null;

            // 清理引用
            idiomChainManager = null;
            questionDataService = null;

            LogDebug("AnswerValidationManager已销毁");
        }

        #endregion

        #region 静态验证方法（为SoftFillQuestionManager提供支持）

        /// <summary>
        /// 静态验证软性填空答案（提供给SoftFillQuestionManager使用）
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>是否正确</returns>
        public static bool ValidateSoftFillAnswerStatic(string answer, NetworkQuestionData question)
        {
            try
            {
                // 创建临时验证器实例
                var validator = new AnswerValidationManager();
                validator.Initialize(null, false); // 不启用缓存，用于静态调用

                var result = validator.ValidateSoftFillAnswer(answer, question);
                return result.isCorrect;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AnswerValidationManager] 静态验证失败: {e.Message}");
                return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// 成语接龙附加数据结构（与GameLogic.FillBlank命名空间保持一致）
    /// </summary>
    [System.Serializable]
    public class IdiomChainAdditionalData
    {
        public string currentIdiom;
        public int chainCount;
        public string[] possibleAnswers;
    }
}