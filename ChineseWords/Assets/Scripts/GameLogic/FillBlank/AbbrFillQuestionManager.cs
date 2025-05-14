using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;
//���͹��ڼ򵥣��ʸ���

namespace GameLogic.FillBlank
{
    /// <summary>
    /// ����ĸ��ȫ���������
    /// - �����ȡ word/idiom/other_idiom ��һ����¼ (Freq ��Χ��)
    /// - �Ӽ�¼�� abbr �ֶ��������ʾ revealCount ������ĸ������λ���� _
    /// - <char> ����� i λ����ĸ
    /// - ��������������� D
    /// - У�飺���ȡ����д����ԡ�D �� abbr ��reveal��ͬ
    /// - ���ͨ�� OnAnswerResult ֪ͨ�������
    /// </summary>
    public class AbbrFillQuestionManager : QuestionManagerBase
    {
        [Header("���ݿ�·�� (StreamingAssets/dictionary.db)")]
        private string dbPath;

        [Header("Freq ��Χ")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        [Header("��ʾ����ĸ���� (0 < revealCount < abbr.Length)")]
        [SerializeField] private int revealCount = 1;

        [Header("UI ���")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_InputField answerInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button surrenderButton;
        [SerializeField] private TMP_Text feedbackText;

        private string currentWord;
        private string currentAbbr;
        private int[] revealIndices;

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
            LoadQuestion();
        }

        public override void LoadQuestion()
        {
            // 1. �����ȡһ����¼
            currentWord = null;
            currentAbbr = null;
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT word, abbr FROM (
  SELECT word, abbr, Freq FROM word
  UNION ALL
  SELECT word, abbr, Freq FROM idiom
  UNION ALL
  SELECT word, abbr, Freq FROM other_idiom
) WHERE Freq BETWEEN @min AND @max
ORDER BY RANDOM() LIMIT 1";
                    cmd.Parameters.AddWithValue("@min", freqMin);
                    cmd.Parameters.AddWithValue("@max", freqMax);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            currentWord = reader.GetString(0);
                            currentAbbr = reader.GetString(1);
                        }
                    }
                }
            }
            if (string.IsNullOrEmpty(currentWord) || string.IsNullOrEmpty(currentAbbr))
            {
                questionText.text = "û�з��������Ĵ�����";
                return;
            }
            // �� LoadQuestion() ���ȡ currentAbbr ��
            currentAbbr = currentAbbr.ToUpperInvariant();

            // 2. ���ѡ�� revealIndices
            int L = currentAbbr.Length;
            int count = Mathf.Clamp(revealCount, 1, L - 1);
            var idxs = Enumerable.Range(0, L).OrderBy(_ => Random.value).Take(count).OrderBy(i => i).ToArray();
            revealIndices = idxs;

            // 3. ��������ַ���
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < L; i++)
            {
                sb.Append(revealIndices.Contains(i) ? currentAbbr[i] : '_');
            }
            questionText.text = $"��Ŀ��������һ������ĸΪ<color=red>{sb}</color>�ĵ���\nHint:_����Ϊ��������ĸ��ͷ����";

            // ������������ʾ
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.ActivateInputField();
        }

        public override void CheckAnswer(string answer)
        {
            string D = answer.Trim();
            bool ok = true;

            // ����У��
            if (D.Length != currentWord.Length)
                ok = false;

            // ���д�����
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
  UNION ALL
  SELECT word FROM other_idiom
) WHERE word = @d";
                        cmd.Parameters.AddWithValue("@d", D);
                        long cnt = (long)cmd.ExecuteScalar();
                        ok = cnt > 0;
                    }
                }
            }

            // abbr У��
            if (ok)
            {
                string inputAbbr = null;
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
SELECT abbr FROM (
  SELECT word, abbr FROM word
  UNION ALL
  SELECT word, abbr FROM idiom
  UNION ALL
  SELECT word, abbr FROM other_idiom
) WHERE word = @d LIMIT 1";
                        cmd.Parameters.AddWithValue("@d", D);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                inputAbbr = reader.GetString(0);
                        }
                    }
                }
                if (string.IsNullOrEmpty(inputAbbr) || inputAbbr.Length != currentAbbr.Length)
                    ok = false;
                else
                {
                    foreach (int i in revealIndices)
                    {
                        if (inputAbbr[i] != currentAbbr[i])
                        {
                            ok = false;
                            break;
                        }
                    }
                }
            }

            StartCoroutine(ShowFeedbackThenNext(ok));
            OnAnswerResult?.Invoke(ok);
        }

        private void OnSubmit()
        {
            if (!string.IsNullOrWhiteSpace(answerInput.text))
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
                feedbackText.text = $"�ش����ʾ���𰸣�{currentWord}";
                feedbackText.color = Color.red;
            }
            yield return new WaitForSeconds(1f);
            // ��һ���� OnAnswerResult ͳһ����
        }
    }
}
