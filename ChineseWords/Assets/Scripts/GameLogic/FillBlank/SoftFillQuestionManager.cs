using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Data.Sqlite;
using Core;

namespace GameLogic.FillBlank
{
    /// <summary>
    /// ���Ե��ʲ�ȫ��
    /// - �����ʣ�Freq��Χ�ڣ�
    /// - ���ѡȡ revealCount ������
    /// - ����ͨ����ɣ�*��ʾ���ⳤ�ȣ�_��ʾһ�������ַ�
    /// - ������� D��Regex ƥ�� Stem ģʽ
    /// - �ٲ��ȷ�� D �Ƿ����
    /// </summary>
    public class SoftFillQuestionManager : QuestionManagerBase
    {
        [Header("���ݿ�·����StreamingAssets/dictionary.db��")]
        private string dbPath;

        [Header("Freq��Χ")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 8;

        [Header("��֪������ (0 < x < L)")]
        [SerializeField] private int revealCount = 2;

        [Header("UI ���")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_InputField answerInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button surrenderButton;
        [SerializeField] private TMP_Text feedbackText;

        private string currentWord;
        private string stemPattern;
        private Regex matchRegex;

        private void Awake()
        {
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/HardFillUI");
            var ui = Instantiate(prefab).transform.Find("UI");
            questionText = ui.Find("QuestionText").GetComponent<TMP_Text>();
            answerInput = ui.Find("AnswerInput").GetComponent<TMP_InputField>();
            submitButton = ui.Find("SubmitButton").GetComponent<Button>();
            surrenderButton = ui.Find("SurrenderButton").GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText").GetComponent<TMP_Text>();

            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);
            surrenderButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.AddListener(OnSurrender);

            feedbackText.text = string.Empty;
            //LoadQuestion();
        }

        public override void LoadQuestion()
        {
            // 1. ������
            currentWord = null;
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
                            currentWord = reader.GetString(0);
                    }
                }
            }
            if (string.IsNullOrEmpty(currentWord))
            {
                questionText.text = "û�з��������Ĵ�����";
                return;
            }

            // 2. ���ѡ x ������
            int L = currentWord.Length;
            int x = Mathf.Clamp(revealCount, 1, L - 1);
            var indices = Enumerable.Range(0, L).ToArray();
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
            }
            var sel = indices.Take(x).OrderBy(i => i).ToArray();

            // 3. ����ͨ������
            var sb = new System.Text.StringBuilder();
            sb.Append("*");
            sb.Append(currentWord[sel[0]]);
            for (int k = 1; k < sel.Length; k++)
            {
                int gap = sel[k] - sel[k - 1] - 1;
                sb.Append(new string('_', gap));
                sb.Append(currentWord[sel[k]]);
            }
            sb.Append("*");
            stemPattern = sb.ToString();

            // 4. ���� stemPattern ���� Regex
            // * -> .*, _ -> .
            var pattern = "^" + string.Concat(stemPattern.Select(c =>
            {
                if (c == '*') return ".*";
                if (c == '_') return ".";
                return Regex.Escape(c.ToString());
            })) + "$";
            matchRegex = new Regex(pattern);

            // ��ʾ
            questionText.text = $"��Ŀ��������һ������<color=red>{stemPattern}</color>��ʽ�ĵ���\nHint��*Ϊ������֣�_Ϊ������";
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.ActivateInputField();
        }

        public override void CheckAnswer(string answer)
        {
            string D = answer.Trim();
            bool ok = matchRegex.IsMatch(D);

            if (ok)
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
) WHERE word = @d";
                        cmd.Parameters.AddWithValue("@d", D);
                        ok = ((long)cmd.ExecuteScalar()) > 0;
                    }
                }
            }

            StartCoroutine(ShowFeedbackThenNext(ok));
            OnAnswerResult?.Invoke(ok);
        }

        private void OnSubmit()
        {
            if (!string.IsNullOrEmpty(answerInput.text))
            {
                StopAllCoroutines();
                CheckAnswer(answerInput.text);
            }
        }

        private void OnSurrender()
        {
            StopAllCoroutines();
            CheckAnswer(string.Empty);
        }

        private IEnumerator ShowFeedbackThenNext(bool isRight)
        {
            if (isRight)
            {
                feedbackText.text = "�ش���ȷ��";
                feedbackText.color = Color.green;
            }
            else
            {
                feedbackText.text = $"�ش���󣬿ɽ���ʾ����{currentWord}";
                feedbackText.color = Color.red;
            }
            yield return new WaitForSeconds(1f);
            // ��һ���� OnAnswerResult ��������
        }
    }
}
