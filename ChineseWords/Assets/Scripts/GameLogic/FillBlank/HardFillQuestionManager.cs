using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// 硬性单词补全题管理器 - UI架构重构版
    /// 
    /// 功能说明：
    /// - 随机生成填空格式题干（如"投_"、"_容"、"奔_跑"等）
    /// - 验证玩家答案是否符合题干格式且在词库中存在
    /// - Host端负责生成题干格式，不预设固定答案
    /// - 支持网络模式下的题目生成和答案验证
    /// </summary>
    public class HardFillQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region 配置字段

        [Header("题目生成配置")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;
        [SerializeField] private int minWordLength = 2;
        [SerializeField] private int maxWordLength = 4;
        [SerializeField, Range(1, 3)] private int maxBlankCount = 2;

        #endregion

        #region 私有字段

        private string dbPath;

        // 当前题目数据
        private string currentPattern;
        private int[] blankPositions;
        private char[] knownCharacters;
        private int wordLength;

        #endregion

        #region IQuestionDataProvider接口实现

        public QuestionType QuestionType => QuestionType.HardFill;

        #endregion

        #region Unity生命周期

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("HardFill题型管理器初始化完成");
        }

        #endregion

        #region 题目数据生成

        /// <summary>
        /// 获取题目数据（IQuestionDataProvider接口实现）
        /// 为Host端抽题使用，生成题干格式但不预设固定答案
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("开始生成HardFill题目数据");

            try
            {
                // 1. 确定词语长度
                wordLength = Random.Range(minWordLength, maxWordLength + 1);

                // 2. 确定空格数量（不能全是空格）
                int blankCount = Random.Range(1, Mathf.Min(maxBlankCount + 1, wordLength));

                // 3. 获取参考词语用于生成题干
                string referenceWord = GetRandomWordFromDatabase(wordLength);

                // 4. 生成题干格式
                GenerateQuestionPattern(referenceWord, blankCount);

                // 5. 构建附加数据
                var additionalData = new HardFillAdditionalData
                {
                    pattern = currentPattern,
                    blankPositions = blankPositions,
                    knownCharacters = knownCharacters,
                    wordLength = wordLength,
                    minFreq = freqMin,
                    maxFreq = freqMax
                };

                // 6. 创建题目数据
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.HardFill,
                    questionText = FormatQuestionText(currentPattern),
                    correctAnswer = "", // 硬填空不设置固定答案
                    options = new string[0],
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"HardFill题目生成成功: {currentPattern} (长度: {wordLength}, 空格数: {blankCount})");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"生成HardFill题目失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从数据库获取指定长度的随机词语
        /// </summary>
        private string GetRandomWordFromDatabase(int targetLength)
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
                                SELECT word FROM word WHERE length = @len AND Freq BETWEEN @min AND @max
                                UNION ALL
                                SELECT word FROM idiom WHERE length = @len AND Freq BETWEEN @min AND @max
                            ) ORDER BY RANDOM() LIMIT 1";

                        cmd.Parameters.AddWithValue("@len", targetLength);
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
        /// 基于参考词生成题干格式
        /// </summary>
        private void GenerateQuestionPattern(string referenceWord, int blankCount)
        {
            wordLength = referenceWord.Length;
            knownCharacters = new char[wordLength];

            // 随机选择要隐藏的位置
            var allPositions = Enumerable.Range(0, wordLength).ToList();
            blankPositions = new int[blankCount];

            for (int i = 0; i < blankCount; i++)
            {
                int randomIndex = Random.Range(0, allPositions.Count);
                blankPositions[i] = allPositions[randomIndex];
                allPositions.RemoveAt(randomIndex);
            }

            System.Array.Sort(blankPositions);

            // 构建题干模式和已知字符数组
            var patternBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < wordLength; i++)
            {
                if (blankPositions.Contains(i))
                {
                    patternBuilder.Append('_');
                    knownCharacters[i] = '\0'; // 标记为未知
                }
                else
                {
                    char ch = referenceWord[i];
                    patternBuilder.Append(ch);
                    knownCharacters[i] = ch;
                }
            }

            currentPattern = patternBuilder.ToString();
            LogDebug($"生成题干格式: {currentPattern} (基于参考词: {referenceWord})");
        }

        /// <summary>
        /// 格式化题目文本
        /// </summary>
        private string FormatQuestionText(string pattern)
        {
            return $"请填写符合格式的词语：{pattern}";
        }


        #endregion

        #region 网络题目处理

        /// <summary>
        /// 加载网络题目数据
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("加载网络HardFill题目");

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

            LogDebug($"网络HardFill题目加载完成: {currentPattern}");
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

            if (networkData.questionType != QuestionType.HardFill)
            {
                LogError($"题目类型不匹配，期望: {QuestionType.HardFill}, 实际: {networkData.questionType}");
                return false;
            }

            if (string.IsNullOrEmpty(networkData.additionalData))
            {
                LogError("缺少题目附加数据");
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
                var additionalInfo = JsonUtility.FromJson<HardFillAdditionalData>(questionData.additionalData);

                currentPattern = additionalInfo.pattern;
                blankPositions = additionalInfo.blankPositions;
                knownCharacters = additionalInfo.knownCharacters;
                wordLength = additionalInfo.wordLength;

                LogDebug($"题目数据解析成功: 格式={currentPattern}, 长度={wordLength}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"解析附加数据失败: {e.Message}");
                return false;
            }
        }

        #endregion

        #region 答案验证

        /// <summary>
        /// 静态答案验证方法 - 供Host端调用
        /// 基于题目数据验证答案，不依赖实例状态
        /// </summary>
        public static bool ValidateAnswerStatic(string answer, NetworkQuestionData questionData)
        {
            if (string.IsNullOrEmpty(answer))
            {
                Debug.Log("[HardFill] 静态验证: 答案为空");
                return false;
            }

            if (questionData?.questionType != QuestionType.HardFill)
            {
                Debug.LogError("[HardFill] 静态验证: 题目类型不匹配");
                return false;
            }

            if (string.IsNullOrEmpty(questionData.additionalData))
            {
                Debug.LogError("[HardFill] 静态验证: 缺少题目附加数据");
                return false;
            }

            try
            {
                var additionalData = JsonUtility.FromJson<HardFillAdditionalData>(questionData.additionalData);

                // 验证长度
                if (answer.Length != additionalData.wordLength)
                {
                    Debug.Log($"[HardFill] 静态验证: 长度不匹配，期望{additionalData.wordLength}，实际{answer.Length}");
                    return false;
                }

                // 验证已知位置的字符
                for (int i = 0; i < additionalData.wordLength; i++)
                {
                    if (additionalData.knownCharacters[i] != '\0' &&
                        answer[i] != additionalData.knownCharacters[i])
                    {
                        Debug.Log($"[HardFill] 静态验证: 位置{i}字符不匹配，期望'{additionalData.knownCharacters[i]}'，实际'{answer[i]}'");
                        return false;
                    }
                }

                // 验证词语是否在数据库中
                bool existsInDB = IsWordInDatabaseStatic(answer, additionalData.minFreq, additionalData.maxFreq);
                Debug.Log($"[HardFill] 静态验证: 词语'{answer}'数据库验证结果={existsInDB}");

                return existsInDB;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HardFill] 静态验证异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 静态数据库查询方法 - 供Host端调用
        /// </summary>
        public static bool IsWordInDatabaseStatic(string word, int minFreq, int maxFreq)
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
                                SELECT word FROM word WHERE Freq BETWEEN @min AND @max
                                UNION ALL
                                SELECT word FROM idiom WHERE Freq BETWEEN @min AND @max
                            ) WHERE word = @word";

                        cmd.Parameters.AddWithValue("@word", word);
                        cmd.Parameters.AddWithValue("@min", minFreq);
                        cmd.Parameters.AddWithValue("@max", maxFreq);

                        long count = (long)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HardFill] 静态数据库查询失败: {e.Message}");
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

            if (answer.Length != wordLength)
            {
                LogDebug($"长度不匹配: 期望{wordLength}, 实际{answer.Length}");
                return false;
            }

            // 检查已知位置的字符
            for (int i = 0; i < wordLength; i++)
            {
                if (knownCharacters[i] != '\0' && answer[i] != knownCharacters[i])
                {
                    LogDebug($"位置{i}字符不匹配: 期望'{knownCharacters[i]}', 实际'{answer[i]}'");
                    return false;
                }
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
                                SELECT word FROM word WHERE Freq BETWEEN @min AND @max
                                UNION ALL
                                SELECT word FROM idiom WHERE Freq BETWEEN @min AND @max
                            ) WHERE word = @word";

                        cmd.Parameters.AddWithValue("@word", word);
                        cmd.Parameters.AddWithValue("@min", freqMin);
                        cmd.Parameters.AddWithValue("@max", freqMax);

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

            // 通知答题结果，触发下一题或其他逻辑
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
            LogDebug("加载本地HardFill题目");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("无法生成本地题目");
                return;
            }

            ParseQuestionData(questionData);
            LogDebug($"本地题目加载完成: {currentPattern}");
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[HardFill] {message}");
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[HardFill] {message}");
        }

        #endregion
    }

    /// <summary>
    /// 硬填空题附加数据结构
    /// </summary>
    [System.Serializable]
    public class HardFillAdditionalData
    {
        public string pattern;           // 题干格式，如"投_"
        public int[] blankPositions;     // 空格位置数组
        public char[] knownCharacters;   // 已知字符数组（'\0'表示空格位置）
        public int wordLength;           // 词语长度
        public int minFreq;              // 频率范围
        public int maxFreq;
    }
}