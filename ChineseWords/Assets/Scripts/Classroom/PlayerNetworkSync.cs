using UnityEngine;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// 玩家网络同步组件 - 改进版本
    /// 同步玩家角色的基本变换数据，不同步摄像机视角
    /// 使用NetworkManager进行数据传输和管理
    /// </summary>
    public class PlayerNetworkSync : MonoBehaviour
    {
        [Header("同步配置")]
        [SerializeField] private bool syncPosition = true; // 同步位置
        [SerializeField] private bool syncRotation = true; // 同步旋转
        [SerializeField] private float sendRate = 15f; // 发送频率（Hz）
        [SerializeField] private float interpolationSpeed = 12f; // 插值速度

        [Header("优化设置")]
        [SerializeField] private float positionThreshold = 0.05f; // 位置变化阈值
        [SerializeField] private float rotationThreshold = 3f; // 旋转变化阈值（度）
        [SerializeField] private bool enablePrediction = true; // 启用预测

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = false;
        [SerializeField] private bool showDetailedStats = false; // 显示详细统计

        // 玩家信息
        private ushort playerId;
        private bool isLocalPlayer = false;
        private bool isInitialized = false;

        // 网络同步数据（用于远程玩家）
        private Vector3 networkPosition;
        private Quaternion networkRotation;
        private Vector3 networkVelocity;
        private float networkReceiveTime;

        // 本地数据（用于本地玩家）
        private Vector3 lastSentPosition;
        private Quaternion lastSentRotation;
        private float lastSendTime;

        // 插值数据（用于远程玩家）
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        // 组件引用
        private Transform playerTransform;
        private Rigidbody playerRigidbody;

        // 统计数据
        private SyncStatistics syncStats = new SyncStatistics();

        // 公共属性
        public ushort PlayerId => playerId;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsInitialized => isInitialized;
        public Vector3 NetworkPosition => networkPosition;
        public Quaternion NetworkRotation => networkRotation;
        public Vector3 TargetPosition => targetPosition;
        public SyncStatistics Stats => syncStats;

        #region 统计数据类

        [System.Serializable]
        public class SyncStatistics
        {
            [Header("网络统计")]
            public int packetsSent;
            public int packetsReceived;
            public float lastSendTime;
            public float lastReceiveTime;

            [Header("位置统计")]
            public Vector3 lastPosition;
            public float totalDistance;
            public float maxSpeed;

            [Header("性能统计")]
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

                // 计算延迟
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
                return $"发送:{packetsSent}包({sendRate:F1}/s), 接收:{packetsReceived}包({receiveRate:F1}/s), 移动:{totalDistance:F1}m";
            }

            public string GetDetailedString()
            {
                return $"发送间隔:{averageSendInterval:F3}s, 接收间隔:{averageReceiveInterval:F3}s, " +
                       $"平均延迟:{averageLag:F3}s, 最大延迟:{maxLag:F3}s, 最大速度:{maxSpeed:F1}m/s";
            }
        }

        #endregion

        #region Unity生命周期

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
                // 本地玩家：检查是否需要发送数据
                CheckAndSendData();
            }
            else
            {
                // 远程玩家：插值到目标位置和旋转
                InterpolateToTarget();
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null && isInitialized)
            {
                NetworkManager.Instance.UnregisterPlayerSync(playerId);
                LogDebug($"从NetworkManager注销: PlayerId={playerId}");
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化玩家网络同步
        /// </summary>
        public void Initialize(ushort playerID)
        {
            playerId = playerID;
            CheckLocalPlayerStatus();

            // 初始化网络数据
            networkPosition = playerTransform.position;
            networkRotation = playerTransform.rotation;
            targetPosition = networkPosition;
            targetRotation = networkRotation;

            // 初始化本地数据
            lastSentPosition = networkPosition;
            lastSentRotation = networkRotation;

            // 重置统计数据
            syncStats = new SyncStatistics();
            syncStats.lastPosition = networkPosition;

            isInitialized = true;
            RegisterWithNetworkManager();

            LogDebug($"PlayerNetworkSync初始化完成 - PlayerID: {playerId}, 本地玩家: {isLocalPlayer}");

            // 如果是本地玩家，通知PlayerCameraController重新检查
            if (isLocalPlayer)
            {
                var cameraController = GetComponent<PlayerCameraController>();
                if (cameraController != null)
                {
                    LogDebug("通知PlayerCameraController重新检查本地玩家状态");
                }
            }
        }

        /// <summary>
        /// 检查本地玩家状态
        /// </summary>
        private void CheckLocalPlayerStatus()
        {
            if (NetworkManager.Instance != null)
            {
                isLocalPlayer = (playerId == NetworkManager.Instance.ClientId);
                LogDebug($"检查本地玩家状态: PlayerID {playerId}, 本地客户端ID {NetworkManager.Instance.ClientId}, 结果: {isLocalPlayer}");
            }
        }

        /// <summary>
        /// 向NetworkManager注册此组件
        /// </summary>
        private void RegisterWithNetworkManager()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RegisterPlayerSync(playerId, this);
                LogDebug($"向NetworkManager注册: PlayerId={playerId}");
            }
        }

        #endregion

        #region 本地玩家数据发送

        /// <summary>
        /// 检查并发送数据
        /// </summary>
        private void CheckAndSendData()
        {
            if (!isLocalPlayer || NetworkManager.Instance == null) return;

            float currentTime = Time.time;

            // 检查发送间隔
            if (currentTime - lastSendTime < 1f / sendRate) return;

            // 检查是否有足够的变化
            if (HasSignificantChange())
            {
                // 计算速度（用于预测）
                Vector3 velocity = Vector3.zero;
                if (enablePrediction && lastSendTime > 0)
                {
                    float deltaTime = currentTime - lastSendTime;
                    if (deltaTime > 0)
                    {
                        velocity = (playerTransform.position - lastSentPosition) / deltaTime;
                    }
                }

                // 发送数据到NetworkManager
                NetworkManager.Instance.SyncPlayerTransform(playerId, playerTransform.position, playerTransform.rotation, velocity);

                // 更新统计和本地记录
                syncStats.UpdateSent(playerTransform.position);
                lastSentPosition = playerTransform.position;
                lastSentRotation = playerTransform.rotation;
                lastSendTime = currentTime;

                LogDebug($"发送同步数据: Pos={lastSentPosition:F2}, Rot={lastSentRotation.eulerAngles:F0}, " +
                         $"Vel={velocity:F2}, 包数:{syncStats.packetsSent}");
            }
        }

        /// <summary>
        /// 检查是否有显著变化
        /// </summary>
        private bool HasSignificantChange()
        {
            // 检查位置变化
            if (syncPosition)
            {
                float positionDelta = Vector3.Distance(playerTransform.position, lastSentPosition);
                if (positionDelta > positionThreshold)
                {
                    LogDebug($"位置变化触发同步: {positionDelta:F3} > {positionThreshold}");
                    return true;
                }
            }

            // 检查旋转变化
            if (syncRotation)
            {
                float rotationDelta = Quaternion.Angle(playerTransform.rotation, lastSentRotation);
                if (rotationDelta > rotationThreshold)
                {
                    LogDebug($"旋转变化触发同步: {rotationDelta:F1}° > {rotationThreshold}°");
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 远程玩家数据接收和插值

        /// <summary>
        /// 接收网络变换数据（由NetworkManager调用）
        /// </summary>
        public void ReceiveNetworkTransform(Vector3 position, Quaternion rotation, Vector3 velocity, float timestamp)
        {
            if (isLocalPlayer) return;

            networkPosition = position;
            networkRotation = rotation;
            networkVelocity = velocity;
            networkReceiveTime = timestamp;

            // 更新统计
            syncStats.UpdateReceived(timestamp);

            targetPosition = networkPosition;
            targetRotation = networkRotation;

            // 应用网络延迟补偿
            ApplyNetworkDelayCompensation();

            LogDebug($"接收网络数据: Pos={networkPosition:F2}, Rot={networkRotation.eulerAngles:F0}, " +
                     $"Vel={networkVelocity:F2}, 延迟:{Time.time - timestamp:F3}s, 包数:{syncStats.packetsReceived}");
        }

        /// <summary>
        /// 插值到目标位置和旋转
        /// </summary>
        private void InterpolateToTarget()
        {
            if (isLocalPlayer) return;

            float deltaTime = Time.deltaTime;
            float speed = interpolationSpeed * deltaTime;

            Vector3 oldPosition = playerTransform.position;
            Quaternion oldRotation = playerTransform.rotation;

            // 位置插值
            if (syncPosition)
            {
                Vector3 newPosition = Vector3.Lerp(playerTransform.position, targetPosition, speed);
                playerTransform.position = newPosition;
            }

            // 旋转插值
            if (syncRotation)
            {
                playerTransform.rotation = Quaternion.Lerp(playerTransform.rotation, targetRotation, speed);
            }

            // 调试信息：只在有明显变化时输出
            if (enableDebugLogs && showDetailedStats)
            {
                float posChange = Vector3.Distance(oldPosition, playerTransform.position);
                float rotChange = Quaternion.Angle(oldRotation, playerTransform.rotation);
                if (posChange > 0.01f || rotChange > 0.5f)
                {
                    LogDebug($"插值更新: 位置变化={posChange:F3}, 旋转变化={rotChange:F1}°, " +
                             $"到目标距离={Vector3.Distance(playerTransform.position, targetPosition):F3}");
                }
            }
        }

        /// <summary>
        /// 应用网络延迟补偿
        /// </summary>
        private void ApplyNetworkDelayCompensation()
        {
            if (!enablePrediction) return;

            // 计算网络延迟
            float lag = Time.time - networkReceiveTime;

            // 应用延迟补偿到目标位置
            if (syncPosition && networkVelocity.magnitude > 0.1f)
            {
                Vector3 compensatedPosition = networkPosition + networkVelocity * lag;

                // 限制补偿距离，避免过度预测
                float maxCompensation = networkVelocity.magnitude * 0.3f; // 最多补偿0.3秒
                Vector3 compensation = compensatedPosition - networkPosition;
                if (compensation.magnitude > maxCompensation)
                {
                    compensation = compensation.normalized * maxCompensation;
                    compensatedPosition = networkPosition + compensation;
                }

                targetPosition = compensatedPosition;

                if (enableDebugLogs && showDetailedStats)
                {
                    LogDebug($"延迟补偿: {lag:F3}s, 原位置={networkPosition:F2}, 补偿后={targetPosition:F2}");
                }
            }
        }

        #endregion

        #region 调试方法

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