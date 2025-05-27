using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
    /// 软性单词补全题管理器
    /// - 支持单机和网络模式
    /// - 实现IQuestionDataProvider接口，支持Host抽题
    /// - 单机模式：随机抽词，生成通配符模式 (*任意长度，_单个字符)
    /// - 网络模式：使用服务器提供的题目数据和模式
    /// - 玩家输入答案，使用正则表达式匹配模式，再验证词库存在性
    /// </summary>
    public class SoftFillQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        private string dbPath;

        [Header("UI设置")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/HardFillUI";

        [Header("单机模式配置")]
        [Header("Freq范围")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;

        [Header("已知字数量 (0 < x < L)")]
        [SerializeField] private int revealCount = 2;

        [Header("UI组件引用")]
        private TMP_Text questionText;
        private TMP_InputField answerInput;
        private Button submitButton;
        private Button surrenderButton;
        private TMP_Text feedbackText;

        private string currentWord;
        private string stemPattern;
        private Regex matchRegex;
        private bool hasAnswered = false;

        // IQuestionDataProvider接口实现
        public QuestionType QuestionType => QuestionType.SoftFill;

        protected override void Awake()
        {
            base.Awake(); // 调用网络基类初始化
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            // 检查是否需要UI（Host抽题模式可能不需要UI）
            if (NeedsUI())
            {
                InitializeUI();
            }
            else
            {
                Debug.Log("[SoftFill] Host抽题模式，跳过UI初始化");
            }
        }

        /// <summary>
        /// 检查是否需要UI
        /// </summary>
        private bool NeedsUI()
        {
            // 如果是HostGameManager的子对象，说明是用于抽题的临时管理器，不需要UI
            // 或者如果是QuestionDataService的子对象，也不需要UI
            return transform.parent == null ||
                   (transform.parent.GetComponent<HostGameManager>() == null &&
                    transform.parent.GetComponent<QuestionDataService>() == null);
        }

        /// <summary>
        /// 获取题目数据（IQuestionDataProvider接口实现）
        /// 专门为Host抽题使用，不显示UI
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            Debug.Log("[SoftFill] Host请求抽题数据");

            // 1. 随机抽词
            string word = GetRandomWord();

            if (string.IsNullOrEmpty(word))
            {
                Debug.LogWarning("[SoftFill] Host抽题：暂无符合条件的词条");
                return null;
            }

            // 2. 生成通配符模式
            string pattern = GenerateWildcardPattern(word);

            if (string.IsNullOrEmpty(pattern))
            {
                Debug.LogWarning("[SoftFill] Host抽题：通配符模式生成失败");
                return null;
            }

            // 3. 创建正则表达式模式（用于验证）
            string regexPattern = CreateRegexPattern(pattern);

            // 4. 创建附加数据
            var additionalData = new SoftFillAdditionalData
            {
                stemPattern = pattern,
                regexPattern = regexPattern,
                revealIndices = ExtractRevealIndices(word, pattern)
            };

            // 5. 创建网络题目数据
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.SoftFill,
                questionText = $"题目：请输入一个符合<color=red>{pattern}</color>格式的单词\nHint：*为任意个字，_为单个字",
                correctAnswer = word,
                options = new string[0], // 填空题不需要选项
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(additionalData)
            };

            Debug.Log($"[SoftFill] Host抽题成功: {pattern} (示例答案: {word})");
            return questionData;
        }

        /// <summary>
        /// 为指定单词生成通配符模式
        /// </summary>
        private string GenerateWildcardPattern(string word)
        {
            if (string.IsNullOrEmpty(word))
                return "";

            int wordLength = word.Length;
            int x = Mathf.Clamp(revealCount, 1, wordLength - 1);

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

            var selectedIndices = indices.Take(x).OrderBy(i => i).ToArray();

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
                // 构建正则表达式模式
                // * -> .*, _ -> .
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
                Debug.LogError($"创建正则表达式失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 从通配符模式中提取显示字符的位置
        /// </summary>
        private int[] ExtractRevealIndices(string word, string pattern)
        {
            var indices = new List<int>();

            // 这是一个简化的提取逻辑，实际可能需要更复杂的解析
            // 对于软填空题，主要是为了传输完整信息
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
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[SoftFill] UIManager实例不存在");
                return;
            }

            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"[SoftFill] 无法加载UI预制体: {uiPrefabPath}");
                return;
            }

            // 获取UI组件
            questionText = ui.Find("QuestionText")?.GetComponent<TMP_Text>();
            answerInput = ui.Find("AnswerInput")?.GetComponent<TMP_InputField>();
            submitButton = ui.Find("SubmitButton")?.GetComponent<Button>();
            surrenderButton = ui.Find("SurrenderButton")?.GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || answerInput == null || submitButton == null ||
                surrenderButton == null || feedbackText == null)
            {
                Debug.LogError("[SoftFill] UI组件获取失败，检查预制体结构");
                return;
            }

            // 绑定按钮事件
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);

            surrenderButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.AddListener(OnSurrender);

            // 绑定输入框回车事件
            answerInput.onSubmit.RemoveAllListeners();
            answerInput.onSubmit.AddListener(OnInputSubmit);

            feedbackText.text = string.Empty;

            Debug.Log("[SoftFill] UI初始化完成");
        }

        /// <summary>
        /// 加载本地题目（单机模式）
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[SoftFill] 加载本地题目");

            // 使用GetQuestionData()方法复用抽题逻辑
            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("没有符合条件的词条。");
                return;
            }

            // 解析题目数据
            currentWord = questionData.correctAnswer;

            // 从附加数据中解析通配符模式
            if (!string.IsNullOrEmpty(questionData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<SoftFillAdditionalData>(questionData.additionalData);
                    stemPattern = additionalInfo.stemPattern;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"解析附加数据失败: {e.Message}，重新生成模式");
                    stemPattern = GenerateWildcardPattern(currentWord);
                }
            }
            else
            {
                stemPattern = GenerateWildcardPattern(currentWord);
            }

            // 创建正则表达式
            CreateMatchRegex();

            // 显示题目
            DisplayQuestion();
        }

        /// <summary>
        /// 加载网络题目（网络模式）
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[SoftFill] 加载网络题目");

            if (networkData == null)
            {
                Debug.LogError("[SoftFill] 网络题目数据为空");
                DisplayErrorMessage("网络题目数据错误");
                return;
            }

            if (networkData.questionType != QuestionType.SoftFill)
            {
                Debug.LogError($"[SoftFill] 题目类型不匹配: 期望{QuestionType.SoftFill}, 实际{networkData.questionType}");
                DisplayErrorMessage("题目类型错误");
                return;
            }

            // 从网络数据解析题目信息
            ParseNetworkQuestionData(networkData);

            // 创建正则表达式
            CreateMatchRegex();

            // 显示题目
            DisplayQuestion();
        }

        /// <summary>
        /// 解析网络题目数据
        /// </summary>
        private void ParseNetworkQuestionData(NetworkQuestionData networkData)
        {
            currentWord = networkData.correctAnswer;

            // 从additionalData中解析通配符模式
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<SoftFillAdditionalData>(networkData.additionalData);
                    stemPattern = additionalInfo.stemPattern;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"解析网络附加数据失败: {e.Message}，从题目文本解析");
                    ExtractPatternFromQuestionText(networkData.questionText);
                }
            }
            else
            {
                // 如果没有附加数据，从questionText中解析
                ExtractPatternFromQuestionText(networkData.questionText);
            }
        }

        /// <summary>
        /// 从题目文本中提取通配符模式
        /// </summary>
        private void ExtractPatternFromQuestionText(string questionText)
        {
            // 尝试从题目文本中提取模式（简单的文本解析）
            var match = Regex.Match(questionText, @"([*_\u4e00-\u9fa5]+)");
            if (match.Success)
            {
                stemPattern = match.Groups[1].Value;
            }
            else
            {
                Debug.LogWarning("无法从题目文本解析模式，使用默认模式");
                stemPattern = GenerateWildcardPattern(currentWord);
            }
        }

        /// <summary>
        /// 从数据库随机获取单词
        /// </summary>
        private string GetRandomWord()
        {
            string word = null;

            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT word FROM (
                                SELECT word, Freq FROM word
                                UNION ALL
                                SELECT word, Freq FROM idiom
                            ) WHERE Freq BETWEEN @min AND @max
                            ORDER BY RANDOM() LIMIT 1";
                        cmd.Parameters.AddWithValue("@min", freqMin);
                        cmd.Parameters.AddWithValue("@max", freqMax);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                word = reader.GetString(0);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"数据库查询失败: {e.Message}");
            }

            return word;
        }

        /// <summary>
        /// 生成通配符模式（保持原有逻辑用于单机模式）
        /// </summary>
        private void GenerateWildcardPattern()
        {
            stemPattern = GenerateWildcardPattern(currentWord);
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
                // 构建正则表达式模式
                // * -> .*, _ -> .
                var pattern = "^" + string.Concat(stemPattern.Select(c =>
                {
                    if (c == '*') return ".*";
                    if (c == '_') return ".";
                    return Regex.Escape(c.ToString());
                })) + "$";

                matchRegex = new Regex(pattern);
                Debug.Log($"[SoftFill] 创建正则表达式: {pattern}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"创建正则表达式失败: {e.Message}");
                matchRegex = null;
            }
        }

        /// <summary>
        /// 显示题目
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(stemPattern))
            {
                DisplayErrorMessage("题目模式生成失败");
                return;
            }

            questionText.text = $"题目：请输入一个符合<color=red>{stemPattern}</color>格式的单词\nHint：*为任意个字，_为单个字";

            // 重置输入和反馈
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.interactable = true;
            submitButton.interactable = true;
            surrenderButton.interactable = true;

            // 激活输入框
            answerInput.ActivateInputField();

            Debug.Log($"[SoftFill] 题目显示完成，模式: {stemPattern} (示例答案: {currentWord})");
        }

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            Debug.LogWarning($"[SoftFill] {message}");

            if (questionText != null)
                questionText.text = message;

            if (feedbackText != null)
                feedbackText.text = "";

            if (answerInput != null)
            {
                answerInput.text = "";
                answerInput.interactable = false;
            }

            if (submitButton != null)
                submitButton.interactable = false;

            if (surrenderButton != null)
                surrenderButton.interactable = false;
        }

        /// <summary>
        /// 检查本地答案（单机模式）
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            Debug.Log($"[SoftFill] 检查本地答案: {answer}");

            bool isCorrect = ValidateAnswer(answer.Trim());
            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// 验证答案
        /// </summary>
        private bool ValidateAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return false;

            // 1. 检查是否匹配通配符模式
            if (matchRegex == null || !matchRegex.IsMatch(answer))
                return false;

            // 2. 检查词是否存在于词库中
            return IsWordInDatabase(answer);
        }

        /// <summary>
        /// 检查词是否在数据库中
        /// </summary>
        private bool IsWordInDatabase(string word)
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
                                SELECT word FROM word
                                UNION ALL
                                SELECT word FROM idiom
                            ) WHERE word = @word";
                        cmd.Parameters.AddWithValue("@word", word);

                        long count = (long)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"数据库查询失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 提交按钮点击
        /// </summary>
        private void OnSubmit()
        {
            if (hasAnswered)
                return;

            string userAnswer = answerInput.text.Trim();
            if (string.IsNullOrEmpty(userAnswer))
                return;

            SubmitAnswer(userAnswer);
        }

        /// <summary>
        /// 输入框回车提交
        /// </summary>
        private void OnInputSubmit(string value)
        {
            if (!hasAnswered && !string.IsNullOrEmpty(value.Trim()))
            {
                SubmitAnswer(value.Trim());
            }
        }

        /// <summary>
        /// 投降按钮点击
        /// </summary>
        private void OnSurrender()
        {
            if (hasAnswered)
                return;

            Debug.Log("[SoftFill] 玩家投降");
            SubmitAnswer(""); // 提交空答案表示投降
        }

        /// <summary>
        /// 提交答案
        /// </summary>
        private void SubmitAnswer(string answer)
        {
            if (hasAnswered)
                return;

            hasAnswered = true;
            StopAllCoroutines();

            // 禁用交互
            answerInput.interactable = false;
            submitButton.interactable = false;
            surrenderButton.interactable = false;

            Debug.Log($"[SoftFill] 提交答案: '{answer}'");

            if (IsNetworkMode())
            {
                HandleNetworkAnswer(answer);
            }
            else
            {
                HandleLocalAnswer(answer);
            }
        }

        /// <summary>
        /// 处理网络模式答案
        /// </summary>
        private void HandleNetworkAnswer(string answer)
        {
            Debug.Log($"[SoftFill] 网络模式提交答案: {answer}");

            // 显示提交状态
            feedbackText.text = "已提交答案，等待服务器结果...";
            feedbackText.color = Color.yellow;

            // 提交答案到服务器
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
            }
            else
            {
                Debug.LogError("[SoftFill] NetworkManager实例不存在，无法提交答案");
            }
        }

        /// <summary>
        /// 处理单机模式答案
        /// </summary>
        private void HandleLocalAnswer(string answer)
        {
            CheckLocalAnswer(answer);
        }

        /// <summary>
        /// 显示反馈信息并通知结果
        /// </summary>
        private IEnumerator ShowFeedbackAndNotify(bool isCorrect)
        {
            // 显示反馈
            if (feedbackText != null)
            {
                if (isCorrect)
                {
                    feedbackText.text = "回答正确！";
                    feedbackText.color = Color.green;
                }
                else
                {
                    feedbackText.text = $"回答错误，可接受示例：{currentWord}";
                    feedbackText.color = Color.red;
                }
            }

            // 等待一段时间
            yield return new WaitForSeconds(1.5f);

            // 通知答题结果
            OnAnswerResult?.Invoke(isCorrect);
        }

        /// <summary>
        /// 显示网络答题结果（由网络系统调用）
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[SoftFill] 收到网络结果: {(isCorrect ? "正确" : "错误")}");

            currentWord = correctAnswer;
            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            // 清理事件监听
            if (submitButton != null)
                submitButton.onClick.RemoveAllListeners();
            if (surrenderButton != null)
                surrenderButton.onClick.RemoveAllListeners();
            if (answerInput != null)
                answerInput.onSubmit.RemoveAllListeners();
        }
    }

    /// <summary>
    /// 软填空题附加数据结构（用于网络传输）
    /// </summary>
    [System.Serializable]
    public class SoftFillAdditionalData
    {
        public string stemPattern;      // 通配符模式 (如 "*中_国*")
        public int[] revealIndices;     // 显示的字符位置
        public string regexPattern;     // 正则表达式模式
    }
}