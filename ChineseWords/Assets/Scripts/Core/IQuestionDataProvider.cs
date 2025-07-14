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