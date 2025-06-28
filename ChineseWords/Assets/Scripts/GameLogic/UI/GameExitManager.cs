using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Core.Network;
using Photon.Pun;
using Core;
using System.Collections;
using Classroom.Player;

namespace UI.GameExit
{
    /// <summary>
    /// 游戏退出管理器 - 简化版
    /// 处理NetworkGameScene中的退出功能
    /// 功能：ESC键监听、退出确认、返回主界面、摄像机控制
    /// </summary>
    public class GameExitManager : MonoBehaviour
    {
        [Header("退出设置")]
        [SerializeField] private KeyCode exitHotkey = KeyCode.Escape;
        [SerializeField] private bool enableExitDuringGame = true;

        [Header("UI引用")]
        [SerializeField] private GameObject exitConfirmDialog;
        [SerializeField] private Button confirmExitButton;
        [SerializeField] private Button cancelExitButton;
        [SerializeField] private TMP_Text confirmText;

        [Header("退出目标")]
        [SerializeField] private string targetScene = "RoomScene";
        [SerializeField] private bool returnToMainMenu = false;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 状态管理
        private bool isExitDialogOpen = false;
        private bool isExiting = false;

        // 网络状态缓存
        private bool wasHost = false;
        private bool wasInGame = false;

        // 摄像机控制 - 新增
        private PlayerCameraController playerCameraController;
        private bool cameraControllerReady = false;

        #region Unity生命周期

        private void Start()
        {
            InitializeExitManager();
        }

        private void OnEnable()
        {
            SubscribeToClassroomManagerEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromClassroomManagerEvents();
        }

        private void Update()
        {
            HandleInput();
        }

        private void OnDestroy()
        {
            CleanupExitManager();
        }

        #endregion

        #region 事件订阅管理 - 新增

        /// <summary>
        /// 订阅ClassroomManager事件
        /// </summary>
        private void SubscribeToClassroomManagerEvents()
        {
            // 如果ClassroomManager存在，订阅其事件
            if (FindObjectOfType<Classroom.ClassroomManager>() != null)
            {
                Classroom.ClassroomManager.OnCameraSetupCompleted += OnCameraSetupCompleted;
                Classroom.ClassroomManager.OnClassroomInitialized += OnClassroomInitialized;
            }
        }

        /// <summary>
        /// 取消订阅ClassroomManager事件
        /// </summary>
        private void UnsubscribeFromClassroomManagerEvents()
        {
            // 如果ClassroomManager存在，取消订阅其事件
            if (FindObjectOfType<Classroom.ClassroomManager>() != null)
            {
                Classroom.ClassroomManager.OnCameraSetupCompleted -= OnCameraSetupCompleted;
                Classroom.ClassroomManager.OnClassroomInitialized -= OnClassroomInitialized;
            }
        }

        /// <summary>
        /// 响应摄像机设置完成事件
        /// </summary>
        private void OnCameraSetupCompleted()
        {
            LogDebug("收到摄像机设置完成事件，开始查找摄像机控制器");
            StartCoroutine(FindPlayerCameraControllerCoroutine());
        }

        /// <summary>
        /// 响应教室初始化完成事件（备用方案）
        /// </summary>
        private void OnClassroomInitialized()
        {
            if (!cameraControllerReady)
            {
                LogDebug("教室初始化完成，尝试查找摄像机控制器（备用方案）");
                StartCoroutine(FindPlayerCameraControllerCoroutine());
            }
        }

        #endregion

        #region 摄像机控制器查找 - 新增

        /// <summary>
        /// 协程方式查找玩家摄像机控制器
        /// </summary>
        private IEnumerator FindPlayerCameraControllerCoroutine()
        {
            LogDebug("开始查找玩家摄像机控制器");

            float timeout = 5f; // 5秒超时
            float elapsed = 0f;

            while (elapsed < timeout && playerCameraController == null)
            {
                playerCameraController = FindPlayerCameraController();

                if (playerCameraController != null)
                {
                    cameraControllerReady = true;
                    LogDebug($"成功找到摄像机控制器: {playerCameraController.name}");
                    break;
                }

                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            if (playerCameraController == null)
            {
                LogDebug("未能找到摄像机控制器，将使用备用方案");
                // 可以添加备用查找逻辑或错误处理
            }
        }

        /// <summary>
        /// 查找玩家摄像机控制器
        /// </summary>
        private PlayerCameraController FindPlayerCameraController()
        {
            // 方式1: 通过ClassroomManager获取（推荐方式）
            var classroomManager = FindObjectOfType<Classroom.ClassroomManager>();
            if (classroomManager != null && classroomManager.IsInitialized)
            {
                var cameraController = classroomManager.GetLocalPlayerCameraController();
                if (cameraController != null)
                {
                    LogDebug($"通过ClassroomManager找到摄像机控制器: {cameraController.name}");
                    return cameraController;
                }
            }

            // 方式2: 遍历所有PlayerCameraController找到本地玩家的
            var controllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in controllers)
            {
                if (controller.IsLocalPlayer)
                {
                    LogDebug($"通过遍历找到本地玩家摄像机控制器: {controller.name}");
                    return controller;
                }
            }

            // 方式3: 查找包含"Local"、"Player"等关键词的摄像机控制器
            foreach (var controller in controllers)
            {
                if (controller.gameObject.name.Contains("Local") ||
                    controller.gameObject.name.Contains("Player"))
                {
                    LogDebug($"通过关键词找到摄像机控制器: {controller.name}");
                    return controller;
                }
            }

            return null;
        }

        /// <summary>
        /// 控制摄像机
        /// </summary>
        private void SetCameraControl(bool enabled)
        {
            if (playerCameraController != null)
            {
                playerCameraController.SetControlEnabled(enabled);
                LogDebug($"摄像机控制: {(enabled ? "启用" : "禁用")}");
            }
            else
            {
                LogDebug("摄像机控制器不可用，无法控制摄像机");
            }
        }

        #endregion

        #region 初始化和清理

        /// <summary>
        /// 初始化退出管理器
        /// </summary>
        private void InitializeExitManager()
        {
            LogDebug("初始化游戏退出管理器");

            // 验证场景
            if (!IsInGameScene())
            {
                LogDebug("不在游戏场景中，禁用退出管理器");
                this.enabled = false;
                return;
            }

            // 设置UI
            SetupUI();

            // 缓存初始状态
            CacheNetworkState();

            // 尝试立即查找摄像机控制器（如果已经初始化）
            if (!cameraControllerReady)
            {
                StartCoroutine(FindPlayerCameraControllerCoroutine());
            }

            LogDebug("游戏退出管理器初始化完成");
        }

        /// <summary>
        /// 设置UI组件
        /// </summary>
        private void SetupUI()
        {
            // 创建退出对话框（如果没有预设的话）
            if (exitConfirmDialog == null)
            {
                CreateExitDialog();
            }

            // 设置按钮事件
            if (confirmExitButton != null)
            {
                confirmExitButton.onClick.RemoveAllListeners();
                confirmExitButton.onClick.AddListener(ConfirmExit);
            }

            if (cancelExitButton != null)
            {
                cancelExitButton.onClick.RemoveAllListeners();
                cancelExitButton.onClick.AddListener(CancelExit);
            }

            // 初始隐藏对话框
            if (exitConfirmDialog != null)
            {
                exitConfirmDialog.SetActive(false);
            }

            LogDebug("UI组件设置完成");
        }

        /// <summary>
        /// 创建退出对话框（备用方案）
        /// </summary>
        private void CreateExitDialog()
        {
            LogDebug("创建默认退出对话框");

            // 这里可以动态创建一个简单的退出对话框
            // 或者从Resources加载预制体
            // 暂时跳过，假设有预设的UI
        }

        /// <summary>
        /// 缓存网络状态
        /// </summary>
        private void CacheNetworkState()
        {
            if (NetworkManager.Instance != null)
            {
                wasHost = NetworkManager.Instance.IsHost;
                LogDebug($"缓存网络状态 - 是否Host: {wasHost}");
            }

            if (HostGameManager.Instance != null)
            {
                wasInGame = HostGameManager.Instance.IsGameInProgress;
                LogDebug($"缓存游戏状态 - 游戏进行中: {wasInGame}");
            }
        }

        /// <summary>
        /// 清理退出管理器
        /// </summary>
        private void CleanupExitManager()
        {
            // 取消所有按钮监听
            if (confirmExitButton != null)
                confirmExitButton.onClick.RemoveAllListeners();

            if (cancelExitButton != null)
                cancelExitButton.onClick.RemoveAllListeners();

            // 取消事件订阅
            UnsubscribeFromClassroomManagerEvents();

            LogDebug("游戏退出管理器已清理");
        }

        #endregion

        #region 输入处理

        /// <summary>
        /// 处理用户输入
        /// </summary>
        private void HandleInput()
        {
            if (isExiting) return;

            // 检测退出热键
            if (Input.GetKeyDown(exitHotkey))
            {
                HandleExitRequest();
            }
        }

        /// <summary>
        /// 处理退出请求
        /// </summary>
        private void HandleExitRequest()
        {
            LogDebug($"检测到退出请求 - 当前状态: 对话框开启={isExitDialogOpen}");

            if (isExitDialogOpen)
            {
                // 对话框已开启，关闭它
                CancelExit();
            }
            else
            {
                // 显示退出确认
                ShowExitConfirmation();
            }
        }

        #endregion

        #region 退出确认对话框

        /// <summary>
        /// 显示退出确认对话框
        /// </summary>
        public void ShowExitConfirmation()
        {
            if (isExitDialogOpen || isExiting) return;

            LogDebug("显示退出确认对话框");

            // 检查退出条件
            if (!CanExit())
            {
                LogDebug("当前状态不允许退出");
                return;
            }

            // 禁用摄像机控制 - 新增
            SetCameraControl(false);

            // 显示对话框
            if (exitConfirmDialog != null)
            {
                exitConfirmDialog.SetActive(true);
                isExitDialogOpen = true;

                // 更新确认文本
                UpdateConfirmText();

                LogDebug("退出确认对话框已显示");
            }
            else
            {
                LogDebug("退出对话框UI不存在，直接执行退出");
                ConfirmExit();
            }
        }

        /// <summary>
        /// 更新确认文本
        /// </summary>
        private void UpdateConfirmText()
        {
            if (confirmText == null) return;

            string message = "确定要退出游戏吗？\n";

            // 根据当前状态添加说明
            if (wasInGame)
            {
                if (wasHost)
                {
                    message += "注意：作为房主退出将结束当前游戏！";
                }
                else
                {
                    message += "退出后将无法重新加入当前游戏。";
                }
            }
            else
            {
                message += "将返回到房间场景。";
            }

            confirmText.text = message;
        }

        #endregion

        #region 退出处理

        /// <summary>
        /// 确认退出
        /// </summary>
        public void ConfirmExit()
        {
            if (isExiting) return;

            LogDebug("用户确认退出游戏");

            isExiting = true;
            HideExitDialog();

            // 执行退出逻辑
            StartExitProcess();
        }

        /// <summary>
        /// 取消退出
        /// </summary>
        public void CancelExit()
        {
            LogDebug("取消退出游戏");

            HideExitDialog();
        }

        /// <summary>
        /// 隐藏退出对话框
        /// </summary>
        private void HideExitDialog()
        {
            if (exitConfirmDialog != null)
            {
                exitConfirmDialog.SetActive(false);
            }

            isExitDialogOpen = false;

            // 启用摄像机控制 - 新增
            SetCameraControl(true);
        }

        /// <summary>
        /// 开始退出流程
        /// </summary>
        private void StartExitProcess()
        {
            LogDebug("开始退出流程");

            // 如果是Host且游戏进行中，需要特殊处理
            if (wasHost && wasInGame)
            {
                HandleHostExitDuringGame();
            }
            else
            {
                // 普通退出流程
                HandleNormalExit();
            }
        }

        /// <summary>
        /// 处理Host在游戏中退出
        /// </summary>
        private void HandleHostExitDuringGame()
        {
            LogDebug("处理Host游戏中退出");

            try
            {
                // 通知HostGameManager结束游戏
                if (HostGameManager.Instance != null)
                {
                    HostGameManager.Instance.ForceEndGame("房主退出游戏");
                }

                // 延迟执行退出，给其他玩家一点时间接收游戏结束消息
                Invoke(nameof(ExecuteExit), 2f);
            }
            catch (System.Exception e)
            {
                LogDebug($"Host退出处理失败: {e.Message}");
                ExecuteExit(); // 备用退出
            }
        }

        /// <summary>
        /// 处理普通退出
        /// </summary>
        private void HandleNormalExit()
        {
            LogDebug("处理普通玩家退出");

            // 如果死亡玩家退出，通知HostGameManager
            if (HostGameManager.Instance != null && NetworkManager.Instance != null)
            {
                ushort myPlayerId = NetworkManager.Instance.ClientId;
                HostGameManager.Instance.RequestReturnToRoom(myPlayerId, "玩家主动退出");
            }

            // 直接执行退出
            ExecuteExit();
        }

        /// <summary>
        /// 执行实际的退出操作
        /// </summary>
        private void ExecuteExit()
        {
            LogDebug("执行实际退出操作");

            try
            {
                if (returnToMainMenu)
                {
                    ReturnToMainMenu();
                }
                else
                {
                    ReturnToRoom();
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"退出执行失败: {e.Message}");
                // 备用方案：直接切换场景
                SceneManager.LoadScene("RoomScene");
            }
        }

        /// <summary>
        /// 返回房间
        /// </summary>
        private void ReturnToRoom()
        {
            LogDebug($"返回房间场景: {targetScene}");

            // 使用SceneTransitionManager静态方法
            bool success = SceneTransitionManager.SwitchToGameScene("GameExitManager");
            if (!success)
            {
                // 备用方案：直接切换场景
                LogDebug("SceneTransitionManager切换失败，使用备用方案");
                SceneManager.LoadScene(targetScene);
            }
        }

        /// <summary>
        /// 返回主菜单
        /// </summary>
        private void ReturnToMainMenu()
        {
            LogDebug("返回主菜单");

            // 先离开Photon房间
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }

            // 使用SceneTransitionManager静态方法
            bool success = SceneTransitionManager.ReturnToMainMenu("GameExitManager");
            if (!success)
            {
                LogDebug("SceneTransitionManager返回主菜单失败，使用备用方案");
                SceneManager.LoadScene("MainMenuScene");
            }
        }

        #endregion

        #region 条件检查

        /// <summary>
        /// 检查是否可以退出
        /// </summary>
        private bool CanExit()
        {
            // 基本检查
            if (!enableExitDuringGame)
            {
                LogDebug("游戏中退出功能已禁用");
                return false;
            }

            // 检查网络状态
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("不在Photon房间中，允许退出");
                return true;
            }

            // 检查玩家状态（如果需要）
            // 例如：死亡玩家随时可以退出，存活玩家需要确认

            LogDebug("退出条件检查通过");
            return true;
        }

        /// <summary>
        /// 检查是否在游戏场景中
        /// </summary>
        private bool IsInGameScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            return currentScene.Contains("NetworkGameScene") || currentScene.Contains("GameScene");
        }

        #endregion

        #region 公共接口

        public void RequestExit()
        {
            LogDebug("收到外部退出请求");
            ShowExitConfirmation();
        }

        public void ForceExit()
        {
            LogDebug("收到强制退出请求");
            if (!isExiting)
            {
                ConfirmExit();
            }
        }

        public bool IsActive()
        {
            return this.enabled && !isExiting;
        }

        public bool IsExitDialogOpen()
        {
            return isExitDialogOpen;
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[GameExitManager] {message}");
            }
        }

        #endregion
    }
}