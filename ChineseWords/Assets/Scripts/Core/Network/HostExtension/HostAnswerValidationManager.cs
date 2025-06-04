using UnityEngine;
using System.Collections.Generic;
using Core;
using Core.Network;
using GameLogic.FillBlank;
using System.Linq;

namespace Core.Network
{
    /// <summary>
    /// 答案验证管理器
    /// 专门负责各种题型的答案验证逻辑
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
        private int maxCacheSize = 1000;

        // 特殊题型管理器缓存
        private IdiomChainQuestionManager idiomChainManager;
        private QuestionDataService questionDataService;

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
        /// 验证软性填空题答案
        /// </summary>
        /// <param name="answer">玩家答案</param>
        /// <param name="question">题目数据</param>
        /// <returns>验证结果</returns>
        private ValidationResult ValidateSoftFillAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"验证软性填空答案: {answer}");

            // 软性填空通常有多个可能的正确答案
            // 这里可以扩展支持多答案验证
            bool isValid = ValidateGenericAnswerInternal(answer, question.correctAnswer);

            // 可以在这里添加软性填空的特殊逻辑
            // 比如同义词检查、部分匹配等

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
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