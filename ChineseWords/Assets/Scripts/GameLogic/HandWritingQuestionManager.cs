using UnityEngine;
using TMPro;
using Core;

namespace GameLogic               // ��������Ŀ���ļ��нṹ����һ�������ռ�
{
    public class HandWritingQuestionManager : QuestionManagerBase
    {
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_InputField inputField;
        private string correctAnswer;

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