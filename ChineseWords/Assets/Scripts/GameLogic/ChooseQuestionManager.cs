using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Core;

namespace GameLogic
{

    public class ChooseQuestionManager : QuestionManagerBase
    {
        [Header("���ݿ�·����StreamingAssets/Temp.db��")]
        private string dbPath;

        private GameObject uiRoot;

        [Header("UI Components")]
        [SerializeField] private TMP_Text questionText;    // ��ʾ���
        [SerializeField] private Button[] optionButtons;   // �ĸ�ѡ�ť
        [SerializeField] private TMP_Text feedbackText;    // ������

        private string correctAnswer;

        void Start()
        {
            // 1. �������ݿ�·��
            dbPath = Application.streamingAssetsPath + "/Temp.db";

            // 2. ���ز�ʵ������ѡ�� UI Prefab����ȷ��·����ȷ��
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/ChooseUI");
            uiRoot = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            if (uiRoot == null)
            {
                Debug.LogError("��Choose��Instantiate ʧ�ܣ������� null");
                return;
            }

            // 3. �ҵ� UI ���ڵ㣨���� Prefab ����һ����Ϊ ��UI�� �������壩
            var uiTrans = uiRoot.transform.Find("UI");
            if (uiTrans == null)
            {
                Debug.LogError($"��Choose���� {uiRoot.name} ���Ҳ�����Ϊ UI ��������");
                return;
            }

            // 4. �õ������
            questionText = uiTrans.Find("QuestionText")?.GetComponent<TMP_Text>();
            feedbackText = uiTrans.Find("FeedbackText")?.GetComponent<TMP_Text>();

            // �ٶ���ť����Ϊ OptionButton1/OptionButton2/OptionButton3/OptionButton4
            optionButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var btnTrans = uiTrans.Find($"OptionButton{i + 1}");
                if (btnTrans == null)
                {
                    Debug.LogError($"�Ҳ��� OptionButton{i + 1}");
                    continue;
                }
                var btn = btnTrans.GetComponent<Button>();
                if (btn == null)
                {
                    Debug.LogError($"OptionButton{i + 1} ��δ�� Button ���");
                    continue;
                }
                optionButtons[i] = btn;

                // 5. �󶨵���¼��������� i ���� OnOptionSelected
                int index = i;
                btn.onClick.AddListener(() => OnOptionSelected(index));
            }

            // 6. ��ʼ��״̬����շ���������۽���
            if (feedbackText != null)
                feedbackText.text = string.Empty;

            // ע�⣺���ﲻ�������� LoadQuestion()�����ⲿ������ͳһ����
        }

        public override void LoadQuestion()
        {

        }

        /// <summary>
        /// ѡ�ť�ص������û������ѡ������ת�ɴ��ַ������ٵ��û���У��
        /// </summary>
        private void OnOptionSelected(int optionIndex)
        {
            // ���磺��ť�Ϲ��� Text �����壬��ʾ��ѡ������
            var btnText = optionButtons[optionIndex].GetComponentInChildren<TMP_Text>()?.text;
            if (string.IsNullOrEmpty(btnText))
                return;

            // ͣ�����ܴ��ڵ�Э�̣������ظ�����
            StopAllCoroutines();

            // ���û����ṩ��У��ӿ�
            CheckAnswer(btnText);
        }
    
    public override void CheckAnswer(string answer)
        {
            bool isRight = answer == correctAnswer;
            OnAnswerResult?.Invoke(isRight);
        }
    }
}