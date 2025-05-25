using UnityEngine;
using Core.Network;

namespace Core
{
    /// <summary>
    /// 网络化题目管理器基类扩展
    /// 为现有的QuestionManagerBase添加网络支持，保持向后兼容
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
        /// 加载题目 - 重写以支持网络数据
        /// </summary>
        public override void LoadQuestion()
        {
            if (isNetworkMode && networkController != null)
            {
                // 网络模式：尝试使用网络题目数据
                networkQuestionData = networkController.GetCurrentNetworkQuestion();
                if (networkQuestionData != null)
                {
                    LoadNetworkQuestion(networkQuestionData);
                    return;
                }
            }

            // 单机模式或没有网络数据：使用原有逻辑
            LoadLocalQuestion();
        }

        /// <summary>
        /// 检查答案 - 重写以支持网络提交
        /// </summary>
        public override void CheckAnswer(string answer)
        {
            if (isNetworkMode && networkController != null)
            {
                // 网络模式：通过网络控制器提交答案
                networkController.SubmitAnswer(answer);
            }
            else
            {
                // 单机模式：使用原有逻辑
                CheckLocalAnswer(answer);
            }
        }

        /// <summary>
        /// 加载本地题目（原有逻辑）
        /// 子类需要实现此方法，内容与原来的LoadQuestion相同
        /// </summary>
        protected abstract void LoadLocalQuestion();

        /// <summary>
        /// 检查本地答案（原有逻辑）
        /// 子类需要实现此方法，内容与原来的CheckAnswer相同
        /// </summary>
        protected abstract void CheckLocalAnswer(string answer);

        /// <summary>
        /// 加载网络题目
        /// 子类可以重写此方法来处理网络题目数据
        /// 默认实现是直接调用本地加载逻辑
        /// </summary>
        protected virtual void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            Debug.Log($"加载网络题目: {networkData.questionType} - {networkData.questionText}");

            // 默认实现：直接调用本地加载逻辑
            // 子类可以重写此方法来使用网络数据
            LoadLocalQuestion();
        }

        /// <summary>
        /// 获取当前网络题目数据
        /// </summary>
        protected NetworkQuestionData GetNetworkQuestionData()
        {
            return networkQuestionData;
        }

        /// <summary>
        /// 判断是否为网络模式
        /// </summary>
        protected bool IsNetworkMode()
        {
            return isNetworkMode;
        }
    }
}