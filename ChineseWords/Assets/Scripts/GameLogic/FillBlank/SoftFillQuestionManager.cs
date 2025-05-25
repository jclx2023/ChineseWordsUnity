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
    /// - 单机模式：随机抽词，生成通配符模式 (*任意长度，_单个字符)
    /// - 网络模式：使用服务器提供的题目数据和模式
    /// - 玩家输入答案，使用正则表达式匹配模式，再验证词库存在性
    /// </summary>
    public class SoftFillQuestionManager : NetworkQuestionManagerBase
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
            answerInput = ui.Find("AnswerInput")?.GetComponent<TMP_InputField>();
            submitButton = ui.Find("SubmitButton")?.GetComponent<Button>();
            surrenderButton = ui.Find("SurrenderButton")?.GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || answerInput == null || submitButton == null ||
                surrenderButton == null || feedbackText == null)
            {
                Debug.LogError("UI组件获取失败，检查预制体结构");
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
        }

        /// <summary>
        /// 加载本地题目（单机模式）
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[SoftFill] 加载本地题目");

            // 1. 随机抽词
            currentWord = GetRandomWord();

            if (string.IsNullOrEmpty(currentWord))
            {
                DisplayErrorMessage("没有符合条件的词条。");
                return;
            }

            // 2. 生成通配符模式
            GenerateWildcardPattern();

            // 3. 创建正则表达式
            CreateMatchRegex();

            // 4. 显示题目
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
                Debug.LogError("网络题目数据为空");
                DisplayErrorMessage("网络题目数据错误");
                return;
            }

            if (networkData.questionType != QuestionType.SoftFill)
            {
                Debug.LogError($"题目类型不匹配: 期望{QuestionType.SoftFill}, 实际{networkData.questionType}");
                LoadLocalQuestion(); // 降级到本地题目
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
                GenerateWildcardPattern();
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
        /// 生成通配符模式
        /// </summary>
        private void GenerateWildcardPattern()
        {
            if (string.IsNullOrEmpty(currentWord))
                return;

            int wordLength = currentWord.Length;
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
            sb.Append(currentWord[selectedIndices[0]]);

            for (int k = 1; k < selectedIndices.Length; k++)
            {
                int gap = selectedIndices[k] - selectedIndices[k - 1] - 1;
                sb.Append(new string('_', gap));
                sb.Append(currentWord[selectedIndices[k]]);
            }
            sb.Append("*");

            stemPattern = sb.ToString();
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

            // 通过基类提交到服务器
            CheckAnswer(answer);
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