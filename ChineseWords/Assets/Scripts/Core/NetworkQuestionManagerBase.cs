using UnityEngine;
using Core.Network;

namespace Core
{
    /// <summary>
    /// ���绯��Ŀ������������չ
    /// Ϊ���е�QuestionManagerBase�������֧�֣�����������
    /// </summary>
    public abstract class NetworkQuestionManagerBase : QuestionManagerBase
    {
        protected NetworkQuestionManagerController networkController;
        protected NetworkQuestionData networkQuestionData;
        protected bool isNetworkMode = false;

        protected virtual void Awake()
        {
            networkController = NetworkQuestionManagerController.Instance;
            isNetworkMode = networkController?.IsMultiplayerMode ?? false;
        }

        /// <summary>
        /// ������Ŀ - ��д��֧����������
        /// </summary>
        public override void LoadQuestion()
        {
            if (isNetworkMode && networkController != null)
            {
                // ����ģʽ������ʹ��������Ŀ����
                networkQuestionData = networkController.GetCurrentNetworkQuestion();
                if (networkQuestionData != null)
                {
                    LoadNetworkQuestion(networkQuestionData);
                    return;
                }
            }

            // ����ģʽ��û���������ݣ�ʹ��ԭ���߼�
            LoadLocalQuestion();
        }

        /// <summary>
        /// ���� - ��д��֧�������ύ
        /// </summary>
        public override void CheckAnswer(string answer)
        {
            if (isNetworkMode && networkController != null)
            {
                // ����ģʽ��ͨ������������ύ��
                networkController.SubmitAnswer(answer);
            }
            else
            {
                // ����ģʽ��ʹ��ԭ���߼�
                CheckLocalAnswer(answer);
            }
        }

        /// <summary>
        /// ���ر�����Ŀ��ԭ���߼���
        /// ������Ҫʵ�ִ˷�����������ԭ����LoadQuestion��ͬ
        /// </summary>
        protected abstract void LoadLocalQuestion();

        /// <summary>
        /// ��鱾�ش𰸣�ԭ���߼���
        /// ������Ҫʵ�ִ˷�����������ԭ����CheckAnswer��ͬ
        /// </summary>
        protected abstract void CheckLocalAnswer(string answer);

        /// <summary>
        /// ����������Ŀ
        /// ���������д�˷���������������Ŀ����
        /// Ĭ��ʵ����ֱ�ӵ��ñ��ؼ����߼�
        /// </summary>
        protected virtual void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log($"����������Ŀ: {networkData.questionType} - {networkData.questionText}");

            // Ĭ��ʵ�֣�ֱ�ӵ��ñ��ؼ����߼�
            // ���������д�˷�����ʹ����������
            LoadLocalQuestion();
        }

        /// <summary>
        /// ��ȡ��ǰ������Ŀ����
        /// </summary>
        protected NetworkQuestionData GetNetworkQuestionData()
        {
            return networkQuestionData;
        }

        /// <summary>
        /// �ж��Ƿ�Ϊ����ģʽ
        /// </summary>
        protected bool IsNetworkMode()
        {
            return isNetworkMode;
        }
    }
}