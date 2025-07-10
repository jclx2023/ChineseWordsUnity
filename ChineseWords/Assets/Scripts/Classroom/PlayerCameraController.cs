using UnityEngine;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// 玩家摄像机控制器 - 仅在本地玩家角色上激活
    /// 绑定到角色头部骨骼的摄像机挂载点，实现180°半球视角限制
    /// 完全本地化控制，不进行网络同步
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("摄像机设置")]
        [SerializeField] private Camera playerCamera; // 玩家摄像机
        [SerializeField] private Transform cameraMount; // 摄像机挂载点
        [SerializeField] private bool createCameraIfMissing = true; // 自动创建摄像机

        [Header("骨骼查找")]
        [SerializeField] private string headBoneName = "head"; // 头部骨骼名称
        [SerializeField] private string cameraMountName = "CameraMount"; // 摄像机挂载点名称
        [SerializeField] private bool autoFindCameraMount = true; // 自动查找摄像机挂载点

        [Header("视角控制")]
        [SerializeField] private float mouseSensitivity = 2f; // 鼠标灵敏度
        [SerializeField] private float smoothing = 5f; // 平滑系数
        [SerializeField] private bool invertYAxis = false; // 反转Y轴

        [Header("视角限制")]
        [SerializeField] private float horizontalAngleLimit = 90f; // 水平角度限制（左右各90°）
        [SerializeField] private float verticalUpLimit = 60f; // 向上角度限制
        [SerializeField] private float verticalDownLimit = 30f; // 向下角度限制

        [Header("座位基准")]
        [SerializeField] private Quaternion seatForwardDirection = Quaternion.identity; // 座位正前方朝向
        [SerializeField] private bool lockToSeatDirection = true; // 锁定到座位朝向

        [Header("描边效果")]
        [SerializeField] private bool enableOutlineEffect = true; // 启用描边效果

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool showAngleLimits = true;

        // 视角控制变量
        private Vector2 mouseDelta;
        private Vector2 smoothedMouseDelta;
        private float currentHorizontalAngle = 0f; // 当前水平角度（相对于座位朝向）
        private float currentVerticalAngle = 0f; // 当前垂直角度

        // 状态变量
        private bool isInitialized = false;
        private bool isLocalPlayer = false;
        private bool isControlEnabled = false;
        private Vector3 initialCameraPosition;
        private Quaternion initialCameraRotation;

        // 输入状态
        private bool cursorLocked = false;

        // 骨骼相关
        private Animator characterAnimator;
        private Transform headBone;

        // 公共属性
        public bool IsInitialized => isInitialized;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsControlEnabled => isControlEnabled;
        public Camera PlayerCamera => playerCamera;

        #region Unity生命周期

        private void Awake()
        {
            // 查找角色动画器
            characterAnimator = GetComponent<Animator>();
            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }

            // 注意：不在Awake中检查本地玩家状态，延迟到Start中
            LogDebug("PlayerCameraController Awake完成，等待初始化");
        }

        private void Start()
        {
            // 延迟检查本地玩家状态，确保PlayerNetworkSync已经初始化
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

        private void LateUpdate()
        {
            if (!isInitialized || !isLocalPlayer) return;

            UpdateCameraPosition();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 检查是否为本地玩家
        /// </summary>
        private void CheckIfLocalPlayer()
        {
            // 通过NetworkManager检查是否为本地玩家
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

            // 查找头部骨骼和摄像机挂载点
            FindHeadBoneAndCameraMount();

            // 查找或创建摄像机
            SetupCamera();

            // 设置初始位置和角度
            if (cameraMount != null)
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
        /// 查找头部骨骼和摄像机挂载点
        /// </summary>
        private void FindHeadBoneAndCameraMount()
        {
            if (characterAnimator == null)
            {
                return;
            }

            // 查找头部骨骼
            headBone = FindBoneRecursive(characterAnimator.transform, headBoneName);

            // 在头部骨骼子级查找摄像机挂载点
            if (autoFindCameraMount)
            {
                cameraMount = FindCameraMountInChildren(headBone);

                if (cameraMount == null)
                {
                    // 如果没有找到，创建一个
                    CreateCameraMount();
                }
            }
        }

        /// <summary>
        /// 递归查找骨骼
        /// </summary>
        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            // 检查当前对象
            if (parent.name.ToLower().Contains(boneName.ToLower()))
            {
                return parent;
            }

            // 递归检查子对象
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindBoneRecursive(parent.GetChild(i), boneName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 在子级中查找摄像机挂载点
        /// </summary>
        private Transform FindCameraMountInChildren(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.ToLower().Contains(cameraMountName.ToLower()) ||
                    child.name.ToLower().Contains("camera") ||
                    child.name.ToLower().Contains("mount"))
                {
                    return child;
                }
            }
            return null;
        }

        /// <summary>
        /// 创建摄像机挂载点
        /// </summary>
        private void CreateCameraMount()
        {
            if (headBone == null) return;

            GameObject mountObject = new GameObject(cameraMountName);
            mountObject.transform.SetParent(headBone);
            mountObject.transform.localPosition = Vector3.zero;
            mountObject.transform.localRotation = Quaternion.identity;

            cameraMount = mountObject.transform;
            LogDebug($"创建摄像机挂载点: {cameraMountName}");
        }

        /// <summary>
        /// 设置摄像机
        /// </summary>
        private void SetupCamera()
        {
            // 先尝试查找现有摄像机
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }

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

            // 挂载描边效果
            if (enableOutlineEffect)
            {
                AttachOutlineEffect();
            }

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
        /// 挂载描边效果
        /// </summary>
        private void AttachOutlineEffect()
        {
            if (playerCamera == null) return;

            try
            {
                // 检查是否已经存在O_CustomImageEffect组件
                var existingOutline = playerCamera.GetComponent("O_CustomImageEffect");
                if (existingOutline != null)
                {
                    LogDebug("摄像机上已存在 O_CustomImageEffect 组件");
                    return;
                }

                // 尝试添加O_CustomImageEffect组件
                System.Type outlineType = System.Type.GetType("O_CustomImageEffect");
                if (outlineType == null)
                {
                    // 在所有程序集中查找
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            outlineType = assembly.GetType("O_CustomImageEffect");
                            if (outlineType != null) break;
                        }
                        catch { continue; }
                    }
                }

                if (outlineType != null)
                {
                    playerCamera.gameObject.AddComponent(outlineType);
                    LogDebug("成功添加 O_CustomImageEffect 组件");
                }
                else
                {
                    LogDebug("未找到 O_CustomImageEffect 类型，请确保插件已正确导入");
                }
            }
            catch (System.Exception ex)
            {
                LogDebug($"挂载 O_CustomImageEffect 时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置初始摄像机变换
        /// </summary>
        private void SetupInitialCameraTransform()
        {
            if (cameraMount == null || playerCamera == null) return;

            // 确保摄像机位置正确
            if (playerCamera.transform.parent != cameraMount)
            {
                playerCamera.transform.SetParent(cameraMount);
                playerCamera.transform.localPosition = Vector3.zero;
                playerCamera.transform.localRotation = Quaternion.identity;
            }

            // 记录初始变换
            initialCameraPosition = cameraMount.localPosition;
            initialCameraRotation = cameraMount.localRotation;

            // 重置角度
            currentHorizontalAngle = 0f;
            currentVerticalAngle = 0f;
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

            LogDebug($"摄像机控制状态: {enabled}");
        }

        /// <summary>
        /// 重置摄像机角度
        /// </summary>
        public void ResetCameraAngle()
        {
            currentHorizontalAngle = 0f;
            currentVerticalAngle = 0f;

            if (cameraMount != null && playerCamera != null)
            {
                // 重置摄像机到初始状态
                playerCamera.transform.localRotation = Quaternion.identity;
            }

            LogDebug("摄像机角度已重置");
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
            if (cameraMount == null || playerCamera == null) return;

            // 更新角度
            currentHorizontalAngle += smoothedMouseDelta.x;
            currentVerticalAngle -= smoothedMouseDelta.y; // 注意负号

            // 应用角度限制
            ApplyAngleLimits();

            // 计算最终旋转
            CalculateAndApplyCameraRotation();
        }

        /// <summary>
        /// 应用角度限制
        /// </summary>
        private void ApplyAngleLimits()
        {
            // 水平角度限制（左右各90°）
            currentHorizontalAngle = Mathf.Clamp(currentHorizontalAngle, -horizontalAngleLimit, horizontalAngleLimit);

            // 垂直角度限制
            currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, -verticalDownLimit, verticalUpLimit);
        }

        /// <summary>
        /// 计算并应用摄像机旋转
        /// </summary>
        private void CalculateAndApplyCameraRotation()
        {
            // 基础旋转（座位朝向）
            Quaternion baseRotation = lockToSeatDirection ? seatForwardDirection : Quaternion.identity;

            // 水平旋转（绕Y轴）
            Quaternion horizontalRotation = Quaternion.AngleAxis(currentHorizontalAngle, Vector3.up);

            // 垂直旋转（绕X轴）
            Quaternion verticalRotation = Quaternion.AngleAxis(currentVerticalAngle, Vector3.right);

            // 组合旋转：基础旋转 * 水平旋转 * 垂直旋转
            Quaternion finalRotation = baseRotation * horizontalRotation * verticalRotation;

            // 应用到摄像机的局部旋转
            playerCamera.transform.localRotation = Quaternion.Inverse(cameraMount.rotation) * finalRotation;
        }

        /// <summary>
        /// 更新摄像机位置
        /// </summary>
        private void UpdateCameraPosition()
        {
            if (cameraMount == null || playerCamera == null) return;

            // 摄像机跟随挂载点位置（已经通过父子关系自动跟随）
            // 这里可以添加额外的位置偏移逻辑，如头部摆动等
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