using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;
using Core.Network;
using Managers;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// 成语接龙题管理器
    /// - 支持单机和网络模式
    /// - 单机模式：从预加载的首题候选中随机选择，玩家接龙
    /// - 网络模式：使用服务器提供的题目数据
    /// - 高亮显示需要接龙的字符，玩家输入下一个成语
    /// - 验证开头字符和成语存在性
    /// </summary>
    public class IdiomChainQuestionManager : NetworkQuestionManagerBase
    {
        private string dbPath;

        [Header("UI设置")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/HardFillUI";

        [Header("单机模式配置")]
        [Header("成语频率区间")]
        [SerializeField] private int minFreq = 0;
        [SerializeField] private int maxFreq = 9;

        [Header("UI组件引用")]
        private TMP_Text questionText;
        private TMP_InputField answerInput;
        private Button submitButton;
        private Button surrenderButton;
        private TMP_Text feedbackText;
        private TimerManager timerManager;

        // 缓存首题候选（单机模式）
        private List<string> firstCandidates = new List<string>();

        // 当前状态
        private string currentIdiom;
        private bool hasAnswered = false;
        private bool isGameInProgress = false;

        protected override void Awake()
        {
            base.Awake(); // 调用网络基类初始化
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            // 预加载首题候选（单机模式用）
            LoadFirstCandidates();
        }

        private void Start()
        {
            InitializeUI();
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
            }
            catch (System.Exception e)
            {
                Debug.LogError($"加载首题候选失败: {e.Message}");
            }

            if (firstCandidates.Count == 0)
            {
                Debug.LogError("首题候选列表为空，成语接龙可能无法正常工作");
            }
            else
            {
                Debug.Log($"已加载 {firstCandidates.Count} 个首题候选成语");
            }
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

            // 获取计时器管理器
            timerManager = GetComponent<TimerManager>();

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
            Debug.Log("[IdiomChain] 加载本地题目");

            if (firstCandidates.Count == 0)
            {
                DisplayErrorMessage("没有可用的成语题目");
                return;
            }

            // 随机选择一个首题成语
            int index = Random.Range(0, firstCandidates.Count);
            currentIdiom = firstCandidates[index];
            firstCandidates.RemoveAt(index); // 移除已使用的成语避免重复

            isGameInProgress = true;
            ShowQuestion(currentIdiom);
        }

        /// <summary>
        /// 加载网络题目（网络模式）
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[IdiomChain] 加载网络题目");

            if (networkData == null)
            {
                Debug.LogError("网络题目数据为空");
                DisplayErrorMessage("网络题目数据错误");
                return;
            }

            if (networkData.questionType != QuestionType.IdiomChain)
            {
                Debug.LogError($"题目类型不匹配: 期望{QuestionType.IdiomChain}, 实际{networkData.questionType}");
                LoadLocalQuestion(); // 降级到本地题目
                return;
            }

            // 使用网络提供的成语
            currentIdiom = networkData.correctAnswer;
            isGameInProgress = true;

            // 如果有特殊显示格式，从additionalData中获取
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<IdiomChainAdditionalData>(networkData.additionalData);
                    if (!string.IsNullOrEmpty(additionalInfo.displayText))
                    {
                        DisplayQuestionDirect(additionalInfo.displayText);
                        return;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"解析网络附加数据失败: {e.Message}");
                }
            }

            ShowQuestion(currentIdiom);
        }

        /// <summary>
        /// 显示题目（高亮最后一个字）
        /// </summary>
        private void ShowQuestion(string idiom)
        {
            if (string.IsNullOrEmpty(idiom))
            {
                DisplayErrorMessage("成语数据错误");
                return;
            }

            hasAnswered = false;

            // 高亮最后一个字
            char lastChar = idiom[idiom.Length - 1];
            string displayText = idiom.Substring(0, idiom.Length - 1) + $"<color=red>{lastChar}</color>";

            DisplayQuestionDirect(displayText);
        }

        /// <summary>
        /// 直接显示题目文本（用于网络模式的预格式化文本）
        /// </summary>
        private void DisplayQuestionDirect(string displayText)
        {
            questionText.text = displayText;
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;

            // 启用交互
            answerInput.interactable = true;
            submitButton.interactable = true;
            surrenderButton.interactable = true;

            // 激活输入框
            answerInput.ActivateInputField();

            Debug.Log($"[IdiomChain] 题目显示完成: {displayText}");
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

            isGameInProgress = false;
        }

        /// <summary>
        /// 检查本地答案（单机模式）
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            Debug.Log($"[IdiomChain] 检查本地答案: {answer}");

            bool isCorrect = ValidateIdiomChain(answer);

            if (isCorrect)
            {
                // 单机模式下继续接龙
                currentIdiom = answer;
                feedbackText.text = "回答正确！继续接龙...";
                feedbackText.color = Color.green;

                // 重启计时器并显示下一题
                if (timerManager != null)
                {
                    timerManager.StopTimer();
                    timerManager.StartTimer();
                }

                Invoke(nameof(ShowNextQuestion), 0.5f);
            }
            else
            {
                // 答错了，游戏结束
                feedbackText.text = GetValidationErrorMessage(answer);
                feedbackText.color = Color.red;

                StartCoroutine(ShowFeedbackAndNotify(false));
            }
        }

        /// <summary>
        /// 验证成语接龙答案
        /// </summary>
        private bool ValidateIdiomChain(string answer)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(currentIdiom))
                return false;

            // 1. 检查开头字符是否正确
            if (answer[0] != currentIdiom[currentIdiom.Length - 1])
                return false;

            // 2. 检查成语是否存在于词库中
            return IsIdiomInDatabase(answer);
        }

        /// <summary>
        /// 检查成语是否在数据库中
        /// </summary>
        private bool IsIdiomInDatabase(string idiom)
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
                Debug.LogError($"数据库查询失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取验证错误信息
        /// </summary>
        private string GetValidationErrorMessage(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "答案不能为空！";

            if (string.IsNullOrEmpty(currentIdiom))
                return "题目数据错误！";

            if (answer[0] != currentIdiom[currentIdiom.Length - 1])
                return $"开头错误！应以'{currentIdiom[currentIdiom.Length - 1]}'开头";

            return "词库中无此成语！";
        }

        /// <summary>
        /// 显示下一题（单机模式连续接龙）
        /// </summary>
        private void ShowNextQuestion()
        {
            if (isGameInProgress)
            {
                ShowQuestion(currentIdiom);
            }
        }

        /// <summary>
        /// 提交按钮点击
        /// </summary>
        private void OnSubmit()
        {
            if (hasAnswered || !isGameInProgress)
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
            if (!hasAnswered && isGameInProgress && !string.IsNullOrEmpty(value.Trim()))
            {
                SubmitAnswer(value.Trim());
            }
        }

        /// <summary>
        /// 投降按钮点击
        /// </summary>
        private void OnSurrender()
        {
            if (hasAnswered || !isGameInProgress)
                return;

            Debug.Log("[IdiomChain] 玩家投降");

            StopAllCoroutines();
            isGameInProgress = false;
            feedbackText.text = "已投降！";
            feedbackText.color = Color.yellow;

            OnAnswerResult?.Invoke(false);
        }

        /// <summary>
        /// 提交答案
        /// </summary>
        private void SubmitAnswer(string answer)
        {
            if (hasAnswered || !isGameInProgress)
                return;

            hasAnswered = true;

            // 禁用交互
            answerInput.interactable = false;
            submitButton.interactable = false;
            surrenderButton.interactable = false;

            Debug.Log($"[IdiomChain] 提交答案: '{answer}'");

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
            Debug.Log($"[IdiomChain] 网络模式提交答案: {answer}");

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
            // 等待一段时间显示反馈
            yield return new WaitForSeconds(1.5f);

            // 通知答题结果
            OnAnswerResult?.Invoke(isCorrect);

            isGameInProgress = false;
        }

        /// <summary>
        /// 显示网络答题结果（由网络系统调用）
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[IdiomChain] 收到网络结果: {(isCorrect ? "正确" : "错误")}");

            isGameInProgress = false;

            if (isCorrect)
            {
                feedbackText.text = "回答正确！";
                feedbackText.color = Color.green;
            }
            else
            {
                feedbackText.text = $"回答错误，正确答案示例：{correctAnswer}";
                feedbackText.color = Color.red;
            }

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
    /// 成语接龙附加数据结构（用于网络传输）
    /// </summary>
    [System.Serializable]
    public class IdiomChainAdditionalData
    {
        public string displayText;      // 预格式化的显示文本
        public string currentIdiom;     // 当前成语
        public char targetChar;         // 需要接龙的字符
    }
}