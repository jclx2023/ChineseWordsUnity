using UnityEngine;
using Photon.Pun;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// �������ͬ����� - ͬ����ҽ�ɫ�Ļ����任����
    /// ��ͬ����Ҫ��λ�ú���ת��Ϣ����ͬ��������ӽ�
    /// </summary>
    public class PlayerNetworkSync : MonoBehaviourPun, IPunObservable
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
        [SerializeField] private bool showNetworkGizmos = false;

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

            // PUN2��PhotonView�����÷�ʽ
            if (photonView != null)
            {
                // PUN2�в�����synchronization���ԣ���Щ������Inspector������
                // photonView.synchronization = ViewSynchronization.UnreliableOnChange; // �Ƴ�����
                // photonView.SendRate = sendRate; // �Ƴ�����

                // PUN2�еķ���Ƶ��ͨ��PhotonNetwork.SendRate���ã�ȫ�֣�
                // ������PhotonView��Inspector������
            }
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
        /// <param name="playerID">���ID</param>
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
            if (!photonView.IsMine) return;

            float currentTime = Time.time;

            // ��鷢�ͼ��
            if (currentTime - lastSendTime < 1f / sendRate) return;

            // ����Ƿ����㹻�ı仯
            if (HasSignificantChange())
            {
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
        /// ��ֵ��Ŀ��λ�ú���ת
        /// </summary>
        private void InterpolateToTarget()
        {
            if (photonView.IsMine) return;

            float deltaTime = Time.deltaTime;
            float speed = interpolationSpeed * deltaTime;

            // λ�ò�ֵ
            if (syncPosition)
            {
                Vector3 newPosition = Vector3.Lerp(playerTransform.position, targetPosition, speed);

                // �������Ԥ�������ٶ����ݣ����Ԥ��λ��
                if (enablePrediction && networkVelocity.magnitude > 0.1f)
                {
                    float timeSinceReceive = Time.time - networkSendTime;
                    Vector3 predictedPosition = targetPosition + networkVelocity * timeSinceReceive;
                    newPosition = Vector3.Lerp(newPosition, predictedPosition, 0.3f);
                }

                playerTransform.position = newPosition;
            }

            // ��ת��ֵ
            if (syncRotation)
            {
                playerTransform.rotation = Quaternion.Lerp(playerTransform.rotation, targetRotation, speed);
            }
        }

        #endregion

        #region Photon����ͬ��

        /// <summary>
        /// Photon�������л�
        /// </summary>
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // ��������
                WriteDataToStream(stream);
            }
            else
            {
                // ��������
                ReadDataFromStream(stream, info);
            }
        }

        /// <summary>
        /// д�����ݵ���
        /// </summary>
        private void WriteDataToStream(PhotonStream stream)
        {
            // ����λ��
            if (syncPosition)
            {
                stream.SendNext(playerTransform.position);

                // �����ٶȣ�����Ԥ�⣩
                if (enablePrediction && lastSendTime > 0)
                {
                    float deltaTime = Time.time - lastSendTime;
                    if (deltaTime > 0)
                    {
                        Vector3 velocity = (playerTransform.position - lastSentPosition) / deltaTime;
                        stream.SendNext(velocity);
                    }
                    else
                    {
                        stream.SendNext(Vector3.zero);
                    }
                }
                else
                {
                    stream.SendNext(Vector3.zero);
                }
            }

            // ������ת
            if (syncRotation)
            {
                stream.SendNext(playerTransform.rotation);
            }

            // ����ʱ���
            stream.SendNext(Time.time);
        }

        /// <summary>
        /// ������ȡ����
        /// </summary>
        private void ReadDataFromStream(PhotonStream stream, PhotonMessageInfo info)
        {
            // ����λ��
            if (syncPosition)
            {
                networkPosition = (Vector3)stream.ReceiveNext();
                networkVelocity = (Vector3)stream.ReceiveNext();
                targetPosition = networkPosition;
            }

            // ������ת
            if (syncRotation)
            {
                networkRotation = (Quaternion)stream.ReceiveNext();
                targetRotation = networkRotation;
            }

            // ����ʱ���
            networkSendTime = (float)stream.ReceiveNext();

            // ���������ӳٲ���
            ApplyNetworkDelayCompensation(info);

            LogDebug($"������������ - λ��: {networkPosition}, ��ת: {networkRotation.eulerAngles}, �ٶ�: {networkVelocity}");
        }

        /// <summary>
        /// Ӧ�������ӳٲ���
        /// </summary>
        private void ApplyNetworkDelayCompensation(PhotonMessageInfo info)
        {
            if (!enablePrediction) return;

            // ���������ӳ�
            double lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));

            // Ӧ���ӳٲ�����Ŀ��λ��
            if (syncPosition && networkVelocity.magnitude > 0.1f)
            {
                targetPosition = networkPosition + networkVelocity * (float)lag;
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

            // PUN2�з���Ƶ�ʲ���ֱ����PhotonView������
            // ��Ҫͨ��PhotonNetwork.SendRateȫ�����û���Inspector������
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

        [ContextMenu("��ʾ����״̬")]
        public void ShowNetworkStatus()
        {
            Debug.Log($"[PlayerNetworkSync] {GetNetworkStatus()}");
        }

        [ContextMenu("ǿ�Ʒ�������")]
        public void ForceSendData()
        {
            if (isLocalPlayer)
            {
                lastSendTime = 0; // ����ʱ����ǿ�Ʒ���
                CheckAndSendData();
            }
        }

        #endregion

        #region ���Կ��ӻ�

        private void OnDrawGizmos()
        {
            if (!showNetworkGizmos || !isInitialized) return;

            // ���Ƶ�ǰλ��
            Gizmos.color = isLocalPlayer ? Color.green : Color.red;
            Gizmos.DrawWireCube(playerTransform.position, Vector3.one * 0.5f);

            // ����Ŀ��λ�ã���Զ����ң�
            if (!isLocalPlayer)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(targetPosition, Vector3.one * 0.3f);

                // ����������
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(playerTransform.position, targetPosition);
            }
        }

        #endregion
    }
}