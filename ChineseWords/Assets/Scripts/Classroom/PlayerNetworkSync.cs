using UnityEngine;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// �������ͬ����� - �Ľ��汾
    /// ͬ����ҽ�ɫ�Ļ����任���ݣ���ͬ��������ӽ�
    /// ʹ��NetworkManager�������ݴ���͹���
    /// </summary>
    public class PlayerNetworkSync : MonoBehaviour
    {
        [Header("ͬ������")]
        [SerializeField] private bool syncPosition = true; // ͬ��λ��
        [SerializeField] private bool syncRotation = true; // ͬ����ת
        [SerializeField] private float sendRate = 15f; // ����Ƶ�ʣ�Hz��
        [SerializeField] private float interpolationSpeed = 12f; // ��ֵ�ٶ�

        [Header("�Ż�����")]
        [SerializeField] private float positionThreshold = 0.05f; // λ�ñ仯��ֵ
        [SerializeField] private float rotationThreshold = 3f; // ��ת�仯��ֵ���ȣ�
        [SerializeField] private bool enablePrediction = true; // ����Ԥ��

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = false;
        [SerializeField] private bool showDetailedStats = false; // ��ʾ��ϸͳ��

        // �����Ϣ
        private ushort playerId;
        private bool isLocalPlayer = false;
        private bool isInitialized = false;

        // ����ͬ�����ݣ�����Զ����ң�
        private Vector3 networkPosition;
        private Quaternion networkRotation;
        private Vector3 networkVelocity;
        private float networkReceiveTime;

        // �������ݣ����ڱ�����ң�
        private Vector3 lastSentPosition;
        private Quaternion lastSentRotation;
        private float lastSendTime;

        // ��ֵ���ݣ�����Զ����ң�
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        // �������
        private Transform playerTransform;
        private Rigidbody playerRigidbody;

        // ͳ������
        private SyncStatistics syncStats = new SyncStatistics();

        // ��������
        public ushort PlayerId => playerId;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsInitialized => isInitialized;
        public Vector3 NetworkPosition => networkPosition;
        public Quaternion NetworkRotation => networkRotation;
        public Vector3 TargetPosition => targetPosition;
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

            [Header("λ��ͳ��")]
            public Vector3 lastPosition;
            public float totalDistance;
            public float maxSpeed;

            [Header("����ͳ��")]
            public float averageSendInterval;
            public float averageReceiveInterval;
            public float maxLag;
            public float averageLag;
            private float totalLag;

            public void UpdateSent(Vector3 position)
            {
                packetsSent++;
                float currentTime = Time.time;

                if (lastSendTime > 0)
                {
                    float interval = currentTime - lastSendTime;
                    averageSendInterval = (averageSendInterval * (packetsSent - 1) + interval) / packetsSent;
                }
                lastSendTime = currentTime;

                if (lastPosition != Vector3.zero)
                {
                    float distance = Vector3.Distance(position, lastPosition);
                    totalDistance += distance;

                    if (lastSendTime > 0)
                    {
                        float speed = distance / (currentTime - lastSendTime);
                        maxSpeed = Mathf.Max(maxSpeed, speed);
                    }
                }
                lastPosition = position;
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
                return $"����:{packetsSent}��({sendRate:F1}/s), ����:{packetsReceived}��({receiveRate:F1}/s), �ƶ�:{totalDistance:F1}m";
            }

            public string GetDetailedString()
            {
                return $"���ͼ��:{averageSendInterval:F3}s, ���ռ��:{averageReceiveInterval:F3}s, " +
                       $"ƽ���ӳ�:{averageLag:F3}s, ����ӳ�:{maxLag:F3}s, ����ٶ�:{maxSpeed:F1}m/s";
            }
        }

        #endregion

        #region Unity��������

        private void Awake()
        {
            playerTransform = transform;
            playerRigidbody = GetComponent<Rigidbody>();
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
                // ������ң�����Ƿ���Ҫ��������
                CheckAndSendData();
            }
            else
            {
                // Զ����ң���ֵ��Ŀ��λ�ú���ת
                InterpolateToTarget();
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
            networkPosition = playerTransform.position;
            networkRotation = playerTransform.rotation;
            targetPosition = networkPosition;
            targetRotation = networkRotation;

            // ��ʼ����������
            lastSentPosition = networkPosition;
            lastSentRotation = networkRotation;

            // ����ͳ������
            syncStats = new SyncStatistics();
            syncStats.lastPosition = networkPosition;

            isInitialized = true;
            RegisterWithNetworkManager();

            LogDebug($"PlayerNetworkSync��ʼ����� - PlayerID: {playerId}, �������: {isLocalPlayer}");

            // ����Ǳ�����ң�֪ͨPlayerCameraController���¼��
            if (isLocalPlayer)
            {
                var cameraController = GetComponent<PlayerCameraController>();
                if (cameraController != null)
                {
                    LogDebug("֪ͨPlayerCameraController���¼�鱾�����״̬");
                }
            }
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
        /// ��鲢��������
        /// </summary>
        private void CheckAndSendData()
        {
            if (!isLocalPlayer || NetworkManager.Instance == null) return;

            float currentTime = Time.time;

            // ��鷢�ͼ��
            if (currentTime - lastSendTime < 1f / sendRate) return;

            // ����Ƿ����㹻�ı仯
            if (HasSignificantChange())
            {
                // �����ٶȣ�����Ԥ�⣩
                Vector3 velocity = Vector3.zero;
                if (enablePrediction && lastSendTime > 0)
                {
                    float deltaTime = currentTime - lastSendTime;
                    if (deltaTime > 0)
                    {
                        velocity = (playerTransform.position - lastSentPosition) / deltaTime;
                    }
                }

                // �������ݵ�NetworkManager
                NetworkManager.Instance.SyncPlayerTransform(playerId, playerTransform.position, playerTransform.rotation, velocity);

                // ����ͳ�ƺͱ��ؼ�¼
                syncStats.UpdateSent(playerTransform.position);
                lastSentPosition = playerTransform.position;
                lastSentRotation = playerTransform.rotation;
                lastSendTime = currentTime;

                LogDebug($"����ͬ������: Pos={lastSentPosition:F2}, Rot={lastSentRotation.eulerAngles:F0}, " +
                         $"Vel={velocity:F2}, ����:{syncStats.packetsSent}");
            }
        }

        /// <summary>
        /// ����Ƿ��������仯
        /// </summary>
        private bool HasSignificantChange()
        {
            // ���λ�ñ仯
            if (syncPosition)
            {
                float positionDelta = Vector3.Distance(playerTransform.position, lastSentPosition);
                if (positionDelta > positionThreshold)
                {
                    LogDebug($"λ�ñ仯����ͬ��: {positionDelta:F3} > {positionThreshold}");
                    return true;
                }
            }

            // �����ת�仯
            if (syncRotation)
            {
                float rotationDelta = Quaternion.Angle(playerTransform.rotation, lastSentRotation);
                if (rotationDelta > rotationThreshold)
                {
                    LogDebug($"��ת�仯����ͬ��: {rotationDelta:F1}�� > {rotationThreshold}��");
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Զ��������ݽ��պͲ�ֵ

        /// <summary>
        /// ��������任���ݣ���NetworkManager���ã�
        /// </summary>
        public void ReceiveNetworkTransform(Vector3 position, Quaternion rotation, Vector3 velocity, float timestamp)
        {
            if (isLocalPlayer) return;

            networkPosition = position;
            networkRotation = rotation;
            networkVelocity = velocity;
            networkReceiveTime = timestamp;

            // ����ͳ��
            syncStats.UpdateReceived(timestamp);

            targetPosition = networkPosition;
            targetRotation = networkRotation;

            // Ӧ�������ӳٲ���
            ApplyNetworkDelayCompensation();

            LogDebug($"������������: Pos={networkPosition:F2}, Rot={networkRotation.eulerAngles:F0}, " +
                     $"Vel={networkVelocity:F2}, �ӳ�:{Time.time - timestamp:F3}s, ����:{syncStats.packetsReceived}");
        }

        /// <summary>
        /// ��ֵ��Ŀ��λ�ú���ת
        /// </summary>
        private void InterpolateToTarget()
        {
            if (isLocalPlayer) return;

            float deltaTime = Time.deltaTime;
            float speed = interpolationSpeed * deltaTime;

            Vector3 oldPosition = playerTransform.position;
            Quaternion oldRotation = playerTransform.rotation;

            // λ�ò�ֵ
            if (syncPosition)
            {
                Vector3 newPosition = Vector3.Lerp(playerTransform.position, targetPosition, speed);
                playerTransform.position = newPosition;
            }

            // ��ת��ֵ
            if (syncRotation)
            {
                playerTransform.rotation = Quaternion.Lerp(playerTransform.rotation, targetRotation, speed);
            }

            // ������Ϣ��ֻ�������Ա仯ʱ���
            if (enableDebugLogs && showDetailedStats)
            {
                float posChange = Vector3.Distance(oldPosition, playerTransform.position);
                float rotChange = Quaternion.Angle(oldRotation, playerTransform.rotation);
                if (posChange > 0.01f || rotChange > 0.5f)
                {
                    LogDebug($"��ֵ����: λ�ñ仯={posChange:F3}, ��ת�仯={rotChange:F1}��, " +
                             $"��Ŀ�����={Vector3.Distance(playerTransform.position, targetPosition):F3}");
                }
            }
        }

        /// <summary>
        /// Ӧ�������ӳٲ���
        /// </summary>
        private void ApplyNetworkDelayCompensation()
        {
            if (!enablePrediction) return;

            // ���������ӳ�
            float lag = Time.time - networkReceiveTime;

            // Ӧ���ӳٲ�����Ŀ��λ��
            if (syncPosition && networkVelocity.magnitude > 0.1f)
            {
                Vector3 compensatedPosition = networkPosition + networkVelocity * lag;

                // ���Ʋ������룬�������Ԥ��
                float maxCompensation = networkVelocity.magnitude * 0.3f; // ��ಹ��0.3��
                Vector3 compensation = compensatedPosition - networkPosition;
                if (compensation.magnitude > maxCompensation)
                {
                    compensation = compensation.normalized * maxCompensation;
                    compensatedPosition = networkPosition + compensation;
                }

                targetPosition = compensatedPosition;

                if (enableDebugLogs && showDetailedStats)
                {
                    LogDebug($"�ӳٲ���: {lag:F3}s, ԭλ��={networkPosition:F2}, ������={targetPosition:F2}");
                }
            }
        }

        #endregion

        #region ���Է���

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerNetworkSync-{playerId}] {message}");
            }
        }
        #endregion
    }
}