using UnityEngine;
using Core;
using GameLogic.Choice;
using GameLogic.FillBlank;
using GameLogic.TorF;
using System;

namespace Core.Network
{
    /// <summary>
    /// ��Ŀ����������
    /// ͳһ������Ŀ�������Ĵ����������ظ�����
    /// </summary>
    public static class QuestionManagerFactory
    {
        /// <summary>
        /// ������Ŀ������
        /// </summary>
        public static QuestionManagerBase CreateManager(
            GameObject parent,
            QuestionType questionType,
            bool isDataOnly = false)
        {
            if (parent == null)
            {
                Debug.LogError("[QuestionManagerFactory] ��������Ϊ��");
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

                    // �����������...
                    default:
                        Debug.LogError($"[QuestionManagerFactory] ��֧�ֵ���Ŀ����: {questionType}");
                        return null;
                }

                if (manager != null)
                {
                    // ���ù���������
                    ConfigureManager(manager, questionType, isNetworkMode, isDataOnly);

                    Debug.Log($"[QuestionManagerFactory] �ɹ�����������: {questionType} " +
                             $"(����ģʽ: {isNetworkMode}, ������: {isDataOnly})");
                }

                return manager;
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestionManagerFactory] ����������ʧ��: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// ������GameObject�ϴ���������
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
        /// �����µ�GameObject����ӹ�����
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
        /// ���ù���������
        /// </summary>
        private static void ConfigureManager(
            QuestionManagerBase manager,
            QuestionType questionType,
            bool isNetworkMode,
            bool isDataOnly)
        {
            if (manager == null) return;

            // ���ù���������
            manager.gameObject.name = $"{questionType}Manager_{(isNetworkMode ? "Network" : "Local")}";

            // ����ǽ�����ģʽ����ӱ��
            if (isDataOnly)
            {
                manager.gameObject.tag = "DataOnlyManager";
                manager.gameObject.name += "_DataOnly";
            }

            // ��������ģʽ������������
            if (isNetworkMode && manager is NetworkQuestionManagerBase networkManager)
            {
                // ����ģʽ����������
                ConfigureNetworkManager(networkManager, isDataOnly);
            }
        }

        /// <summary>
        /// �������������
        /// </summary>
        private static void ConfigureNetworkManager(NetworkQuestionManagerBase networkManager, bool isDataOnly)
        {
            // �����������������������������
            // ��������������ص�����
        }

        /// <summary>
        /// ����Ƿ�֧��ָ������Ŀ����
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
                    // ��Щ������δʵ��
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// ��ȡ����֧�ֵ���Ŀ����
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
        /// ��ȫ���ٹ�����
        /// </summary>
        public static void DestroyManager(QuestionManagerBase manager)
        {
            if (manager == null) return;

            try
            {
                // �����������Դ
                if (manager.OnAnswerResult != null)
                {
                    // ����¼�������
                    foreach (var d in manager.OnAnswerResult.GetInvocationList())
                        manager.OnAnswerResult -= (System.Action<bool>)d;
                }

                // ����GameObject
                if (manager.gameObject != null)
                {
                    UnityEngine.Object.Destroy(manager.gameObject);
                }

                Debug.Log($"[QuestionManagerFactory] ������������: {manager.GetType().Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestionManagerFactory] ���ٹ�����ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// �������ٹ�����
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