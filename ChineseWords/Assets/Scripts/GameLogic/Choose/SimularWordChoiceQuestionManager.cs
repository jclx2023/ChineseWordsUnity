using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.Choice
{
    /// <summary>
    /// 近义词选择题管理器
    /// - 支持单机和网络模式
    /// - 实现IQuestionDataProvider接口，支持Host抽题
    /// - 单机模式：从 simular_usage_questions 表随机取一条记录
    /// - 网络模式：使用服务器提供的题目数据
    /// - 将 stem 显示在題干，True/1/2/3 四个选项随机打乱分配给按钮
    /// - 玩家点击按钮判定正误，通过 OnAnswerResult 通知外层
    /// </summary>
    public class SimularWordChoiceQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        private string dbPath;

        [Header("UI设置")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/ChooseUI";

        [Header("UI组件引用")]
        private TMP_Text questionText;
        private Button[] optionButtons;
        private TMP_Text feedbackText;

        private string correctOption;
        private bool hasAnswered = false;

        // IQuestionDataProvider接口实现
        public QuestionType QuestionType => QuestionType.SimularWordChoice;

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
                Debug.Log("[SimularWord] Host抽题模式，跳过UI初始化");
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
            Debug.Log("[SimularWord] Host请求抽题数据");

            string stem = null;
            List<string> choices = new List<string>(4);
            string correctOption = null;

            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT [stem], [True] AS correct, [1] AS opt1, [2] AS opt2, [3] AS opt3
                            FROM simular_usage_questions
                            ORDER BY RANDOM()
                            LIMIT 1";

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                stem = reader.GetString(0);
                                correctOption = reader.GetString(1);

                                choices.Add(reader.GetString(1)); // correct
                                choices.Add(reader.GetString(2)); // opt1
                                choices.Add(reader.GetString(3)); // opt2
                                choices.Add(reader.GetString(4)); // opt3
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Host抽题数据库查询失败: {e.Message}");
                return null;
            }

            if (string.IsNullOrEmpty(stem) || choices.Count == 0)
            {
                Debug.LogWarning("[SimularWord] Host抽题：暂无题目数据");
                return null;
            }

            // 随机打乱选项
            ShuffleChoices(choices);

            // 创建网络题目数据
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.SimularWordChoice,
                questionText = stem,
                correctAnswer = correctOption,
                options = choices.ToArray(),
                timeLimit = 30f,
                additionalData = "{\"source\": \"SimularWordChoiceQuestionManager\"}"
            };

            Debug.Log($"[SimularWord] Host抽题成功: {stem}");
            return questionData;
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[SimularWord] UIManager实例不存在");
                return;
            }

            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"[SimularWord] 无法加载UI预制体: {uiPrefabPath}");
                return;
            }

            // 获取UI组件
            questionText = ui.Find("QuestionText")?.GetComponent<TMP_Text>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || feedbackText == null)
            {
                Debug.LogError("[SimularWord] UI组件获取失败，检查预制体结构");
                return;
            }

            // 初始化选项按钮
            InitializeOptionButtons(ui);

            Debug.Log("[SimularWord] UI初始化完成");
        }

        /// <summary>
        /// 初始化选项按钮
        /// </summary>
        private void InitializeOptionButtons(Transform ui)
        {
            optionButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var btnTransform = ui.Find($"OptionButton{i + 1}");
                if (btnTransform == null)
                {
                    Debug.LogError($"找不到按钮: OptionButton{i + 1}");
                    continue;
                }

                var btn = btnTransform.GetComponent<Button>();
                if (btn == null)
                {
                    Debug.LogError($"OptionButton{i + 1} 没有Button组件");
                    continue;
                }

                optionButtons[i] = btn;
                int index = i; // 避免闭包问题

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnOptionClicked(index));
            }
        }

        /// <summary>
        /// 加载本地题目（单机模式）
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[SimularWord] 加载本地题目");

            // 使用GetQuestionData()方法复用抽题逻辑
            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("暂无题目数据");
                return;
            }

            // 设置正确答案
            correctOption = questionData.correctAnswer;

            // 显示题目
            List<string> choices = new List<string>(questionData.options);
            DisplayQuestion(questionData.questionText, choices);
        }

        /// <summary>
        /// 加载网络题目（网络模式）
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[SimularWord] 加载网络题目");

            if (networkData == null)
            {
                Debug.LogError("[SimularWord] 网络题目数据为空");
                DisplayErrorMessage("网络题目数据错误");
                return;
            }

            if (networkData.questionType != QuestionType.SimularWordChoice)
            {
                Debug.LogError($"[SimularWord] 题目类型不匹配: 期望{QuestionType.SimularWordChoice}, 实际{networkData.questionType}");
                DisplayErrorMessage("题目类型错误");
                return;
            }

            correctOption = networkData.correctAnswer;

            // 网络模式下选项已由服务器打乱，直接显示
            List<string> choices = new List<string>(networkData.options);
            DisplayQuestion(networkData.questionText, choices);
        }

        /// <summary>
        /// 显示题目内容
        /// </summary>
        private void DisplayQuestion(string stem, List<string> choices)
        {
            if (questionText == null || optionButtons == null)
            {
                Debug.LogError("[SimularWord] UI组件未初始化");
                return;
            }

            hasAnswered = false;

            // 显示题干
            questionText.text = stem;
            if (feedbackText != null)
                feedbackText.text = string.Empty;

            // 设置选项按钮
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (i < choices.Count)
                {
                    var txt = optionButtons[i].GetComponentInChildren<TMP_Text>();
                    if (txt != null)
                    {
                        txt.text = choices[i];
                        optionButtons[i].gameObject.SetActive(true);
                        optionButtons[i].interactable = true;
                    }
                }
                else
                {
                    // 隐藏多余的按钮
                    optionButtons[i].gameObject.SetActive(false);
                }
            }

            Debug.Log($"[SimularWord] 题目显示完成: {stem}");
        }

        /// <summary>
        /// 随机打乱选项
        /// </summary>
        private void ShuffleChoices(List<string> choices)
        {
            for (int i = choices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                string temp = choices[i];
                choices[i] = choices[j];
                choices[j] = temp;
            }
        }

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            Debug.LogWarning($"[SimularWord] {message}");

            if (questionText != null)
                questionText.text = message;

            if (feedbackText != null)
                feedbackText.text = "";

            // 隐藏所有选项按钮
            if (optionButtons != null)
            {
                foreach (var btn in optionButtons)
                {
                    if (btn != null)
                        btn.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 检查本地答案（单机模式）
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            // 选择题通过按钮点击处理，此方法不直接使用
            Debug.Log("[SimularWord] CheckLocalAnswer 被调用，但选择题通过按钮处理");
        }

        /// <summary>
        /// 选项按钮点击处理
        /// </summary>
        private void OnOptionClicked(int index)
        {
            if (hasAnswered)
            {
                Debug.Log("[SimularWord] 已经回答过了，忽略重复点击");
                return;
            }

            if (index >= optionButtons.Length || optionButtons[index] == null)
            {
                Debug.LogError($"[SimularWord] 无效的选项索引: {index}");
                return;
            }

            var selectedText = optionButtons[index].GetComponentInChildren<TMP_Text>();
            if (selectedText == null)
            {
                Debug.LogError("[SimularWord] 获取选项文本失败");
                return;
            }

            string selectedAnswer = selectedText.text;
            hasAnswered = true;

            Debug.Log($"[SimularWord] 选择了选项 {index + 1}: {selectedAnswer}");

            // 禁用所有按钮防止重复点击
            DisableAllButtons();

            if (IsNetworkMode())
            {
                HandleNetworkAnswer(selectedAnswer);
            }
            else
            {
                HandleLocalAnswer(selectedAnswer);
            }
        }

        /// <summary>
        /// 处理网络模式答案
        /// </summary>
        private void HandleNetworkAnswer(string answer)
        {
            Debug.Log($"[SimularWord] 网络模式提交答案: {answer}");

            // 显示提交状态
            if (feedbackText != null)
            {
                feedbackText.text = "已提交答案，等待服务器结果...";
                feedbackText.color = Color.yellow;
            }

            // 提交答案到服务器
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
            }
            else
            {
                Debug.LogError("[SimularWord] NetworkManager实例不存在，无法提交答案");
            }
        }

        /// <summary>
        /// 处理单机模式答案
        /// </summary>
        private void HandleLocalAnswer(string answer)
        {
            bool isCorrect = answer == correctOption;
            Debug.Log($"[SimularWord] 单机模式答题结果: {(isCorrect ? "正确" : "错误")}");

            // 显示结果并通知外层
            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// 禁用所有选项按钮
        /// </summary>
        private void DisableAllButtons()
        {
            if (optionButtons != null)
            {
                foreach (var btn in optionButtons)
                {
                    if (btn != null)
                        btn.interactable = false;
                }
            }
        }

        /// <summary>
        /// 重新启用所有选项按钮
        /// </summary>
        private void EnableAllButtons()
        {
            if (optionButtons != null)
            {
                foreach (var btn in optionButtons)
                {
                    if (btn != null && btn.gameObject.activeInHierarchy)
                        btn.interactable = true;
                }
            }
        }

        /// <summary>
        /// 显示反馈信息并通知结果
        /// </summary>
        private IEnumerator ShowFeedbackAndNotify(bool isCorrect)
        {
            // 显示反馈
            if (feedbackText != null)
            {
                feedbackText.color = isCorrect ? Color.green : Color.red;
                feedbackText.text = isCorrect ? "回答正确！" : $"回答错误，正确答案是：{correctOption}";
            }

            // 等待一段时间
            yield return new WaitForSeconds(1.5f);

            // 通知答题结果
            OnAnswerResult?.Invoke(isCorrect);

            // 为下一题准备
            EnableAllButtons();
        }

        /// <summary>
        /// 显示网络答题结果（由网络系统调用）
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[SimularWord] 收到网络结果: {(isCorrect ? "正确" : "错误")}");

            correctOption = correctAnswer;
            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            // 清理按钮事件监听
            if (optionButtons != null)
            {
                foreach (var btn in optionButtons)
                {
                    if (btn != null)
                        btn.onClick.RemoveAllListeners();
                }
            }
        }
    }
}