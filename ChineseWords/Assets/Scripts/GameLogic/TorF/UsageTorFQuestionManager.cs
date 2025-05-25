using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.TorF
{
    /// <summary>
    /// 成语/词语使用判断题管理器
    /// - 支持单机和网络模式
    /// - 单机模式：从 simular_usage_questions 表随机取一条记录
    /// - 网络模式：使用服务器提供的题目数据
    /// - 随机决定展示正确示例或错误示例
    /// - 将替换后文本插入到下划线位置并高亮
    /// - 玩家选择"正确"/"错误"判定
    /// </summary>
    public class UsageTorFQuestionManager : NetworkQuestionManagerBase
    {
        private string dbPath;

        [Header("UI设置")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/TorFUI";

        [Header("UI组件引用")]
        private TMP_Text questionText;
        private Button trueButton;
        private Button falseButton;
        private TMP_Text feedbackText;

        private TMP_Text textTrue;
        private TMP_Text textFalse;

        private bool isInstanceCorrect;     // 当前实例是否正确
        private string currentStem;         // 当前题干
        private string correctFill;         // 正确填空
        private string currentFill;         // 当前使用的填空
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
            trueButton = ui.Find("TrueButton")?.GetComponent<Button>();
            falseButton = ui.Find("FalseButton")?.GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || trueButton == null || falseButton == null || feedbackText == null)
            {
                Debug.LogError("UI组件获取失败，检查预制体结构");
                return;
            }

            // 获取按钮文本组件
            textTrue = trueButton.GetComponentInChildren<TMP_Text>();
            textFalse = falseButton.GetComponentInChildren<TMP_Text>();

            if (textTrue == null || textFalse == null)
            {
                Debug.LogError("按钮文本组件获取失败");
                return;
            }

            // 绑定按钮事件
            trueButton.onClick.RemoveAllListeners();
            falseButton.onClick.RemoveAllListeners();
            trueButton.onClick.AddListener(() => OnSelectAnswer(true));
            falseButton.onClick.AddListener(() => OnSelectAnswer(false));

            feedbackText.text = string.Empty;
        }

        /// <summary>
        /// 加载本地题目（单机模式）
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[UsageTorF] 加载本地题目");

            // 1. 从数据库获取使用判断题数据
            var usageData = GetRandomUsageData();
            if (usageData == null)
            {
                DisplayErrorMessage("暂无题目数据");
                return;
            }

            currentStem = usageData.stem;
            correctFill = usageData.correctFill;

            // 2. 随机决定展示正确还是错误示例
            isInstanceCorrect = Random.value < 0.5f;
            currentFill = isInstanceCorrect ?
                correctFill :
                usageData.wrongFills[Random.Range(0, usageData.wrongFills.Count)];

            // 3. 显示题目
            DisplayQuestion();
        }

        /// <summary>
        /// 加载网络题目（网络模式）
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[UsageTorF] 加载网络题目");

            if (networkData == null)
            {
                Debug.LogError("网络题目数据为空");
                DisplayErrorMessage("网络题目数据错误");
                return;
            }

            if (networkData.questionType != QuestionType.UsageTorF)
            {
                Debug.LogError($"题目类型不匹配: 期望{QuestionType.UsageTorF}, 实际{networkData.questionType}");
                LoadLocalQuestion(); // 降级到本地题目
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
            // 解析正确答案（true/false）
            isInstanceCorrect = networkData.correctAnswer.ToLower() == "true";

            // 从additionalData中解析额外信息
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<UsageTorFAdditionalData>(networkData.additionalData);
                    currentStem = additionalInfo.stem;
                    correctFill = additionalInfo.correctFill;
                    currentFill = additionalInfo.currentFill;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"解析网络附加数据失败: {e.Message}");
                    // 从题目文本解析
                    ExtractInfoFromQuestionText(networkData.questionText);
                }
            }
            else
            {
                // 从题目文本解析
                ExtractInfoFromQuestionText(networkData.questionText);
            }

            Debug.Log($"[UsageTorF] 网络题目解析完成: isCorrect={isInstanceCorrect}, fill={currentFill}");
        }

        /// <summary>
        /// 从题目文本中提取信息
        /// </summary>
        private void ExtractInfoFromQuestionText(string questionText)
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
                Debug.LogError($"获取使用判断题数据失败: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 显示题目
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(currentStem) || string.IsNullOrEmpty(currentFill))
            {
                DisplayErrorMessage("题目数据错误");
                return;
            }

            // 构造题干：替换第一个下划线段并高亮填空内容
            string displayText = ReplaceUnderscoreWithHighlight(currentStem, currentFill);
            questionText.text = displayText;
            feedbackText.text = string.Empty;

            // 设置按钮文本
            textTrue.text = "正确";
            textFalse.text = "错误";

            // 启用按钮
            trueButton.interactable = true;
            falseButton.interactable = true;

            Debug.Log($"[UsageTorF] 题目显示完成: {displayText} (实例{(isInstanceCorrect ? "正确" : "错误")})");
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

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            if (questionText != null)
                questionText.text = message;

            if (feedbackText != null)
                feedbackText.text = "";

            if (trueButton != null)
                trueButton.interactable = false;

            if (falseButton != null)
                falseButton.interactable = false;
        }

        /// <summary>
        /// 检查本地答案（单机模式）
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            // 判断题通过按钮点击处理，此方法不直接使用
            Debug.Log("[UsageTorF] CheckLocalAnswer 被调用，但判断题通过按钮处理");
        }

        /// <summary>
        /// 选择答案处理
        /// </summary>
        private void OnSelectAnswer(bool selectedTrue)
        {
            if (hasAnswered)
            {
                Debug.Log("已经回答过了，忽略重复点击");
                return;
            }

            hasAnswered = true;

            // 禁用按钮防止重复点击
            trueButton.interactable = false;
            falseButton.interactable = false;

            Debug.Log($"[UsageTorF] 选择了答案: {(selectedTrue ? "正确" : "错误")}");

            if (IsNetworkMode())
            {
                HandleNetworkAnswer(selectedTrue.ToString().ToLower());
            }
            else
            {
                HandleLocalAnswer(selectedTrue);
            }
        }

        /// <summary>
        /// 处理网络模式答案
        /// </summary>
        private void HandleNetworkAnswer(string answer)
        {
            Debug.Log($"[UsageTorF] 网络模式提交答案: {answer}");

            // 显示提交状态
            feedbackText.text = "已提交答案，等待服务器结果...";
            feedbackText.color = Color.yellow;

            // 通过基类提交到服务器
            CheckAnswer(answer);
        }

        /// <summary>
        /// 处理单机模式答案
        /// </summary>
        private void HandleLocalAnswer(bool selectedTrue)
        {
            // 判断逻辑：选择"正确"且实例正确，或选择"错误"且实例错误
            bool isCorrect = (selectedTrue && isInstanceCorrect) || (!selectedTrue && !isInstanceCorrect);

            Debug.Log($"[UsageTorF] 单机模式答题结果: {(isCorrect ? "正确" : "错误")}");

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// 显示反馈信息并通知结果
        /// </summary>
        private IEnumerator ShowFeedbackAndNotify(bool isCorrect)
        {
            // 显示反馈
            feedbackText.color = isCorrect ? Color.green : Color.red;
            feedbackText.text = isCorrect ? "回答正确！" : "回答错误！";

            // 等待一段时间
            yield return new WaitForSeconds(1.5f);

            // 通知答题结果
            OnAnswerResult?.Invoke(isCorrect);

            // 重新启用按钮为下一题准备
            trueButton.interactable = true;
            falseButton.interactable = true;
        }

        /// <summary>
        /// 显示网络答题结果（由网络系统调用）
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[UsageTorF] 收到网络结果: {(isCorrect ? "正确" : "错误")}");

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            // 清理按钮事件监听
            if (trueButton != null)
                trueButton.onClick.RemoveAllListeners();
            if (falseButton != null)
                falseButton.onClick.RemoveAllListeners();
        }
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
    /// 使用判断题附加数据结构（用于网络传输）
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