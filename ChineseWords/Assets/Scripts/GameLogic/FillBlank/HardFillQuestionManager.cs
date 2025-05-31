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
    /// 修复后的硬性单词补全题管理器
    /// 
    /// 修复内容：
    /// 1. 题干生成：随机生成填空格式（如"投_"、"_容"、"奔_跑"等）
    /// 2. 答案验证：检查玩家答案是否符合题干格式且在数据库中存在
    /// 3. 不再限制玩家必须填写服务端预设的特定词语
    /// 4. Host端只负责生成题干格式，不预设"正确答案"
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

        [Header("题干配置")]
        [SerializeField] private int minWordLength = 2;
        [SerializeField] private int maxWordLength = 4;
        [SerializeField, Range(1, 3)] private int maxBlankCount = 2; // 最大空格数量

        [Header("UI组件引用")]
        private TMP_Text questionText;
        private TMP_InputField answerInput;
        private Button submitButton;
        private Button surrenderButton;
        private TMP_Text feedbackText;

        // 当前题目信息
        private string currentPattern;        // 题干格式，如"投_"、"_容"
        private int[] blankPositions;         // 空格位置
        private char[] knownCharacters;       // 已知字符
        private int wordLength;               // 词语长度
        private bool hasAnswered = false;

        // IQuestionDataProvider接口实现
        public QuestionType QuestionType => QuestionType.HardFill;

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            if (NeedsUI())
            {
                InitializeUI();
            }
            else
            {
                Debug.Log("[HardFill] Host抽题模式，跳过UI初始化");
            }
        }

        private bool NeedsUI()
        {
            return transform.parent == null ||
                   (transform.parent.GetComponent<HostGameManager>() == null &&
                    transform.parent.GetComponent<QuestionDataService>() == null);
        }

        /// <summary>
        /// 获取题目数据（IQuestionDataProvider接口实现）
        /// 修复：只生成题干格式，不预设固定答案
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            Debug.Log("[HardFill] Host请求生成题干");

            // 1. 随机选择词语长度
            wordLength = Random.Range(minWordLength, maxWordLength + 1);

            // 2. 随机选择空格数量（不能全是空格）
            int blankCount = Random.Range(1, Mathf.Min(maxBlankCount + 1, wordLength));

            // 3. 从数据库随机选择一个词作为参考（用于生成题干格式）
            string referenceWord = GetRandomWordForPattern(wordLength);

            if (string.IsNullOrEmpty(referenceWord))
            {
                Debug.LogWarning($"[HardFill] 无法找到长度为{wordLength}的参考词语");
                return null;
            }

            // 4. 基于参考词生成题干格式
            GenerateQuestionPattern(referenceWord, blankCount);

            // 5. 创建附加数据（包含验证所需信息）
            var additionalData = new HardFillAdditionalData
            {
                pattern = currentPattern,
                blankPositions = blankPositions,
                knownCharacters = knownCharacters,
                wordLength = wordLength,
                minFreq = freqMin,
                maxFreq = freqMax
            };

            // 6. 创建网络题目数据（不包含固定答案）
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.HardFill,
                questionText = $"请填写符合格式的词语：{currentPattern}",
                correctAnswer = "", // 不设置固定答案
                options = new string[0],
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(additionalData)
            };

            Debug.Log($"[HardFill] 题干生成完成: {currentPattern} (长度: {wordLength})");
            return questionData;
        }

        /// <summary>
        /// 从数据库获取随机词语作为生成题干的参考
        /// </summary>
        private string GetRandomWordForPattern(int targetLength)
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
                                SELECT word, Freq, length FROM word
                                UNION ALL
                                SELECT word, Freq, length FROM idiom
                            ) WHERE length = @len AND Freq BETWEEN @min AND @max 
                            ORDER BY RANDOM() LIMIT 1";

                        cmd.Parameters.AddWithValue("@len", targetLength);
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

            Debug.Log($"[HardFill] 生成题干: {currentPattern} (基于参考词: {referenceWord})");
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

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("无法生成题目");
                return;
            }

            ParseQuestionData(questionData);
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
                Debug.LogError($"[HardFill] 题目类型不匹配");
                DisplayErrorMessage("题目类型错误");
                return;
            }

            ParseQuestionData(networkData);
            DisplayQuestion();
        }

        /// <summary>
        /// 解析题目数据
        /// </summary>
        private void ParseQuestionData(NetworkQuestionData questionData)
        {
            if (!string.IsNullOrEmpty(questionData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<HardFillAdditionalData>(questionData.additionalData);
                    currentPattern = additionalInfo.pattern;
                    blankPositions = additionalInfo.blankPositions;
                    knownCharacters = additionalInfo.knownCharacters;
                    wordLength = additionalInfo.wordLength;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"解析附加数据失败: {e.Message}");
                    DisplayErrorMessage("题目数据解析错误");
                    return;
                }
            }
            else
            {
                Debug.LogError("缺少题目附加数据");
                DisplayErrorMessage("题目数据不完整");
            }
        }

        /// <summary>
        /// 显示题目
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(currentPattern))
            {
                DisplayErrorMessage("题目数据错误");
                return;
            }

            questionText.text = $"请填写符合格式的词语：{currentPattern}";

            // 重置输入和反馈
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.interactable = true;
            submitButton.interactable = true;
            surrenderButton.interactable = true;

            // 设置输入框字符限制
            answerInput.characterLimit = wordLength;
            answerInput.ActivateInputField();

            Debug.Log($"[HardFill] 题目显示完成: {currentPattern}");
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
            StartCoroutine(ShowFeedbackAndNotify(isCorrect, answer.Trim()));
        }

        /// <summary>
        /// 修复后的答案验证逻辑
        /// </summary>
        private bool ValidateAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
            {
                Debug.Log("[HardFill] 答案为空");
                return false;
            }

            // 1. 检查长度是否匹配
            if (answer.Length != wordLength)
            {
                Debug.Log($"[HardFill] 长度不匹配: 期望{wordLength}, 实际{answer.Length}");
                return false;
            }

            // 2. 检查已知位置的字符是否匹配
            for (int i = 0; i < wordLength; i++)
            {
                if (knownCharacters[i] != '\0' && answer[i] != knownCharacters[i])
                {
                    Debug.Log($"[HardFill] 位置{i}字符不匹配: 期望'{knownCharacters[i]}', 实际'{answer[i]}'");
                    return false;
                }
            }

            // 3. 检查词语是否存在于数据库中
            bool existsInDB = IsWordInDatabase(answer);
            Debug.Log($"[HardFill] 词语'{answer}'在数据库中: {existsInDB}");

            return existsInDB;
        }

        /// <summary>
        /// 静态验证方法 - 供Host调用
        /// 基于题目数据验证答案，不依赖实例状态
        /// </summary>
        public static bool ValidateAnswerStatic(string answer, NetworkQuestionData questionData)
        {
            if (string.IsNullOrEmpty(answer))
            {
                Debug.Log("[HardFill] 静态验证：答案为空");
                return false;
            }

            if (questionData.questionType != QuestionType.HardFill)
            {
                Debug.LogError("[HardFill] 静态验证：题目类型不匹配");
                return false;
            }

            // 解析题目附加数据
            if (string.IsNullOrEmpty(questionData.additionalData))
            {
                Debug.LogError("[HardFill] 静态验证：缺少题目附加数据");
                return false;
            }

            try
            {
                var additionalData = JsonUtility.FromJson<HardFillAdditionalData>(questionData.additionalData);

                // 1. 检查长度
                if (answer.Length != additionalData.wordLength)
                {
                    Debug.Log($"[HardFill] 静态验证：长度不匹配 期望{additionalData.wordLength}, 实际{answer.Length}");
                    return false;
                }

                // 2. 检查已知位置的字符是否匹配
                for (int i = 0; i < additionalData.wordLength; i++)
                {
                    if (additionalData.knownCharacters[i] != '\0' &&
                        answer[i] != additionalData.knownCharacters[i])
                    {
                        Debug.Log($"[HardFill] 静态验证：位置{i}字符不匹配 期望'{additionalData.knownCharacters[i]}', 实际'{answer[i]}'");
                        return false;
                    }
                }

                // 3. 检查词语是否在数据库中
                bool existsInDB = IsWordInDatabaseStatic(answer, additionalData.minFreq, additionalData.maxFreq);
                Debug.Log($"[HardFill] 静态验证：词语'{answer}'数据库验证结果: {existsInDB}");

                return existsInDB;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HardFill] 静态验证失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 静态数据库查询方法 - 供Host调用
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

            feedbackText.text = "已提交答案，等待服务器验证...";
            feedbackText.color = Color.yellow;

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
            }
            else
            {
                Debug.LogError("[HardFill] NetworkManager实例不存在");
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
        private IEnumerator ShowFeedbackAndNotify(bool isCorrect, string userAnswer)
        {
            if (feedbackText != null)
            {
                if (isCorrect)
                {
                    feedbackText.text = $"回答正确！'{userAnswer}' 符合要求";
                    feedbackText.color = Color.green;
                }
                else
                {
                    if (string.IsNullOrEmpty(userAnswer))
                    {
                        feedbackText.text = "未提交答案";
                    }
                    else
                    {
                        // 提供更详细的错误信息
                        string errorReason = GetValidationErrorReason(userAnswer);
                        feedbackText.text = $"回答错误：{errorReason}";
                    }
                    feedbackText.color = Color.red;
                }
            }

            yield return new WaitForSeconds(2f);

            OnAnswerResult?.Invoke(isCorrect);
        }

        /// <summary>
        /// 获取验证失败的详细原因
        /// </summary>
        private string GetValidationErrorReason(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "答案为空";

            if (answer.Length != wordLength)
                return $"长度不正确（应为{wordLength}字）";

            // 检查已知字符是否匹配
            for (int i = 0; i < wordLength; i++)
            {
                if (knownCharacters[i] != '\0' && answer[i] != knownCharacters[i])
                    return $"第{i + 1}位字符应为'{knownCharacters[i]}'";
            }

            // 如果格式正确但不在数据库中
            if (!IsWordInDatabase(answer))
                return "该词语不在词库中";

            return "未知错误";
        }

        /// <summary>
        /// 显示网络答题结果（由网络系统调用）
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[HardFill] 收到网络结果: {(isCorrect ? "正确" : "错误")}");

            string userAnswer = answerInput.text.Trim();
            StartCoroutine(ShowFeedbackAndNotify(isCorrect, userAnswer));
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            if (submitButton != null)
                submitButton.onClick.RemoveAllListeners();
            if (surrenderButton != null)
                surrenderButton.onClick.RemoveAllListeners();
            if (answerInput != null)
                answerInput.onSubmit.RemoveAllListeners();
        }
    }

    /// <summary>
    /// 硬填空题附加数据结构（修复后）
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