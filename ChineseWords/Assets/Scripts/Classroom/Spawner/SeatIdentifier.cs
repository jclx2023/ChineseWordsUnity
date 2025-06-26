using UnityEngine;

namespace Classroom.Scene
{
    /// <summary>
    /// 座位标识组件 - 用于标识和管理每个座位实例
    /// 挂载在桌椅一体预制体实例上，便于后续查找和管理
    /// </summary>
    public class SeatIdentifier : MonoBehaviour
    {
        [Header("座位信息")]
        [SerializeField] public int seatIndex = -1; // 座位索引
        [SerializeField] private bool isOccupied = false; // 是否被占用
        [SerializeField] private string occupantName = ""; // 占用者名称

        [Header("座位配置")]
        [SerializeField] private Transform playerSpawnPoint; // 玩家生成点（可选）
        [SerializeField] private Transform cameraMount; // 摄像机挂载点（可选）

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = false;

        // 座位数据引用
        [System.NonSerialized]
        public CircularSeatingSystem.SeatData seatData;

        // 公共属性
        public bool IsOccupied => isOccupied;
        public string OccupantName => occupantName;
        public Transform PlayerSpawnPoint => playerSpawnPoint;
        public Transform CameraMount => cameraMount;

        #region Unity生命周期

        private void Awake()
        {
            // 自动查找玩家生成点和摄像机挂载点
            AutoFindSpawnAndCameraPoints();
        }

        private void Start()
        {
            LogDebug($"座位{seatIndex}已初始化");
        }

        #endregion

        #region 座位管理

        /// <summary>
        /// 占用座位
        /// </summary>
        /// <param name="playerName">玩家名称</param>
        /// <returns>是否成功占用</returns>
        public bool OccupySeat(string playerName)
        {
            if (isOccupied)
            {
                LogDebug($"座位{seatIndex}已被{occupantName}占用，无法分配给{playerName}");
                return false;
            }

            isOccupied = true;
            occupantName = playerName;
            LogDebug($"座位{seatIndex}已分配给{playerName}");

            return true;
        }

        /// <summary>
        /// 释放座位
        /// </summary>
        public void ReleaseSeat()
        {
            if (isOccupied)
            {
                LogDebug($"座位{seatIndex}已释放（之前占用者：{occupantName}）");
                isOccupied = false;
                occupantName = "";
            }
        }

        /// <summary>
        /// 检查是否可以被指定玩家占用
        /// </summary>
        /// <param name="playerName">玩家名称</param>
        /// <returns>是否可占用</returns>
        public bool CanBeOccupiedBy(string playerName)
        {
            return !isOccupied || occupantName == playerName;
        }

        #endregion

        #region 位置查找

        /// <summary>
        /// 自动查找玩家生成点和摄像机挂载点
        /// </summary>
        private void AutoFindSpawnAndCameraPoints()
        {
            // 查找玩家生成点
            if (playerSpawnPoint == null)
            {
                playerSpawnPoint = FindChildByName("PlayerSpawnPoint") ??
                                 FindChildByName("SpawnPoint") ??
                                 FindChildByName("PlayerPos");

                if (playerSpawnPoint == null && transform.childCount > 0)
                {
                    // 如果没有专门的生成点，使用椅子位置作为默认生成点
                    Transform chairTransform = FindChildByName("Chair") ??
                                             FindChildByName("椅子") ??
                                             transform; // 如果找不到椅子，使用根transform
                    playerSpawnPoint = chairTransform;
                }
            }

            // 查找摄像机挂载点
            if (cameraMount == null)
            {
                cameraMount = FindChildByName("CameraMount") ??
                            FindChildByName("CameraPos") ??
                            FindChildByName("HeadMount");

                if (cameraMount == null)
                {
                    // 如果没有专门的摄像机挂载点，创建一个
                    GameObject cameraMountObj = new GameObject("CameraMount");
                    cameraMountObj.transform.SetParent(transform);
                    cameraMountObj.transform.localPosition = new Vector3(0, 1.8f, 0); // 假设椅子高度1.8米
                    cameraMountObj.transform.localRotation = Quaternion.identity;
                    cameraMount = cameraMountObj.transform;

                    LogDebug($"为座位{seatIndex}自动创建摄像机挂载点");
                }
            }
        }

        /// <summary>
        /// 根据名称查找子对象
        /// </summary>
        private Transform FindChildByName(string name)
        {
            return transform.Find(name);
        }

        /// <summary>
        /// 递归查找子对象（包含部分匹配）
        /// </summary>
        private Transform FindChildByNameRecursive(string name)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.Contains(name))
                    return child;

                Transform found = child.GetComponentInChildren<Transform>().Find(name);
                if (found != null)
                    return found;
            }
            return null;
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取座位的世界坐标位置
        /// </summary>
        /// <returns>座位世界坐标</returns>
        public Vector3 GetSeatWorldPosition()
        {
            return transform.position;
        }

        /// <summary>
        /// 获取座位的朝向
        /// </summary>
        /// <returns>座位朝向</returns>
        public Quaternion GetSeatRotation()
        {
            return transform.rotation;
        }

        /// <summary>
        /// 获取玩家应该生成的位置
        /// </summary>
        /// <returns>玩家生成位置</returns>
        public Vector3 GetPlayerSpawnPosition()
        {
            return playerSpawnPoint != null ? playerSpawnPoint.position : transform.position;
        }

        /// <summary>
        /// 获取摄像机挂载位置
        /// </summary>
        /// <returns>摄像机挂载位置</returns>
        public Vector3 GetCameraMountPosition()
        {
            return cameraMount != null ? cameraMount.position : transform.position + Vector3.up * 1.8f;
        }

        /// <summary>
        /// 获取摄像机挂载旋转
        /// </summary>
        /// <returns>摄像机挂载旋转</returns>
        public Quaternion GetCameraMountRotation()
        {
            return cameraMount != null ? cameraMount.rotation : transform.rotation;
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SeatIdentifier-{seatIndex}] {message}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (seatIndex >= 0)
            {
                // 绘制座位编号
                Gizmos.color = isOccupied ? Color.red : Color.green;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);

                // 绘制玩家生成点
                if (playerSpawnPoint != null)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireSphere(playerSpawnPoint.position, 0.2f);
                }

                // 绘制摄像机挂载点
                if (cameraMount != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(cameraMount.position, Vector3.one * 0.1f);

                    // 绘制摄像机视线方向
                    Vector3 forward = cameraMount.rotation * Vector3.forward;
                    Gizmos.DrawRay(cameraMount.position, forward * 2f);
                }
            }
        }

        [ContextMenu("显示座位信息")]
        private void ShowSeatInfo()
        {
            string info = $"=== 座位 {seatIndex} 信息 ===\n";
            info += $"占用状态: {(isOccupied ? "已占用" : "空闲")}\n";
            info += $"占用者: {(string.IsNullOrEmpty(occupantName) ? "无" : occupantName)}\n";
            info += $"玩家生成点: {(playerSpawnPoint != null ? "✓" : "✗")}\n";
            info += $"摄像机挂载点: {(cameraMount != null ? "✓" : "✗")}\n";
            info += $"座位位置: {transform.position}\n";

            Debug.Log(info);
        }

        [ContextMenu("重新查找挂载点")]
        private void RefindMountPoints()
        {
            playerSpawnPoint = null;
            cameraMount = null;
            AutoFindSpawnAndCameraPoints();
            LogDebug("已重新查找挂载点");
        }

        #endregion
    }
}