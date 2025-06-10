using UnityEngine;
using Core;
using GameLogic.Choice;
using GameLogic.FillBlank;
using GameLogic.TorF;
using System;

namespace Core.Network
{
    /// <summary>
    /// 题目管理器工厂
    /// 统一管理题目管理器的创建，避免重复代码
    /// </summary>
    public static class QuestionManagerFactory
    {
        /// <summary>
        /// 创建题目管理器
        /// </summary>
        public static QuestionManagerBase CreateManager(
            GameObject parent,
            QuestionType questionType,
            bool isDataOnly = false)
        {
            if (parent == null)
            {
                Debug.LogError("[QuestionManagerFactory] 父对象不能为空");
                return null;
            }

            try
            {
                QuestionManagerBase manager = null;

                switch (questionType)
                {
                    case QuestionType.ExplanationChoice:
                        manager = parent.AddComponent<ExplanationChoiceQuestionManager>();
                        break;

                    case QuestionType.HardFill:
                        manager = parent.AddComponent<HardFillQuestionManager>();
                        break;

                    case QuestionType.SoftFill:
                        manager = parent.AddComponent<SoftFillQuestionManager>();
                        break;

                    case QuestionType.TextPinyin:
                        manager = parent.AddComponent<TextPinyinQuestionManager>();
                        break;

                    case QuestionType.SimularWordChoice:
                        manager = parent.AddComponent<SimularWordChoiceQuestionManager>();
                        break;

                    case QuestionType.SentimentTorF:
                        manager = parent.AddComponent<SentimentTorFQuestionManager>();
                        break;

                    case QuestionType.UsageTorF:
                        manager = parent.AddComponent<UsageTorFQuestionManager>();
                        break;

                    case QuestionType.IdiomChain:
                        manager = parent.AddComponent<IdiomChainQuestionManager>();
                        break;

                    // 添加其他题型...
                    default:
                        Debug.LogError($"[QuestionManagerFactory] 不支持的题目类型: {questionType}");
                        return null;
                }

                if (manager != null)
                {
                    // 设置管理器属性
                    ConfigureManager(manager, questionType, isNetworkMode, isDataOnly);

                    Debug.Log($"[QuestionManagerFactory] 成功创建管理器: {questionType} " +
                             $"(网络模式: {isNetworkMode}, 仅数据: {isDataOnly})");
                }

                return manager;
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestionManagerFactory] 创建管理器失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 在现有GameObject上创建管理器
        /// </summary>
        public static QuestionManagerBase CreateManagerOnGameObject(
            GameObject gameObject,
            QuestionType questionType,
            bool isNetworkMode = false,
            bool isDataOnly = false)
        {
            return CreateManager(gameObject, questionType, isNetworkMode, isDataOnly);
        }

        /// <summary>
        /// 创建新的GameObject并添加管理器
        /// </summary>
        public static QuestionManagerBase CreateManagerWithGameObject(
            string name,
            Transform parent,
            QuestionType questionType,
            bool isNetworkMode = false,
            bool isDataOnly = false)
        {
            GameObject newObj = new GameObject(name);
            if (parent != null)
                newObj.transform.SetParent(parent);

            return CreateManager(newObj, questionType, isNetworkMode, isDataOnly);
        }

        /// <summary>
        /// 配置管理器属性
        /// </summary>
        private static void ConfigureManager(
            QuestionManagerBase manager,
            QuestionType questionType,
            bool isNetworkMode,
            bool isDataOnly)
        {
            if (manager == null) return;

            // 设置管理器名称
            manager.gameObject.name = $"{questionType}Manager_{(isNetworkMode ? "Network" : "Local")}";

            // 如果是仅数据模式，添加标记
            if (isDataOnly)
            {
                manager.gameObject.tag = "DataOnlyManager";
                manager.gameObject.name += "_DataOnly";
            }

            // 根据网络模式进行特殊配置
            if (isNetworkMode && manager is NetworkQuestionManagerBase networkManager)
            {
                // 网络模式的特殊配置
                ConfigureNetworkManager(networkManager, isDataOnly);
            }
        }

        /// <summary>
        /// 配置网络管理器
        /// </summary>
        private static void ConfigureNetworkManager(NetworkQuestionManagerBase networkManager, bool isDataOnly)
        {
            // 这里可以添加网络管理器的特殊配置
            // 例如设置网络相关的属性
        }

        /// <summary>
        /// 检查是否支持指定的题目类型
        /// </summary>
        public static bool IsQuestionTypeSupported(QuestionType questionType)
        {
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.HardFill:
                case QuestionType.SoftFill:
                case QuestionType.TextPinyin:
                case QuestionType.SimularWordChoice:
                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                case QuestionType.IdiomChain:
                    return true;

                case QuestionType.HandWriting:
                case QuestionType.AbbrFill:
                    // 这些类型暂未实现
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取所有支持的题目类型
        /// </summary>
        public static QuestionType[] GetSupportedQuestionTypes()
        {
            return new QuestionType[]
            {
                QuestionType.ExplanationChoice,
                QuestionType.HardFill,
                QuestionType.SoftFill,
                QuestionType.TextPinyin,
                QuestionType.SimularWordChoice,
                QuestionType.SentimentTorF,
                QuestionType.UsageTorF,
                QuestionType.IdiomChain,
            };
        }

        /// <summary>
        /// 安全销毁管理器
        /// </summary>
        public static void DestroyManager(QuestionManagerBase manager)
        {
            if (manager == null) return;

            try
            {
                // 清理管理器资源
                if (manager.OnAnswerResult != null)
                {
                    // 清空事件监听器
                    foreach (var d in manager.OnAnswerResult.GetInvocationList())
                        manager.OnAnswerResult -= (System.Action<bool>)d;
                }

                // 销毁GameObject
                if (manager.gameObject != null)
                {
                    UnityEngine.Object.Destroy(manager.gameObject);
                }

                Debug.Log($"[QuestionManagerFactory] 管理器已销毁: {manager.GetType().Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestionManagerFactory] 销毁管理器失败: {e.Message}");
            }
        }

        /// <summary>
        /// 批量销毁管理器
        /// </summary>
        public static void DestroyManagers(params QuestionManagerBase[] managers)
        {
            if (managers == null) return;

            foreach (var manager in managers)
            {
                DestroyManager(manager);
            }
        }
    }
}