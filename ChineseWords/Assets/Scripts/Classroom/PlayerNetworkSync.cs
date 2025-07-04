using UnityEngine;
using Core.Network;
using Photon.Pun;

namespace Classroom.Player
{
    /// <summary>
    /// �������ͬ����� - ͬ����ҽ�ɫ�Ļ����任����
    /// ��ͬ����Ҫ��λ�ú���ת��Ϣ����ͬ��������ӽ�
    /// ʹ��ͳһ��NetworkManager�������ݴ���
    /// </summary>
    public class PlayerNetworkSync : MonoBehaviour
    {
        [Header("ͬ������")]
        [SerializeField] private bool syncPosition = true; // ͬ��λ��
        [SerializeField] private bool syncRotation = true; // ͬ����ת
        [SerializeField] private float sendRate = 10f; // ����Ƶ�ʣ�Hz��
        [SerializeField] private float interpolationSpeed = 10f; // ��ֵ�ٶ�

        [Header("�Ż�����")]
        [SerializeField] private float positionThreshold = 0.1f; // λ�ñ仯��ֵ
        [SerializeField] private float rotationThreshold = 5f; // ��ת�仯��ֵ���ȣ�
        [SerializeField] private bool enablePrediction = true; // ����Ԥ��

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = false;

        // �����Ϣ
        private ushort playerId;
        private bool isLocalPlayer = false;
        private bool isInitialized = false;

        // ����ͬ������
        private Vector3 networkPosition;
        private Quaternion networkRotation;
        private Vector3 networkVelocity;
        private float networkSendTime;

        // ��������
        private Vector3 lastSentPosition;
        private Quaternion lastSentRotation;
        private float lastSendTime;

        // ��ֵ����
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        // �������
        private Transform playerTransform;
        private Rigidbody playerRigidbody;

        // ��������
        public ushort PlayerId => playerId;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsInitialized => isInitialized;

        #region Unity��������

        private void Awake()
        {
            playerTransform = transform;
            playerRigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            // ����Ѿ���ʼ�������鱾�����״̬
            if (isInitialized)
            {
                CheckLocalPlayerStatus();
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

            isInitialized = true;
            LogDebug($"PlayerNetworkSync��ʼ����� - PlayerID: {playerId}, �������: {isLocalPlayer}");

            // ����Ǳ�����ң�֪ͨPlayerCameraController���¼��
            if (isLocalPlayer)
            {
                var cameraController = GetComponent<PlayerCameraController>();
                if (cameraController != null)
                {
                    // �������¼�飨ͨ���������˽�з�������ӹ���������
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

        #endregion

        #region ����������ݷ���

        /// <summary>
        /// ��鲢��������
        /// </summary>
        private void CheckAndSendData()
        {
            if (!isLocalPlayer) return;

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

                // ��������
                NetworkManager.Instance.SyncPlayerTransform(playerId, playerTransform.position, playerTransform.rotation, velocity);

                // ��������͵�����
                lastSentPosition = playerTransform.position;
                lastSentRotation = playerTransform.rotation;
                lastSendTime = currentTime;

                LogDebug($"����λ������: {lastSentPosition}, ��ת: {lastSentRotation.eulerAngles}");
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
                    return true;
                }
            }

            // �����ת�仯
            if (syncRotation)
            {
                float rotationDelta = Quaternion.Angle(playerTransform.rotation, lastSentRotation);
                if (rotationDelta > rotationThreshold)
                {
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
            networkSendTime = timestamp;

            targetPosition = networkPosition;
            targetRotation = networkRotation;

            // Ӧ�������ӳٲ���
            ApplyNetworkDelayCompensation();

            LogDebug($"������������ - λ��: {networkPosition}, ��ת: {networkRotation.eulerAngles}, �ٶ�: {networkVelocity}");
        }

        /// <summary>
        /// ��ֵ��Ŀ��λ�ú���ת
        /// </summary>
        private void InterpolateToTarget()
        {
            if (isLocalPlayer) return;

            float deltaTime = Time.deltaTime;
            float speed = interpolationSpeed * deltaTime;

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
        }

        /// <summary>
        /// Ӧ�������ӳٲ���
        /// </summary>
        private void ApplyNetworkDelayCompensation()
        {
            if (!enablePrediction) return;

            // ���������ӳ�
            float lag = Time.time - networkSendTime;

            // Ӧ���ӳٲ�����Ŀ��λ��
            if (syncPosition && networkVelocity.magnitude > 0.1f)
            {
                targetPosition = networkPosition + networkVelocity * lag;
            }

            LogDebug($"�����ӳٲ���: {lag:F3}s, ������λ��: {targetPosition}");
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ǿ��ͬ����ָ��λ��
        /// </summary>
        public void ForceSync(Vector3 position, Quaternion rotation)
        {
            if (!isLocalPlayer) return;

            playerTransform.position = position;
            playerTransform.rotation = rotation;

            // ������������
            lastSentPosition = position;
            lastSentRotation = rotation;
            lastSendTime = Time.time;

            LogDebug($"ǿ��ͬ����λ��: {position}, ��ת: {rotation.eulerAngles}");
        }

        /// <summary>
        /// ����ͬ������
        /// </summary>
        public void SetSyncSettings(bool position, bool rotation, float rate)
        {
            syncPosition = position;
            syncRotation = rotation;
            sendRate = rate;

            LogDebug($"ͬ�����ø��� - λ��: {syncPosition}, ��ת: {syncRotation}, Ƶ��: {sendRate}Hz");
        }

        /// <summary>
        /// ��ȡ����״̬��Ϣ
        /// </summary>
        public string GetNetworkStatus()
        {
            if (!isInitialized) return "δ��ʼ��";

            string status = $"PlayerID: {playerId}, ����: {isLocalPlayer}, ";
            status += $"λ��: {playerTransform.position:F1}, ";
            status += $"Ŀ��λ��: {targetPosition:F1}, ";
            status += $"�����ӳ�: {PhotonNetwork.GetPing()}ms";

            return status;
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