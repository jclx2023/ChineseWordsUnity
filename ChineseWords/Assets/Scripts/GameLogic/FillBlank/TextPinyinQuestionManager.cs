using UnityEngine;
using System.Collections;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// 文字拼音题管理器 - UI架构重构版
    /// 
    /// 功能说明：
    /// - 随机抽词，随机定位字符，解析拼音数据
    /// - 解析character.Tpinyin JSON数据进行无调比对
    /// - 反馈展示带调拼音，提升用户体验
    /// - Host端生成题目数据和拼音验证信息
    /// 
    /// 重构内容：
    /// - 移除所有UI相关代码，UI功能已转移至AnswerUIManager
    /// - 保留数据生成、网络通信和答案验证核心功能
    /// - 优化代码结构，统一题型管理器代码风格
    /// - 简化验证逻辑，不添加备用题目机制
    /// </summary>
    public class TextPinyinQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        #region 配置字段

        [Header("题目生成配置")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;

        #endregion

        #region 私有字段

        private string dbPath;

        // 当前题目数据
        private string currentWord;
        private string targetCharacter;
        private int characterIndex;
        private string correctPinyinNoTone;     // 无调拼音（用于比对）
        private string correctPinyinTone;       // 带调拼音（用于反馈）

        #endregion

        #region IQuestionDataProvider接口实现

        public QuestionType QuestionType => QuestionType.TextPinyin;

        #endregion

        #region Unity生命周期

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("TextPinyin题型管理器初始化完成");
        }

        #endregion

        #region 题目数据生成

        /// <summary>
        /// 获取题目数据（IQuestionDataProvider接口实现）
        /// 为Host端抽题使用，生成拼音题目和验证数据
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("开始生成TextPinyin题目数据");

            try
            {
                // 1. 随机选择词语
                string word = GetRandomWordFromDatabase();
                if (string.IsNullOrEmpty(word))
                {
                    LogError("无法找到符合频率范围的词语");
                    return null;
                }

                // 2. 随机定位目标字符
                int charIndex = Random.Range(0, word.Length);
                string targetChar = word.Substring(charIndex, 1);

                // 3. 获取拼音数据
                string pinyinNoTone, pinyinTone;
                if (!GetPinyinDataForGeneration(targetChar, word, charIndex, out pinyinNoTone, out pinyinTone))
                {
                    LogError($"无法获取字符'{targetChar}'的拼音数据");
                    return null;
                }

                // 4. 构建附加数据
                var additionalData = new TextPinyinAdditionalData
                {
                    word = word,
                    targetCharacter = targetChar,
                    characterIndex = charIndex,
                    correctPinyinTone = pinyinTone,
                    correctPinyinNoTone = pinyinNoTone
                };

                // 5. 创建题目数据
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.TextPinyin,
                    questionText = FormatQuestionText(word, targetChar),
                    correctAnswer = pinyinNoTone, // 无调拼音用于验证
                    options = new string[0],
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(additionalData)
                };

                LogDebug($"TextPinyin题目生成成功: {word} -> {targetChar} ({pinyinNoTone})");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"生成TextPinyin题目失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从数据库随机获取词语
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
                            SELECT word FROM word
                            WHERE Freq BETWEEN @min AND @max
                            ORDER BY RANDOM()
                            LIMIT 1";

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
        /// 获取拼音数据用于题目生成
        /// </summary>
        private bool GetPinyinDataForGeneration(string character, string word, int charIndex, out string pinyinNoTone, out string pinyinTone)
        {
            pinyinNoTone = "";
            pinyinTone = "";

            try
            {
                // 1. 获取字符的无调拼音
                string rawTpinyin = null;
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT Tpinyin FROM character
                            WHERE char=@ch LIMIT 1";
                        cmd.Parameters.AddWithValue("@ch", character);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                rawTpinyin = reader.GetString(0);
                        }
                    }
                }

                // 解析JSON格式的拼音数据
                pinyinNoTone = ParseTpinyinJson(rawTpinyin);
                if (string.IsNullOrEmpty(pinyinNoTone))
                {
                    LogError($"无法解析字符'{character}'的无调拼音");
                    return false;
                }

                // 2. 获取词语的带调拼音
                string fullTonePinyin = null;
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT pinyin FROM word
                            WHERE word=@w LIMIT 1";
                        cmd.Parameters.AddWithValue("@w", word);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                fullTonePinyin = reader.GetString(0);
                        }
                    }
                }

                // 提取目标字符的带调拼音
                pinyinTone = ExtractTonePinyinAtIndex(fullTonePinyin, charIndex, pinyinNoTone);

                LogDebug($"拼音数据获取成功: {character} -> 无调:{pinyinNoTone}, 带调:{pinyinTone}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"获取拼音数据失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 解析Tpinyin JSON数据
        /// </summary>
        private string ParseTpinyinJson(string rawTpinyin)
        {
            if (string.IsNullOrEmpty(rawTpinyin))
                return "";

            try
            {
                string parsed = rawTpinyin;
                if (rawTpinyin.StartsWith("["))
                {
                    var inner = rawTpinyin.Substring(1, rawTpinyin.Length - 2);
                    var parts = inner.Split(',');
                    parsed = parts[0].Trim().Trim('"');
                }
                return (parsed ?? "").ToLower();
            }
            catch (System.Exception e)
            {
                LogError($"解析Tpinyin JSON失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 提取指定位置的带调拼音
        /// </summary>
        private string ExtractTonePinyinAtIndex(string fullTonePinyin, int charIndex, string fallbackPinyin)
        {
            try
            {
                var toneParts = fullTonePinyin?.Trim().Split(' ');
                if (toneParts == null || charIndex < 0 || charIndex >= toneParts.Length)
                {
                    LogDebug($"带调拼音提取失败，使用无调拼音作为备用");
                    return fallbackPinyin;
                }
                return toneParts[charIndex];
            }
            catch (System.Exception e)
            {
                LogError($"提取带调拼音失败: {e.Message}");
                return fallbackPinyin;
            }
        }

        /// <summary>
        /// 格式化题目文本
        /// </summary>
        private string FormatQuestionText(string word, string targetChar)
        {
            return $"题目：{word}\n\"<color=red>{targetChar}</color>\" 的读音是？";
        }

        #endregion

        #region 网络题目处理

        /// <summary>
        /// 加载网络题目数据
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("加载网络TextPinyin题目");

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

            LogDebug($"网络TextPinyin题目加载完成: {currentWord} -> {targetCharacter}");
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

            if (networkData.questionType != QuestionType.TextPinyin)
            {
                LogError($"题目类型不匹配，期望: {QuestionType.TextPinyin}, 实际: {networkData.questionType}");
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
                correctPinyinNoTone = questionData.correctAnswer.ToLower();

                // 从附加数据中解析详细信息
                if (!string.IsNullOrEmpty(questionData.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<TextPinyinAdditionalData>(questionData.additionalData);
                    currentWord = additionalInfo.word;
                    targetCharacter = additionalInfo.targetCharacter;
                    characterIndex = additionalInfo.characterIndex;
                    correctPinyinTone = additionalInfo.correctPinyinTone;
                    correctPinyinNoTone = additionalInfo.correctPinyinNoTone;
                }
                else
                {
                    // 从题目文本中提取信息（备用方案）
                    ExtractInfoFromQuestionText(questionData.questionText);
                }

                LogDebug($"题目数据解析成功: 词语={currentWord}, 目标字符={targetCharacter}, 正确拼音={correctPinyinNoTone}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"解析题目数据失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从题目文本中提取信息（备用方案）
        /// </summary>
        private void ExtractInfoFromQuestionText(string questionText)
        {
            try
            {
                var lines = questionText.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("题目："))
                    {
                        var wordStart = line.IndexOf("题目：") + 3;
                        var wordEnd = line.IndexOf("\n");
                        if (wordEnd == -1) wordEnd = line.Length;
                        currentWord = line.Substring(wordStart, wordEnd - wordStart).Trim();
                        break;
                    }
                }

                // 从HTML标记中提取目标字符
                var colorTagStart = questionText.IndexOf("<color=red>");
                var colorTagEnd = questionText.IndexOf("</color>");
                if (colorTagStart != -1 && colorTagEnd != -1)
                {
                    targetCharacter = questionText.Substring(colorTagStart + 11, colorTagEnd - colorTagStart - 11);
                }

                // 如果没有带调拼音，设置为答案本身
                if (string.IsNullOrEmpty(correctPinyinTone))
                    correctPinyinTone = correctPinyinNoTone;

                LogDebug("从题目文本提取信息完成");
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
            Debug.Log($"[TextPinyin] 静态验证答案: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[TextPinyin] 静态验证: 答案为空或题目数据无效");
                return false;
            }

            if (question.questionType != QuestionType.TextPinyin)
            {
                Debug.LogError($"[TextPinyin] 静态验证: 题目类型不匹配，期望TextPinyin，实际{question.questionType}");
                return false;
            }

            try
            {
                // 获取正确的无调拼音
                string correctPinyin = question.correctAnswer?.ToLower();
                if (string.IsNullOrEmpty(correctPinyin))
                {
                    Debug.Log("[TextPinyin] 静态验证: 题目数据中缺少正确答案");
                    return false;
                }

                // 处理用户答案：去除引号、转小写
                string processedAnswer = ProcessPinyinAnswer(answer.Trim());

                bool isCorrect = processedAnswer == correctPinyin;
                Debug.Log($"[TextPinyin] 静态验证结果: 处理后答案='{processedAnswer}', 正确答案='{correctPinyin}', 结果={isCorrect}");

                return isCorrect;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TextPinyin] 静态验证异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 处理拼音答案格式
        /// </summary>
        private static string ProcessPinyinAnswer(string answer)
        {
            return answer.Replace("\"", "")
                         .Replace("\u201c", "") // 左双引号 "
                         .Replace("\u201d", "") // 右双引号 "
                         .Trim()
                         .ToLower();
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
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(correctPinyinNoTone))
            {
                LogDebug("答案或正确拼音为空");
                return false;
            }

            string processedAnswer = ProcessPinyinAnswer(answer);
            bool isCorrect = processedAnswer == correctPinyinNoTone;

            LogDebug($"本地验证结果: 处理后答案='{processedAnswer}', 正确答案='{correctPinyinNoTone}', 结果={isCorrect}");
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

            // 更新正确答案（服务器可能提供带调拼音）
            if (!string.IsNullOrEmpty(correctAnswer))
                correctPinyinTone = correctAnswer;

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
            LogDebug("加载本地TextPinyin题目");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("无法生成本地题目");
                return;
            }

            ParseQuestionData(questionData);
            LogDebug($"本地题目加载完成: {currentWord} -> {targetCharacter}");
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[TextPinyin] {message}");
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[TextPinyin] {message}");
        }

        #endregion
    }

    /// <summary>
    /// 文字拼音题附加数据结构
    /// </summary>
    [System.Serializable]
    public class TextPinyinAdditionalData
    {
        public string word;                 // 完整词语
        public string targetCharacter;      // 目标字符
        public int characterIndex;          // 字符位置
        public string correctPinyinTone;    // 带调拼音（用于反馈）
        public string correctPinyinNoTone;  // 无调拼音（用于比对）
    }
}