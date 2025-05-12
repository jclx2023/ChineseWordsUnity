using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Core;

namespace GameLogic
{

    public class ChooseQuestionManager : QuestionManagerBase
    {
        [Header("数据库路径（StreamingAssets/Temp.db）")]
        private string dbPath;

        private GameObject uiRoot;

        [Header("UI Components")]
        [SerializeField] private TMP_Text questionText;    // 显示题干
        [SerializeField] private Button[] optionButtons;   // 四个选项按钮
        [SerializeField] private TMP_Text feedbackText;    // 正误反馈

        private string correctAnswer;

        void Start()
        {
            // 1. 构造数据库路径
            dbPath = Application.streamingAssetsPath + "/Temp.db";

            // 2. 加载并实例化多选题 UI Prefab（请确保路径正确）
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/ChooseUI");
            uiRoot = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            if (uiRoot == null)
            {
                Debug.LogError("【Choose】Instantiate 失败，返回了 null");
                return;
            }

            // 3. 找到 UI 根节点（假设 Prefab 下有一个名为 “UI” 的子物体）
            var uiTrans = uiRoot.transform.Find("UI");
            if (uiTrans == null)
            {
                Debug.LogError($"【Choose】在 {uiRoot.name} 下找不到名为 UI 的子物体");
                return;
            }

            // 4. 拿到各组件
            questionText = uiTrans.Find("QuestionText")?.GetComponent<TMP_Text>();
            feedbackText = uiTrans.Find("FeedbackText")?.GetComponent<TMP_Text>();

            // 假定按钮命名为 OptionButton1/OptionButton2/OptionButton3/OptionButton4
            optionButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var btnTrans = uiTrans.Find($"OptionButton{i + 1}");
                if (btnTrans == null)
                {
                    Debug.LogError($"找不到 OptionButton{i + 1}");
                    continue;
                }
                var btn = btnTrans.GetComponent<Button>();
                if (btn == null)
                {
                    Debug.LogError($"OptionButton{i + 1} 上未挂 Button 组件");
                    continue;
                }
                optionButtons[i] = btn;

                // 5. 绑定点击事件：把索引 i 传给 OnOptionSelected
                int index = i;
                btn.onClick.AddListener(() => OnOptionSelected(index));
            }

            // 6. 初始化状态：清空反馈，字体聚焦等
            if (feedbackText != null)
                feedbackText.text = string.Empty;

            // 注意：这里不主动调用 LoadQuestion()，由外部控制器统一出题
        }

        public override void LoadQuestion()
        {

        }

        /// <summary>
        /// 选项按钮回调：把用户点击的选项索引转成答案字符串，再调用基类校验
        /// </summary>
        private void OnOptionSelected(int optionIndex)
        {
            // 例如：按钮上挂了 Text 子物体，显示了选项文字
            var btnText = optionButtons[optionIndex].GetComponentInChildren<TMP_Text>()?.text;
            if (string.IsNullOrEmpty(btnText))
                return;

            // 停掉可能存在的协程，避免重复反馈
            StopAllCoroutines();

            // 调用基类提供的校验接口
            CheckAnswer(btnText);
        }
    
    public override void CheckAnswer(string answer)
        {
            bool isRight = answer == correctAnswer;
            OnAnswerResult?.Invoke(isRight);
        }
    }
}