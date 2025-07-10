using UnityEngine;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// 玩家网络同步组件 - 专注头部旋转同步版本
    /// 由于玩家不移动位置，去除位置和角色旋转同步，专注于头部旋转
    /// 使用NetworkManager进行头部旋转数据传输和管理
    /// </summary>
    public class PlayerNetworkSync : MonoBehaviour
    {
        [Header("同步配置")]
        [SerializeField] private bool syncHeadRotation = true; // 同步头部旋转
        [SerializeField] private float sendRate = 15f; // 发送频率（Hz）
        [SerializeField] private float interpolationSpeed = 12f; // 插值速度

        [Header("优化设置")]
        [SerializeField] private float headRotationThreshold = 3f; // 头部旋转变化阈值（度）
        [SerializeField] private bool enablePrediction = false; // 头部旋转不需要预测

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = false;

        // 玩家信息
        private ushort playerId;
        private bool isLocalPlayer = false;
        private bool isInitialized = false;

        // 网络同步数据（用于远程玩家）
        private float networkHeadHorizontalAngle;
        private float networkHeadVerticalAngle;
        private float networkReceiveTime;

        // 本地数据（用于本地玩家）
        private float lastSentHeadHorizontalAngle;
        private float lastSentHeadVerticalAngle;
        private float lastSendTime;

        // 插值数据（用于远程玩家）
        private float targetHeadHorizontalAngle;
        private float targetHeadVerticalAngle;

        // 组件引用
        private PlayerHeadController headController;

        // 统计数据
        private SyncStatistics syncStats = new SyncStatistics();

        // 公共属性
        public ushort PlayerId => playerId;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsInitialized => isInitialized;
        public float NetworkHeadHorizontalAngle => networkHeadHorizontalAngle;
        public float NetworkHeadVerticalAngle => networkHeadVerticalAngle;
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

            [Header("头部旋转统计")]
            public float lastHeadHorizontalAngle;
            public float lastHeadVerticalAngle;
            public float totalHeadRotation;
            public float maxHeadRotationSpeed;

            [Header("性能统计")]
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

                // 更新头部旋转统计
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
                return $"头部同步 - 发送:{packetsSent}包({sendRate:F1}/s), 接收:{packetsReceived}包({receiveRate:F1}/s), 总转动:{totalHeadRotation:F0}°";
            }

            public string GetDetailedString()
            {
                return $"发送间隔:{averageSendInterval:F3}s, 接收间隔:{averageReceiveInterval:F3}s, " +
                       $"平均延迟:{averageLag:F3}s, 最大延迟:{maxLag:F3}s, 最大头部转速:{maxHeadRotationSpeed:F1}°/s";
            }
        }

        #endregion

        #region Unity生命周期

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
                // 本地玩家：检查是否需要发送头部数据
                CheckAndSendHeadData();
            }
            else
            {
                // 远程玩家：插值头部旋转
                InterpolateHeadRotation();
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
            networkHeadHorizontalAngle = 0f;
            networkHeadVerticalAngle = 0f;

            targetHeadHorizontalAngle = 0f;
            targetHeadVerticalAngle = 0f;

            // 初始化本地数据
            lastSentHeadHorizontalAngle = 0f;
            lastSentHeadVerticalAngle = 0f;

            // 重置统计数据
            syncStats = new SyncStatistics();

            isInitialized = true;
            RegisterWithNetworkManager();

            LogDebug($"PlayerNetworkSync初始化完成 - PlayerID: {playerId}, 本地玩家: {isLocalPlayer}");
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
        /// 检查并发送头部数据
        /// </summary>
        private void CheckAndSendHeadData()
        {
            // 安全检查：网络不可用时停止发送
            if (!isLocalPlayer || !syncHeadRotation ||
                NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                return;
            }

            float currentTime = Time.time;

            // 检查发送间隔
            if (currentTime - lastSendTime < 1f / sendRate) return;

            // 获取当前头部旋转数据
            float currentHeadHorizontal = 0f;
            float currentHeadVertical = 0f;
            if (headController != null && headController.IsInitialized)
            {
                currentHeadHorizontal = headController.CurrentHeadHorizontalAngle;
                currentHeadVertical = headController.CurrentHeadVerticalAngle;
            }

            // 检查是否有足够的变化
            if (HasSignificantHeadRotationChange(currentHeadHorizontal, currentHeadVertical))
            {
                // 发送头部旋转数据
                SendHeadRotationData(currentHeadHorizontal, currentHeadVertical);

                // 更新统计和本地记录
                syncStats.UpdateSent(currentHeadHorizontal, currentHeadVertical);
                lastSentHeadHorizontalAngle = currentHeadHorizontal;
                lastSentHeadVerticalAngle = currentHeadVertical;
                lastSendTime = currentTime;

                LogDebug($"发送头部旋转数据: H={currentHeadHorizontal:F1}°, V={currentHeadVertical:F1}°, 包数:{syncStats.packetsSent}");
            }
        }

        /// <summary>
        /// 由PlayerHeadController调用，发送头部旋转数据
        /// </summary>
        public void SendHeadRotation(float horizontalAngle, float verticalAngle)
        {
            if (!isLocalPlayer || !syncHeadRotation) return;

            SendHeadRotationData(horizontalAngle, verticalAngle);

            // 更新发送记录
            lastSentHeadHorizontalAngle = horizontalAngle;
            lastSentHeadVerticalAngle = verticalAngle;
            lastSendTime = Time.time;

            LogDebug($"PlayerHeadController请求发送头部旋转: H={horizontalAngle:F1}°, V={verticalAngle:F1}°");
        }

        /// <summary>
        /// 发送头部旋转数据到网络
        /// </summary>
        private void SendHeadRotationData(float horizontalAngle, float verticalAngle)
        {
            // 安全检查：确保NetworkManager和网络连接可用
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                return; // 静默返回，不输出错误
            }

            NetworkManager.Instance.SyncPlayerHeadRotation(playerId, horizontalAngle, verticalAngle);
        }

        /// <summary>
        /// 检查头部旋转是否有显著变化
        /// </summary>
        private bool HasSignificantHeadRotationChange(float currentHeadHorizontal, float currentHeadVertical)
        {
            float horizontalDelta = Mathf.Abs(currentHeadHorizontal - lastSentHeadHorizontalAngle);
            float verticalDelta = Mathf.Abs(currentHeadVertical - lastSentHeadVerticalAngle);

            if (horizontalDelta > headRotationThreshold || verticalDelta > headRotationThreshold)
            {
                LogDebug($"头部旋转变化触发同步: H={horizontalDelta:F1}°, V={verticalDelta:F1}° > {headRotationThreshold}°");
                return true;
            }

            return false;
        }

        #endregion

        #region 远程玩家数据接收和插值

        /// <summary>
        /// 接收网络头部旋转数据（由NetworkManager调用）
        /// </summary>
        public void ReceiveNetworkHeadRotation(float headHorizontal, float headVertical, float timestamp)
        {
            if (isLocalPlayer) return;

            networkHeadHorizontalAngle = headHorizontal;
            networkHeadVerticalAngle = headVertical;
            networkReceiveTime = timestamp;

            targetHeadHorizontalAngle = headHorizontal;
            targetHeadVerticalAngle = headVertical;

            // 更新统计
            syncStats.UpdateReceived(timestamp);

            LogDebug($"接收网络头部旋转: H={headHorizontal:F1}°, V={headVertical:F1}°, 延迟:{Time.time - timestamp:F3}s, 包数:{syncStats.packetsReceived}");
        }

        /// <summary>
        /// 插值头部旋转
        /// </summary>
        private void InterpolateHeadRotation()
        {
            if (isLocalPlayer || headController == null) return;

            float deltaTime = Time.deltaTime;
            float speed = interpolationSpeed * deltaTime;

            // 获取当前头部角度
            float currentHeadH = headController.CurrentHeadHorizontalAngle;
            float currentHeadV = headController.CurrentHeadVerticalAngle;

            // 插值到目标角度
            float newHeadH = Mathf.LerpAngle(currentHeadH, targetHeadHorizontalAngle, speed);
            float newHeadV = Mathf.LerpAngle(currentHeadV, targetHeadVerticalAngle, speed);

            // 应用到头部控制器
            headController.ReceiveNetworkHeadRotation(newHeadH, newHeadV);
        }

        #endregion

        #region 调试方法

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