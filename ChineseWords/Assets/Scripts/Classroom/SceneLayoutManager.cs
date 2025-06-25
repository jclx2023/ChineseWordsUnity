using UnityEngine;
using System.Collections.Generic;

namespace Classroom.Scene
{
    /// <summary>
    /// 场景布局管理器 - 负责教室场景的整体布局初始化
    /// 阶段一：基础场景搭建（单机测试）
    /// </summary>
    public class SceneLayoutManager : MonoBehaviour
    {
        [Header("场景配置")]
        [SerializeField] private Transform blackboardTransform;
        [SerializeField] private Vector3 blackboardPosition = new Vector3(0, 0, 10);
        [SerializeField] private Vector3 blackboardRotationOffset = new Vector3(0, 90, 0); // 黑板额外旋转偏移
        [SerializeField] private Vector3 classroomCenter = Vector3.zero;

        [Header("预制体引用")]
        [SerializeField] private GameObject deskChairPrefab; // 桌椅一体预制体

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool autoInitializeOnStart = true;

        // 组件引用
        private CircularSeatingSystem seatingSystem;

        // 单例模式
        public static SceneLayoutManager Instance { get; private set; }

        // 公共属性
        public Transform BlackboardTransform => blackboardTransform;
        public Vector3 BlackboardPosition => blackboardPosition;
        public Vector3 ClassroomCenter => classroomCenter;
        public bool IsInitialized { get; private set; }

        #region Unity生命周期

        private void Awake()
        {
            // 单例初始化
            if (Instance == null)
            {
                Instance = this;
                LogDebug("SceneLayoutManager 单例已创建");
            }
            else
            {
                LogDebug("发现重复的SceneLayoutManager，销毁当前对象");
                Destroy(gameObject);
                return;
            }

            // 初始化组件
            InitializeComponents();
        }

        private void Start()
        {
            if (autoInitializeOnStart)
            {
                InitializeScene();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                LogDebug("SceneLayoutManager 单例已清理");
            }
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            // 获取或创建圆弧座位系统
            seatingSystem = GetComponent<CircularSeatingSystem>();
            if (seatingSystem == null)
            {
                seatingSystem = gameObject.AddComponent<CircularSeatingSystem>();
                LogDebug("自动添加 CircularSeatingSystem 组件");
            }

            LogDebug("SceneLayoutManager 组件初始化完成");
        }

        /// <summary>
        /// 初始化整个场景
        /// </summary>
        public void InitializeScene()
        {
            if (IsInitialized)
            {
                LogDebug("场景已经初始化，跳过重复初始化");
                return;
            }

            LogDebug("开始初始化教室场景...");

            // 1. 设置黑板位置（教室已存在于场景中）
            SetupBlackboard();

            // 2. 初始化座位系统
            InitializeSeatingSystem();

            IsInitialized = true;
            LogDebug("教室场景初始化完成");
        }

        #endregion

        #region 场景设置方法

        /// <summary>
        /// 设置黑板位置和引用（教室已存在于场景中）
        /// </summary>
        private void SetupBlackboard()
        {
            // 如果没有手动指定黑板Transform，尝试自动查找
            if (blackboardTransform == null)
            {
                blackboardTransform = FindBlackboardInScene();
            }

            // 如果还是没找到，创建一个虚拟黑板位置
            if (blackboardTransform == null)
            {
                GameObject virtualBlackboard = new GameObject("VirtualBlackboard");
                virtualBlackboard.transform.position = blackboardPosition;
                blackboardTransform = virtualBlackboard.transform;
                LogDebug("创建虚拟黑板位置");
            }
            else
            {
                // 更新黑板位置
                blackboardPosition = blackboardTransform.position;

                // 应用黑板旋转偏移
                if (blackboardRotationOffset != Vector3.zero)
                {
                    blackboardTransform.rotation *= Quaternion.Euler(blackboardRotationOffset);
                    LogDebug($"应用黑板旋转偏移: {blackboardRotationOffset}");
                }

                LogDebug($"黑板位置已设置：{blackboardPosition}");
            }
        }

        /// <summary>
        /// 在场景中查找黑板
        /// </summary>
        private Transform FindBlackboardInScene()
        {
            // 尝试通过名称查找
            string[] possibleNames = { "Blackboard", "BlackBoard", "黑板", "Board" };

            foreach (string name in possibleNames)
            {
                GameObject found = GameObject.Find(name);
                if (found != null)
                {
                    LogDebug($"找到黑板对象：{found.name}");
                    return found.transform;
                }
            }

            // 尝试通过标签查找
            GameObject blackboardByTag = GameObject.FindWithTag("Blackboard");
            if (blackboardByTag != null)
            {
                LogDebug($"通过标签找到黑板：{blackboardByTag.name}");
                return blackboardByTag.transform;
            }

            LogDebug("未在场景中找到黑板对象");
            return null;
        }

        /// <summary>
        /// 初始化座位系统
        /// </summary>
        private void InitializeSeatingSystem()
        {
            if (seatingSystem != null)
            {
                seatingSystem.Initialize(this);
                LogDebug("座位系统初始化完成");
            }
            else
            {
                LogDebug("座位系统组件未找到，无法初始化");
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 生成指定数量的桌椅（测试用）
        /// </summary>
        /// <param name="playerCount">玩家数量</param>
        public void GenerateSeatsForTesting(int playerCount)
        {
            if (!IsInitialized)
            {
                InitializeScene();
            }

            if (seatingSystem != null)
            {
                seatingSystem.GenerateSeats(playerCount);
                LogDebug($"为{playerCount}名玩家生成座位（测试模式）");
            }
        }

        /// <summary>
        /// 清理所有生成的座位
        /// </summary>
        public void ClearAllSeats()
        {
            if (seatingSystem != null)
            {
                seatingSystem.ClearAllSeats();
                LogDebug("已清理所有座位");
            }
        }

        /// <summary>
        /// 重新初始化场景
        /// </summary>
        public void ReinitializeScene()
        {
            LogDebug("重新初始化场景");

            IsInitialized = false;
            ClearAllSeats();

            InitializeScene();
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneLayoutManager] {message}");
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // 绘制教室中心
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(classroomCenter, 0.5f);

            // 绘制黑板位置
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(blackboardPosition, new Vector3(3, 2, 0.2f));

            // 绘制黑板到教室中心的连线
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(classroomCenter, blackboardPosition);
        }

        [ContextMenu("测试初始化场景")]
        private void TestInitializeScene()
        {
            InitializeScene();
        }

        [ContextMenu("测试生成4人座位")]
        private void TestGenerate4Seats()
        {
            GenerateSeatsForTesting(4);
        }

        [ContextMenu("测试生成8人座位")]
        private void TestGenerate8Seats()
        {
            GenerateSeatsForTesting(8);
        }

        [ContextMenu("测试生成12人座位")]
        private void TestGenerate12Seats()
        {
            GenerateSeatsForTesting(12);
        }

        [ContextMenu("清理所有座位")]
        private void TestClearSeats()
        {
            ClearAllSeats();
        }

        #endregion
    }
}