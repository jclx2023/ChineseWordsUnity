using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Core;

namespace GameLogic.TorF
{
    /// <summary>
    /// ����/����������ж����������
    /// - �� sentiment �����ȡһ����¼
    /// - ���� source �� word_id ��ȡ��Ӧ����
    /// - ӳ�� polarity Ϊ������/����/����/���С�
    /// - ����һ��Ȩ�ؽϵ͵ġ����С����Ż��������͸��Ŵ�
    /// - �������ȷ�𰸺͸��Ŵ𰸷��䵽���Ұ�ť
    /// - ��ҵ�����ж��Ƿ���ȷ
    /// </summary>
    public class SentimentTorFQuestionManager : QuestionManagerBase
    {
        [Header("���ݿ�·�� (StreamingAssets/dictionary.db)")]
        private string dbPath;

        [Header("Freq ��Χ (��ѡ)")]
        [SerializeField] private int freqMin = 0;
        [SerializeField] private int freqMax = 9;

        [Header("UI ���")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private Button buttonA;
        [SerializeField] private Button buttonB;
        [SerializeField] private TMP_Text feedbackText;

        private TMP_Text textA;
        private TMP_Text textB;

        private string currentWord;
        private int currentPolarity;
        private int choiceAPolarity;
        private int choiceBPolarParty;

        private void Awake()
        {
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/TorFUI");
            var ui = Instantiate(prefab).transform.Find("UI");
            questionText = ui.Find("QuestionText").GetComponent<TMP_Text>();
            buttonA = ui.Find("TrueButton").GetComponent<Button>();
            buttonB = ui.Find("FalseButton").GetComponent<Button>();
            feedbackText = ui.Find("FeedbackText").GetComponent<TMP_Text>();

            textA = buttonA.GetComponentInChildren<TMP_Text>();
            textB = buttonB.GetComponentInChildren<TMP_Text>();

            buttonA.onClick.RemoveAllListeners();
            buttonB.onClick.RemoveAllListeners();
            buttonA.onClick.AddListener(() => OnSelect(choiceAPolarity));
            buttonB.onClick.AddListener(() => OnSelect(choiceBPolarParty));

            feedbackText.text = string.Empty;
            LoadQuestion();
        }

        public override void LoadQuestion()
        {
            // ���ѡ sentiment
            int sid = -1; string source = null; int wid = -1;
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id,source,word_id,polarity FROM sentiment ORDER BY RANDOM() LIMIT 1";
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            sid = reader.GetInt32(0);
                            source = reader.GetString(1);
                            wid = reader.GetInt32(2);
                            currentPolarity = reader.GetInt32(3);
                        }
                    }
                }
            }
            if (wid < 0 || string.IsNullOrEmpty(source))
            {
                questionText.text = "������Ч��м�¼��";
                return;
            }

            // ���ȡ��
            string table = source.ToLower() == "word" ? "word" : source.ToLower() == "idiom" ? "idiom" : "other_idiom";
            currentWord = null;
            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT word FROM {table} WHERE id=@id LIMIT 1";
                    cmd.Parameters.AddWithValue("@id", wid);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) currentWord = reader.GetString(0);
                    }
                }
            }
            if (string.IsNullOrEmpty(currentWord))
            {
                questionText.text = "�Ҳ�����Ӧ������";
                LoadQuestion();
            }

            // ӳ����ı�
            string correctText = MapPolarity(currentPolarity);
            // ���ɸ���
            var candidates = new List<int> { 0, 1, 2, 3 }.Where(p => p != currentPolarity).ToList();
            var weights = candidates.Select(p => p == 3 ? 0.2f : 1f).ToList();
            int wrongPol = WeightedChoice(candidates, weights);
            string wrongText = MapPolarity(wrongPol);

            // ���䵽��ť
            if (Random.value < 0.5f)
            {
                choiceAPolarity = currentPolarity;
                choiceBPolarParty = wrongPol;
                textA.text = correctText;
                textB.text = wrongText;
            }
            else
            {
                choiceAPolarity = wrongPol;
                choiceBPolarParty = currentPolarity;
                textA.text = wrongText;
                textB.text = correctText;
            }

            questionText.text = $"��Ŀ���ж����д�����������\n    ��<color=red>{currentWord}</color>��";
            feedbackText.text = string.Empty;
        }

        public override void CheckAnswer(string answer)
        {
            // ����ʹ�ð�ť�ص�����ͨ���ı�����
            Debug.Log("CheckAnswer �������ڴ����͡�");
        }

        private void OnSelect(int selectedPol)
        {
            bool isRight = selectedPol == currentPolarity;
            StartCoroutine(ShowFeedbackThenNext(isRight));
            OnAnswerResult?.Invoke(isRight);
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
                feedbackText.text = $"�ش������ȷ���ǣ�{MapPolarity(currentPolarity)}";
                feedbackText.color = Color.red;
            }
            yield return new WaitForSeconds(1f);
        }

        private static string MapPolarity(int p)
        {
            switch (p)
            {
                case 0: return "����";
                case 1: return "����";
                case 2: return "����";
                case 3: return "����";
                default: return string.Empty;
            }
        }

        private static int WeightedChoice(List<int> items, List<float> weights)
        {
            float total = weights.Sum();
            float r = Random.value * total;
            float acc = 0;
            for (int i = 0; i < items.Count; i++)
            {
                acc += weights[i];
                if (r <= acc) return items[i];
            }
            return items.Last();
        }
    }
}
