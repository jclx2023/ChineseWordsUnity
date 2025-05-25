using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// ���Ե��ʲ�ȫ�������
    /// - ֧�ֵ���������ģʽ
    /// - ����ģʽ�������ʣ�����ͨ���ģʽ (*���ⳤ�ȣ�_�����ַ�)
    /// - ����ģʽ��ʹ�÷������ṩ����Ŀ���ݺ�ģʽ
    /// - �������𰸣�ʹ��������ʽƥ��ģʽ������֤�ʿ������
    /// </summary>
    public class SoftFillQuestionManager : NetworkQuestionManagerBase
    {
        private string dbPath;

        [Header("UI����")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/HardFillUI";

        [Header("����ģʽ����")]
        [Header("Freq��Χ")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;

        [Header("��֪������ (0 < x < L)")]
        [SerializeField] private int revealCount = 2;

        [Header("UI�������")]
        private TMP_Text questionText;
        private TMP_InputField answerInput;
        private Button submitButton;
        private Button surrenderButton;
        private TMP_Text feedbackText;

        private string currentWord;
        private string stemPattern;
        private Regex matchRegex;
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
            answerInput = ui.Find("AnswerInput")?.GetComponent<TMP_InputField>();
            submitButton = ui.Find("SubmitButton")?.GetComponent<Button>();
            surrenderButton = ui.Find("SurrenderButton")?.GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || answerInput == null || submitButton == null ||
                surrenderButton == null || feedbackText == null)
            {
                Debug.LogError("UI�����ȡʧ�ܣ����Ԥ����ṹ");
                return;
            }

            // �󶨰�ť�¼�
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);

            surrenderButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.AddListener(OnSurrender);

            // �������س��¼�
            answerInput.onSubmit.RemoveAllListeners();
            answerInput.onSubmit.AddListener(OnInputSubmit);

            feedbackText.text = string.Empty;
        }

        /// <summary>
        /// ���ر�����Ŀ������ģʽ��
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[SoftFill] ���ر�����Ŀ");

            // 1. ������
            currentWord = GetRandomWord();

            if (string.IsNullOrEmpty(currentWord))
            {
                DisplayErrorMessage("û�з��������Ĵ�����");
                return;
            }

            // 2. ����ͨ���ģʽ
            GenerateWildcardPattern();

            // 3. ����������ʽ
            CreateMatchRegex();

            // 4. ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ����������Ŀ������ģʽ��
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[SoftFill] ����������Ŀ");

            if (networkData == null)
            {
                Debug.LogError("������Ŀ����Ϊ��");
                DisplayErrorMessage("������Ŀ���ݴ���");
                return;
            }

            if (networkData.questionType != QuestionType.SoftFill)
            {
                Debug.LogError($"��Ŀ���Ͳ�ƥ��: ����{QuestionType.SoftFill}, ʵ��{networkData.questionType}");
                LoadLocalQuestion(); // ������������Ŀ
                return;
            }

            // ���������ݽ�����Ŀ��Ϣ
            ParseNetworkQuestionData(networkData);

            // ����������ʽ
            CreateMatchRegex();

            // ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ����������Ŀ����
        /// </summary>
        private void ParseNetworkQuestionData(NetworkQuestionData networkData)
        {
            currentWord = networkData.correctAnswer;

            // ��additionalData�н���ͨ���ģʽ
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<SoftFillAdditionalData>(networkData.additionalData);
                    stemPattern = additionalInfo.stemPattern;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"�������總������ʧ��: {e.Message}������Ŀ�ı�����");
                    ExtractPatternFromQuestionText(networkData.questionText);
                }
            }
            else
            {
                // ���û�и������ݣ���questionText�н���
                ExtractPatternFromQuestionText(networkData.questionText);
            }
        }

        /// <summary>
        /// ����Ŀ�ı�����ȡͨ���ģʽ
        /// </summary>
        private void ExtractPatternFromQuestionText(string questionText)
        {
            // ���Դ���Ŀ�ı�����ȡģʽ���򵥵��ı�������
            var match = Regex.Match(questionText, @"([*_\u4e00-\u9fa5]+)");
            if (match.Success)
            {
                stemPattern = match.Groups[1].Value;
            }
            else
            {
                Debug.LogWarning("�޷�����Ŀ�ı�����ģʽ��ʹ��Ĭ��ģʽ");
                GenerateWildcardPattern();
            }
        }

        /// <summary>
        /// �����ݿ������ȡ����
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
                            SELECT word FROM (
                                SELECT word, Freq FROM word
                                UNION ALL
                                SELECT word, Freq FROM idiom
                            ) WHERE Freq BETWEEN @min AND @max
                            ORDER BY RANDOM() LIMIT 1";
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
                Debug.LogError($"���ݿ��ѯʧ��: {e.Message}");
            }

            return word;
        }

        /// <summary>
        /// ����ͨ���ģʽ
        /// </summary>
        private void GenerateWildcardPattern()
        {
            if (string.IsNullOrEmpty(currentWord))
                return;

            int wordLength = currentWord.Length;
            int x = Mathf.Clamp(revealCount, 1, wordLength - 1);

            // ���ѡ��Ҫ��ʾ���ַ�λ��
            var indices = Enumerable.Range(0, wordLength).ToArray();

            // Fisher-Yates ϴ���㷨
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }

            var selectedIndices = indices.Take(x).OrderBy(i => i).ToArray();

            // ����ͨ������
            var sb = new System.Text.StringBuilder();
            sb.Append("*");
            sb.Append(currentWord[selectedIndices[0]]);

            for (int k = 1; k < selectedIndices.Length; k++)
            {
                int gap = selectedIndices[k] - selectedIndices[k - 1] - 1;
                sb.Append(new string('_', gap));
                sb.Append(currentWord[selectedIndices[k]]);
            }
            sb.Append("*");

            stemPattern = sb.ToString();
        }

        /// <summary>
        /// ����ƥ���������ʽ
        /// </summary>
        private void CreateMatchRegex()
        {
            if (string.IsNullOrEmpty(stemPattern))
                return;

            try
            {
                // ����������ʽģʽ
                // * -> .*, _ -> .
                var pattern = "^" + string.Concat(stemPattern.Select(c =>
                {
                    if (c == '*') return ".*";
                    if (c == '_') return ".";
                    return Regex.Escape(c.ToString());
                })) + "$";

                matchRegex = new Regex(pattern);
                Debug.Log($"[SoftFill] ����������ʽ: {pattern}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"����������ʽʧ��: {e.Message}");
                matchRegex = null;
            }
        }

        /// <summary>
        /// ��ʾ��Ŀ
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(stemPattern))
            {
                DisplayErrorMessage("��Ŀģʽ����ʧ��");
                return;
            }

            questionText.text = $"��Ŀ��������һ������<color=red>{stemPattern}</color>��ʽ�ĵ���\nHint��*Ϊ������֣�_Ϊ������";

            // ��������ͷ���
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.interactable = true;
            submitButton.interactable = true;
            surrenderButton.interactable = true;

            // ���������
            answerInput.ActivateInputField();

            Debug.Log($"[SoftFill] ��Ŀ��ʾ��ɣ�ģʽ: {stemPattern} (ʾ����: {currentWord})");
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
        /// ��鱾�ش𰸣�����ģʽ��
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            Debug.Log($"[SoftFill] ��鱾�ش�: {answer}");

            bool isCorrect = ValidateAnswer(answer.Trim());
            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// ��֤��
        /// </summary>
        private bool ValidateAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return false;

            // 1. ����Ƿ�ƥ��ͨ���ģʽ
            if (matchRegex == null || !matchRegex.IsMatch(answer))
                return false;

            // 2. �����Ƿ�����ڴʿ���
            return IsWordInDatabase(answer);
        }

        /// <summary>
        /// �����Ƿ������ݿ���
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
                Debug.LogError($"���ݿ��ѯʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// �ύ��ť���
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
        /// �����س��ύ
        /// </summary>
        private void OnInputSubmit(string value)
        {
            if (!hasAnswered && !string.IsNullOrEmpty(value.Trim()))
            {
                SubmitAnswer(value.Trim());
            }
        }

        /// <summary>
        /// Ͷ����ť���
        /// </summary>
        private void OnSurrender()
        {
            if (hasAnswered)
                return;

            Debug.Log("[SoftFill] ���Ͷ��");
            SubmitAnswer(""); // �ύ�մ𰸱�ʾͶ��
        }

        /// <summary>
        /// �ύ��
        /// </summary>
        private void SubmitAnswer(string answer)
        {
            if (hasAnswered)
                return;

            hasAnswered = true;
            StopAllCoroutines();

            // ���ý���
            answerInput.interactable = false;
            submitButton.interactable = false;
            surrenderButton.interactable = false;

            Debug.Log($"[SoftFill] �ύ��: '{answer}'");

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
        /// ��������ģʽ��
        /// </summary>
        private void HandleNetworkAnswer(string answer)
        {
            Debug.Log($"[SoftFill] ����ģʽ�ύ��: {answer}");

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
            CheckLocalAnswer(answer);
        }

        /// <summary>
        /// ��ʾ������Ϣ��֪ͨ���
        /// </summary>
        private IEnumerator ShowFeedbackAndNotify(bool isCorrect)
        {
            // ��ʾ����
            if (isCorrect)
            {
                feedbackText.text = "�ش���ȷ��";
                feedbackText.color = Color.green;
            }
            else
            {
                feedbackText.text = $"�ش���󣬿ɽ���ʾ����{currentWord}";
                feedbackText.color = Color.red;
            }

            // �ȴ�һ��ʱ��
            yield return new WaitForSeconds(1.5f);

            // ֪ͨ������
            OnAnswerResult?.Invoke(isCorrect);
        }

        /// <summary>
        /// ��ʾ�����������������ϵͳ���ã�
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[SoftFill] �յ�������: {(isCorrect ? "��ȷ" : "����")}");

            currentWord = correctAnswer;
            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// ������Դ
        /// </summary>
        private void OnDestroy()
        {
            // �����¼�����
            if (submitButton != null)
                submitButton.onClick.RemoveAllListeners();
            if (surrenderButton != null)
                surrenderButton.onClick.RemoveAllListeners();
            if (answerInput != null)
                answerInput.onSubmit.RemoveAllListeners();
        }
    }

    /// <summary>
    /// ������⸽�����ݽṹ���������紫�䣩
    /// </summary>
    [System.Serializable]
    public class SoftFillAdditionalData
    {
        public string stemPattern;      // ͨ���ģʽ (�� "*��_��*")
        public int[] revealIndices;     // ��ʾ���ַ�λ��
        public string regexPattern;     // ������ʽģʽ
    }
}