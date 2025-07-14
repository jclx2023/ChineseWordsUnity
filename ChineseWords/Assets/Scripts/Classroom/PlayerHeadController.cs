using UnityEngine;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// 玩家头部控制器 - 专门处理头部骨骼旋转
    /// 独立于摄像机控制，负责头部的可视化表现和网络同步
    /// 由于玩家不移动位置，只处理头部旋转逻辑
    /// </summary>
    public class PlayerHeadController : MonoBehaviour
    {
        [Header("头部控制设置")]
        [SerializeField] private bool enableHeadRotation = true; // 启用头部旋转
        [SerializeField] private bool enableHeadHorizontalRotation = true; // 启用头部水平旋转
        [SerializeField] private bool enableHeadVerticalRotation = false; // 启用头部垂直旋转（鬼畜模式）

        [Header("头部旋转限制")]
        [SerializeField] private float headHorizontalLimit = 90f; // 头部水平旋转限制（左右各90度）
        [SerializeField] private float headVerticalUpLimit = 30f; // 头部向上角度限制
        [SerializeField] private float headVerticalDownLimit = 15f; // 头部向下角度限制

        [Header("头部平滑设置")]
        [SerializeField] private float headSmoothSpeed = 8f; // 头部旋转平滑速度
        [SerializeField] private bool useSmoothing = true; // 是否使用平滑

        [Header("骨骼查找")]
        [SerializeField] private string headBoneName = "head"; // 头部骨骼名称
        [SerializeField] private bool autoFindHeadBone = true; // 自动查找头部骨骼

        [Header("网络同步")]
        [SerializeField] private bool syncHeadRotation = true; // 同步头部旋转
        [SerializeField] private float syncThreshold = 3f; // 同步阈值（度）

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = false;

        // 头部旋转数据
        private float targetHeadHorizontalAngle = 0f; // 目标头部水平角度
        private float targetHeadVerticalAngle = 0f; // 目标头部垂直角度
        private float currentHeadHorizontalAngle = 0f; // 当前头部水平角度
        private float currentHeadVerticalAngle = 0f; // 当前头部垂直角度

        // 状态变量
        private bool isInitialized = false;
        private bool isLocalPlayer = false;
        private bool isControlEnabled = false;

        // 骨骼相关
        private Animator characterAnimator;
        private Transform headBone;
        private Quaternion initialHeadRotation; // 头部初始旋转
        private Quaternion baseHeadRotation; // 头部基础旋转（座位朝向）

        // 网络同步相关
        private float lastSyncTime = 0f;
        private float lastSentHorizontalAngle = 0f;
        private float lastSentVerticalAngle = 0f;

        // 公共属性
        public bool IsInitialized => isInitialized;
        public bool IsLocalPlayer => isLocalPlayer;
        public float CurrentHeadHorizontalAngle => currentHeadHorizontalAngle;
        public float CurrentHeadVerticalAngle => currentHeadVerticalAngle;
        public Transform HeadBone => headBone;

        #region Unity生命周期

        private void Awake()
        {
            // 查找角色动画器
            characterAnimator = GetComponent<Animator>();
            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }

            LogDebug("PlayerHeadController Awake完成");
        }

        private void Start()
        {
            // 检查本地玩家状态
            CheckIfLocalPlayer();

            if (isLocalPlayer) // 编辑器中也初始化，便于调试
            {
                InitializeHeadController();
            }
        }

        private void Update()
        {
            if (!isInitialized) return;

            // 更新头部旋转（无论本地还是远程玩家都需要）
            UpdateHeadRotation();

            // 本地玩家额外处理网络同步检查
            if (isLocalPlayer && syncHeadRotation)
            {
                CheckAndSendHeadRotation();
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 检查是否为本地玩家
        /// </summary>
        private void CheckIfLocalPlayer()
        {
            var networkSync = GetComponent<PlayerNetworkSync>();
            if (networkSync != null)
            {
                isLocalPlayer = networkSync.IsLocalPlayer;
            }
            else
            {
                // 备用方案：通过角色名称判断
                isLocalPlayer = name.Contains($"Player_{NetworkManager.Instance?.ClientId:D2}");
            }

            LogDebug($"检查本地玩家状态: {isLocalPlayer}");
        }

        /// <summary>
        /// 初始化头部控制器
        /// </summary>
        private void InitializeHeadController()
        {
            LogDebug("开始初始化头部控制器");

            // 查找头部骨骼
            if (autoFindHeadBone)
            {
                FindHeadBone();
            }

            // 记录初始状态
            SetupInitialHeadTransform();

            isInitialized = true;
            isControlEnabled = true;

            LogDebug($"头部控制器初始化完成，头部骨骼: {headBone.name}");
        }

        /// <summary>
        /// 查找头部骨骼
        /// </summary>
        private void FindHeadBone()
        {
            if (characterAnimator == null) return;
            headBone = FindBoneRecursive(characterAnimator.transform, headBoneName);
        }

        /// <summary>
        /// 递归查找骨骼
        /// </summary>
        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name.ToLower().Contains(boneName.ToLower()))
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindBoneRecursive(parent.GetChild(i), boneName);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// 设置初始头部变换
        /// </summary>
        private void SetupInitialHeadTransform()
        {
            if (headBone == null) return;

            // 记录头部初始旋转
            initialHeadRotation = headBone.rotation;
            baseHeadRotation = initialHeadRotation;

            // 重置角度
            targetHeadHorizontalAngle = 0f;
            targetHeadVerticalAngle = 0f;
            currentHeadHorizontalAngle = 0f;
            currentHeadVerticalAngle = 0f;

            LogDebug($"记录头部初始旋转: {initialHeadRotation.eulerAngles}");
        }

        #endregion

        #region 头部旋转控制

        /// <summary>
        /// 从摄像机控制器接收旋转输入
        /// </summary>
        /// <param name="horizontalAngle">总的水平角度</param>
        /// <param name="verticalAngle">总的垂直角度</param>
        /// <returns>返回头部实际承担的角度</returns>
        public Vector2 ReceiveRotationInput(float horizontalAngle, float verticalAngle)
        {
            if (!isLocalPlayer || !isInitialized || !enableHeadRotation)
                return Vector2.zero;

            // 计算头部应该承担的旋转
            float headHorizontal = 0f;
            float headVertical = 0f;

            if (enableHeadHorizontalRotation)
            {
                headHorizontal = Mathf.Clamp(horizontalAngle, -headHorizontalLimit, headHorizontalLimit);
            }

            if (enableHeadVerticalRotation)
            {
                headVertical = Mathf.Clamp(verticalAngle, -headVerticalDownLimit, headVerticalUpLimit);
            }

            // 设置目标角度
            targetHeadHorizontalAngle = headHorizontal;
            targetHeadVerticalAngle = headVertical;

            return new Vector2(headHorizontal, headVertical);
        }

        /// <summary>
        /// 更新头部旋转
        /// </summary>
        private void UpdateHeadRotation()
        {
            if (!isInitialized || headBone == null) return;

            // 平滑插值到目标角度
            if (useSmoothing)
            {
                float deltaTime = Time.deltaTime;
                float speed = headSmoothSpeed * deltaTime;

                currentHeadHorizontalAngle = Mathf.LerpAngle(currentHeadHorizontalAngle, targetHeadHorizontalAngle, speed);
                currentHeadVerticalAngle = Mathf.LerpAngle(currentHeadVerticalAngle, targetHeadVerticalAngle, speed);
            }
            else
            {
                currentHeadHorizontalAngle = targetHeadHorizontalAngle;
                currentHeadVerticalAngle = targetHeadVerticalAngle;
            }

            // 应用旋转到头部骨骼
            ApplyHeadRotation();
        }

        /// <summary>
        /// 应用头部旋转
        /// </summary>
        private void ApplyHeadRotation()
        {
            if (headBone == null) return;

            // 水平旋转（绕Y轴）
            Quaternion horizontalRotation = enableHeadHorizontalRotation ?
                Quaternion.AngleAxis(currentHeadHorizontalAngle, Vector3.up) : Quaternion.identity;

            // 垂直旋转（绕X轴）
            Quaternion verticalRotation = enableHeadVerticalRotation ?
                Quaternion.AngleAxis(currentHeadVerticalAngle, Vector3.right) : Quaternion.identity;

            // 组合旋转：基础旋转 * 水平旋转 * 垂直旋转
            Quaternion finalHeadRotation = baseHeadRotation * horizontalRotation * verticalRotation;

            // 应用到头部骨骼
            headBone.rotation = finalHeadRotation;
        }

        /// <summary>
        /// 设置基础头部旋转（座位朝向）
        /// </summary>
        public void SetBaseHeadRotation(Quaternion baseRotation)
        {
            baseHeadRotation = initialHeadRotation * baseRotation;
            LogDebug($"设置头部基础旋转: {baseRotation.eulerAngles}");
        }

        #endregion

        #region 网络同步相关

        /// <summary>
        /// 检查并发送头部旋转数据
        /// </summary>
        private void CheckAndSendHeadRotation()
        {
            // 安全检查：网络不可用时停止发送
            if (!isLocalPlayer || !syncHeadRotation ||
                NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected ||
                Time.time - lastSyncTime < 1f / 15f) // 15Hz发送频率
            {
                return;
            }

            // 检查是否有显著变化
            float horizontalDelta = Mathf.Abs(currentHeadHorizontalAngle - lastSentHorizontalAngle);
            float verticalDelta = Mathf.Abs(currentHeadVerticalAngle - lastSentVerticalAngle);

            if (horizontalDelta > syncThreshold || verticalDelta > syncThreshold)
            {
                // 发送头部旋转数据
                SendHeadRotationData();

                lastSentHorizontalAngle = currentHeadHorizontalAngle;
                lastSentVerticalAngle = currentHeadVerticalAngle;
                lastSyncTime = Time.time;

                LogDebug($"发送头部旋转: H={currentHeadHorizontalAngle:F1}°, V={currentHeadVerticalAngle:F1}°");
            }
        }

        /// <summary>
        /// 发送头部旋转数据
        /// </summary>
        private void SendHeadRotationData()
        {
            // 获取PlayerNetworkSync组件来发送数据
            var networkSync = GetComponent<PlayerNetworkSync>();
            if (networkSync != null && NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                // 通知网络同步组件发送头部数据
                networkSync.SendHeadRotation(currentHeadHorizontalAngle, currentHeadVerticalAngle);
            }
        }

        /// <summary>
        /// 接收网络头部旋转数据（用于远程玩家）
        /// </summary>
        public void ReceiveNetworkHeadRotation(float horizontalAngle, float verticalAngle)
        {
            if (isLocalPlayer) return;

            targetHeadHorizontalAngle = horizontalAngle;
            targetHeadVerticalAngle = verticalAngle;

            LogDebug($"接收网络头部旋转: H={horizontalAngle:F1}°, V={verticalAngle:F1}°");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 启用/禁用头部控制
        /// </summary>
        public void SetHeadControlEnabled(bool enabled)
        {
            isControlEnabled = enabled;
            LogDebug($"头部控制状态: {enabled}");
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[PlayerHeadController] {message}");
            }
        }

        #endregion

    }
}