using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// 硬性单词补全题管理器
    /// - 支持单机和网络模式
    /// - 实现IQuestionDataProvider接口，支持Host抽题
    /// - 单机模式：按词长加权随机抽词，随机展示部分字符
    /// - 网络模式：使用服务器提供的题目数据
    /// - 玩家输入完整词，校验长度、保留字、词库存在性
    /// - "投降"视为提交空答案，视作错误
    /// </summary>
    public class HardFillQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        private string dbPath;

        [Header("UI设置")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/HardFillUI";

        [Header("单机模式配置")]
        [Header("频率范围（Freq）")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        [Header("按词长加权抽词比例（总和=1）")]
        [SerializeField, Range(0f, 1f)] private float weight2 = 0.32f;
        [SerializeField, Range(0f, 1f)] private float weight3 = 0.32f;
        [SerializeField, Range(0f, 1f)] private float weight4 = 0.32f;
        [SerializeField, Range(0f, 1f)] private float weightOther = 0.04f;

        [Header("已知字数量（小于词长）")]
        [SerializeField] private int revealCount = 2;

        [Header("UI组件引用")]
        private TMP_Text questionText;
        private TMP_InputField answerInput;
        private Button submitButton;
        private Button surrenderButton;
        private TMP_Text feedbackText;

        private string currentWord;
        private int[] revealIndices;
        private bool hasAnswered = false;

        // IQuestionDataProvider接口实现
        public QuestionType QuestionType => QuestionType.HardFill;

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
                Debug.Log("[HardFill] Host抽题模式，跳过UI初始化");
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
            Debug.Log("[HardFill] Host请求抽题数据");

            // 1. 根据权重确定目标词长
            int targetLength = GetWeightedWordLength();

            // 2. 随机抽词
            string word = GetRandomWord(targetLength);

            if (string.IsNullOrEmpty(word))
            {
                Debug.LogWarning("[HardFill] Host抽题：暂无符合条件的词条");
                return null;
            }

            // 3. 生成显示模式
            int[] revealPattern = GenerateRevealPattern(word);

            // 4. 构造显示文本
            string displayText = CreateDisplayText(word, revealPattern);

            // 5. 创建附加数据
            var additionalData = new HardFillAdditionalData
            {
                revealIndices = revealPattern,
                revealCount = revealPattern.Length,
                displayPattern = displayText
            };

            // 6. 创建网络题目数据
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.HardFill,
                questionText = $"题目：{displayText}",
                correctAnswer = word,
                options = new string[0], // 填空题不需要选项
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(additionalData)
            };

            Debug.Log($"[HardFill] Host抽题成功: {displayText} (答案: {word})");
            return questionData;
        }

        /// <summary>
        /// 为指定单词生成显示模式
        /// </summary>
        private int[] GenerateRevealPattern(string word)
        {
            if (string.IsNullOrEmpty(word))
                return new int[0];

            int wordLength = word.Length;
            int revealNum = Mathf.Clamp(revealCount, 1, wordLength - 1);

            // 随机选择要显示的位置
            var allIndices = Enumerable.Range(0, wordLength).ToArray();

            // Fisher-Yates 洗牌算法
            for (int i = allIndices.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = allIndices[i];
                allIndices[i] = allIndices[j];
                allIndices[j] = temp;
            }

            return allIndices.Take(revealNum).OrderBy(x => x).ToArray();
        }

        /// <summary>
        /// 创建显示文本
        /// </summary>
        private string CreateDisplayText(string word, int[] revealPattern)
        {
            if (string.IsNullOrEmpty(word) || revealPattern == null)
                return "";

            var displayText = new System.Text.StringBuilder();
            for (int i = 0; i < word.Length; i++)
            {
                if (revealPattern.Contains(i))
                    displayText.Append(word[i]);
                else
                    displayText.Append('_');
            }

            return displayText.ToString();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[HardFill] UIManager实例不存在");
                return;
            }

            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"[HardFill] 无法加载UI预制体: {uiPrefabPath}");
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
                Debug.LogError("[HardFill] UI组件获取失败，检查预制体结构");
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

            Debug.Log("[HardFill] UI初始化完成");
        }

        /// <summary>
        /// 加载本地题目（单机模式）
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[HardFill] 加载本地题目");

            // 使用GetQuestionData()方法复用抽题逻辑
            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("没有符合条件的词条。");
                return;
            }

            // 解析题目数据
            currentWord = questionData.correctAnswer;

            // 从附加数据中解析显示模式
            if (!string.IsNullOrEmpty(questionData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<HardFillAdditionalData>(questionData.additionalData);
                    revealIndices = additionalInfo.revealIndices;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"解析附加数据失败: {e.Message}，重新生成显示模式");
                    revealIndices = GenerateRevealPattern(currentWord);
                }
            }
            else
            {
                revealIndices = GenerateRevealPattern(currentWord);
            }

            // 显示题目
            DisplayQuestion();
        }

        /// <summary>
        /// 加载网络题目（网络模式）
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[HardFill] 加载网络题目");

            if (networkData == null)
            {
                Debug.LogError("[HardFill] 网络题目数据为空");
                DisplayErrorMessage("网络题目数据错误");
                return;
            }

            if (networkData.questionType != QuestionType.HardFill)
            {
                Debug.LogError($"[HardFill] 题目类型不匹配: 期望{QuestionType.HardFill}, 实际{networkData.questionType}");
                DisplayErrorMessage("题目类型错误");
                return;
            }

            // 从网络数据中解析题目信息
            ParseNetworkQuestionData(networkData);

            // 显示题目
            DisplayQuestion();
        }

        /// <summary>
        /// 解析网络题目数据
        /// </summary>
        private void ParseNetworkQuestionData(NetworkQuestionData networkData)
        {
            currentWord = networkData.correctAnswer;

            // 从additionalData中解析显示模式（JSON格式）
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<HardFillAdditionalData>(networkData.additionalData);
                    revealIndices = additionalInfo.revealIndices;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"解析网络附加数据失败: {e.Message}，使用默认显示模式");
                    revealIndices = GenerateRevealPattern(currentWord);
                }
            }
            else
            {
                // 如果没有附加数据，使用默认模式
                revealIndices = GenerateRevealPattern(currentWord);
            }
        }

        /// <summary>
        /// 根据权重确定词长
        /// </summary>
        private int GetWeightedWordLength()
        {
            float r = Random.value;
            if (r < weight2) return 2;
            else if (r < weight2 + weight3) return 3;
            else if (r < weight2 + weight3 + weight4) return 4;
            else return -1; // 其他长度
        }

        /// <summary>
        /// 从数据库随机获取单词
        /// </summary>
        private string GetRandomWord(int targetLength)
        {
            string word = null;

            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        if (targetLength > 0)
                        {
                            cmd.CommandText = @"
                                SELECT word FROM (
                                    SELECT word, Freq, length FROM word
                                    UNION ALL
                                    SELECT word, Freq, length FROM idiom
                                ) WHERE length = @len AND Freq BETWEEN @min AND @max 
                                ORDER BY RANDOM() LIMIT 1";
                            cmd.Parameters.AddWithValue("@len", targetLength);
                        }
                        else
                        {
                            cmd.CommandText = @"
                                SELECT word FROM (
                                    SELECT word, Freq, length FROM word
                                    UNION ALL
                                    SELECT word, Freq, length FROM idiom
                                ) WHERE length NOT IN (2,3,4) AND Freq BETWEEN @min AND @max 
                                ORDER BY RANDOM() LIMIT 1";
                        }
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
        /// 生成字符显示模式（保持原有逻辑用于单机模式）
        /// </summary>
        private void GenerateRevealPattern()
        {
            revealIndices = GenerateRevealPattern(currentWord);
        }

        /// <summary>
        /// 显示题目
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(currentWord) || revealIndices == null)
            {
                DisplayErrorMessage("题目数据错误");
                return;
            }

            // 构造显示文本
            string displayText = CreateDisplayText(currentWord, revealIndices);

            questionText.text = $"题目：{displayText}";

            // 重置输入和反馈
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.interactable = true;
            submitButton.interactable = true;
            surrenderButton.interactable = true;

            // 激活输入框
            answerInput.ActivateInputField();

            Debug.Log($"[HardFill] 题目显示完成: {displayText} (答案: {currentWord})");
        }

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            Debug.LogWarning($"[HardFill] {message}");

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
            Debug.Log($"[HardFill] 检查本地答案: {answer}");

            bool isCorrect = ValidateAnswer(answer.Trim());
            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// 验证答案
        /// </summary>
        private bool ValidateAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(currentWord))
                return false;

            // 1. 检查长度
            if (answer.Length != currentWord.Length)
                return false;

            // 2. 检查已显示的字符是否正确
            foreach (int idx in revealIndices)
            {
                if (answer[idx] != currentWord[idx])
                    return false;
            }

            // 3. 检查词是否存在于词库中
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

            Debug.Log("[HardFill] 玩家投降");
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

            Debug.Log($"[HardFill] 提交答案: '{answer}'");

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
            Debug.Log($"[HardFill] 网络模式提交答案: {answer}");

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
                Debug.LogError("[HardFill] NetworkManager实例不存在，无法提交答案");
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
                    feedbackText.text = $"回答错误，正确示例：{currentWord}";
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
            Debug.Log($"[HardFill] 收到网络结果: {(isCorrect ? "正确" : "错误")}");

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
    /// 硬填空题附加数据结构（用于网络传输）
    /// </summary>
    [System.Serializable]
    public class HardFillAdditionalData
    {
        public int[] revealIndices;
        public int revealCount;
        public string displayPattern;
    }
}