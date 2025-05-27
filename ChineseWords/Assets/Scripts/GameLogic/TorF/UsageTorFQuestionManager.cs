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
    /// ����/����ʹ���ж��������
    /// - ֧�ֵ���������ģʽ
    /// - ʵ��IQuestionDataProvider�ӿڣ�֧��Host����
    /// - ����ģʽ���� simular_usage_questions �����ȡһ����¼
    /// - ����ģʽ��ʹ�÷������ṩ����Ŀ����
    /// - �������չʾ��ȷʾ�������ʾ��
    /// - ���滻���ı����뵽�»���λ�ò�����
    /// - ���ѡ��"��ȷ"/"����"�ж�
    /// </summary>
    public class UsageTorFQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        private string dbPath;

        [Header("UI����")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/TorFUI";

        [Header("UI�������")]
        private TMP_Text questionText;
        private Button trueButton;
        private Button falseButton;
        private TMP_Text feedbackText;

        private TMP_Text textTrue;
        private TMP_Text textFalse;

        private bool isInstanceCorrect;     // ��ǰʵ���Ƿ���ȷ
        private string currentStem;         // ��ǰ���
        private string correctFill;         // ��ȷ���
        private string currentFill;         // ��ǰʹ�õ����
        private bool hasAnswered = false;

        // IQuestionDataProvider�ӿ�ʵ��
        public QuestionType QuestionType => QuestionType.UsageTorF;

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
                Debug.Log("[UsageTorF] Host����ģʽ������UI��ʼ��");
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
            Debug.Log("[UsageTorF] Host�����������");

            // 1. �����ݿ��ȡʹ���ж�������
            var usageData = GetRandomUsageData();
            if (usageData == null)
            {
                Debug.LogWarning("[UsageTorF] Host���⣺������Ŀ����");
                return null;
            }

            // 2. �������չʾ��ȷ���Ǵ���ʾ��
            bool isInstanceCorrect = Random.value < 0.5f;
            string currentFill = isInstanceCorrect ?
                usageData.correctFill :
                usageData.wrongFills[Random.Range(0, usageData.wrongFills.Count)];

            // 3. ������ʾ�ı�
            string questionText = ReplaceUnderscoreWithHighlight(usageData.stem, currentFill);

            // 4. ������������
            var additionalData = new UsageTorFAdditionalData
            {
                stem = usageData.stem,
                correctFill = usageData.correctFill,
                currentFill = currentFill,
                isInstanceCorrect = isInstanceCorrect,
                wrongFills = usageData.wrongFills.ToArray()
            };

            // 5. ����������Ŀ����
            var networkQuestionData = new NetworkQuestionData
            {
                questionType = QuestionType.UsageTorF,
                questionText = questionText,
                correctAnswer = isInstanceCorrect.ToString().ToLower(), // "true" �� "false"
                options = new string[] { "��ȷ", "����" }, // �̶�������ѡ��
                timeLimit = 30f,
                additionalData = JsonUtility.ToJson(additionalData)
            };

            Debug.Log($"[UsageTorF] Host����ɹ�: {currentFill} (ʵ��{(isInstanceCorrect ? "��ȷ" : "����")})");
            return networkQuestionData;
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUI()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[UsageTorF] UIManagerʵ��������");
                return;
            }

            var ui = UIManager.Instance.LoadUI(uiPrefabPath);
            if (ui == null)
            {
                Debug.LogError($"[UsageTorF] �޷�����UIԤ����: {uiPrefabPath}");
                return;
            }

            // ��ȡUI���
            questionText = ui.Find("QuestionText")?.GetComponent<TMP_Text>();
            trueButton = ui.Find("TrueButton")?.GetComponent<Button>();
            falseButton = ui.Find("FalseButton")?.GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText")?.GetComponent<TMP_Text>();

            if (questionText == null || trueButton == null || falseButton == null || feedbackText == null)
            {
                Debug.LogError("[UsageTorF] UI�����ȡʧ�ܣ����Ԥ����ṹ");
                return;
            }

            // ��ȡ��ť�ı����
            textTrue = trueButton.GetComponentInChildren<TMP_Text>();
            textFalse = falseButton.GetComponentInChildren<TMP_Text>();

            if (textTrue == null || textFalse == null)
            {
                Debug.LogError("[UsageTorF] ��ť�ı������ȡʧ��");
                return;
            }

            // �󶨰�ť�¼�
            trueButton.onClick.RemoveAllListeners();
            falseButton.onClick.RemoveAllListeners();
            trueButton.onClick.AddListener(() => OnSelectAnswer(true));
            falseButton.onClick.AddListener(() => OnSelectAnswer(false));

            feedbackText.text = string.Empty;

            Debug.Log("[UsageTorF] UI��ʼ�����");
        }

        /// <summary>
        /// ���ر�����Ŀ������ģʽ��
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            Debug.Log("[UsageTorF] ���ر�����Ŀ");

            // ʹ��GetQuestionData()�������ó����߼�
            var questionData = GetQuestionData();
            if (questionData == null)
            {
                DisplayErrorMessage("������Ŀ����");
                return;
            }

            // ������Ŀ����
            isInstanceCorrect = questionData.correctAnswer.ToLower() == "true";

            // �Ӹ��������н�����ϸ��Ϣ
            if (!string.IsNullOrEmpty(questionData.additionalData))
            {
                try
                {
                    var additionalInfo = JsonUtility.FromJson<UsageTorFAdditionalData>(questionData.additionalData);
                    currentStem = additionalInfo.stem;
                    correctFill = additionalInfo.correctFill;
                    currentFill = additionalInfo.currentFill;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"������������ʧ��: {e.Message}������������Ŀ");
                    LoadOriginalLocalQuestion();
                    return;
                }
            }
            else
            {
                LoadOriginalLocalQuestion();
                return;
            }

            // ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ԭʼ�ı�����Ŀ�����߼������ã�
        /// </summary>
        private void LoadOriginalLocalQuestion()
        {
            // 1. �����ݿ��ȡʹ���ж�������
            var usageData = GetRandomUsageData();
            if (usageData == null)
            {
                DisplayErrorMessage("������Ŀ����");
                return;
            }

            currentStem = usageData.stem;
            correctFill = usageData.correctFill;

            // 2. �������չʾ��ȷ���Ǵ���ʾ��
            isInstanceCorrect = Random.value < 0.5f;
            currentFill = isInstanceCorrect ?
                correctFill :
                usageData.wrongFills[Random.Range(0, usageData.wrongFills.Count)];

            // 3. ��ʾ��Ŀ
            DisplayQuestion();
        }

        /// <summary>
        /// ����������Ŀ������ģʽ��
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log("[UsageTorF] ����������Ŀ");

            if (networkData == null)
            {
                Debug.LogError("[UsageTorF] ������Ŀ����Ϊ��");
                DisplayErrorMessage("������Ŀ���ݴ���");
                return;
            }

            if (networkData.questionType != QuestionType.UsageTorF)
            {
                Debug.LogError($"[UsageTorF] ��Ŀ���Ͳ�ƥ��: ����{QuestionType.UsageTorF}, ʵ��{networkData.questionType}");
                DisplayErrorMessage("��Ŀ���ʹ���");
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
            // ������ȷ�𰸣�true/false��
            isInstanceCorrect = networkData.correctAnswer.ToLower() == "true";

            // ��additionalData�н���������Ϣ
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
                    Debug.LogWarning($"�������總������ʧ��: {e.Message}");
                    // ����Ŀ�ı�����
                    ExtractInfoFromQuestionText(networkData.questionText);
                }
            }
            else
            {
                // ����Ŀ�ı�����
                ExtractInfoFromQuestionText(networkData.questionText);
            }

            Debug.Log($"[UsageTorF] ������Ŀ�������: isCorrect={isInstanceCorrect}, fill={currentFill}");
        }

        /// <summary>
        /// ����Ŀ�ı�����ȡ��Ϣ
        /// </summary>
        private void ExtractInfoFromQuestionText(string questionText)
        {
            // ���ԴӸ�����ǩ����ȡ��ǰ�������
            var colorTagStart = questionText.IndexOf("<color=red>");
            var colorTagEnd = questionText.IndexOf("</color>");

            if (colorTagStart != -1 && colorTagEnd != -1)
            {
                colorTagStart += "<color=red>".Length;
                currentFill = questionText.Substring(colorTagStart, colorTagEnd - colorTagStart);

                // �ع�ԭʼ��ɣ������������滻Ϊ�»��ߣ�
                var before = questionText.Substring(0, colorTagStart - "<color=red>".Length);
                var after = questionText.Substring(colorTagEnd + "</color>".Length);
                currentStem = before + new string('_', currentFill.Length) + after;
            }
            else
            {
                // ���û�и�����ǩ��ֱ��ʹ����Ŀ�ı�
                currentStem = questionText;
                currentFill = "δ֪";
            }

            // ������ȷ���Ϊ��ǰ��գ�����ģʽ���޷�ȷ����
            correctFill = currentFill;
        }

        /// <summary>
        /// ��ȡ���ʹ���ж�������
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
                Debug.LogError($"��ȡʹ���ж�������ʧ��: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// ��ʾ��Ŀ
        /// </summary>
        private void DisplayQuestion()
        {
            hasAnswered = false;

            if (string.IsNullOrEmpty(currentStem) || string.IsNullOrEmpty(currentFill))
            {
                DisplayErrorMessage("��Ŀ���ݴ���");
                return;
            }

            // ������ɣ��滻��һ���»��߶β������������
            string displayText = ReplaceUnderscoreWithHighlight(currentStem, currentFill);
            questionText.text = displayText;
            feedbackText.text = string.Empty;

            // ���ð�ť�ı�
            textTrue.text = "��ȷ";
            textFalse.text = "����";

            // ���ð�ť
            trueButton.interactable = true;
            falseButton.interactable = true;

            Debug.Log($"[UsageTorF] ��Ŀ��ʾ���: {displayText} (ʵ��{(isInstanceCorrect ? "��ȷ" : "����")})");
        }

        /// <summary>
        /// ������е��»����滻Ϊ�������������
        /// </summary>
        private string ReplaceUnderscoreWithHighlight(string stem, string fill)
        {
            int start = stem.IndexOf('_');
            if (start >= 0)
            {
                // ���������»��߳���
                int end = start;
                while (end < stem.Length && stem[end] == '_')
                    end++;

                // ƴ���滻��ǰ���� + ������� + �󲿷�
                string before = stem.Substring(0, start);
                string after = stem.Substring(end);
                return before + $"<color=red>{fill}</color>" + after;
            }
            else
            {
                // û���»�����ֱ����ʾԭ��
                return stem;
            }
        }

        /// <summary>
        /// ��ʾ������Ϣ
        /// </summary>
        private void DisplayErrorMessage(string message)
        {
            Debug.LogWarning($"[UsageTorF] {message}");

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
        /// ��鱾�ش𰸣�����ģʽ��
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            // �ж���ͨ����ť��������˷�����ֱ��ʹ��
            Debug.Log("[UsageTorF] CheckLocalAnswer �����ã����ж���ͨ����ť����");
        }

        /// <summary>
        /// ѡ��𰸴���
        /// </summary>
        private void OnSelectAnswer(bool selectedTrue)
        {
            if (hasAnswered)
            {
                Debug.Log("[UsageTorF] �Ѿ��ش���ˣ������ظ����");
                return;
            }

            hasAnswered = true;

            // ���ð�ť��ֹ�ظ����
            trueButton.interactable = false;
            falseButton.interactable = false;

            Debug.Log($"[UsageTorF] ѡ���˴�: {(selectedTrue ? "��ȷ" : "����")}");

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
        /// ��������ģʽ��
        /// </summary>
        private void HandleNetworkAnswer(string answer)
        {
            Debug.Log($"[UsageTorF] ����ģʽ�ύ��: {answer}");

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
                Debug.LogError("[UsageTorF] NetworkManagerʵ�������ڣ��޷��ύ��");
            }
        }

        /// <summary>
        /// ������ģʽ��
        /// </summary>
        private void HandleLocalAnswer(bool selectedTrue)
        {
            // �ж��߼���ѡ��"��ȷ"��ʵ����ȷ����ѡ��"����"��ʵ������
            bool isCorrect = (selectedTrue && isInstanceCorrect) || (!selectedTrue && !isInstanceCorrect);

            Debug.Log($"[UsageTorF] ����ģʽ������: {(isCorrect ? "��ȷ" : "����")}");

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
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
                feedbackText.text = isCorrect ? "�ش���ȷ��" : "�ش����";
            }

            // �ȴ�һ��ʱ��
            yield return new WaitForSeconds(1.5f);

            // ֪ͨ������
            OnAnswerResult?.Invoke(isCorrect);

            // �������ð�ťΪ��һ��׼��
            if (trueButton != null)
                trueButton.interactable = true;
            if (falseButton != null)
                falseButton.interactable = true;
        }

        /// <summary>
        /// ��ʾ�����������������ϵͳ���ã�
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            Debug.Log($"[UsageTorF] �յ�������: {(isCorrect ? "��ȷ" : "����")}");

            StartCoroutine(ShowFeedbackAndNotify(isCorrect));
        }

        /// <summary>
        /// ������Դ
        /// </summary>
        private void OnDestroy()
        {
            // ����ť�¼�����
            if (trueButton != null)
                trueButton.onClick.RemoveAllListeners();
            if (falseButton != null)
                falseButton.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// ʹ���ж������ݽṹ
    /// </summary>
    public class UsageData
    {
        public string stem;                 // ��ɣ������»��ߣ�
        public string correctFill;          // ��ȷ���
        public List<string> wrongFills;     // �������ѡ��
    }

    /// <summary>
    /// ʹ���ж��⸽�����ݽṹ���������紫�䣩
    /// </summary>
    [System.Serializable]
    public class UsageTorFAdditionalData
    {
        public string stem;                 // ԭʼ��ɣ����»��ߣ�
        public string correctFill;          // ��ȷ�������
        public string currentFill;          // ��ǰʹ�õ��������
        public bool isInstanceCorrect;      // ��ǰʵ���Ƿ���ȷ
        public string[] wrongFills;         // ����ѡ���б�
    }
}