using UnityEngine;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// 玩家摄像机控制器 - 简化版本，专注于摄像机控制
    /// 不再直接控制头部骨骼，通过PlayerHeadController协调头部旋转
    /// 由于玩家不移动，去除了所有位置相关的逻辑
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("摄像机设置")]
        [SerializeField] private Camera playerCamera; // 玩家摄像机
        [SerializeField] private Transform cameraMount; // 摄像机挂载点
        [SerializeField] private bool createCameraIfMissing = true; // 自动创建摄像机

        [Header("摄像机挂载")]
        [SerializeField] private string cameraMountName = "CameraMount"; // 摄像机挂载点名称
        [SerializeField] private bool autoFindCameraMount = true; // 自动查找摄像机挂载点
        [SerializeField] private bool mountToCharacterRoot = true; // 挂载到角色根部而不是头部

        [Header("视角控制")]
        [SerializeField] private float mouseSensitivity = 2f; // 鼠标灵敏度
        [SerializeField] private float smoothing = 5f; // 平滑系数
        [SerializeField] private bool invertYAxis = false; // 反转Y轴

        [Header("视角限制")]
        [SerializeField] private float horizontalAngleLimit = 75f;
        [SerializeField] private float verticalUpLimit = 60f; // 向上角度限制
        [SerializeField] private float verticalDownLimit = 30f; // 向下角度限制

        [Header("头部协调")]
        [SerializeField] private bool enableHeadCoordination = true; // 启用头部协调
        [SerializeField] private float headCoordinationRatio = 0.3f; // 头部承担的旋转比例（降低，让摄像机承担更多）
        [SerializeField] private bool allowDiagonalMovement = true; // 允许对角线运动
        [SerializeField] private bool useIndependentAxes = false; // 使用独立轴控制

        [Header("座位基准")]
        [SerializeField] private Quaternion seatForwardDirection = Quaternion.identity; // 座位正前方朝向
        [SerializeField] private bool lockToSeatDirection = true; // 锁定到座位朝向

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 视角控制变量
        private Vector2 mouseDelta;
        private Vector2 smoothedMouseDelta;
        private float currentHorizontalAngle = 0f; // 当前水平角度（相对于座位朝向）
        private float currentVerticalAngle = 0f; // 当前垂直角度

        // 摄像机实际承担的角度（扣除头部承担的部分）
        private float cameraHorizontalAngle = 0f;
        private float cameraVerticalAngle = 0f;

        // 状态变量
        private bool isInitialized = false;
        private bool isLocalPlayer = false;
        private bool isControlEnabled = false;
        private Quaternion initialCameraRotation;

        // 输入状态
        private bool cursorLocked = false;

        // 组件引用
        private PlayerHeadController headController;

        // 公共属性
        public bool IsInitialized => isInitialized;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsControlEnabled => isControlEnabled;
        public Camera PlayerCamera => playerCamera;
        public float CurrentHorizontalAngle => currentHorizontalAngle;
        public float CurrentVerticalAngle => currentVerticalAngle;

        #region Unity生命周期

        private void Awake()
        {
            // 获取头部控制器引用
            headController = GetComponent<PlayerHeadController>();

            LogDebug("PlayerCameraController Awake完成");
        }

        private void Start()
        {
            // 延迟检查本地玩家状态
            CheckIfLocalPlayer();

            if (isLocalPlayer)
            {
                InitializeCameraController();
            }
            else
            {
                // 非本地玩家禁用此组件
                enabled = false;
                LogDebug("非本地玩家，禁用摄像机控制器");
            }
        }

        private void Update()
        {
            if (!isInitialized || !isControlEnabled || !isLocalPlayer) return;

            HandleInput();
            UpdateCameraRotation();
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
        /// 初始化摄像机控制器
        /// </summary>
        private void InitializeCameraController()
        {
            LogDebug("开始初始化摄像机控制器");

            // 查找或创建摄像机挂载点
            SetupCameraMount();

            // 查找或创建摄像机
            SetupCamera();

            // 设置初始变换
            if (cameraMount != null && playerCamera != null)
            {
                SetupInitialCameraTransform();
            }

            // 锁定鼠标
            SetCursorLock(true);

            isInitialized = true;
            isControlEnabled = true;

            LogDebug("摄像机控制器初始化完成");
        }

        /// <summary>
        /// 设置摄像机挂载点
        /// </summary>
        private void SetupCameraMount()
        {
            if (autoFindCameraMount)
            {
                // 优先查找现有的挂载点
                cameraMount = FindCameraMountInHierarchy();

                if (cameraMount == null)
                {
                    CreateCameraMount();
                }
            }
        }

        /// <summary>
        /// 在层级中查找摄像机挂载点
        /// </summary>
        private Transform FindCameraMountInHierarchy()
        {
            // 在角色根部查找
            Transform mount = transform.Find(cameraMountName);
            if (mount != null) return mount;

            // 递归查找
            mount = FindTransformRecursive(transform, cameraMountName);
            if (mount != null) return mount;

            // 查找包含camera或mount关键字的对象
            mount = FindTransformRecursive(transform, "camera");
            if (mount != null) return mount;

            mount = FindTransformRecursive(transform, "mount");
            return mount;
        }

        /// <summary>
        /// 递归查找Transform
        /// </summary>
        private Transform FindTransformRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.ToLower().Contains(name.ToLower()))
                {
                    return child;
                }

                Transform result = FindTransformRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// 创建摄像机挂载点
        /// </summary>
        private void CreateCameraMount()
        {
            GameObject mountObject = new GameObject(cameraMountName);

            if (mountToCharacterRoot)
            {
                // 挂载到角色根部
                mountObject.transform.SetParent(transform);
                mountObject.transform.localPosition = new Vector3(0, 1.8f, 0); // 头部高度
            }
            else
            {
                // 挂载到头部骨骼（如果有的话）
                if (headController != null && headController.HeadBone != null)
                {
                    mountObject.transform.SetParent(headController.HeadBone);
                    mountObject.transform.localPosition = Vector3.zero;
                }
                else
                {
                    // 降级到角色根部
                    mountObject.transform.SetParent(transform);
                    mountObject.transform.localPosition = new Vector3(0, 1.8f, 0);
                }
            }

            mountObject.transform.localRotation = Quaternion.identity;
            cameraMount = mountObject.transform;

            LogDebug($"创建摄像机挂载点: {cameraMountName} at {mountObject.transform.position}");
        }

        /// <summary>
        /// 设置摄像机
        /// </summary>
        private void SetupCamera()
        {

            // 如果没有摄像机且允许创建，则创建一个
            if (playerCamera == null && createCameraIfMissing)
            {
                CreatePlayerCamera();
            }

            if (playerCamera == null)
            {
                Debug.LogError("[PlayerCameraController] 未找到摄像机且禁止自动创建");
                return;
            }

            // 设置摄像机属性
            ConfigureCamera();


            LogDebug("摄像机设置完成");
        }

        /// <summary>
        /// 创建玩家摄像机
        /// </summary>
        private void CreatePlayerCamera()
        {
            GameObject cameraObject = new GameObject("PlayerCamera");

            // 将摄像机作为挂载点的子对象
            if (cameraMount != null)
            {
                cameraObject.transform.SetParent(cameraMount);
                cameraObject.transform.localPosition = Vector3.zero;
                cameraObject.transform.localRotation = Quaternion.identity;
            }
            else
            {
                cameraObject.transform.SetParent(transform);
                cameraObject.transform.localPosition = new Vector3(0, 1.8f, 0);
            }

            playerCamera = cameraObject.AddComponent<Camera>();

            // 禁用音频监听器（避免冲突）
            var audioListener = cameraObject.GetComponent<AudioListener>();
            if (audioListener != null)
            {
                Destroy(audioListener);
            }
        }

        /// <summary>
        /// 配置摄像机属性
        /// </summary>
        private void ConfigureCamera()
        {
            if (playerCamera == null) return;

            // 设置为主摄像机
            playerCamera.tag = "MainCamera";

            // 基本摄像机设置
            playerCamera.fieldOfView = 60f;
            playerCamera.nearClipPlane = 0.1f;
            playerCamera.farClipPlane = 1000f;

            // 确保只有本地玩家的摄像机是激活的
            playerCamera.enabled = isLocalPlayer;

            LogDebug($"摄像机配置完成，激活状态: {playerCamera.enabled}");
        }

        /// <summary>
        /// 设置初始摄像机变换
        /// </summary>
        private void SetupInitialCameraTransform()
        {
            // 确保摄像机位置正确
            if (playerCamera.transform.parent != cameraMount)
            {
                playerCamera.transform.SetParent(cameraMount);
                playerCamera.transform.localPosition = Vector3.zero;
                playerCamera.transform.localRotation = Quaternion.identity;
            }

            // 记录初始变换（不包含偏移，偏移在ApplyCameraRotation中处理）
            initialCameraRotation = Quaternion.identity;

            // 重置角度
            currentHorizontalAngle = 0f;
            currentVerticalAngle = 0f;
            cameraHorizontalAngle = 0f;
            cameraVerticalAngle = 0f;

            LogDebug("摄像机初始变换设置完成，90度偏移将在ApplyCameraRotation中应用");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 设置摄像机挂载点和座位朝向
        /// </summary>
        /// <param name="mount">挂载点Transform</param>
        /// <param name="seatRotation">座位朝向</param>
        public void SetCameraMount(Transform mount, Quaternion seatRotation)
        {
            cameraMount = mount;
            seatForwardDirection = seatRotation;

            LogDebug($"设置摄像机挂载点: {mount?.name}, 座位朝向: {seatRotation.eulerAngles}");

            if (isInitialized)
            {
                SetupInitialCameraTransform();
            }

            // 同时设置头部控制器的基础旋转
            if (headController != null)
            {
                headController.SetBaseHeadRotation(seatRotation);
            }
        }

        /// <summary>
        /// 启用/禁用控制
        /// </summary>
        public void SetControlEnabled(bool enabled)
        {
            isControlEnabled = enabled;

            if (!enabled)
            {
                SetCursorLock(false);
            }
            else if (isLocalPlayer)
            {
                SetCursorLock(true);
            }

            // 同时控制头部控制器
            if (headController != null)
            {
                headController.SetHeadControlEnabled(enabled);
            }

            LogDebug($"摄像机控制状态: {enabled}");
        }

        #endregion

        #region 输入处理

        /// <summary>
        /// 处理输入
        /// </summary>
        private void HandleInput()
        {
            // 获取鼠标输入
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            if (invertYAxis)
            {
                mouseY = -mouseY;
            }

            // 应用灵敏度
            mouseDelta = new Vector2(mouseX, mouseY) * mouseSensitivity;

            // 平滑处理
            smoothedMouseDelta = Vector2.Lerp(smoothedMouseDelta, mouseDelta, Time.deltaTime * smoothing);

            // 检查ESC键切换鼠标锁定
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleCursorLock();
            }
        }

        /// <summary>
        /// 更新摄像机旋转
        /// </summary>
        private void UpdateCameraRotation()
        {
            if (playerCamera == null) return;

            // 更新总的角度
            currentHorizontalAngle += smoothedMouseDelta.x;
            currentVerticalAngle -= smoothedMouseDelta.y; // 注意负号

            // 应用角度限制
            ApplyAngleLimits();

            // 协调头部和摄像机的旋转
            CoordinateHeadAndCameraRotation();

            // 应用摄像机旋转
            ApplyCameraRotation();
        }

        /// <summary>
        /// 协调头部和摄像机的旋转
        /// </summary>
        private void CoordinateHeadAndCameraRotation()
        {
            Vector2 headRotation = Vector2.zero;

            // 如果启用头部协调且头部控制器存在
            if (enableHeadCoordination && headController != null && headController.IsInitialized)
            {
                if (useIndependentAxes)
                {
                    // 独立轴模式：水平给头部，垂直给摄像机
                    float headHorizontal = currentHorizontalAngle * headCoordinationRatio;
                    headRotation = headController.ReceiveRotationInput(headHorizontal, 0f);

                    cameraHorizontalAngle = currentHorizontalAngle - headRotation.x;
                    cameraVerticalAngle = currentVerticalAngle; // 垂直完全由摄像机处理
                }
                else if (allowDiagonalMovement)
                {
                    // 自由模式：头部只承担小部分，摄像机处理大部分
                    float headHorizontal = currentHorizontalAngle * headCoordinationRatio;
                    float headVertical = currentVerticalAngle * (headCoordinationRatio * 0.5f); // 垂直给头部更少

                    headRotation = headController.ReceiveRotationInput(headHorizontal, headVertical);

                    // 摄像机承担剩余部分
                    cameraHorizontalAngle = currentHorizontalAngle - headRotation.x;
                    cameraVerticalAngle = currentVerticalAngle - headRotation.y;
                }
                else
                {
                    // 原始模式：让头部控制器处理旋转输入，返回头部实际承担的角度
                    headRotation = headController.ReceiveRotationInput(currentHorizontalAngle, currentVerticalAngle);

                    // 摄像机承担剩余的旋转
                    cameraHorizontalAngle = currentHorizontalAngle - headRotation.x;
                    cameraVerticalAngle = currentVerticalAngle - headRotation.y;
                }
            }
            else
            {
                // 禁用头部协调时，摄像机承担所有旋转
                cameraHorizontalAngle = currentHorizontalAngle;
                cameraVerticalAngle = currentVerticalAngle;
            }

        }

        /// <summary>
        /// 应用角度限制
        /// </summary>
        private void ApplyAngleLimits()
        {
            // 水平角度限制
            currentHorizontalAngle = Mathf.Clamp(currentHorizontalAngle, -horizontalAngleLimit, horizontalAngleLimit);

            // 垂直角度限制
            currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, -verticalDownLimit, verticalUpLimit);
        }

        /// <summary>
        /// 应用摄像机旋转
        /// </summary>
        private void ApplyCameraRotation()
        {
            // 基础旋转（座位朝向，如果头部不处理水平旋转的话）
            bool headHandlesHorizontal = enableHeadCoordination && headController != null;
            Quaternion baseRotation = (lockToSeatDirection && !headHandlesHorizontal) ?
                seatForwardDirection : Quaternion.identity;

            // 摄像机初始朝向偏移
            Quaternion initialOffset = Quaternion.AngleAxis(90f, Vector3.right);

            // 水平旋转（绕Y轴）
            Quaternion horizontalRotation = Quaternion.AngleAxis(cameraHorizontalAngle, Vector3.up);

            // 垂直旋转（绕X轴）
            Quaternion verticalRotation = Quaternion.AngleAxis(cameraVerticalAngle, Vector3.right);

            // 组合旋转：基础旋转 * 初始偏移 * 水平旋转 * 垂直旋转
            Quaternion finalRotation = baseRotation * initialOffset * horizontalRotation * verticalRotation;

            // 应用到摄像机
            if (mountToCharacterRoot || cameraMount.parent == transform)
            {
                // 如果挂载到角色根部，直接应用旋转
                playerCamera.transform.localRotation = finalRotation;
            }
            else
            {
                // 如果挂载到其他骨骼，需要考虑挂载点的旋转
                playerCamera.transform.localRotation = Quaternion.Inverse(cameraMount.rotation) * finalRotation;
            }

        }

        #endregion

        #region 鼠标控制

        /// <summary>
        /// 设置鼠标锁定状态
        /// </summary>
        private void SetCursorLock(bool lockCursor)
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                cursorLocked = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                cursorLocked = false;
            }

            LogDebug($"鼠标锁定状态: {cursorLocked}");
        }

        /// <summary>
        /// 切换鼠标锁定状态
        /// </summary>
        private void ToggleCursorLock()
        {
            SetCursorLock(!cursorLocked);
        }

        #endregion

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerCameraController] {message}");
            }
        }

        #region 事件处理

        private void OnDisable()
        {
            // 禁用时解锁鼠标
            if (cursorLocked)
            {
                SetCursorLock(false);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // 应用失去焦点时解锁鼠标
            if (!hasFocus && cursorLocked)
            {
                SetCursorLock(false);
            }
        }

        #endregion
    }
}