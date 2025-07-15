using UnityEngine;
using System.Collections;
using Classroom;
using Core.Network;

namespace Classroom.Teacher
{
    /// <summary>
    /// TeacherManager������չ - ������TeacherManager���������������������
    /// ���޸��κ����з�����ֻ����������ع���
    /// </summary>
    public class TeacherSpeechAnnouncer : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private bool enableAnnouncement = true;
        [SerializeField] private bool announceAnswerResults = true;
        [SerializeField] private bool announceCardUsage = true;
        [SerializeField] private bool announceGameEvents = true;

        [Header("������������")]
        [SerializeField] private AnnouncementTexts announcementTexts = new AnnouncementTexts();

        private TeacherManager teacherManager;

        #region �����ı�����
        [System.Serializable]
        public class AnnouncementTexts
        {
            [Header("����������")]
            public string[] correctAnswers = {
                "�ܺã�",
                "��ò���",
                "��ȷ��",
                "���㣡",
                "�������֣�"
            };

            public string[] wrongAnswers = {
                "���㶼�ܴ��",
                "����ϸ���룡",
                "�´�Ҫ����һ�㣡",
                "��ȥ�úø�ϰ��",
                "��ô�򵥶����᣿"
            };

            [Header("��Ϸ�¼�����")]
            public string[] gameStart = {
                "��Ϸ��ʼ�ˣ���Ҽ��ͣ�",
                "�����ǿ�ʼ����Ŀ����ʴ�",
                "׼�������𣿿�ʼ���⣡"
            };

            public string[] playerJoined = {
                "��ӭ��ͬѧ��",
                "������һλͬѧ��",
                "��ӭ������ã�"
            };

            public string[] playerLeft = {
                "��ͬѧ�뿪��",
                "�ټ���",
                "�´�������"
            };

            public string[] gameVictory = {
                "��ϲ���ʤ����",
                "�������㣡",
                "���ǽ���Ĺھ���",
                "̫���ˣ�"
            };

            public string[] playerEliminated = {
                "���ź�����̭�ˣ�",
                "�´μ��ͣ�",
                "�����ˣ�",
                "����Ŭ����"
            };

            [Header("����ʹ�ò���")]
            public string[] cardUsageGeneral = {
                "ʹ�������⿨�ƣ�",
                "��Ȥ�Ĳ��ԣ�",
                "����Ч��������"
            };

            public string[] cardUsageTargeted = {
                "��{0}ʹ����{1}��",
                "��{0}������{1}��",
                "{0}��Ϊ��{1}��Ŀ�꣡"
            };
        }
        #endregion

        private void Awake()
        {
            teacherManager = GetComponent<TeacherManager>();

            // �Զ���������������
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

        #region �¼����ģ������ģ���Ӱ��ԭ�ж��ģ�
        private void SubscribeToEvents()
        {
            if (NetworkManager.Instance != null)
            {
                // ������ص��¼�����
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

        #region �¼���������������Ӱ����Ϸ�߼���
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

        #region ������������
        /// <summary>
        /// ��������ʹ�ã����ⲿ���ã�
        /// </summary>
        public void AnnounceCardUsage(string playerName, string cardName, string targetName = null)
        {
            if (!enableAnnouncement || !announceCardUsage) return;

            string message;
            if (string.IsNullOrEmpty(targetName))
            {
                // ��Ŀ�꿨��
                message = GetRandomText(announcementTexts.cardUsageGeneral);
            }
            else
            {
                // ��Ŀ�꿨��
                string template = GetRandomText(announcementTexts.cardUsageTargeted);
                message = string.Format(template, targetName, cardName);
            }

            TeacherSpeechBubble.Say(message, SpeechType.CardAction);
        }

        /// <summary>
        /// �����Զ�����Ϣ
        /// </summary>
        public void AnnounceCustom(string message, SpeechType type = SpeechType.Normal)
        {
            if (!enableAnnouncement) return;
            TeacherSpeechBubble.Say(message, type);
        }

        /// <summary>
        /// �����ִα仯
        /// </summary>
        public void AnnounceTurnChange(string playerName)
        {
            if (!enableAnnouncement) return;
            TeacherSpeechBubble.Say($"�ֵ�{playerName}�ˣ�", SpeechType.Normal);
        }

        /// <summary>
        /// ��ӭ����
        /// </summary>
        public void AnnounceWelcome()
        {
            TeacherSpeechBubble.Say("��ӭ�������ã�", SpeechType.Normal);
        }
        #endregion

        #region ��̬��ݷ������������ű����ã�
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
        /// ��̬��������������ʹ��
        /// </summary>
        public static void AnnounceCard(string playerName, string cardName, string targetName = null)
        {
            Instance?.AnnounceCardUsage(playerName, cardName, targetName);
        }

        /// <summary>
        /// ��̬�����������Զ�����Ϣ
        /// </summary>
        public static void Announce(string message, SpeechType type = SpeechType.Normal)
        {
            Instance?.AnnounceCustom(message, type);
        }

        /// <summary>
        /// ��̬�����������ִα仯
        /// </summary>
        public static void AnnounceTurn(string playerName)
        {
            Instance?.AnnounceTurnChange(playerName);
        }
        #endregion

        #region ��������
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