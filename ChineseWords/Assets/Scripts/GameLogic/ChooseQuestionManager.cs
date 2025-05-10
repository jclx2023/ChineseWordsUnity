using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Core;

namespace GameLogic
{

    public class ChooseQuestionManager : QuestionManagerBase
    {
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private Button[] optionButtons;
        private string correctAnswer;

        public override void LoadQuestion()
        {

        }

        public override void CheckAnswer(string answer)
        {
            bool isRight = answer == correctAnswer;
            OnAnswerResult?.Invoke(isRight);
        }
    }
}