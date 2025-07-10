using UnityEngine;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// �������ͬ����� - רעͷ����תͬ���汾
    /// ������Ҳ��ƶ�λ�ã�ȥ��λ�úͽ�ɫ��תͬ����רע��ͷ����ת
    /// ʹ��NetworkManager����ͷ����ת���ݴ���͹���
    /// </summary>
    public class PlayerNetworkSync : MonoBehaviour
    {
        [Header("ͬ������")]
        [SerializeField] private bool syncHeadRotation = true; // ͬ��ͷ����ת
        [SerializeField] private float sendRate = 15f; // ����Ƶ�ʣ�Hz��
        [SerializeField] private float interpolationSpeed = 12f; // ��ֵ�ٶ�

        [Header("�Ż�����")]
        [SerializeField] private float headRotationThreshold = 3f; // ͷ����ת�仯��ֵ���ȣ�
        [SerializeField] private bool enablePrediction = false; // ͷ����ת����ҪԤ��

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = false;

        // �����Ϣ
        private ushort playerId;
        private bool isLocalPlayer = false;
        private bool isInitialized = false;

        // ����ͬ�����ݣ�����Զ����ң�
        private float networkHeadHorizontalAngle;
        private float networkHeadVerticalAngle;
        private float networkReceiveTime;

        // �������ݣ����ڱ�����ң�
        private float lastSentHeadHorizontalAngle;
        private float lastSentHeadVerticalAngle;
        private float lastSendTime;

        // ��ֵ���ݣ�����Զ����ң�
        private float targetHeadHorizontalAngle;
        private float targetHeadVerticalAngle;

        // �������
        private PlayerHeadController headController;

        // ͳ������
        private SyncStatistics syncStats = new SyncStatistics();

        // ��������
        public ushort PlayerId => playerId;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsInitialized => isInitialized;
        public float NetworkHeadHorizontalAngle => networkHeadHorizontalAngle;
        public float NetworkHeadVerticalAngle => networkHeadVerticalAngle;
        public SyncStatistics Stats => syncStats;

        #region ͳ��������

        [System.Serializable]
        public class SyncStatistics
        {
            [Header("����ͳ��")]
            public int packetsSent;
            public int packetsReceived;
            public float lastSendTime;
            public float lastReceiveTime;

            [Header("ͷ����תͳ��")]
            public float lastHeadHorizontalAngle;
            public float lastHeadVerticalAngle;
            public float totalHeadRotation;
            public float maxHeadRotationSpeed;

            [Header("����ͳ��")]
            public float averageSendInterval;
            public float averageReceiveInterval;
            public float maxLag;
            public float averageLag;
            private float totalLag;

            public void UpdateSent(float headH, float headV)
            {
                packetsSent++;
                float currentTime = Time.time;

                if (lastSendTime > 0)
                {
                    float interval = currentTime - lastSendTime;
                    averageSendInterval = (averageSendInterval * (packetsSent - 1) + interval) / packetsSent;
                }
                lastSendTime = currentTime;

                // ����ͷ����תͳ��
                if (packetsSent > 1)
                {
                    float headRotationDelta = Mathf.Abs(headH - lastHeadHorizontalAngle) + Mathf.Abs(headV - lastHeadVerticalAngle);
                    totalHeadRotation += headRotationDelta;

                    if (lastSendTime > 0)
                    {
                        float headRotationSpeed = headRotationDelta / (currentTime - lastSendTime);
                        maxHeadRotationSpeed = Mathf.Max(maxHeadRotationSpeed, headRotationSpeed);
                    }
                }
                lastHeadHorizontalAngle = headH;
                lastHeadVerticalAngle = headV;
            }

            public void UpdateReceived(float timestamp)
            {
                packetsReceived++;
                float currentTime = Time.time;
                lastReceiveTime = currentTime;

                // �����ӳ�
                float lag = currentTime - timestamp;
                maxLag = Mathf.Max(maxLag, lag);
                totalLag += lag;
                averageLag = totalLag / packetsReceived;

                if (packetsReceived > 1)
                {
                    float interval = currentTime - lastReceiveTime;
                    averageReceiveInterval = (averageReceiveInterval * (packetsReceived - 1) + interval) / packetsReceived;
                }
            }

            public string GetSummaryString()
            {
                float sendRate = packetsSent > 0 && lastSendTime > 0 ? packetsSent / lastSendTime : 0f;
                float receiveRate = packetsReceived > 0 && lastReceiveTime > 0 ? packetsReceived / lastReceiveTime : 0f;
                return $"ͷ��ͬ�� - ����:{packetsSent}��({sendRate:F1}/s), ����:{packetsReceived}��({receiveRate:F1}/s), ��ת��:{totalHeadRotation:F0}��";
            }

            public string GetDetailedString()
            {
                return $"���ͼ��:{averageSendInterval:F3}s, ���ռ��:{averageReceiveInterval:F3}s, " +
                       $"ƽ���ӳ�:{averageLag:F3}s, ����ӳ�:{maxLag:F3}s, ���ͷ��ת��:{maxHeadRotationSpeed:F1}��/s";
            }
        }

        #endregion

        #region Unity��������

        private void Awake()
        {
            headController = GetComponent<PlayerHeadController>();
        }

        private void Start()
        {
            if (isInitialized)
            {
                CheckLocalPlayerStatus();
                RegisterWithNetworkManager();
            }
        }

        private void Update()
        {
            if (!isInitialized) return;

            if (isLocalPlayer)
            {
                // ������ң�����Ƿ���Ҫ����ͷ������
                CheckAndSendHeadData();
            }
            else
            {
                // Զ����ң���ֵͷ����ת
                InterpolateHeadRotation();
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null && isInitialized)
            {
                NetworkManager.Instance.UnregisterPlayerSync(playerId);
                LogDebug($"��NetworkManagerע��: PlayerId={playerId}");
            }
        }

        #endregion

        #region ��ʼ��

        /// <summary>
        /// ��ʼ���������ͬ��
        /// </summary>
        public void Initialize(ushort playerID)
        {
            playerId = playerID;
            CheckLocalPlayerStatus();

            // ��ʼ����������
            networkHeadHorizontalAngle = 0f;
            networkHeadVerticalAngle = 0f;

            targetHeadHorizontalAngle = 0f;
            targetHeadVerticalAngle = 0f;

            // ��ʼ����������
            lastSentHeadHorizontalAngle = 0f;
            lastSentHeadVerticalAngle = 0f;

            // ����ͳ������
            syncStats = new SyncStatistics();

            isInitialized = true;
            RegisterWithNetworkManager();

            LogDebug($"PlayerNetworkSync��ʼ����� - PlayerID: {playerId}, �������: {isLocalPlayer}");
        }

        /// <summary>
        /// ��鱾�����״̬
        /// </summary>
        private void CheckLocalPlayerStatus()
        {
            if (NetworkManager.Instance != null)
            {
                isLocalPlayer = (playerId == NetworkManager.Instance.ClientId);
                LogDebug($"��鱾�����״̬: PlayerID {playerId}, ���ؿͻ���ID {NetworkManager.Instance.ClientId}, ���: {isLocalPlayer}");
            }
        }

        /// <summary>
        /// ��NetworkManagerע������
        /// </summary>
        private void RegisterWithNetworkManager()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RegisterPlayerSync(playerId, this);
                LogDebug($"��NetworkManagerע��: PlayerId={playerId}");
            }
        }

        #endregion

        #region ����������ݷ���

        /// <summary>
        /// ��鲢����ͷ������
        /// </summary>
        private void CheckAndSendHeadData()
        {
            // ��ȫ��飺���粻����ʱֹͣ����
            if (!isLocalPlayer || !syncHeadRotation ||
                NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                return;
            }

            float currentTime = Time.time;

            // ��鷢�ͼ��
            if (currentTime - lastSendTime < 1f / sendRate) return;

            // ��ȡ��ǰͷ����ת����
            float currentHeadHorizontal = 0f;
            float currentHeadVertical = 0f;
            if (headController != null && headController.IsInitialized)
            {
                currentHeadHorizontal = headController.CurrentHeadHorizontalAngle;
                currentHeadVertical = headController.CurrentHeadVerticalAngle;
            }

            // ����Ƿ����㹻�ı仯
            if (HasSignificantHeadRotationChange(currentHeadHorizontal, currentHeadVertical))
            {
                // ����ͷ����ת����
                SendHeadRotationData(currentHeadHorizontal, currentHeadVertical);

                // ����ͳ�ƺͱ��ؼ�¼
                syncStats.UpdateSent(currentHeadHorizontal, currentHeadVertical);
                lastSentHeadHorizontalAngle = currentHeadHorizontal;
                lastSentHeadVerticalAngle = currentHeadVertical;
                lastSendTime = currentTime;

                LogDebug($"����ͷ����ת����: H={currentHeadHorizontal:F1}��, V={currentHeadVertical:F1}��, ����:{syncStats.packetsSent}");
            }
        }

        /// <summary>
        /// ��PlayerHeadController���ã�����ͷ����ת����
        /// </summary>
        public void SendHeadRotation(float horizontalAngle, float verticalAngle)
        {
            if (!isLocalPlayer || !syncHeadRotation) return;

            SendHeadRotationData(horizontalAngle, verticalAngle);

            // ���·��ͼ�¼
            lastSentHeadHorizontalAngle = horizontalAngle;
            lastSentHeadVerticalAngle = verticalAngle;
            lastSendTime = Time.time;

            LogDebug($"PlayerHeadController������ͷ����ת: H={horizontalAngle:F1}��, V={verticalAngle:F1}��");
        }

        /// <summary>
        /// ����ͷ����ת���ݵ�����
        /// </summary>
        private void SendHeadRotationData(float horizontalAngle, float verticalAngle)
        {
            // ��ȫ��飺ȷ��NetworkManager���������ӿ���
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                return; // ��Ĭ���أ����������
            }

            NetworkManager.Instance.SyncPlayerHeadRotation(playerId, horizontalAngle, verticalAngle);
        }

        /// <summary>
        /// ���ͷ����ת�Ƿ��������仯
        /// </summary>
        private bool HasSignificantHeadRotationChange(float currentHeadHorizontal, float currentHeadVertical)
        {
            float horizontalDelta = Mathf.Abs(currentHeadHorizontal - lastSentHeadHorizontalAngle);
            float verticalDelta = Mathf.Abs(currentHeadVertical - lastSentHeadVerticalAngle);

            if (horizontalDelta > headRotationThreshold || verticalDelta > headRotationThreshold)
            {
                LogDebug($"ͷ����ת�仯����ͬ��: H={horizontalDelta:F1}��, V={verticalDelta:F1}�� > {headRotationThreshold}��");
                return true;
            }

            return false;
        }

        #endregion

        #region Զ��������ݽ��պͲ�ֵ

        /// <summary>
        /// ��������ͷ����ת���ݣ���NetworkManager���ã�
        /// </summary>
        public void ReceiveNetworkHeadRotation(float headHorizontal, float headVertical, float timestamp)
        {
            if (isLocalPlayer) return;

            networkHeadHorizontalAngle = headHorizontal;
            networkHeadVerticalAngle = headVertical;
            networkReceiveTime = timestamp;

            targetHeadHorizontalAngle = headHorizontal;
            targetHeadVerticalAngle = headVertical;

            // ����ͳ��
            syncStats.UpdateReceived(timestamp);

            LogDebug($"��������ͷ����ת: H={headHorizontal:F1}��, V={headVertical:F1}��, �ӳ�:{Time.time - timestamp:F3}s, ����:{syncStats.packetsReceived}");
        }

        /// <summary>
        /// ��ֵͷ����ת
        /// </summary>
        private void InterpolateHeadRotation()
        {
            if (isLocalPlayer || headController == null) return;

            float deltaTime = Time.deltaTime;
            float speed = interpolationSpeed * deltaTime;

            // ��ȡ��ǰͷ���Ƕ�
            float currentHeadH = headController.CurrentHeadHorizontalAngle;
            float currentHeadV = headController.CurrentHeadVerticalAngle;

            // ��ֵ��Ŀ��Ƕ�
            float newHeadH = Mathf.LerpAngle(currentHeadH, targetHeadHorizontalAngle, speed);
            float newHeadV = Mathf.LerpAngle(currentHeadV, targetHeadVerticalAngle, speed);

            // Ӧ�õ�ͷ��������
            headController.ReceiveNetworkHeadRotation(newHeadH, newHeadV);
        }

        #endregion

        #region ���Է���

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[PlayerNetworkSync-{playerId}] {message}");
            }
        }
        #endregion
    }
}