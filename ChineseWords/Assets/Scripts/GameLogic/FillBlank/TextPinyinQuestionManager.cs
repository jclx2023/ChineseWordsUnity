using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Mono.Data.Sqlite;
using System.Data;
using Core;

namespace GameLogic
{
    /// <summary>
    /// ����ƴ�����������
    /// - ������
    /// - �����λ��
    /// - ���� character.Tpinyin JSON ���ȶԣ��޵���Сд��
    /// - ����չʾ word.pinyin��������
    /// - Ͷ����Ϊ����𰸣�ͨ�� OnAnswerResult ������һ��
    /// - Debug.Log ����
    /// </summary>
    public class TextPinyinQuestionManager : QuestionManagerBase
    {
        [Header("���ݿ�·����StreamingAssets/dictionary.db)")]
        private string dbPath;

        [Header("Ƶ�ʷ�Χ")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;

        [Header("UI ���")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_InputField answerInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private Button surrenderButton;

        private string correctPinyinNoTone;
        private string correctPinyinTone;

        private void Awake()
        {
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
            Debug.Log($"[TextPinyin] Awake: dbPath={dbPath}");
        }

        private void Start()
        {
            Debug.Log("[TextPinyin] Start: ��ʼ�� UI");
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/HardFillUI");
            var uiRoot = Instantiate(prefab);
            var uiTrans = uiRoot.transform.Find("UI");

            questionText = uiTrans.Find("QuestionText").GetComponent<TMP_Text>();
            answerInput = uiTrans.Find("AnswerInput").GetComponent<TMP_InputField>();
            submitButton = uiTrans.Find("SubmitButton").GetComponent<Button>();
            feedbackText = uiTrans.Find("FeedbackText").GetComponent<TMP_Text>();
            surrenderButton = uiTrans.Find("SurrenderButton").GetComponent<Button>();

            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);

            surrenderButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.AddListener(OnSurrender);

            feedbackText.text = "";
            LoadQuestion();
        }

        private void OnSurrender()
        {
            Debug.Log("[TextPinyin] Surrender clicked, treat as wrong answer");
            StopAllCoroutines();
            // ���������ж�
            CheckAnswer("");
        }

        public override void LoadQuestion()
        {
            Debug.Log("[TextPinyin] LoadQuestion: ��ʼ��������");
            // 1. ���ѡ��
            string word = null;
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
                        if (reader.Read()) word = reader.GetString(0);
                    }
                }
            }
            Debug.Log($"[TextPinyin] Selected word={word}");
            if (string.IsNullOrEmpty(word))
            {
                questionText.text = "û�з���Ƶ�ʷ�Χ�Ĵʣ�";
                return;
            }

            // 2. ��λ��
            int pos = Random.Range(0, word.Length);
            string ch = word.Substring(pos, 1);
            Debug.Log($"[TextPinyin] Target char='{ch}', index={pos}");

            // 3. ���� Tpinyin
            string rawTpinyin = null;
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT Tpinyin FROM character
                         WHERE char=@ch LIMIT 1";
                    cmd.Parameters.AddWithValue("@ch", ch);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) rawTpinyin = reader.GetString(0);
                    }
                }
            }
            Debug.Log($"[TextPinyin] Raw Tpinyin JSON={rawTpinyin}");
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
            {
                questionText.text = $"�ʿ����Ҳ��� '{ch}' ��ƴ����";
                return;
            }

            // 4. ����ƴ��
            string fullTone = null;
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
                        if (reader.Read()) fullTone = reader.GetString(0);
                    }
                }
            }
            Debug.Log($"[TextPinyin] Full word pinyin tone={fullTone}");
            var toneParts = fullTone?.Trim().Split(' ');
            if (toneParts == null || pos < 0 || pos >= toneParts.Length)
            {
                questionText.text = "ƴ�����������ֳ��Ȳ�ƥ�䣡";
                Debug.LogError($"[TextPinyin] toneParts.Length={toneParts?.Length}, pos={pos}");
                return;
            }
            correctPinyinTone = toneParts[pos];
            Debug.Log($"[TextPinyin] CorrectTone={correctPinyinTone}");

            // �������
            questionText.text = $"��Ŀ��{word}\n��<color=red>{ch}</color>�� �Ķ����ǣ�";
            answerInput.text = "";
            feedbackText.text = "";
            answerInput.ActivateInputField();
        }

        public override void CheckAnswer(string answer)
        {
            Debug.Log($"[TextPinyin] CheckAnswer: rawAnswer=\"{answer}\"");
            var processed = answer.Replace("\"", "").Replace("��", "").Replace("��", "").Trim().ToLower();
            Debug.Log($"[TextPinyin] CheckAnswer: processed=\"{processed}\"");
            bool isRight = processed == correctPinyinNoTone;
            Debug.Log($"[TextPinyin] CheckAnswer: isRight={isRight}");
            // �ȷ�������ͨ���¼�������һ��
            StartCoroutine(ShowFeedbackThenNext(isRight));
            OnAnswerResult?.Invoke(isRight);
        }

        private void OnSubmit()
        {
            if (string.IsNullOrWhiteSpace(answerInput.text)) return;
            StopAllCoroutines();
            CheckAnswer(answerInput.text);
        }

        private IEnumerator ShowFeedbackThenNext(bool isRight)
        {
            if (isRight)
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
            yield return new WaitForSeconds(1f);
            // ����ֱ�� LoadQuestion���ɼ��� OnAnswerResult ���߼�������
        }
    }
}
