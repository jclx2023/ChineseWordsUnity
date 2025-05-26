using Core.Network;

namespace Core
{
    /// <summary>
    /// 题目数据提供者接口
    /// 为Host模式下的题目抽取提供标准化接口
    /// </summary>
    public interface IQuestionDataProvider
    {
        /// <summary>
        /// 获取题目数据（不显示UI）
        /// </summary>
        /// <returns>题目数据，如果获取失败返回null</returns>
        NetworkQuestionData GetQuestionData();

        /// <summary>
        /// 题目类型
        /// </summary>
        QuestionType QuestionType { get; }
    }
}

namespace Core
{
    /// <summary>
    /// 网络题目数据（扩展现有的NetworkQuestionData）
    /// </summary>
    public static class NetworkQuestionDataExtensions
    {
        /// <summary>
        /// 从本地题目管理器创建网络题目数据
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