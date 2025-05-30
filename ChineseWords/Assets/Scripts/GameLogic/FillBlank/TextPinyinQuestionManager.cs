using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// 文字拼音题管理器
    /// - 支持单机和网络模式
    /// - 实现IQuestionDataProvider接口，支持Host抽题
    /// - 单机模式：随机抽词，随机定位字，解析拼音数据
    /// - 网络模式：使用服务器提供的题目数据
    /// - 解析 character.Tpinyin JSON 并比对（无调、小写）
    /// - 反馈展示带调拼音
    /// </summary>
    public class TextPinyinQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        private string dbPath;

        [Header("UI设置")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/HardFillUI";

        [Header("单机模式配置")]
        [Header("频率范围")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;

        [Header("UI组件引用")]
        private TMP_Text questionText;
        private TMP_InputField answerInput;
        private Button submitButton;
        private Button surrenderButton;
        private TMP_Text feedbackText;

        private string correctPinyinNoTone;     // 无调拼音（用于比对）
        private string correctPinyinTone;       // 带调拼音（用于反馈）
        private string currentWord;             // 当前词语
        private string targetCharacter;         // 目标字符
        private int characterIndex;             // 字符位置
        private bool hasAnswered = false;

        // IQuestionDataProvider接口实现
        public QuestionType QuestionType => QuestionType.TextPinyin;

        protected override void Awake()
        {
            base.Awake(); // 调用网络基类初始化
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
            Debug.Log($"[TextPinyin] Awake: dbPath={dbPath}");
        }

        private void Start()
        {
            // 检查是否需要UI（Host抽题模式可能不需要UI）
            if (NeedsUI())
            {
                Debug.Log("[TextPinyin] Start: 初始化 UI");
                InitializeUI();
            }
            else
            {
                Debug.Log("[TextPinyin] Host抽题模式，跳过UI初始化");
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
            Debug.Log("[TextPinyin] Host请求抽题数据");

            // 1. 随机选词
            string word = GetRandomWord();
            if (string.IsNullOrEmpty(word))
            {
                Debug.LogWarning("[TextPinyin] Host抽题：没有符合频率范围的词");
                return null;
            }

            Debug.Log($"[TextPinyin] Host抽题选词: {word}");

            // 2. 随机定位字符
            int charIndex = Random.Range(0, word.Length);
            string targetChar = word.Substring(charIndex, 1);
            Debug.Log($"[TextPinyin] Host抽题目标字符: '{targetChar}', 位置: {charIndex}");

            // 3. 获取拼音数据
            string pinyinNoTone, pinyinTone;
            if (!GetPinyinDataForHost(targetChar, word, charIndex, out pinyinNoTone, out pinyinTone))
            {
                Debug.LogWarning($"[TextPinyin] Host抽题：找不到字符 '{targetChar}' 的拼音数据");
                return null;
            }

            // 4. 创建附加数据
            var additionalData = new TextPinyinAdditionalData
            {
                word = word,
                targetCharacter = targetChar,
                characterIndex = charIndex,
                correctPinyinTone = pinyinTone,
                correctPinyinNoTone = pinyinNoTone
            };

            // 5. 创建网络题目数据
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.TextPinyin,
                questionText = $"题目：{word}\n\"<color=red>{targetChar}</color>\" 的读音是？",
                correctAnswer = pinyinNoTone, // 无调拼音用于验证
                options = new string[0], // 拼音题不需要选项
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(additionalData)
            };

            Debug.Log($"[TextPinyin] Host抽题成功: {word} -> {targetChar} ({pinyinNoTone})");
            return questionData;
        }

        /// <summary>
        /// 为Host抽题获取拼音数据
        /// </summary>
        private bool GetPinyinDataForHost(string character, string word, int charIndex, out string pinyinNoTone, out string pinyinTone)
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

                Debug.Log($"[TextPinyin] Host抽题 Raw Tpinyin JSON={rawTpinyin}");

                // 解析JSON格式的拼音数据
                string parsed = rawTpinyin;
                if (!string.IsNullOrEmpty(rawTpinyin) && rawTpinyin.StartsWith("["))
                {
                    var inner = rawTpinyin.Substring(1, rawTpinyin.Length - 2);
                    var parts = inner.Split(',');
                    parsed = parts[0].Trim().Trim('"');
                }
                pinyinNoTone = (parsed ?? "").ToLower();
                Debug.Log($"[TextPinyin] Host抽题 Parsed TpinyinNoTone={pinyinNoTone}");

                if (string.IsNullOrEmpty(pinyinNoTone))
                    return false;

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

                Debug.Log($"[TextPinyin] Host抽题 Full word pinyin tone={fullTonePinyin}");

                // 提取目标字符的带调拼音
                var toneParts = fullTonePinyin?.Trim().Split(' ');
                if (toneParts == null || charIndex < 0 || charIndex >= toneParts.Length)
                {
                    Debug.LogError($"[TextPinyin] Host抽题 toneParts.Length={toneParts?.Length}, index={charIndex}");
                    pinyinTone = pinyinNoTone; // 降级使用无调拼音
                }
                else
                {
                    pinyinTone = toneParts[charIndex];
                }

                Debug.Log($"[TextPinyin] Host抽题 CorrectTone={pinyinTone}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Host抽题获取拼音数据失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[TextPinyin] UIManager实例不存在");
                return;
            }

            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"[TextPinyin] 无法加载UI预制体: {uiPrefabPath}");
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
                Debug.LogError("[TextPinyin] UI组件获取失败，检查预制体结构");
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

            Debug.Log("[TextPinyin] UI初始化完成");
        }

        /// <summary>
        /// 加载本地题目（单机模式）
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[TextPinyin] 加载本地题目");

            // 使用GetQuestionData()方法复用抽题逻辑
            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("没有符合条件的词条！");
                return;
            }

            // 解析题目数据
            currentWord = questionData.questionText; // 从题目文本中提取
            correctPinyinNoTone = questionData.correctAnswer;

            // 从附加数据中解析详细信息
            if (!string.IsNullOrEmpty(questionData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<TextPinyinAdditionalData>(questionData.additionalData);
                    currentWord = additionalInfo.word;
                    targetCharacter = additionalInfo.targetCharacter;
                    characterIndex = additionalInfo.characterIndex;
                    correctPinyinTone = additionalInfo.correctPinyinTone;
                    correctPinyinNoTone = additionalInfo.correctPinyinNoTone;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"解析附加数据失败: {e.Message}，重新生成题目");
                    LoadOriginalLocalQuestion();
                    return;
                }
            }
            else
            {
                LoadOriginalLocalQuestion();
                return;
            }

            // 显示题目
            DisplayQuestion();
        }

        /// <summary>
        /// 原始的本地题目加载逻辑（备用）
        /// </summary>
        private void LoadOriginalLocalQuestion()
        {
            // 1. 随机选词
            currentWord = GetRandomWord();
            if (string.IsNullOrEmpty(currentWord))
            {
                DisplayErrorMessage("没有符合频率范围的词！");
                return;
            }

            Debug.Log($"[TextPinyin] Selected word={currentWord}");

            // 2. 随机定位字符
            characterIndex = Random.Range(0, currentWord.Length);
            targetCharacter = currentWord.Substring(characterIndex, 1);
            Debug.Log($"[TextPinyin] Target char='{targetCharacter}', index={characterIndex}");

            // 3. 获取拼音数据
            if (!GetPinyinData(targetCharacter, currentWord, characterIndex))
            {
                DisplayErrorMessage($"词库中找不到 '{targetCharacter}' 的拼音！");
                return;
            }

            // 4. 显示题目
            DisplayQuestion();
        }

        /// <summary>
        /// 加载网络题目（网络模式）
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[TextPinyin] 加载网络题目");

            if (networkData == null)
            {
                Debug.LogError("[TextPinyin] 网络题目数据为空");
                DisplayErrorMessage("网络题目数据错误");
                return;
            }

            if (networkData.questionType != QuestionType.TextPinyin)
            {
                Debug.LogError($"[TextPinyin] 题目类型不匹配: 期望{QuestionType.TextPinyin}, 实际{networkData.questionType}");
                DisplayErrorMessage("题目类型错误");
                return;
            }

            // 从网络数据解析题目信息
            ParseNetworkQuestionData(networkData);

            // 显示题目
            DisplayQuestion();
        }

        /// <summary>
        /// 解析网络题目数据
        /// </summary>
        private void ParseNetworkQuestionData(NetworkQuestionData networkData)
        {
            correctPinyinNoTone = networkData.correctAnswer.ToLower();

            // 从additionalData中解析额外信息
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<TextPinyinAdditionalData>(networkData.additionalData);
                    currentWord = additionalInfo.word;
                    targetCharacter = additionalInfo.targetCharacter;
                    characterIndex = additionalInfo.characterIndex;
                    correctPinyinTone = additionalInfo.correctPinyinTone;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"解析网络附加数据失败: {e.Message}");
                    // 从题目文本中提取信息
                    ExtractInfoFromQuestionText(networkData.questionText);
                }
            }
            else
            {
                // 从题目文本中提取信息
                ExtractInfoFromQuestionText(networkData.questionText);
            }

            Debug.Log($"[TextPinyin] 网络题目解析完成: word={currentWord}, target={targetCharacter}, correct={correctPinyinNoTone}");
        }

        /// <summary>
        /// 从题目文本中提取信息（备用方案）
        /// </summary>
        private void ExtractInfoFromQuestionText(string questionText)
        {
            // 简单的文本解析，提取词语和目标字符
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

            // 尝试从HTML标记中提取目标字符
            var colorTagStart = questionText.IndexOf("<color=red>");
            var colorTagEnd = questionText.IndexOf("</color>");
            if (colorTagStart != -1 && colorTagEnd != -1)
            {
                targetCharacter = questionText.Substring(colorTagStart + 11, colorTagEnd - colorTagStart - 11);
            }

            // 如果没有带调拼音，设置为答案本身
            if (string.IsNullOrEmpty(correctPinyinTone))
                correctPinyinTone = correctPinyinNoTone;
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
                            SELECT word FROM word
                            WHERE Freq BETWEEN @min AND @max
                            ORDER BY RANDOM()
                            LIMIT 1";
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
        /// 获取拼音数据（保持原有逻辑用于单机模式）
        /// </summary>
        private bool GetPinyinData(string character, string word, int characterIndex)
        {
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

                Debug.Log($"[TextPinyin] Raw Tpinyin JSON={rawTpinyin}");

                // 解析JSON格式的拼音数据
                string parsed = rawTpinyin;
                if (!string.IsNullOrEmpty(rawTpinyin) && rawTpinyin.StartsWith("["))
                {
                    var inner = rawTpinyin.Substring(1, rawTpinyin.Length - 2);
                    var parts = inner.Split(',');
                    parsed = parts[0].Trim().Trim('"');
                }
                correctPinyinNoTone = (parsed ?? "").ToLower();
                Debug.Log($"[TextPinyin] Parsed TpinyinNoTone={correctPinyinNoTone}");

                if (string.IsNullOrEmpty(correctPinyinNoTone))
                    return false;

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

                Debug.Log($"[TextPinyin] Full word pinyin tone={fullTonePinyin}");

                // 提取目标字符的带调拼音
                var toneParts = fullTonePinyin?.Trim().Split(' ');
                if (toneParts == null || characterIndex < 0 || characterIndex >= toneParts.Length)
                {
                    Debug.LogError($"[TextPinyin] toneParts.Length={toneParts?.Length}, index={characterIndex}");
                    correctPinyinTone = correctPinyinNoTone; // 降级使用无调拼音
                }
                else
                {
                    correctPinyinTone = toneParts[characterIndex];
                }

                Debug.Log($"[TextPinyin] CorrectTone={correctPinyinTone}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"获取拼音数据失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 显示题目
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(currentWord) || string.IsNullOrEmpty(targetCharacter))
            {
                DisplayErrorMessage("题目数据错误");
                return;
            }

            // 构造题干（高亮目标字符）
            questionText.text = $"题目：{currentWord}\n\"<color=red>{targetCharacter}</color>\" 的读音是？";

            // 重置输入和反馈
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.interactable = true;
            submitButton.interactable = true;
            surrenderButton.interactable = true;

            // 激活输入框
            answerInput.ActivateInputField();

            Debug.Log($"[TextPinyin] 题目显示完成: {currentWord} -> {targetCharacter}");
        }

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            Debug.LogWarning($"[TextPinyin] {message}");

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
            Debug.Log($"[TextPinyin] CheckAnswer: rawAnswer=\"{answer}\"");

            bool isCorrect = ValidatePinyinAnswer(answer);
            Debug.Log($"[TextPinyin] CheckAnswer: isCorrect={isCorrect}");

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// 验证拼音答案
        /// </summary>
        private bool ValidatePinyinAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(correctPinyinNoTone))
                return false;

            // 处理答案：去除引号、转小写（兼容低版本C#）
            var processedAnswer = answer.Replace("\"", "")
                                       .Replace("\u201c", "") // 左双引号 "
                                       .Replace("\u201d", "") // 右双引号 "
                                       .Trim()
                                       .ToLower();

            Debug.Log("[TextPinyin] CheckAnswer: processed=" + processedAnswer);

            return processedAnswer == correctPinyinNoTone;
        }

        /// <summary>
        /// 提交按钮点击
        /// </summary>
        private void OnSubmit()
        {
            if (hasAnswered)
                return;

            string userAnswer = answerInput.text.Trim();
            if (string.IsNullOrWhiteSpace(userAnswer))
                return;

            SubmitAnswer(userAnswer);
        }

        /// <summary>
        /// 输入框回车提交
        /// </summary>
        private void OnInputSubmit(string value)
        {
            if (!hasAnswered && !string.IsNullOrWhiteSpace(value))
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

            Debug.Log("[TextPinyin] Surrender clicked, treat as wrong answer");
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

            Debug.Log($"[TextPinyin] 提交答案: '{answer}'");

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
            Debug.Log($"[TextPinyin] 网络模式提交答案: {answer}");

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
                Debug.LogError("[TextPinyin] NetworkManager实例不存在，无法提交答案");
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
                    Debug.Log("[TextPinyin] ShowFeedback: 正确");
                }
                else
                {
                    feedbackText.text = $"回答错误，正确拼音是：{correctPinyinTone}";
                    feedbackText.color = Color.red;
                    Debug.Log($"[TextPinyin] ShowFeedback: 错误，正确拼音={correctPinyinTone}");
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
            Debug.Log($"[TextPinyin] 收到网络结果: {(isCorrect ? "正确" : "错误")}");

            // 更新正确答案（服务器可能提供带调拼音）
            if (!string.IsNullOrEmpty(correctAnswer))
                correctPinyinTone = correctAnswer;

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
    /// 文字拼音题附加数据结构（用于网络传输）
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