using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;

namespace GameLogic.Choice
{
    /// <summary>
    /// �������ѡ�����������
    /// - �� WordExplanationChoice �����ȡһ����¼
    /// - ��ʾ stem
    /// - �� true��false1��false2��false3 �ĸ�ѡ��������Һ����� OptionButton1-4
    /// - ���ѡ����ж��Ƿ������ȷѡ��
    /// - ͨ�� OnAnswerResult ֪ͨ���ͳһ����
    /// </summary>
    public class ExplanationChoiceQuestionManager : QuestionManagerBase
    {
        private string dbPath;

        [Header("UI Prefab (Resources/Prefabs/InGame/ExplanationChoiceUI)")]
        [SerializeField] private string uiPrefabPath = "Prefabs/InGame/ChooseUI";

        private TMP_Text questionText;
        private Button[] optionButtons;
        private TMP_Text feedbackText;

        private string correctOption;

        private void Awake()
        {
            dbPath = Application.streamingAssetsPath + "/dictionary.db";
        }

        private void Start()
        {
            // ʵ���� UI
            var prefab = Resources.Load<GameObject>(uiPrefabPath);
            var uiRoot = Instantiate(prefab);
            var ui = uiRoot.transform.Find("UI");

            // �� UI
            questionText = ui.Find("QuestionText").GetComponent<TMP_Text>();
            feedbackText = ui.Find("FeedbackText").GetComponent<TMP_Text>();

            // option buttons
            optionButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var btn = ui.Find($"OptionButton{i + 1}").GetComponent<Button>();
                optionButtons[i] = btn;
                int idx = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnOptionClicked(idx));
            }

            //LoadQuestion();
        }

        public override void LoadQuestion()
        {
            Debug.Log("����һ��");
            string stem = null;
            List<string> choices = new List<string>(4);

            using (var conn = new SqliteConnection("URI=file:" + dbPath))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT stem, [true], false1, false2, false3
  FROM WordExplanationChoice
 ORDER BY RANDOM()
    LIMIT 1";
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stem = reader.GetString(0);
                            correctOption = reader.GetString(1);
                            choices.Add(reader.GetString(1));
                            choices.Add(reader.GetString(2));
                            choices.Add(reader.GetString(3));
                            choices.Add(reader.GetString(4));
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(stem))
            {
                questionText.text = "������Ŀ����";
                return;
            }

            // ��ʾ���
            questionText.text = stem;
            feedbackText.text = string.Empty;

            // �������ѡ��
            for (int i = choices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = choices[i]; choices[i] = choices[j]; choices[j] = tmp;
            }

            // ��ֵ��ť�ı�
            for (int i = 0; i < optionButtons.Length; i++)
            {
                var txt = optionButtons[i].GetComponentInChildren<TMP_Text>();
                txt.text = choices[i];
            }
        }

        public override void CheckAnswer(string answer)
        {
            // ����ͨ����ť�ص�������ʹ���ı�����
            Debug.Log("CheckAnswer δ��ʹ��");
        }

        private void OnOptionClicked(int index)
        {
            var selected = optionButtons[index].GetComponentInChildren<TMP_Text>().text;
            bool isRight = selected == correctOption;
            StartCoroutine(ShowFeedbackThenNext(isRight));
            OnAnswerResult?.Invoke(isRight);
        }

        private IEnumerator ShowFeedbackThenNext(bool isRight)
        {
            feedbackText.color = isRight ? Color.green : Color.red;
            feedbackText.text = isRight ? "�ش���ȷ��" : $"�ش������ȷ���ǣ�{correctOption}";
            yield return new WaitForSeconds(1f);
            // ��һ���������� OnAnswerResult ����
        }
    }
}
