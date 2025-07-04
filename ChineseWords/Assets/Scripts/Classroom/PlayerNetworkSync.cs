using UnityEngine;
using Core.Network;
using Photon.Pun;

namespace Classroom.Player
{
    /// <summary>
    /// 玩家网络同步组件 - 同步玩家角色的基本变换数据
    /// 仅同步必要的位置和旋转信息，不同步摄像机视角
    /// 使用统一的NetworkManager进行数据传输
    /// </summary>
    public class PlayerNetworkSync : MonoBehaviour
    {
        [Header("同步配置")]
        [SerializeField] private bool syncPosition = true; // 同步位置
        [SerializeField] private bool syncRotation = true; // 同步旋转
        [SerializeField] private float sendRate = 10f; // 发送频率（Hz）
        [SerializeField] private float interpolationSpeed = 10f; // 插值速度

        [Header("优化设置")]
        [SerializeField] private float positionThreshold = 0.1f; // 位置变化阈值
        [SerializeField] private float rotationThreshold = 5f; // 旋转变化阈值（度）
        [SerializeField] private bool enablePrediction = true; // 启用预测

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = false;

        // 玩家信息
        private ushort playerId;
        private bool isLocalPlayer = false;
        private bool isInitialized = false;

        // 网络同步数据
        private Vector3 networkPosition;
        private Quaternion networkRotation;
        private Vector3 networkVelocity;
        private float networkSendTime;

        // 本地数据
        private Vector3 lastSentPosition;
        private Quaternion lastSentRotation;
        private float lastSendTime;

        // 插值数据
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        // 组件引用
        private Transform playerTransform;
        private Rigidbody playerRigidbody;

        // 公共属性
        public ushort PlayerId => playerId;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsInitialized => isInitialized;

        #region Unity生命周期

        private void Awake()
        {
            playerTransform = transform;
            playerRigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            // 如果已经初始化，则检查本地玩家状态
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
                // 本地玩家：检查是否需要发送数据
                CheckAndSendData();
            }
            else
            {
                // 远程玩家：插值到目标位置和旋转
                InterpolateToTarget();
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

            isInitialized = true;
            LogDebug($"PlayerNetworkSync初始化完成 - PlayerID: {playerId}, 本地玩家: {isLocalPlayer}");

            // 如果是本地玩家，通知PlayerCameraController重新检查
            if (isLocalPlayer)
            {
                var cameraController = GetComponent<PlayerCameraController>();
                if (cameraController != null)
                {
                    // 触发重新检查（通过反射调用私有方法或添加公共方法）
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

        #endregion

        #region 本地玩家数据发送

        /// <summary>
        /// 检查并发送数据
        /// </summary>
        private void CheckAndSendData()
        {
            if (!isLocalPlayer) return;

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

                // 发送数据
                NetworkManager.Instance.SyncPlayerTransform(playerId, playerTransform.position, playerTransform.rotation, velocity);

                // 更新最后发送的数据
                lastSentPosition = playerTransform.position;
                lastSentRotation = playerTransform.rotation;
                lastSendTime = currentTime;

                LogDebug($"发送位置数据: {lastSentPosition}, 旋转: {lastSentRotation.eulerAngles}");
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
                    return true;
                }
            }

            // 检查旋转变化
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
            networkSendTime = timestamp;

            targetPosition = networkPosition;
            targetRotation = networkRotation;

            // 应用网络延迟补偿
            ApplyNetworkDelayCompensation();

            LogDebug($"接收网络数据 - 位置: {networkPosition}, 旋转: {networkRotation.eulerAngles}, 速度: {networkVelocity}");
        }

        /// <summary>
        /// 插值到目标位置和旋转
        /// </summary>
        private void InterpolateToTarget()
        {
            if (isLocalPlayer) return;

            float deltaTime = Time.deltaTime;
            float speed = interpolationSpeed * deltaTime;

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
        }

        /// <summary>
        /// 应用网络延迟补偿
        /// </summary>
        private void ApplyNetworkDelayCompensation()
        {
            if (!enablePrediction) return;

            // 计算网络延迟
            float lag = Time.time - networkSendTime;

            // 应用延迟补偿到目标位置
            if (syncPosition && networkVelocity.magnitude > 0.1f)
            {
                targetPosition = networkPosition + networkVelocity * lag;
            }

            LogDebug($"网络延迟补偿: {lag:F3}s, 补偿后位置: {targetPosition}");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 强制同步到指定位置
        /// </summary>
        public void ForceSync(Vector3 position, Quaternion rotation)
        {
            if (!isLocalPlayer) return;

            playerTransform.position = position;
            playerTransform.rotation = rotation;

            // 立即发送数据
            lastSentPosition = position;
            lastSentRotation = rotation;
            lastSendTime = Time.time;

            LogDebug($"强制同步到位置: {position}, 旋转: {rotation.eulerAngles}");
        }

        /// <summary>
        /// 设置同步配置
        /// </summary>
        public void SetSyncSettings(bool position, bool rotation, float rate)
        {
            syncPosition = position;
            syncRotation = rotation;
            sendRate = rate;

            LogDebug($"同步设置更新 - 位置: {syncPosition}, 旋转: {syncRotation}, 频率: {sendRate}Hz");
        }

        /// <summary>
        /// 获取网络状态信息
        /// </summary>
        public string GetNetworkStatus()
        {
            if (!isInitialized) return "未初始化";

            string status = $"PlayerID: {playerId}, 本地: {isLocalPlayer}, ";
            status += $"位置: {playerTransform.position:F1}, ";
            status += $"目标位置: {targetPosition:F1}, ";
            status += $"网络延迟: {PhotonNetwork.GetPing()}ms";

            return status;
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