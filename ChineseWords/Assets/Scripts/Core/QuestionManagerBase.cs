
using UnityEngine;

namespace Core
{
    public abstract class QuestionManagerBase : MonoBehaviour
    {
        public abstract void LoadQuestion();
        public abstract void CheckAnswer(string answer);
        public System.Action<bool> OnAnswerResult;
    }
}
