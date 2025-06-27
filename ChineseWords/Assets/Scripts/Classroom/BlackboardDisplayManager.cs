using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;
using Core.Network;
using System.Collections.Generic;

namespace UI.Blackboard
{
    /// <summary>
    /// �ڰ���ʾ������
    /// ������3D�����еĺڰ�����ʾ��Ŀ����
    /// </summary>
    public class BlackboardDisplayManager : MonoBehaviour
    {
        [Header("�ڰ�����")]
        [SerializeField] private Canvas blackboardCanvas;
        [SerializeField] private RectTransform blackboardArea;

        [Header("��ʾ����")]
        [SerializeField] private RectTransform questionArea;
        [SerializeField] private RectTransform optionsArea;
        [SerializeField] private RectTransform statusArea;

        [Header("UI���")]
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI[] optionTexts;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("��ʾ����")]
        [SerializeField] private float questionFontSize = 24f;
        [SerializeField] private float optionFontSize = 20f;
        [SerializeField] private Color questionColor = Color.black;
        [SerializeField] private Color optionColor = Color.black;

        public static BlackboardDisplayManager Instance { get; private set; }

        // ��ǰ��ʾ״̬
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
        /// ��ʼ���ڰ����
        /// </summary>
        private void InitializeBlackboard()
        {
            // ȷ��CanvasΪWorld Spaceģʽ
            if (blackboardCanvas != null)
            {
                blackboardCanvas.renderMode = RenderMode.WorldSpace;
            }

            // ��ʼ��ѡ���ı�����
            if (optionTexts == null || optionTexts.Length == 0)
            {
                SetupOptionTexts();
            }

            // ���ó�ʼ�����С����ɫ
            ApplyDisplaySettings();

            // ��ʼ�����ʾ
            ClearDisplay();
        }

        /// <summary>
        /// ����ѡ���ı����
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
                    // ��̬����ѡ���ı�
                    GameObject optionObj = new GameObject($"Option{i + 1}");
                    optionObj.transform.SetParent(optionsArea, false);
                    optionTexts[i] = optionObj.AddComponent<TextMeshProUGUI>();
                }
            }
        }

        /// <summary>
        /// Ӧ����ʾ����
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
        /// ��ʾ��Ŀ
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

            // ����������ʾ����
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

            // ��ʾ״̬
            if (statusText != null)
            {
                statusText.text = "������";
                statusText.color = Color.blue;
            }
        }

        /// <summary>
        /// ��ʾѡ����
        /// </summary>
        private void DisplayChoiceQuestion(NetworkQuestionData questionData)
        {
            // ��ʾ���
            if (questionText != null)
            {
                questionText.text = questionData.questionText;
            }

            // ��ʾѡ��
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

            // ���ز���Ҫ������
            SetAreaVisibility(true, true, false);
        }

        /// <summary>
        /// ��ʾ�����
        /// </summary>
        private void DisplayFillQuestion(NetworkQuestionData questionData)
        {
            // ��ʾ��ɣ����ܰ����»��߻������
            if (questionText != null)
            {
                questionText.text = questionData.questionText;
            }

            // ����ѡ������
            SetAreaVisibility(true, false, false);
        }

        /// <summary>
        /// ��ʾ�ж���
        /// </summary>
        private void DisplayTrueFalseQuestion(NetworkQuestionData questionData)
        {
            // ��ʾ���
            if (questionText != null)
            {
                questionText.text = questionData.questionText;
            }

            // ��ʾ��/��ѡ��
            if (optionTexts != null)
            {
                optionTexts[0].text = "A. ��ȷ";
                optionTexts[0].gameObject.SetActive(true);
                optionTexts[1].text = "B. ����";
                optionTexts[1].gameObject.SetActive(true);

                // ��������ѡ��
                for (int i = 2; i < optionTexts.Length; i++)
                {
                    optionTexts[i].gameObject.SetActive(false);
                }
            }

            SetAreaVisibility(true, true, false);
        }

        /// <summary>
        /// ��ʾͨ����Ŀ
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
        /// ��������ɼ���
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
        /// �����ʾ����
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
        /// ������ʾ״̬
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