using UnityEngine;
using System.Collections;
using Classroom;
using Core.Network;

namespace Classroom.Teacher
{
    /// <summary>
    /// TeacherManager播报扩展 - 在现有TeacherManager基础上添加语音播报功能
    /// 不修改任何现有方法，只新增播报相关功能
    /// </summary>
    public class TeacherSpeechAnnouncer : MonoBehaviour
    {
        [Header("播报设置")]
        [SerializeField] private bool enableAnnouncement = true;
        [SerializeField] private bool announceAnswerResults = true;
        [SerializeField] private bool announceCardUsage = true;
        [SerializeField] private bool announceGameEvents = true;

        [Header("播报内容配置")]
        [SerializeField] private AnnouncementTexts announcementTexts = new AnnouncementTexts();

        private TeacherManager teacherManager;

        #region 播报文本配置
        [System.Serializable]
        public class AnnouncementTexts
        {
            [Header("答题结果播报")]
            public string[] correctAnswers = {
                "很好！",
                "答得不错！",
                "正确！",
                "优秀！",
                "继续保持！"
            };

            public string[] wrongAnswers = {
                "这你都能答错！",
                "再仔细想想！",
                "下次要认真一点！",
                "回去好好复习！",
                "这么简单都不会？"
            };

            [Header("游戏事件播报")]
            public string[] gameStart = {
                "游戏开始了！大家加油！",
                "让我们开始今天的课堂问答！",
                "准备好了吗？开始答题！"
            };

            public string[] playerJoined = {
                "欢迎新同学！",
                "又来了一位同学！",
                "欢迎加入课堂！"
            };

            public string[] playerLeft = {
                "有同学离开了",
                "再见！",
                "下次再来！"
            };

            public string[] gameVictory = {
                "恭喜获得胜利！",
                "表现优秀！",
                "你是今天的冠军！",
                "太棒了！"
            };

            public string[] playerEliminated = {
                "很遗憾被淘汰了！",
                "下次加油！",
                "出局了！",
                "继续努力！"
            };

            [Header("卡牌使用播报")]
            public string[] cardUsageGeneral = {
                "使用了特殊卡牌！",
                "有趣的策略！",
                "卡牌效果发动！"
            };

            public string[] cardUsageTargeted = {
                "对{0}使用了{1}！",
                "向{0}发动了{1}！",
                "{0}成为了{1}的目标！"
            };
        }
        #endregion

        private void Awake()
        {
            teacherManager = GetComponent<TeacherManager>();

            // 自动添加语音气泡组件
            if (enableAnnouncement && GetComponent<TeacherSpeechBubble>() == null)
            {
                gameObject.AddComponent<TeacherSpeechBubble>();
            }
        }

        private void Start()
        {
            if (enableAnnouncement)
            {
                SubscribeToEvents();
                AnnounceWelcome();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #region 事件订阅（新增的，不影响原有订阅）
        private void SubscribeToEvents()
        {
            if (NetworkManager.Instance != null)
            {
                // 播报相关的事件订阅
                if (announceAnswerResults)
                    NetworkManager.OnPlayerAnswerResult += OnPlayerAnswerResult_Announce;

                if (announceGameEvents)
                {
                    NetworkManager.OnPlayerJoined += OnPlayerJoined_Announce;
                    NetworkManager.OnPlayerLeft += OnPlayerLeft_Announce;
                    NetworkManager.OnGameStarted += OnGameStarted_Announce;
                    NetworkManager.OnGameVictory += OnGameVictory_Announce;
                    NetworkManager.OnPlayerDied += OnPlayerDied_Announce;
                }
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerAnswerResult -= OnPlayerAnswerResult_Announce;
                NetworkManager.OnPlayerJoined -= OnPlayerJoined_Announce;
                NetworkManager.OnPlayerLeft -= OnPlayerLeft_Announce;
                NetworkManager.OnGameStarted -= OnGameStarted_Announce;
                NetworkManager.OnGameVictory -= OnGameVictory_Announce;
                NetworkManager.OnPlayerDied -= OnPlayerDied_Announce;
            }
        }
        #endregion

        #region 事件处理（纯播报，不影响游戏逻辑）
        private void OnPlayerAnswerResult_Announce(ushort playerId, bool isCorrect, string answer)
        {
            if (!announceAnswerResults) return;

            if (isCorrect)
            {
                string message = GetRandomText(announcementTexts.correctAnswers);
                TeacherSpeechBubble.Say(message, SpeechType.Encouragement);
            }
            else
            {
                string message = GetRandomText(announcementTexts.wrongAnswers);
                TeacherSpeechBubble.Say(message, SpeechType.Warning);
            }
        }

        private void OnPlayerJoined_Announce(ushort playerId)
        {
            string message = GetRandomText(announcementTexts.playerJoined);
            TeacherSpeechBubble.Say(message, SpeechType.Normal);
        }

        private void OnPlayerLeft_Announce(ushort playerId)
        {
            string message = GetRandomText(announcementTexts.playerLeft);
            TeacherSpeechBubble.Say(message, SpeechType.System);
        }

        private void OnGameStarted_Announce(int totalPlayers, int alivePlayers, ushort firstPlayerId)
        {
            string message = GetRandomText(announcementTexts.gameStart);
            TeacherSpeechBubble.Say(message, SpeechType.Excited);
        }

        private void OnGameVictory_Announce(ushort winnerId, string winnerName, string reason)
        {
            string message = GetRandomText(announcementTexts.gameVictory);
            TeacherSpeechBubble.Say(message, SpeechType.Excited);
        }

        private void OnPlayerDied_Announce(ushort playerId, string reason)
        {
            string message = GetRandomText(announcementTexts.playerEliminated);
            TeacherSpeechBubble.Say(message, SpeechType.Warning);
        }
        #endregion

        #region 公共播报方法
        /// <summary>
        /// 播报卡牌使用（供外部调用）
        /// </summary>
        public void AnnounceCardUsage(string playerName, string cardName, string targetName = null)
        {
            if (!enableAnnouncement || !announceCardUsage) return;

            string message;
            if (string.IsNullOrEmpty(targetName))
            {
                // 无目标卡牌
                message = GetRandomText(announcementTexts.cardUsageGeneral);
            }
            else
            {
                // 有目标卡牌
                string template = GetRandomText(announcementTexts.cardUsageTargeted);
                message = string.Format(template, targetName, cardName);
            }

            TeacherSpeechBubble.Say(message, SpeechType.CardAction);
        }

        /// <summary>
        /// 播报自定义消息
        /// </summary>
        public void AnnounceCustom(string message, SpeechType type = SpeechType.Normal)
        {
            if (!enableAnnouncement) return;
            TeacherSpeechBubble.Say(message, type);
        }

        /// <summary>
        /// 播报轮次变化
        /// </summary>
        public void AnnounceTurnChange(string playerName)
        {
            if (!enableAnnouncement) return;
            TeacherSpeechBubble.Say($"轮到{playerName}了！", SpeechType.Normal);
        }

        /// <summary>
        /// 欢迎播报
        /// </summary>
        public void AnnounceWelcome()
        {
            TeacherSpeechBubble.Say("欢迎来到课堂！", SpeechType.Normal);
        }
        #endregion

        #region 静态便捷方法（供其他脚本调用）
        private static TeacherSpeechAnnouncer instance;
        public static TeacherSpeechAnnouncer Instance
        {
            get
            {
                if (instance == null)
                    instance = FindObjectOfType<TeacherSpeechAnnouncer>();
                return instance;
            }
        }

        /// <summary>
        /// 静态方法：播报卡牌使用
        /// </summary>
        public static void AnnounceCard(string playerName, string cardName, string targetName = null)
        {
            Instance?.AnnounceCardUsage(playerName, cardName, targetName);
        }

        /// <summary>
        /// 静态方法：播报自定义消息
        /// </summary>
        public static void Announce(string message, SpeechType type = SpeechType.Normal)
        {
            Instance?.AnnounceCustom(message, type);
        }

        /// <summary>
        /// 静态方法：播报轮次变化
        /// </summary>
        public static void AnnounceTurn(string playerName)
        {
            Instance?.AnnounceTurnChange(playerName);
        }
        #endregion

        #region 辅助方法
        private string GetRandomText(string[] texts)
        {
            if (texts == null || texts.Length == 0) return "";
            return texts[Random.Range(0, texts.Length)];
        }

        private void Awake_SetInstance()
        {
            if (instance == null) instance = this;
        }
        #endregion
    }
}