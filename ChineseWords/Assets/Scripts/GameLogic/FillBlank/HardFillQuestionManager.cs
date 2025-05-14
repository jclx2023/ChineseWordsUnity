using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;

namespace GameLogic
{
    /// <summary>
    /// Ӳ�Ե��ʲ�ȫ���������
    /// - ���ʳ���Ȩ�����ʣ�ʹ��Ԥ����ӵ� length �ֶΣ�
    /// - ���չʾ revealCount ���ַ�������λ���»���
    /// - ������������ʣ�У�鳤�ȡ������֡��ʿ������
    /// - ��Ͷ������Ϊ�ύ�մ𰸣���������ͨ�� OnAnswerResult ������һ��
    /// </summary>
    public class HardFillQuestionManager : QuestionManagerBase
    {
        [Header("���ݿ�·����StreamingAssets/dictionary.db��")]
        private string dbPath;

        [Header("Ƶ�ʷ�Χ��Freq��")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        [Header("���ʳ���Ȩ��ʱ������ܺ�=1��")]
        [SerializeField, Range(0f, 1f)] private float weight2 = 0.32f;
        [SerializeField, Range(0f, 1f)] private float weight3 = 0.32f;
        [SerializeField, Range(0f, 1f)] private float weight4 = 0.32f;
        [SerializeField, Range(0f, 1f)] private float weightOther = 0.04f;

        [Header("��֪��������С�ڴʳ���")]
        [SerializeField] private int revealCount = 2;

        [Header("UI ���")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_InputField answerInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button surrenderButton;
        [SerializeField] private TMP_Text feedbackText;

        private string currentWord;
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

        private void OnSurrender()
        {
            // ��Ϊ�մ��ύ��������
            StopAllCoroutines();
            CheckAnswer(string.Empty);
        }

        public override void LoadQuestion()
        {
            // 1. ����Ȩ��ȷ��Ŀ��ʳ�
            float r = Random.value;
            int targetLength;
            if (r < weight2) targetLength = 2;
            else if (r < weight2 + weight3) targetLength = 3;
            else if (r < weight2 + weight3 + weight4) targetLength = 4;
            else targetLength = -1;

            // 2. ������
            currentWord = null;
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    if (targetLength > 0)
                    {
                        cmd.CommandText = @"
SELECT word FROM (
    SELECT word, Freq, length FROM word
    UNION ALL
    SELECT word, Freq, length FROM idiom
) WHERE length = @len AND Freq BETWEEN @min AND @max ORDER BY RANDOM() LIMIT 1";
                        cmd.Parameters.AddWithValue("@len", targetLength);
                    }
                    else
                    {
                        cmd.CommandText = @"
SELECT word FROM (
    SELECT word, Freq, length FROM word
    UNION ALL
    SELECT word, Freq, length FROM idiom
) WHERE length NOT IN (2,3,4) AND Freq BETWEEN @min AND @max ORDER BY RANDOM() LIMIT 1";
                    }
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

            // 3. ���ѡ revealIndices
            int L = currentWord.Length;
            int revealNum = Mathf.Clamp(revealCount, 1, L - 1);
            var all = Enumerable.Range(0, L).ToArray();
            for (int i = all.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = all[i]; all[i] = all[j]; all[j] = tmp;
            }
            revealIndices = all.Take(revealNum).OrderBy(x => x).ToArray();

            // 4. �������
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < L; i++)
                sb.Append(revealIndices.Contains(i) ? currentWord[i] : '_');
            questionText.text = $"��Ŀ��{sb}";

            // ������������ʾ
            answerInput.text = string.Empty;
            feedbackText.text = string.Empty;
            answerInput.ActivateInputField();
        }

        public override void CheckAnswer(string answer)
        {
            string D = answer.Trim();
            bool ok = true;

            if (D.Length != currentWord.Length)
                ok = false;

            if (ok)
            {
                foreach (int idx in revealIndices)
                {
                    if (D[idx] != currentWord[idx])
                    {
                        ok = false;
                        break;
                    }
                }
            }

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
                        long cnt = (long)cmd.ExecuteScalar();
                        ok = cnt > 0;
                    }
                }
            }

            StartCoroutine(ShowFeedbackThenNext(ok));
            OnAnswerResult?.Invoke(ok);
        }

        private void OnSubmit()
        {
            if (string.IsNullOrWhiteSpace(answerInput.text))
                return;
            StopAllCoroutines();
            CheckAnswer(answerInput.text);
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
                feedbackText.text = $"�ش������ȷʾ����{currentWord}";
                feedbackText.color = Color.red;
            }
            yield return new WaitForSeconds(1f);
            // ������ OnAnswerResult ������ͳһ����
        }
    }
}
