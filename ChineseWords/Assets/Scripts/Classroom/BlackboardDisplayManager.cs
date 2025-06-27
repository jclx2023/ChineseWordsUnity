using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;
using Core.Network;
using System.Collections.Generic;

namespace UI.Blackboard
{
    /// <summary>
    /// 黑板显示管理器
    /// 负责在3D场景中的黑板上显示题目内容
    /// </summary>
    public class BlackboardDisplayManager : MonoBehaviour
    {
        [Header("黑板配置")]
        [SerializeField] private Canvas blackboardCanvas;
        [SerializeField] private RectTransform blackboardArea;

        [Header("显示区域")]
        [SerializeField] private RectTransform questionArea;
        [SerializeField] private RectTransform optionsArea;
        [SerializeField] private RectTransform statusArea;

        [Header("UI组件")]
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI[] optionTexts;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("显示配置")]
        [SerializeField] private float questionFontSize = 24f;
        [SerializeField] private float optionFontSize = 20f;
        [SerializeField] private Color questionColor = Color.black;
        [SerializeField] private Color optionColor = Color.black;

        public static BlackboardDisplayManager Instance { get; private set; }

        // 当前显示状态
        private NetworkQuestionData currentQuestion;
        private bool isDisplaying = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeBlackboard();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 初始化黑板组件
        /// </summary>
        private void InitializeBlackboard()
        {
            // 确保Canvas为World Space模式
            if (blackboardCanvas != null)
            {
                blackboardCanvas.renderMode = RenderMode.WorldSpace;
            }

            // 初始化选项文本数组
            if (optionTexts == null || optionTexts.Length == 0)
            {
                SetupOptionTexts();
            }

            // 设置初始字体大小和颜色
            ApplyDisplaySettings();

            // 初始清空显示
            ClearDisplay();
        }

        /// <summary>
        /// 设置选项文本组件
        /// </summary>
        private void SetupOptionTexts()
        {
            if (optionsArea == null) return;

            var existingTexts = optionsArea.GetComponentsInChildren<TextMeshProUGUI>();
            optionTexts = new TextMeshProUGUI[4];

            for (int i = 0; i < 4; i++)
            {
                if (i < existingTexts.Length)
                {
                    optionTexts[i] = existingTexts[i];
                }
                else
                {
                    // 动态创建选项文本
                    GameObject optionObj = new GameObject($"Option{i + 1}");
                    optionObj.transform.SetParent(optionsArea, false);
                    optionTexts[i] = optionObj.AddComponent<TextMeshProUGUI>();
                }
            }
        }

        /// <summary>
        /// 应用显示设置
        /// </summary>
        private void ApplyDisplaySettings()
        {
            if (questionText != null)
            {
                questionText.fontSize = questionFontSize;
                questionText.color = questionColor;
            }

            if (optionTexts != null)
            {
                foreach (var optionText in optionTexts)
                {
                    if (optionText != null)
                    {
                        optionText.fontSize = optionFontSize;
                        optionText.color = optionColor;
                    }
                }
            }
        }

        /// <summary>
        /// 显示题目
        /// </summary>
        public void DisplayQuestion(NetworkQuestionData questionData)
        {
            if (questionData == null)
            {
                ClearDisplay();
                return;
            }

            currentQuestion = questionData;
            isDisplaying = true;

            // 根据题型显示内容
            switch (questionData.questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                    DisplayChoiceQuestion(questionData);
                    break;

                case QuestionType.HardFill:
                case QuestionType.SoftFill:
                case QuestionType.IdiomChain:
                case QuestionType.TextPinyin:
                    DisplayFillQuestion(questionData);
                    break;

                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    DisplayTrueFalseQuestion(questionData);
                    break;

                default:
                    DisplayGenericQuestion(questionData);
                    break;
            }

            // 显示状态
            if (statusText != null)
            {
                statusText.text = "请作答";
                statusText.color = Color.blue;
            }
        }

        /// <summary>
        /// 显示选择题
        /// </summary>
        private void DisplayChoiceQuestion(NetworkQuestionData questionData)
        {
            // 显示题干
            if (questionText != null)
            {
                questionText.text = questionData.questionText;
            }

            // 显示选项
            if (optionTexts != null && questionData.options != null)
            {
                for (int i = 0; i < optionTexts.Length; i++)
                {
                    if (i < questionData.options.Length)
                    {
                        optionTexts[i].text = $"{(char)('A' + i)}. {questionData.options[i]}";
                        optionTexts[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        optionTexts[i].gameObject.SetActive(false);
                    }
                }
            }

            // 隐藏不需要的区域
            SetAreaVisibility(true, true, false);
        }

        /// <summary>
        /// 显示填空题
        /// </summary>
        private void DisplayFillQuestion(NetworkQuestionData questionData)
        {
            // 显示题干（可能包含下划线或高亮）
            if (questionText != null)
            {
                questionText.text = questionData.questionText;
            }

            // 隐藏选项区域
            SetAreaVisibility(true, false, false);
        }

        /// <summary>
        /// 显示判断题
        /// </summary>
        private void DisplayTrueFalseQuestion(NetworkQuestionData questionData)
        {
            // 显示题干
            if (questionText != null)
            {
                questionText.text = questionData.questionText;
            }

            // 显示是/否选项
            if (optionTexts != null)
            {
                optionTexts[0].text = "A. 正确";
                optionTexts[0].gameObject.SetActive(true);
                optionTexts[1].text = "B. 错误";
                optionTexts[1].gameObject.SetActive(true);

                // 隐藏其他选项
                for (int i = 2; i < optionTexts.Length; i++)
                {
                    optionTexts[i].gameObject.SetActive(false);
                }
            }

            SetAreaVisibility(true, true, false);
        }

        /// <summary>
        /// 显示通用题目
        /// </summary>
        private void DisplayGenericQuestion(NetworkQuestionData questionData)
        {
            if (questionText != null)
            {
                questionText.text = questionData.questionText;
            }

            SetAreaVisibility(true, false, false);
        }

        /// <summary>
        /// 设置区域可见性
        /// </summary>
        private void SetAreaVisibility(bool showQuestion, bool showOptions, bool showStatus)
        {
            if (questionArea != null)
                questionArea.gameObject.SetActive(showQuestion);

            if (optionsArea != null)
                optionsArea.gameObject.SetActive(showOptions);

            if (statusArea != null)
                statusArea.gameObject.SetActive(showStatus);
        }

        /// <summary>
        /// 清空显示内容
        /// </summary>
        public void ClearDisplay()
        {
            currentQuestion = null;
            isDisplaying = false;

            if (questionText != null)
            {
                questionText.text = "";
            }

            if (optionTexts != null)
            {
                foreach (var optionText in optionTexts)
                {
                    if (optionText != null)
                    {
                        optionText.text = "";
                        optionText.gameObject.SetActive(false);
                    }
                }
            }

            if (statusText != null)
            {
                statusText.text = "";
            }

            SetAreaVisibility(false, false, false);
        }

        /// <summary>
        /// 更新显示状态
        /// </summary>
        public void UpdateDisplayStatus(string status, Color color)
        {
            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = color;
                statusArea.gameObject.SetActive(true);
            }
        }
    }
}