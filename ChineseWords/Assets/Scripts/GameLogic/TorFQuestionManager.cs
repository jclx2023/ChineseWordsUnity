using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Data;
using Mono.Data.Sqlite;
using Core;

namespace GameLogic
{
    /// <summary>
    /// �ж����������ֻ���� UI ��ʼ���͡���/�١���ť�ص���
    /// ����ͻ������ⲿ QuestionManagerController ͳһ����
    /// </summary>
    public class TorFQuestionManager : QuestionManagerBase
    {
        [Header("���ݿ�·����StreamingAssets/Temp.db��")]
        private string dbPath;

        private GameObject uiRoot;

        [Header("UI Components")]
        [SerializeField] private TMP_Text questionText;    // ��ʾ���
        [SerializeField] private Button trueButton;        // ���桱��ť
        [SerializeField] private Button falseButton;       // ���١���ť
        [SerializeField] private TMP_Text feedbackText;    // ������ʾ
        private string correctAnswer;
        void Start()
        {
            // 1. �������ݿ�·��
            dbPath = Application.streamingAssetsPath + "/Temp.db";

            // 2. ���ز�ʵ�����ж��� UI Prefab
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/TorFUI");
            uiRoot = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            if (uiRoot == null)
            {
                Debug.LogError("��TorF��Instantiate ʧ�ܣ������� null");
                return;
            }

            // 3. �ҵ� UI ���ڵ㣨���� Prefab ����һ����Ϊ ��UI�� �������壩
            var uiTrans = uiRoot.transform.Find("UI");
            if (uiTrans == null)
            {
                Debug.LogError($"��TorF���� {uiRoot.name} ���Ҳ�����Ϊ UI ��������");
                return;
            }

            // 4. ��ȡ�����
            questionText = uiTrans.Find("QuestionText")?.GetComponent<TMP_Text>();
            feedbackText = uiTrans.Find("FeedbackText")?.GetComponent<TMP_Text>();

            var tBtnTrans = uiTrans.Find("TrueButton");
            if (tBtnTrans == null)
                Debug.LogError("�Ҳ��� TrueButton");
            else
                trueButton = tBtnTrans.GetComponent<Button>();

            var fBtnTrans = uiTrans.Find("FalseButton");
            if (fBtnTrans == null)
                Debug.LogError("�Ҳ��� FalseButton");
            else
                falseButton = fBtnTrans.GetComponent<Button>();

            // 5. ���������
            if (questionText == null) Debug.LogError("QuestionText δ�󶨻�ȱʧ TMP_Text");
            if (trueButton == null) Debug.LogError("TrueButton δ�󶨻�ȱʧ Button");
            if (falseButton == null) Debug.LogError("FalseButton δ�󶨻�ȱʧ Button");
            if (feedbackText == null) Debug.LogError("FeedbackText δ�󶨻�ȱʧ TMP_Text");
            if (questionText == null || trueButton == null || falseButton == null || feedbackText == null)
                return;

            // 6. ����ɼ��������ⱻ�ظ���
            trueButton.onClick.RemoveAllListeners();
            falseButton.onClick.RemoveAllListeners();

            // 7. �󶨵���¼�
            trueButton.onClick.AddListener(() => OnOptionSelected(true));
            falseButton.onClick.AddListener(() => OnOptionSelected(false));

            // 8. ��ʼ�������ı�
            feedbackText.text = string.Empty;
        }
        /// <summary>
        /// ��/�ٰ�ť�ص�
        /// </summary>
        private void OnOptionSelected(bool isTrue)
        {
            // ֹͣ����Э�̣���ֹ�������
            StopAllCoroutines();

            // ������ֵת�������ݿ���һ�µ��ַ�����ʽ���� "True"/"False" ���� "1"/"0"��
            string ans = isTrue ? "True" : "False";
            CheckAnswer(ans);
        }

        public override void LoadQuestion()
        {
            // �����ݿ�ȡ�⣬����ֵ�� questionText.text �� correctAnswer
        }

        public override void CheckAnswer(string answer)
        {
            bool isRight = answer.Trim() == correctAnswer;
            OnAnswerResult?.Invoke(isRight);
        }
    }
}