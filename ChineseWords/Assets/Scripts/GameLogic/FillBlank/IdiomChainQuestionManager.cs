using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// 成语接龙题管理器 - UI架构重构版（简化修复）
    /// 
    /// 功能说明：
    /// - 从预加载的首题候选中随机选择，玩家接龙
    /// - 高亮显示需要接龙的字符，验证开头字符和成语存在性
    /// - 特殊逻辑：答对后用答案作为下一题继续接龙
    /// - Host端生成题目数据和接龙验证信息
    /// 
    /// 修复内容：
    /// - 简化题干格式，移除过度的HTML标签和字体大小设置
    /// - 保留关键的颜色高亮（最后一个字）
    /// - 与其他题型管理器保持一致的代码风格
    /// - 优化显示文本生成逻辑
    /// </summary>
    public class IdiomChainQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region 配置字段

        [Header("题目生成配置")]
        [SerializeField] private int minFreq = 0;
        [SerializeField] private int maxFreq = 9;

        #endregion

        #region 私有字段

        private string dbPath;

        // 缓存首题候选（单机模式）
        private List<string> firstCandidates = new List<string>();

        // 当前状态
        private string currentIdiom;
        private bool isGameInProgress = false;

        #endregion

        #region IQuestionDataProvider接口实现

        public QuestionType QuestionType => QuestionType.IdiomChain;

        #endregion

        #region Unity生命周期

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            // 预加载首题候选
            LoadFirstCandidates();

            LogDebug("IdiomChain题型管理器初始化完成");
        }

        #endregion

        #region 题目数据生成

        /// <summary>
        /// 获取题目数据（IQuestionDataProvider接口实现）
        /// 为Host端抽题使用，生成成语接龙题目和验证数据
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("开始生成IdiomChain题目数据");

            try
            {
                string selectedIdiom = GetRandomFirstIdiom();
                if (string.IsNullOrEmpty(selectedIdiom))
                {
                    LogError("没有可用的成语题目");
                    return null;
                }

                // 创建简洁的显示文本（保留关键高亮）
                string displayText = CreateSimpleDisplayText(selectedIdiom, true);

                var additionalData = new IdiomChainAdditionalData
                {
                    displayText = displayText,
                    currentIdiom = selectedIdiom,
                    targetChar = selectedIdiom[selectedIdiom.Length - 1]
                };

                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.IdiomChain,
                    questionText = displayText,
                    correctAnswer = "",
                    options = new string[0],
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"IdiomChain题目生成成功: {selectedIdiom} -> {displayText}");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"生成IdiomChain题目失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建成语接龙的特殊题目（用于答对后的连续接龙）
        /// </summary>
        public NetworkQuestionData CreateContinuationQuestion(string baseIdiom, int chainCount = 0, string previousIdiom = null)
        {
            LogDebug($"创建连续接龙题目，基于: {baseIdiom}, 第{chainCount + 1}个");

            if (string.IsNullOrEmpty(baseIdiom))
                return null;

            try
            {
                // 创建简洁的显示文本
                string displayText = CreateSimpleDisplayText(baseIdiom, false, chainCount, previousIdiom);

                // 创建附加数据
                var additionalData = new IdiomChainAdditionalData
                {
                    displayText = displayText,
                    currentIdiom = baseIdiom,
                    targetChar = baseIdiom[baseIdiom.Length - 1]
                };

                // 创建网络题目数据
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.IdiomChain,
                    questionText = displayText,
                    correctAnswer = "",
                    options = new string[0],
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"连续接龙题目创建成功: {baseIdiom} -> {displayText}");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"创建连续接龙题目失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 重载原有方法以保持兼容性
        /// </summary>
        public NetworkQuestionData CreateContinuationQuestion(string baseIdiom)
        {
            return CreateContinuationQuestion(baseIdiom, 0, null);
        }

        /// <summary>
        /// 创建简洁的显示文本（保留关键高亮）
        /// </summary>
        private string CreateSimpleDisplayText(string idiom, bool isFirstQuestion, int chainCount = 0, string previousIdiom = null)
        {
            if (string.IsNullOrEmpty(idiom))
                return "成语接龙";

            // 高亮最后一个字
            string highlightedIdiom = CreateHighlightedIdiom(idiom);

            if (isFirstQuestion)
            {
                // 首题：简洁显示
                return $"成语接龙：{highlightedIdiom}";
            }
            else
            {
                // 连续题目：稍微提供上下文
                string result = $"接龙：{highlightedIdiom}";

                // 可选：显示上一个成语（如果有且不太长）
                if (!string.IsNullOrEmpty(previousIdiom) && chainCount > 0)
                {
                    result = $"上一个：{previousIdiom}\n{result}";
                }

                return result;
            }
        }

        /// <summary>
        /// 创建高亮的成语（只高亮最后一个字）
        /// </summary>
        private string CreateHighlightedIdiom(string idiom)
        {
            if (string.IsNullOrEmpty(idiom) || idiom.Length < 2)
                return idiom;

            // 只高亮最后一个字，使用红色
            char lastChar = idiom[idiom.Length - 1];
            return idiom.Substring(0, idiom.Length - 1) + $"<color=red>{lastChar}</color>";
        }

        /// <summary>
        /// 预加载首题候选成语
        /// </summary>
        private void LoadFirstCandidates()
        {
            firstCandidates.Clear();

            try
            {
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
                        {
                            while (reader.Read())
                                firstCandidates.Add(reader.GetString(0));
                        }
                    }
                }

                LogDebug($"已加载 {firstCandidates.Count} 个首题候选成语");
            }
            catch (System.Exception e)
            {
                LogError($"加载首题候选失败: {e.Message}");
            }

            if (firstCandidates.Count == 0)
            {
                LogError("首题候选列表为空，成语接龙可能无法正常工作");
            }
        }

        /// <summary>
        /// 随机获取首题成语
        /// </summary>
        private string GetRandomFirstIdiom()
        {
            if (firstCandidates.Count == 0)
            {
                LogError("首题候选列表为空");
                return null;
            }

            // 随机选择（不移除，因为Host端可能需要多次抽题）
            int index = Random.Range(0, firstCandidates.Count);
            return firstCandidates[index];
        }

        #endregion

        #region 网络题目处理

        /// <summary>
        /// 加载网络题目数据
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("加载网络IdiomChain题目");

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

            isGameInProgress = true;
            LogDebug($"网络IdiomChain题目加载完成: {currentIdiom}");
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

            if (networkData.questionType != QuestionType.IdiomChain)
            {
                LogError($"题目类型不匹配，期望: {QuestionType.IdiomChain}, 实际: {networkData.questionType}");
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
                // 从附加数据中获取成语信息
                if (!string.IsNullOrEmpty(questionData.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<IdiomChainAdditionalData>(questionData.additionalData);
                    currentIdiom = additionalInfo.currentIdiom;
                }
                else
                {
                    // 备用方案：从题目文本中提取成语
                    currentIdiom = ExtractIdiomFromDisplayText(questionData.questionText);
                }

                LogDebug($"题目数据解析成功: 当前成语={currentIdiom}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"解析题目数据失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从显示文本中提取成语（优化版）
        /// </summary>
        private string ExtractIdiomFromDisplayText(string displayText)
        {
            if (string.IsNullOrEmpty(displayText))
                return "";

            try
            {
                // 移除HTML标签
                string cleanText = RemoveHtmlTags(displayText);

                // 查找四字成语（更精确的正则）
                var match = System.Text.RegularExpressions.Regex.Match(cleanText, @"[\u4e00-\u9fa5]{4}");

                return match.Success ? match.Value : "";
            }
            catch (System.Exception e)
            {
                LogError($"从显示文本提取成语失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 移除HTML标签的工具方法
        /// </summary>
        private string RemoveHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            try
            {
                return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", "");
            }
            catch (System.Exception e)
            {
                LogError($"移除HTML标签失败: {e.Message}");
                return input;
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
            Debug.Log($"[IdiomChain] 静态验证答案: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[IdiomChain] 静态验证: 答案为空或题目数据无效");
                return false;
            }

            if (question.questionType != QuestionType.IdiomChain)
            {
                Debug.LogError($"[IdiomChain] 静态验证: 题目类型不匹配，期望IdiomChain，实际{question.questionType}");
                return false;
            }

            try
            {
                // 从附加数据中获取当前成语
                string baseIdiom = "";
                if (!string.IsNullOrEmpty(question.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<IdiomChainAdditionalData>(question.additionalData);
                    baseIdiom = additionalInfo.currentIdiom;
                }

                if (string.IsNullOrEmpty(baseIdiom))
                {
                    Debug.Log("[IdiomChain] 静态验证: 无法获取基础成语");
                    return false;
                }

                bool isCorrect = ValidateIdiomChainStatic(answer.Trim(), baseIdiom);
                Debug.Log($"[IdiomChain] 静态验证结果: {isCorrect}");

                return isCorrect;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[IdiomChain] 静态验证异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 静态成语接龙验证
        /// </summary>
        private static bool ValidateIdiomChainStatic(string answer, string baseIdiom)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(baseIdiom))
                return false;

            // 1. 检查开头字符是否正确
            if (answer[0] != baseIdiom[baseIdiom.Length - 1])
            {
                Debug.Log($"[IdiomChain] 静态验证: 开头字符不匹配，期望'{baseIdiom[baseIdiom.Length - 1]}'，实际'{answer[0]}'");
                return false;
            }

            // 2. 检查成语是否存在于词库中
            bool existsInDB = IsIdiomInDatabaseStatic(answer);
            Debug.Log($"[IdiomChain] 静态验证: 成语'{answer}'在数据库中: {existsInDB}");

            return existsInDB;
        }

        /// <summary>
        /// 静态数据库查询 - 供Host端调用
        /// </summary>
        public static bool IsIdiomInDatabaseStatic(string idiom)
        {
            try
            {
                string dbPath = Application.streamingAssetsPath + "/dictionary.db";

                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM (
                                SELECT word FROM idiom WHERE word=@idiom
                                UNION
                                SELECT word FROM other_idiom WHERE word=@idiom
                            )";
                        cmd.Parameters.AddWithValue("@idiom", idiom);

                        long count = (long)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[IdiomChain] 静态数据库查询失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 验证成语接龙答案（实例方法，供外部调用）
        /// </summary>
        public bool ValidateIdiomChain(string answer, string baseIdiom)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(baseIdiom))
                return false;

            // 1. 检查开头字符是否正确
            if (answer[0] != baseIdiom[baseIdiom.Length - 1])
                return false;

            // 2. 检查成语是否存在于词库中
            return IsIdiomInLocalDatabase(answer);
        }

        /// <summary>
        /// 验证成语接龙答案（使用当前成语）
        /// </summary>
        public bool ValidateIdiomChain(string answer)
        {
            return ValidateIdiomChain(answer, currentIdiom);
        }

        /// <summary>
        /// 本地答案验证（用于单机模式或网络模式的本地验证）
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            LogDebug($"验证本地答案: {answer}");

            bool isCorrect = ValidateIdiomChain(answer);

            if (isCorrect)
            {
                // 单机模式下继续接龙
                currentIdiom = answer;
                LogDebug("回答正确，继续接龙");
            }
            else
            {
                LogDebug($"回答错误: {GetValidationErrorMessage(answer)}");
            }

            OnAnswerResult?.Invoke(isCorrect);
        }

        /// <summary>
        /// 检查成语是否在本地数据库中
        /// </summary>
        private bool IsIdiomInLocalDatabase(string idiom)
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
                                SELECT word FROM idiom WHERE word=@idiom
                                UNION
                                SELECT word FROM other_idiom WHERE word=@idiom
                            )";
                        cmd.Parameters.AddWithValue("@idiom", idiom);

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

        /// <summary>
        /// 获取验证错误信息
        /// </summary>
        private string GetValidationErrorMessage(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "答案不能为空";

            if (string.IsNullOrEmpty(currentIdiom))
                return "题目数据错误";

            if (answer[0] != currentIdiom[currentIdiom.Length - 1])
                return $"开头错误，应以'{currentIdiom[currentIdiom.Length - 1]}'开头";

            return "词库中无此成语";
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

            isGameInProgress = false;
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
            LogDebug("加载本地IdiomChain题目");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("无法生成本地题目");
                return;
            }

            ParseQuestionData(questionData);
            isGameInProgress = true;
            LogDebug($"本地题目加载完成: {currentIdiom}");
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[IdiomChain] {message}");
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[IdiomChain] {message}");
        }

        #endregion
    }

    /// <summary>
    /// 成语接龙附加数据结构
    /// </summary>
    [System.Serializable]
    public class IdiomChainAdditionalData
    {
        public string displayText;      // 预格式化的显示文本
        public string currentIdiom;     // 当前成语
        public char targetChar;         // 需要接龙的字符
    }
}