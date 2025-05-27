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
    /// - ʵ��IQuestionDataProvider�ӿڣ�֧��Host����
    /// - ����ģʽ���� simular_usage_questions �����ȡһ����¼
    /// - ����ģʽ��ʹ�÷������ṩ����Ŀ����
    /// - �� stem ��ʾ���}�ɣ�True/1/2/3 �ĸ�ѡ��������ҷ������ť
    /// - ��ҵ����ť�ж�����ͨ�� OnAnswerResult ֪ͨ���
    /// </summary>
    public class SimularWordChoiceQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
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

        // IQuestionDataProvider�ӿ�ʵ��
        public QuestionType QuestionType => QuestionType.SimularWordChoice;

        protected override void Awake()
        {
            base.Awake(); // ������������ʼ��
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            // ����Ƿ���ҪUI��Host����ģʽ���ܲ���ҪUI��
            if (NeedsUI())
            {
                InitializeUI();
            }
            else
            {
                Debug.Log("[SimularWord] Host����ģʽ������UI��ʼ��");
            }
        }

        /// <summary>
        /// ����Ƿ���ҪUI
        /// </summary>
        private bool NeedsUI()
        {
            // �����HostGameManager���Ӷ���˵�������ڳ������ʱ������������ҪUI
            // ���������QuestionDataService���Ӷ���Ҳ����ҪUI
            return transform.parent == null ||
                   (transform.parent.GetComponent<HostGameManager>() == null &&
                    transform.parent.GetComponent<QuestionDataService>() == null);
        }

        /// <summary>
        /// ��ȡ��Ŀ���ݣ�IQuestionDataProvider�ӿ�ʵ�֣�
        /// ר��ΪHost����ʹ�ã�����ʾUI
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            Debug.Log("[SimularWord] Host�����������");

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
                Debug.LogError($"Host�������ݿ��ѯʧ��: {e.Message}");
                return null;
            }

            if (string.IsNullOrEmpty(stem) || choices.Count == 0)
            {
                Debug.LogWarning("[SimularWord] Host���⣺������Ŀ����");
                return null;
            }

            // �������ѡ��
            ShuffleChoices(choices);

            // ����������Ŀ����
            var questionData = new NetworkQuestionData
            {
                questionType = QuestionType.SimularWordChoice,
                questionText = stem,
                correctAnswer = correctOption,
                options = choices.ToArray(),
                timeLimit = 30f,
                additionalData = "{\"source\": \"SimularWordChoiceQuestionManager\"}"
            };

            Debug.Log($"[SimularWord] Host����ɹ�: {stem}");
            return questionData;
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUI()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[SimularWord] UIManagerʵ��������");
                return;
            }

            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"[SimularWord] �޷�����UIԤ����: {uiPrefabPath}");
                return;
            }

            // ��ȡUI���
            questionText = ui.Find("QuestionText")?.GetComponent<TMP_Text>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || feedbackText == null)
            {
                Debug.LogError("[SimularWord] UI�����ȡʧ�ܣ����Ԥ����ṹ");
                return;
            }

            // ��ʼ��ѡ�ť
            InitializeOptionButtons(ui);

            Debug.Log("[SimularWord] UI��ʼ�����");
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

            // ʹ��GetQuestionData()�������ó����߼�
            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("������Ŀ����");
                return;
            }

            // ������ȷ��
            correctOption = questionData.correctAnswer;

            // ��ʾ��Ŀ
            List<string> choices = new List<string>(questionData.options);
            DisplayQuestion(questionData.questionText, choices);
        }

        /// <summary>
        /// ����������Ŀ������ģʽ��
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[SimularWord] ����������Ŀ");

            if (networkData == null)
            {
                Debug.LogError("[SimularWord] ������Ŀ����Ϊ��");
                DisplayErrorMessage("������Ŀ���ݴ���");
                return;
            }

            if (networkData.questionType != QuestionType.SimularWordChoice)
            {
                Debug.LogError($"[SimularWord] ��Ŀ���Ͳ�ƥ��: ����{QuestionType.SimularWordChoice}, ʵ��{networkData.questionType}");
                DisplayErrorMessage("��Ŀ���ʹ���");
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
            if (questionText == null || optionButtons == null)
            {
                Debug.LogError("[SimularWord] UI���δ��ʼ��");
                return;
            }

            hasAnswered = false;

            // ��ʾ���
            questionText.text = stem;
            if (feedbackText != null)
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

            Debug.Log($"[SimularWord] ��Ŀ��ʾ���: {stem}");
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
            Debug.LogWarning($"[SimularWord] {message}");

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
                Debug.Log("[SimularWord] �Ѿ��ش���ˣ������ظ����");
                return;
            }

            if (index >= optionButtons.Length || optionButtons[index] == null)
            {
                Debug.LogError($"[SimularWord] ��Ч��ѡ������: {index}");
                return;
            }

            var selectedText = optionButtons[index].GetComponentInChildren<TMP_Text>();
            if (selectedText == null)
            {
                Debug.LogError("[SimularWord] ��ȡѡ���ı�ʧ��");
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
            if (feedbackText != null)
            {
                feedbackText.text = "���ύ�𰸣��ȴ����������...";
                feedbackText.color = Color.yellow;
            }

            // �ύ�𰸵�������
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
            }
            else
            {
                Debug.LogError("[SimularWord] NetworkManagerʵ�������ڣ��޷��ύ��");
            }
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
            if (feedbackText != null)
            {
                feedbackText.color = isCorrect ? Color.green : Color.red;
                feedbackText.text = isCorrect ? "�ش���ȷ��" : $"�ش������ȷ���ǣ�{correctOption}";
            }

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