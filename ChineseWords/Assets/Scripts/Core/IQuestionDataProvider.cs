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