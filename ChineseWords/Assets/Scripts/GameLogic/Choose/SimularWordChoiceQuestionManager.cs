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
    /// �����ѡ���������
    /// - ֧�ֵ���������ģʽ
    /// - ����ģʽ���� simular_usage_questions �����ȡһ����¼
    /// - ����ģʽ��ʹ�÷������ṩ����Ŀ����
    /// - �� stem ��ʾ���}�ɣ�True/1/2/3 �ĸ�ѡ��������ҷ������ť
    /// - ��ҵ����ť�ж�����ͨ�� OnAnswerResult ֪ͨ���
    /// </summary>
    public class SimularWordChoiceQuestionManager : NetworkQuestionManagerBase
    {
        private string dbPath;

        [Header("UI����")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/ChooseUI";

        [Header("UI�������")]
        private TMP_Text questionText;
        private Button[] optionButtons;
        private TMP_Text feedbackText;

        private string correctOption;
        private bool hasAnswered = false;

        protected override void Awake()
        {
            base.Awake(); // ������������ʼ��
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            InitializeUI();
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUI()
        {
            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"�޷�����UIԤ����: {uiPrefabPath}");
                return;
            }

            // ��ȡUI���
            questionText = ui.Find("QuestionText")?.GetComponent<TMP_Text>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || feedbackText == null)
            {
                Debug.LogError("UI�����ȡʧ�ܣ����Ԥ����ṹ");
                return;
            }

            // ��ʼ��ѡ�ť
            InitializeOptionButtons(ui);
        }

        /// <summary>
        /// ��ʼ��ѡ�ť
        /// </summary>
        private void InitializeOptionButtons(Transform ui)
        {
            optionButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var btnTransform = ui.Find($"OptionButton{i + 1}");
                if (btnTransform == null)
                {
                    Debug.LogError($"�Ҳ�����ť: OptionButton{i + 1}");
                    continue;
                }

                var btn = btnTransform.GetComponent<Button>();
                if (btn == null)
                {
                    Debug.LogError($"OptionButton{i + 1} û��Button���");
                    continue;
                }

                optionButtons[i] = btn;
                int index = i; // ����հ�����

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnOptionClicked(index));
            }
        }

        /// <summary>
        /// ���ر�����Ŀ������ģʽ��
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[SimularWord] ���ر�����Ŀ");

            string stem = null;
            List<string> choices = new List<string>(4);

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

                                // ��Ӵ���ѡ��
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
                Debug.LogError($"���ݿ��ѯʧ��: {e.Message}");
                DisplayErrorMessage("���ݿ�����޷�������Ŀ");
                return;
            }

            if (string.IsNullOrEmpty(stem) || choices.Count == 0)
            {
                DisplayErrorMessage("������Ŀ����");
                return;
            }

            // �����ȷѡ�����
            choices.Add(correctOption);
            ShuffleChoices(choices);

            // ��ʾ��Ŀ
            DisplayQuestion(stem, choices);
        }

        /// <summary>
        /// ����������Ŀ������ģʽ��
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[SimularWord] ����������Ŀ");

            if (networkData == null)
            {
                Debug.LogError("������Ŀ����Ϊ��");
                DisplayErrorMessage("������Ŀ���ݴ���");
                return;
            }

            if (networkData.questionType != QuestionType.SimularWordChoice)
            {
                Debug.LogError($"��Ŀ���Ͳ�ƥ��: ����{QuestionType.SimularWordChoice}, ʵ��{networkData.questionType}");
                LoadLocalQuestion(); // ������������Ŀ
                return;
            }

            correctOption = networkData.correctAnswer;

            // ����ģʽ��ѡ�����ɷ��������ң�ֱ����ʾ
            List<string> choices = new List<string>(networkData.options);
            DisplayQuestion(networkData.questionText, choices);
        }

        /// <summary>
        /// ��ʾ��Ŀ����
        /// </summary>
        private void DisplayQuestion(string stem, List<string> choices)
        {
            hasAnswered = false;

            // ��ʾ���
            questionText.text = stem;
            feedbackText.text = string.Empty;

            // ����ѡ�ť
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
                    // ���ض���İ�ť
                    optionButtons[i].gameObject.SetActive(false);
                }
            }

            Debug.Log($"��Ŀ��ʾ���: {stem}");
        }

        /// <summary>
        /// �������ѡ��
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
        /// ��ʾ������Ϣ
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            if (questionText != null)
                questionText.text = message;

            if (feedbackText != null)
                feedbackText.text = "";

            // ��������ѡ�ť
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
        /// ��鱾�ش𰸣�����ģʽ��
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            // ѡ����ͨ����ť��������˷�����ֱ��ʹ��
            Debug.Log("[SimularWord] CheckLocalAnswer �����ã���ѡ����ͨ����ť����");
        }

        /// <summary>
        /// ѡ�ť�������
        /// </summary>
        private void OnOptionClicked(int index)
        {
            if (hasAnswered)
            {
                Debug.Log("�Ѿ��ش���ˣ������ظ����");
                return;
            }

            if (index >= optionButtons.Length || optionButtons[index] == null)
            {
                Debug.LogError($"��Ч��ѡ������: {index}");
                return;
            }

            var selectedText = optionButtons[index].GetComponentInChildren<TMP_Text>();
            if (selectedText == null)
            {
                Debug.LogError("��ȡѡ���ı�ʧ��");
                return;
            }

            string selectedAnswer = selectedText.text;
            hasAnswered = true;

            Debug.Log($"[SimularWord] ѡ����ѡ�� {index + 1}: {selectedAnswer}");

            // �������а�ť��ֹ�ظ����
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
        /// ��������ģʽ��
        /// </summary>
        private void HandleNetworkAnswer(string answer)
        {
            Debug.Log($"[SimularWord] ����ģʽ�ύ��: {answer}");

            // ��ʾ�ύ״̬
            feedbackText.text = "���ύ�𰸣��ȴ����������...";
            feedbackText.color = Color.yellow;

            // ͨ�������ύ��������
            CheckAnswer(answer);
        }

        /// <summary>
        /// ������ģʽ��
        /// </summary>
        private void HandleLocalAnswer(string answer)
        {
            bool isCorrect = answer == correctOption;
            Debug.Log($"[SimularWord] ����ģʽ������: {(isCorrect ? "��ȷ" : "����")}");

            // ��ʾ�����֪ͨ���
            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// ��������ѡ�ť
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
        /// ������������ѡ�ť
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
        /// ��ʾ������Ϣ��֪ͨ���
        /// </summary>
        private IEnumerator ShowFeedbackAndNotify(bool isCorrect)
        {
            // ��ʾ����
            feedbackText.color = isCorrect ? Color.green : Color.red;
            feedbackText.text = isCorrect ? "�ش���ȷ��" : $"�ش������ȷ���ǣ�{correctOption}";

            // �ȴ�һ��ʱ��
            yield return new WaitForSeconds(1.5f);

            // ֪ͨ������
            OnAnswerResult?.Invoke(isCorrect);

            // Ϊ��һ��׼��
            EnableAllButtons();
        }

        /// <summary>
        /// ��ʾ�����������������ϵͳ���ã�
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[SimularWord] �յ�������: {(isCorrect ? "��ȷ" : "����")}");

            correctOption = correctAnswer;
            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// ������Դ
        /// </summary>
        private void OnDestroy()
        {
            // ����ť�¼�����
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