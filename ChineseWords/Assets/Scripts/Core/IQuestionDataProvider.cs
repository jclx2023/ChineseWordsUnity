using Core.Network;

namespace Core
{
    /// <summary>
    /// ��Ŀ�����ṩ�߽ӿ�
    /// ΪHostģʽ�µ���Ŀ��ȡ�ṩ��׼���ӿ�
    /// </summary>
    public interface IQuestionDataProvider
    {
        /// <summary>
        /// ��ȡ��Ŀ���ݣ�����ʾUI��
        /// </summary>
        /// <returns>��Ŀ���ݣ������ȡʧ�ܷ���null</returns>
        NetworkQuestionData GetQuestionData();

        /// <summary>
        /// ��Ŀ����
        /// </summary>
        QuestionType QuestionType { get; }
    }
}

namespace Core
{
    /// <summary>
    /// ������Ŀ���ݣ���չ���е�NetworkQuestionData��
    /// </summary>
    public static class NetworkQuestionDataExtensions
    {
        /// <summary>
        /// �ӱ�����Ŀ����������������Ŀ����
        /// </summary>
        public static NetworkQuestionData CreateFromLocalData(
            QuestionType questionType,
            string questionText,
            string correctAnswer,
            string[] options = null,
            float timeLimit = 30f,
            string additionalData = null)
        {
            return new NetworkQuestionData
            {
                questionType = questionType,
                questionText = questionText,
                correctAnswer = correctAnswer,
                options = options ?? new string[0],
                timeLimit = timeLimit,
                additionalData = additionalData ?? "{}"
            };
        }
    }
}