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
    /// ����ƴ���������
    /// - ֧�ֵ���������ģʽ
    /// - ����ģʽ�������ʣ������λ�֣�����ƴ������
    /// - ����ģʽ��ʹ�÷������ṩ����Ŀ����
    /// - ���� character.Tpinyin JSON ���ȶԣ��޵���Сд��
    /// - ����չʾ����ƴ��
    /// </summary>
    public class TextPinyinQuestionManager : NetworkQuestionManagerBase
    {
        private string dbPath;

        [Header("UI����")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/HardFillUI";

        [Header("����ģʽ����")]
        [Header("Ƶ�ʷ�Χ")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;

        [Header("UI�������")]
        private TMP_Text questionText;
        private TMP_InputField answerInput;
        private Button submitButton;
        private Button surrenderButton;
        private TMP_Text feedbackText;

        private string correctPinyinNoTone;     // �޵�ƴ�������ڱȶԣ�
        private string correctPinyinTone;       // ����ƴ�������ڷ�����
        private string currentWord;             // ��ǰ����
        private string targetCharacter;         // Ŀ���ַ�
        private bool hasAnswered = false;

        protected override void Awake()
        {
            base.Awake(); // ������������ʼ��
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
            Debug.Log($"[TextPinyin] Awake: dbPath={dbPath}");
        }

        private void Start()
        {
            Debug.Log("[TextPinyin] Start: ��ʼ�� UI");
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
            Debug.Log("[TextPinyin] LoadQuestion: ��ʼ��������");

            // 1. ���ѡ��
            currentWord = GetRandomWord();
            if (string.IsNullOrEmpty(currentWord))
            {
                DisplayErrorMessage("û�з���Ƶ�ʷ�Χ�Ĵʣ�");
                return;
            }

            Debug.Log($"[TextPinyin] Selected word={currentWord}");

            // 2. �����λ�ַ�
            int characterIndex = Random.Range(0, currentWord.Length);
            targetCharacter = currentWord.Substring(characterIndex, 1);
            Debug.Log($"[TextPinyin] Target char='{targetCharacter}', index={characterIndex}");

            // 3. ��ȡƴ������
            if (!GetPinyinData(targetCharacter, currentWord, characterIndex))
            {
                DisplayErrorMessage($"�ʿ����Ҳ��� '{targetCharacter}' ��ƴ����");
                return;
            }

            // 4. ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ����������Ŀ������ģʽ��
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[TextPinyin] ����������Ŀ");

            if (networkData == null)
            {
                Debug.LogError("������Ŀ����Ϊ��");
                DisplayErrorMessage("������Ŀ���ݴ���");
                return;
            }

            if (networkData.questionType != QuestionType.TextPinyin)
            {
                Debug.LogError($"��Ŀ���Ͳ�ƥ��: ����{QuestionType.TextPinyin}, ʵ��{networkData.questionType}");
                LoadLocalQuestion(); // ������������Ŀ
                return;
            }

            // ���������ݽ�����Ŀ��Ϣ
            ParseNetworkQuestionData(networkData);

            // ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ����������Ŀ����
        /// </summary>
        private void ParseNetworkQuestionData(NetworkQuestionData networkData)
        {
            correctPinyinNoTone = networkData.correctAnswer.ToLower();

            // ��additionalData�н���������Ϣ
            if (!string.IsNullOrEmpty(networkData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<TextPinyinAdditionalData>(networkData.additionalData);
                    currentWord = additionalInfo.word;
                    targetCharacter = additionalInfo.targetCharacter;
                    correctPinyinTone = additionalInfo.correctPinyinTone;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"�������總������ʧ��: {e.Message}");
                    // ����Ŀ�ı�����ȡ��Ϣ
                    ExtractInfoFromQuestionText(networkData.questionText);
                }
            }
            else
            {
                // ����Ŀ�ı�����ȡ��Ϣ
                ExtractInfoFromQuestionText(networkData.questionText);
            }

            Debug.Log($"[TextPinyin] ������Ŀ�������: word={currentWord}, target={targetCharacter}, correct={correctPinyinNoTone}");
        }

        /// <summary>
        /// ����Ŀ�ı�����ȡ��Ϣ�����÷�����
        /// </summary>
        private void ExtractInfoFromQuestionText(string questionText)
        {
            // �򵥵��ı���������ȡ�����Ŀ���ַ�
            var lines = questionText.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("��Ŀ��"))
                {
                    var wordStart = line.IndexOf("��Ŀ��") + 3;
                    var wordEnd = line.IndexOf("\n");
                    if (wordEnd == -1) wordEnd = line.Length;
                    currentWord = line.Substring(wordStart, wordEnd - wordStart).Trim();
                    break;
                }
            }

            // ���Դ�HTML�������ȡĿ���ַ�
            var colorTagStart = questionText.IndexOf("<color=red>");
            var colorTagEnd = questionText.IndexOf("</color>");
            if (colorTagStart != -1 && colorTagEnd != -1)
            {
                targetCharacter = questionText.Substring(colorTagStart + 11, colorTagEnd - colorTagStart - 11);
            }

            // ���û�д���ƴ��������Ϊ�𰸱���
            if (string.IsNullOrEmpty(correctPinyinTone))
                correctPinyinTone = correctPinyinNoTone;
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
                Debug.LogError($"���ݿ��ѯʧ��: {e.Message}");
            }

            return word;
        }

        /// <summary>
        /// ��ȡƴ������
        /// </summary>
        private bool GetPinyinData(string character, string word, int characterIndex)
        {
            try
            {
                // 1. ��ȡ�ַ����޵�ƴ��
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

                // ����JSON��ʽ��ƴ������
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

                // 2. ��ȡ����Ĵ���ƴ��
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

                // ��ȡĿ���ַ��Ĵ���ƴ��
                var toneParts = fullTonePinyin?.Trim().Split(' ');
                if (toneParts == null || characterIndex < 0 || characterIndex >= toneParts.Length)
                {
                    Debug.LogError($"[TextPinyin] toneParts.Length={toneParts?.Length}, index={characterIndex}");
                    correctPinyinTone = correctPinyinNoTone; // ����ʹ���޵�ƴ��
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
                Debug.LogError($"��ȡƴ������ʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ��ʾ��Ŀ
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(currentWord) || string.IsNullOrEmpty(targetCharacter))
            {
                DisplayErrorMessage("��Ŀ���ݴ���");
                return;
            }

            // ������ɣ�����Ŀ���ַ���
            questionText.text = $"��Ŀ��{currentWord}\n\"<color=red>{targetCharacter}</color>\" �Ķ����ǣ�";

            // ��������ͷ���
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.interactable = true;
            submitButton.interactable = true;
            surrenderButton.interactable = true;

            // ���������
            answerInput.ActivateInputField();

            Debug.Log($"[TextPinyin] ��Ŀ��ʾ���: {currentWord} -> {targetCharacter}");
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
            Debug.Log($"[TextPinyin] CheckAnswer: rawAnswer=\"{answer}\"");

            bool isCorrect = ValidatePinyinAnswer(answer);
            Debug.Log($"[TextPinyin] CheckAnswer: isCorrect={isCorrect}");

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// ��֤ƴ����
        /// </summary>
        private bool ValidatePinyinAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(correctPinyinNoTone))
                return false;

            // ����𰸣�ȥ�����š�תСд�����ݵͰ汾C#��
            var processedAnswer = answer.Replace("\"", "")
                                       .Replace("\u201c", "") // ��˫���� "
                                       .Replace("\u201d", "") // ��˫���� "
                                       .Trim()
                                       .ToLower();

            Debug.Log("[TextPinyin] CheckAnswer: processed=" + processedAnswer);

            return processedAnswer == correctPinyinNoTone;
        }

        /// <summary>
        /// �ύ��ť���
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
        /// �����س��ύ
        /// </summary>
        private void OnInputSubmit(string value)
        {
            if (!hasAnswered && !string.IsNullOrWhiteSpace(value))
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

            Debug.Log("[TextPinyin] Surrender clicked, treat as wrong answer");
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

            Debug.Log($"[TextPinyin] �ύ��: '{answer}'");

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
            Debug.Log($"[TextPinyin] ����ģʽ�ύ��: {answer}");

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
                Debug.Log("[TextPinyin] ShowFeedback: ��ȷ");
            }
            else
            {
                feedbackText.text = $"�ش������ȷƴ���ǣ�{correctPinyinTone}";
                feedbackText.color = Color.red;
                Debug.Log($"[TextPinyin] ShowFeedback: ������ȷƴ��={correctPinyinTone}");
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
            Debug.Log($"[TextPinyin] �յ�������: {(isCorrect ? "��ȷ" : "����")}");

            // ������ȷ�𰸣������������ṩ����ƴ����
            if (!string.IsNullOrEmpty(correctAnswer))
                correctPinyinTone = correctAnswer;

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
    /// ����ƴ���⸽�����ݽṹ���������紫�䣩
    /// </summary>
    [System.Serializable]
    public class TextPinyinAdditionalData
    {
        public string word;                 // ��������
        public string targetCharacter;      // Ŀ���ַ�
        public int characterIndex;          // �ַ�λ��
        public string correctPinyinTone;    // ����ƴ�������ڷ�����
        public string correctPinyinNoTone;  // �޵�ƴ�������ڱȶԣ�
    }
}