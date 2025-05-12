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
    /// 判断题管理器，只负责 UI 初始化和“真/假”按钮回调，
    /// 出题和换题由外部 QuestionManagerController 统一调度
    /// </summary>
    public class TorFQuestionManager : QuestionManagerBase
    {
        [Header("数据库路径（StreamingAssets/Temp.db）")]
        private string dbPath;

        private GameObject uiRoot;

        [Header("UI Components")]
        [SerializeField] private TMP_Text questionText;    // 显示题干
        [SerializeField] private Button trueButton;        // “真”按钮
        [SerializeField] private Button falseButton;       // “假”按钮
        [SerializeField] private TMP_Text feedbackText;    // 反馈提示
        private string correctAnswer;
        void Start()
        {
            // 1. 构造数据库路径
            dbPath = Application.streamingAssetsPath + "/Temp.db";

            // 2. 加载并实例化判断题 UI Prefab
            var prefab = Resources.Load<GameObject>("Prefabs/InGame/TorFUI");
            uiRoot = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            if (uiRoot == null)
            {
                Debug.LogError("【TorF】Instantiate 失败，返回了 null");
                return;
            }

            // 3. 找到 UI 根节点（假设 Prefab 下有一个名为 “UI” 的子物体）
            var uiTrans = uiRoot.transform.Find("UI");
            if (uiTrans == null)
            {
                Debug.LogError($"【TorF】在 {uiRoot.name} 下找不到名为 UI 的子物体");
                return;
            }

            // 4. 获取各组件
            questionText = uiTrans.Find("QuestionText")?.GetComponent<TMP_Text>();
            feedbackText = uiTrans.Find("FeedbackText")?.GetComponent<TMP_Text>();

            var tBtnTrans = uiTrans.Find("TrueButton");
            if (tBtnTrans == null)
                Debug.LogError("找不到 TrueButton");
            else
                trueButton = tBtnTrans.GetComponent<Button>();

            var fBtnTrans = uiTrans.Find("FalseButton");
            if (fBtnTrans == null)
                Debug.LogError("找不到 FalseButton");
            else
                falseButton = fBtnTrans.GetComponent<Button>();

            // 5. 检查必填组件
            if (questionText == null) Debug.LogError("QuestionText 未绑定或缺失 TMP_Text");
            if (trueButton == null) Debug.LogError("TrueButton 未绑定或缺失 Button");
            if (falseButton == null) Debug.LogError("FalseButton 未绑定或缺失 Button");
            if (feedbackText == null) Debug.LogError("FeedbackText 未绑定或缺失 TMP_Text");
            if (questionText == null || trueButton == null || falseButton == null || feedbackText == null)
                return;

            // 6. 清除旧监听，避免被重复绑定
            trueButton.onClick.RemoveAllListeners();
            falseButton.onClick.RemoveAllListeners();

            // 7. 绑定点击事件
            trueButton.onClick.AddListener(() => OnOptionSelected(true));
            falseButton.onClick.AddListener(() => OnOptionSelected(false));

            // 8. 初始化反馈文本
            feedbackText.text = string.Empty;
        }
        /// <summary>
        /// 真/假按钮回调
        /// </summary>
        private void OnOptionSelected(bool isTrue)
        {
            // 停止所有协程，防止多次切题
            StopAllCoroutines();

            // 将布尔值转成与数据库中一致的字符串形式（如 "True"/"False" 或者 "1"/"0"）
            string ans = isTrue ? "True" : "False";
            CheckAnswer(ans);
        }

        public override void LoadQuestion()
        {
            // 从数据库取题，并赋值给 questionText.text 和 correctAnswer
        }

        public override void CheckAnswer(string answer)
        {
            bool isRight = answer.Trim() == correctAnswer;
            OnAnswerResult?.Invoke(isRight);
        }
    }
}