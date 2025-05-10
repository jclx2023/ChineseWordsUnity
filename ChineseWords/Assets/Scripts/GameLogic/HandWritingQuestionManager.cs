using UnityEngine;
using TMPro;
using Core;

namespace GameLogic               // 根据你项目的文件夹结构，给一个命名空间
{
    public class HandWritingQuestionManager : QuestionManagerBase
    {
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_InputField inputField;
        private string correctAnswer;

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