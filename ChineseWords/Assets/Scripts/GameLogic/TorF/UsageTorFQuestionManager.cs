using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.TorF
{
    /// <summary>
    /// 成语/词语使用判断题管理器 - UI架构重构版
    /// 
    /// 功能说明：
    /// - 从simular_usage_questions表随机取一条记录
    /// - 随机决定展示正确示例或错误示例
    /// - 将替换后文本插入到下划线位置并高亮
    /// - 玩家选择"正确"/"错误"判定
    /// - Host端生成题目数据和使用验证信息
    public class UsageTorFQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region 私有字段

        private string dbPath;

        // 当前题目数据
        private bool isInstanceCorrect;     // 当前实例是否正确
        private string currentStem;         // 当前题干
        private string correctFill;         // 正确填空
        private string currentFill;         // 当前使用的填空

        #endregion

        #region IQuestionDataProvider接口实现

        public QuestionType QuestionType => QuestionType.UsageTorF;

        #endregion

        #region Unity生命周期

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("UsageTorF题型管理器初始化完成");
        }

        #endregion

        #region 题目数据生成

        /// <summary>
        /// 获取题目数据（IQuestionDataProvider接口实现）
        /// 为Host端抽题使用，生成使用判断题目和选项数据
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("开始生成UsageTorF题目数据");

            try
            {
                // 1. 从数据库获取使用判断题数据
                var usageData = GetRandomUsageData();
                if (usageData == null)
                {
                    LogError("无法获取使用判断题数据");
                    return null;
                }

                // 2. 随机决定展示正确还是错误示例
                bool isInstanceCorrect = Random.value < 0.5f;
                string currentFill = isInstanceCorrect ?
                    usageData.correctFill :
                    usageData.wrongFills[Random.Range(0, usageData.wrongFills.Count)];

                // 3. 构造显示文本
                string questionText = ReplaceUnderscoreWithHighlight(usageData.stem, currentFill);

                // 4. 构建附加数据
                var additionalData = new UsageTorFAdditionalData
                {
                    stem = usageData.stem,
                    correctFill = usageData.correctFill,
                    currentFill = currentFill,
                    isInstanceCorrect = isInstanceCorrect,
                    wrongFills = usageData.wrongFills.ToArray()
                };

                // 5. 创建题目数据
                var networkQuestionData = new NetworkQuestionData
                {
                    questionType = QuestionType.UsageTorF,
                    questionText = questionText,
                    correctAnswer = isInstanceCorrect.ToString().ToLower(), // "true" 或 "false"
                    options = new string[] { "正确", "错误" }, // 固定的两个选项
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"UsageTorF题目生成成功: {currentFill} (实例{(isInstanceCorrect ? "正确" : "错误")})");
                return networkQuestionData;
            }
            catch (System.Exception e)
            {
                LogError($"生成UsageTorF题目失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取随机使用判断题数据
        /// </summary>
        private UsageData GetRandomUsageData()
        {
            try
            {
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
                                var usageData = new UsageData
                                {
                                    stem = reader.GetString(0),
                                    correctFill = reader.GetString(1),
                                    wrongFills = new List<string>
                                    {
                                        reader.GetString(2),
                                        reader.GetString(3),
                                        reader.GetString(4)
                                    }
                                };
                                return usageData;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                LogError($"获取使用判断题数据失败: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 将题干中的下划线替换为高亮的填空内容
        /// </summary>
        private string ReplaceUnderscoreWithHighlight(string stem, string fill)
        {
            int start = stem.IndexOf('_');
            if (start >= 0)
            {
                // 计算连续下划线长度
                int end = start;
                while (end < stem.Length && stem[end] == '_')
                    end++;

                // 拼接替换：前部分 + 高亮填空 + 后部分
                string before = stem.Substring(0, start);
                string after = stem.Substring(end);
                return before + $"<color=red>{fill}</color>" + after;
            }
            else
            {
                // 没有下划线则直接显示原文
                return stem;
            }
        }

        #endregion

        #region 网络题目处理

        /// <summary>
        /// 加载网络题目数据
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("加载网络UsageTorF题目");

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

            LogDebug($"网络UsageTorF题目加载完成: {currentFill}");
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

            if (networkData.questionType != QuestionType.UsageTorF)
            {
                LogError($"题目类型不匹配，期望: {QuestionType.UsageTorF}, 实际: {networkData.questionType}");
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
                // 解析正确答案（true/false）
                isInstanceCorrect = questionData.correctAnswer.ToLower() == "true";

                // 从附加数据中解析详细信息
                if (!string.IsNullOrEmpty(questionData.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<UsageTorFAdditionalData>(questionData.additionalData);
                    currentStem = additionalInfo.stem;
                    correctFill = additionalInfo.correctFill;
                    currentFill = additionalInfo.currentFill;
                }
                else
                {
                    // 从题目文本解析
                    ExtractInfoFromQuestionText(questionData.questionText);
                }

                LogDebug($"题目数据解析成功: 实例正确={isInstanceCorrect}, 当前填空={currentFill}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"解析题目数据失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从题目文本中提取信息
        /// </summary>
        private void ExtractInfoFromQuestionText(string questionText)
        {
            try
            {
                // 尝试从高亮标签中提取当前填空内容
                var colorTagStart = questionText.IndexOf("<color=red>");
                var colorTagEnd = questionText.IndexOf("</color>");

                if (colorTagStart != -1 && colorTagEnd != -1)
                {
                    colorTagStart += "<color=red>".Length;
                    currentFill = questionText.Substring(colorTagStart, colorTagEnd - colorTagStart);

                    // 重构原始题干（将高亮部分替换为下划线）
                    var before = questionText.Substring(0, colorTagStart - "<color=red>".Length);
                    var after = questionText.Substring(colorTagEnd + "</color>".Length);
                    currentStem = before + new string('_', currentFill.Length) + after;
                }
                else
                {
                    // 如果没有高亮标签，直接使用题目文本
                    currentStem = questionText;
                    currentFill = "未知";
                }

                // 设置正确填空为当前填空（网络模式下无法确定）
                correctFill = currentFill;
            }
            catch (System.Exception e)
            {
                LogError($"从题目文本提取信息失败: {e.Message}");
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
            Debug.Log($"[UsageTorF] 静态验证答案: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[UsageTorF] 静态验证: 答案为空或题目数据无效");
                return false;
            }

            if (question.questionType != QuestionType.UsageTorF)
            {
                Debug.LogError($"[UsageTorF] 静态验证: 题目类型不匹配，期望UsageTorF，实际{question.questionType}");
                return false;
            }

            try
            {
                string correctAnswer = question.correctAnswer?.Trim().ToLower();
                if (string.IsNullOrEmpty(correctAnswer))
                {
                    Debug.Log("[UsageTorF] 静态验证: 题目数据中缺少正确答案");
                    return false;
                }

                // 标准化用户答案
                string normalizedAnswer = NormalizeAnswer(answer.Trim());

                bool isCorrect = normalizedAnswer == correctAnswer;
                Debug.Log($"[UsageTorF] 静态验证结果: 用户答案='{answer.Trim()}', 标准化='{normalizedAnswer}', 正确答案='{correctAnswer}', 结果={isCorrect}");

                return isCorrect;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UsageTorF] 静态验证异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 标准化答案格式
        /// </summary>
        private static string NormalizeAnswer(string answer)
        {
            // 处理各种可能的答案格式
            switch (answer.ToLower())
            {
                case "true":
                case "正确":
                case "对":
                case "是":
                    return "true";
                case "false":
                case "错误":
                case "错":
                case "否":
                    return "false";
                default:
                    return answer.ToLower();
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

            // 标准化答案
            string normalizedAnswer = NormalizeAnswer(answer.Trim());
            string expectedAnswer = isInstanceCorrect ? "true" : "false";

            bool isCorrect = normalizedAnswer == expectedAnswer;
            LogDebug($"本地验证结果: 标准化答案='{normalizedAnswer}', 期望答案='{expectedAnswer}', 结果={isCorrect}");

            return isCorrect;
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
            LogDebug("加载本地UsageTorF题目");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("无法生成本地题目");
                return;
            }

            ParseQuestionData(questionData);
            LogDebug($"本地题目加载完成: {currentFill}");
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[UsageTorF] {message}");
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[UsageTorF] {message}");
        }

        #endregion
    }

    /// <summary>
    /// 使用判断题数据结构
    /// </summary>
    public class UsageData
    {
        public string stem;                 // 题干（包含下划线）
        public string correctFill;          // 正确填空
        public List<string> wrongFills;     // 错误填空选项
    }

    /// <summary>
    /// 使用判断题附加数据结构
    /// </summary>
    [System.Serializable]
    public class UsageTorFAdditionalData
    {
        public string stem;                 // 原始题干（含下划线）
        public string correctFill;          // 正确填空内容
        public string currentFill;          // 当前使用的填空内容
        public bool isInstanceCorrect;      // 当前实例是否正确
        public string[] wrongFills;         // 错误选项列表
    }
}