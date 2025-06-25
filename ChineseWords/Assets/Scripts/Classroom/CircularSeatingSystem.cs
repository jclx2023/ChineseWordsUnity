using UnityEngine;
using System.Collections.Generic;

namespace Classroom.Scene
{
    /// <summary>
    /// 圆弧座位系统 - 负责计算并生成面向黑板的圆弧状桌椅布局
    /// 基于极坐标系统的位置分配算法
    /// </summary>
    public class CircularSeatingSystem : MonoBehaviour
    {
        [Header("圆弧布局配置")]
        [SerializeField] private float baseRadius = 3f; // 基础半径（适配8x10米教室）
        [SerializeField] private float radiusPerPlayer = 0.15f; // 每个玩家增加的半径
        [SerializeField] private float maxRadius = 4.5f; // 最大半径限制（不超过教室边界）
        [SerializeField] private float minRadius = 2.5f; // 最小半径限制

        [Header("弧度范围配置")]
        [SerializeField] private float smallGroupArc = 120f; // 2-4人弧度
        [SerializeField] private float mediumGroupArc = 150f; // 5-8人弧度
        [SerializeField] private float largeGroupArc = 180f; // 9+人弧度

        [Header("座位偏移配置")]
        [SerializeField] private float seatHeight = 0f; // 座位高度偏移（0表示贴地）
        [SerializeField] private Vector3 seatRotationOffset = new Vector3(0, 180, 0); // 座位旋转偏移（让椅子面向黑板）
        [SerializeField] private bool debugChairDirection = true; // 调试椅子朝向
        [SerializeField] private bool enableGroundDetection = true; // 启用地面检测
        [SerializeField] private LayerMask groundLayerMask = 1; // 地面层级遮罩

        [Header("间距配置")]
        [SerializeField] private float minAngleBetweenSeats = 12f; // 最小角度间距（度）
        [SerializeField] private float distanceFromBlackboard = 1.5f; // 距离黑板的最小距离（适配教室大小）

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color gizmoColor = Color.cyan;

        // 座位数据结构
        [System.Serializable]
        public class SeatData
        {
            public Vector3 seatPosition; // 桌椅一体的位置
            public Quaternion seatRotation; // 桌椅一体的旋转
            public int seatIndex;
            public GameObject seatInstance; // 桌椅一体实例

            public SeatData(int index)
            {
                seatIndex = index;
            }
        }

        // 私有变量
        private SceneLayoutManager sceneManager;
        private List<SeatData> generatedSeats = new List<SeatData>();
        private Vector3 arcCenter;
        private bool isInitialized = false;

        // 公共属性
        public List<SeatData> GeneratedSeats => generatedSeats;
        public bool IsInitialized => isInitialized;
        public int SeatCount => generatedSeats.Count;

        #region 初始化

        /// <summary>
        /// 初始化座位系统
        /// </summary>
        /// <param name="layoutManager">场景布局管理器引用</param>
        public void Initialize(SceneLayoutManager layoutManager)
        {
            sceneManager = layoutManager;
            CalculateArcCenter();
            isInitialized = true;
            LogDebug("CircularSeatingSystem 初始化完成");
        }

        /// <summary>
        /// 计算圆弧中心点
        /// </summary>
        private void CalculateArcCenter()
        {
            if (sceneManager != null)
            {
                Vector3 blackboardPos = sceneManager.BlackboardPosition;
                Vector3 classroomCenter = sceneManager.ClassroomCenter;

                // 针对8x10米教室的优化布局
                // 黑板在(5, 1.5, 0)，教室中心在(0, 0, 0)
                // 圆弧中心应该在黑板前方适当距离

                Vector3 directionFromBoard;
                if (Vector3.Distance(blackboardPos, classroomCenter) < 0.1f)
                {
                    // 如果黑板和教室中心重合，默认黑板面向-Z方向
                    directionFromBoard = Vector3.back;
                }
                else
                {
                    directionFromBoard = (classroomCenter - blackboardPos).normalized;
                }

                arcCenter = blackboardPos + directionFromBoard * distanceFromBlackboard;

                // 确保圆弧中心在教室范围内（8x10米教室边界检查）
                arcCenter.x = Mathf.Clamp(arcCenter.x, -3.5f, 3.5f); // 8米宽度的一半减去安全边距
                arcCenter.z = Mathf.Clamp(arcCenter.z, -4.5f, 4.5f); // 10米深度的一半减去安全边距

                LogDebug($"圆弧中心计算完成：{arcCenter}，黑板位置：{blackboardPos}");
            }
        }

        #endregion

        #region 座位生成

        /// <summary>
        /// 生成指定数量的座位
        /// </summary>
        /// <param name="playerCount">玩家数量</param>
        public void GenerateSeats(int playerCount)
        {
            if (!isInitialized)
            {
                LogDebug("座位系统未初始化，无法生成座位");
                return;
            }

            if (playerCount <= 0)
            {
                LogDebug("玩家数量无效，无法生成座位");
                return;
            }

            LogDebug($"开始生成{playerCount}个座位...");

            // 清理现有座位
            ClearAllSeats();

            // 计算布局参数
            SeatingParameters parameters = CalculateSeatingParameters(playerCount);

            // 生成座位
            for (int i = 0; i < playerCount; i++)
            {
                SeatData seatData = CreateSeatAtIndex(i, playerCount, parameters);
                generatedSeats.Add(seatData);
            }

            LogDebug($"座位生成完成，共生成{generatedSeats.Count}个座位");
        }

        /// <summary>
        /// 计算座位参数
        /// </summary>
        private SeatingParameters CalculateSeatingParameters(int playerCount)
        {
            SeatingParameters parameters = new SeatingParameters();

            // 计算半径（针对8x10米教室优化）
            parameters.radius = Mathf.Clamp(
                baseRadius + (playerCount * radiusPerPlayer),
                minRadius,
                maxRadius
            );

            // 计算弧度范围（更保守的角度设置）
            if (playerCount <= 3)
                parameters.arcDegrees = 90f;  // 小组更紧凑
            else if (playerCount <= 6)
                parameters.arcDegrees = 120f; // 中组适中
            else if (playerCount <= 10)
                parameters.arcDegrees = 150f; // 大组扩展
            else
                parameters.arcDegrees = 180f; // 最大组

            // 转换为弧度
            parameters.arcRadians = parameters.arcDegrees * Mathf.Deg2Rad;

            // 计算起始和结束角度
            parameters.startAngle = -parameters.arcRadians * 0.5f;
            parameters.endAngle = parameters.arcRadians * 0.5f;

            // 验证最小角度间距
            float actualAngleStep = parameters.arcDegrees / Mathf.Max(1, playerCount - 1);
            if (actualAngleStep < minAngleBetweenSeats && playerCount > 1)
            {
                LogDebug($"角度间距过小({actualAngleStep:F1}°)，调整弧度范围");
                parameters.arcDegrees = minAngleBetweenSeats * (playerCount - 1);
                parameters.arcDegrees = Mathf.Min(parameters.arcDegrees, 180f); // 不超过180度
                parameters.arcRadians = parameters.arcDegrees * Mathf.Deg2Rad;
                parameters.startAngle = -parameters.arcRadians * 0.5f;
                parameters.endAngle = parameters.arcRadians * 0.5f;
            }

            LogDebug($"座位参数：半径={parameters.radius:F1}m, 弧度={parameters.arcDegrees:F1}°, 玩家数={playerCount}");
            return parameters;
        }

        /// <summary>
        /// 在指定索引创建座位
        /// </summary>
        private SeatData CreateSeatAtIndex(int index, int totalCount, SeatingParameters parameters)
        {
            SeatData seatData = new SeatData(index);

            // 计算当前座位的角度
            float currentAngle;
            if (totalCount == 1)
            {
                currentAngle = 0; // 单人时面向黑板
            }
            else
            {
                float t = (float)index / (totalCount - 1);
                currentAngle = Mathf.Lerp(parameters.startAngle, parameters.endAngle, t);
            }

            // 计算桌椅一体的基础位置
            Vector3 basePosition = CalculatePositionOnArc(currentAngle, parameters.radius);

            // 地面检测和高度调整
            seatData.seatPosition = CalculateGroundPosition(basePosition);

            // 计算朝向（椅子面向黑板）
            Vector3 directionToBlackboard = (sceneManager.BlackboardPosition - seatData.seatPosition).normalized;
            seatData.seatRotation = Quaternion.LookRotation(directionToBlackboard);

            // 应用旋转偏移
            if (seatRotationOffset != Vector3.zero)
            {
                seatData.seatRotation *= Quaternion.Euler(seatRotationOffset);
            }

            // 调试信息
            if (debugChairDirection)
            {
                Vector3 chairForward = seatData.seatRotation * Vector3.forward;
                LogDebug($"座位{index}: 位置{seatData.seatPosition}, 朝向黑板方向{directionToBlackboard}, 椅子前方{chairForward}");
            }

            // 创建实际的桌椅对象
            CreatePhysicalSeat(seatData);

            return seatData;
        }

        /// <summary>
        /// 计算圆弧上的位置
        /// </summary>
        private Vector3 CalculatePositionOnArc(float angle, float radius)
        {
            float x = arcCenter.x + radius * Mathf.Sin(angle);
            float z = arcCenter.z + radius * Mathf.Cos(angle);
            return new Vector3(x, arcCenter.y, z);
        }

        /// <summary>
        /// 计算贴地位置
        /// </summary>
        private Vector3 CalculateGroundPosition(Vector3 basePosition)
        {
            Vector3 finalPosition = basePosition;

            if (enableGroundDetection)
            {
                // 从上方向下射线检测地面
                Vector3 rayStart = basePosition + Vector3.up * 5f; // 从5米高度开始检测

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f, groundLayerMask))
                {
                    // 找到地面，将座位放置在地面上
                    finalPosition.y = hit.point.y + seatHeight;
                    LogDebug($"地面检测成功：{hit.point.y:F2}, 座位高度：{finalPosition.y:F2}");
                }
                else
                {
                    // 没有检测到地面，使用默认高度
                    finalPosition.y = seatHeight;
                    LogDebug($"未检测到地面，使用默认高度：{finalPosition.y:F2}");
                }
            }
            else
            {
                // 不使用地面检测，直接设置高度
                finalPosition.y = seatHeight;
            }

            return finalPosition;
        }

        /// <summary>
        /// 创建物理座位对象
        /// </summary>
        private void CreatePhysicalSeat(SeatData seatData)
        {
            // 创建桌椅一体对象
            GameObject deskChairPrefab = GetDeskChairPrefab();
            if (sceneManager != null && deskChairPrefab != null)
            {
                seatData.seatInstance = Instantiate(deskChairPrefab, seatData.seatPosition, seatData.seatRotation);
                seatData.seatInstance.name = $"DeskChair_{seatData.seatIndex:D2}";

                // 可选：为座位添加标识组件，便于后续查找和管理
                SeatIdentifier identifier = seatData.seatInstance.GetComponent<SeatIdentifier>();
                if (identifier == null)
                {
                    identifier = seatData.seatInstance.AddComponent<SeatIdentifier>();
                }
                identifier.seatIndex = seatData.seatIndex;
                identifier.seatData = seatData;
            }
            else
            {
                LogDebug($"无法创建座位{seatData.seatIndex}：桌椅预制体未设置");
            }
        }

        #endregion

        #region 座位管理

        /// <summary>
        /// 清理所有座位
        /// </summary>
        public void ClearAllSeats()
        {
            LogDebug($"清理{generatedSeats.Count}个座位");

            foreach (SeatData seat in generatedSeats)
            {
                if (seat.seatInstance != null)
                    DestroyImmediate(seat.seatInstance);
            }

            generatedSeats.Clear();
        }

        /// <summary>
        /// 获取指定索引的座位数据
        /// </summary>
        public SeatData GetSeatData(int index)
        {
            if (index >= 0 && index < generatedSeats.Count)
                return generatedSeats[index];
            return null;
        }

        /// <summary>
        /// 获取距离指定位置最近的座位
        /// </summary>
        public SeatData GetNearestSeat(Vector3 position)
        {
            if (generatedSeats.Count == 0) return null;

            SeatData nearest = generatedSeats[0];
            float minDistance = Vector3.Distance(position, nearest.seatPosition);

            foreach (SeatData seat in generatedSeats)
            {
                float distance = Vector3.Distance(position, seat.seatPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = seat;
                }
            }

            return nearest;
        }

        #endregion

        #region 辅助方法

        private GameObject GetDeskChairPrefab()
        {
            return sceneManager?.GetComponent<SceneLayoutManager>()?.GetType()
                ?.GetField("deskChairPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(sceneManager) as GameObject;
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CircularSeatingSystem] {message}");
            }
        }

        #endregion

        #region 调试可视化

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !isInitialized) return;

            Gizmos.color = gizmoColor;

            // 绘制圆弧中心
            Gizmos.DrawWireSphere(arcCenter, 0.3f);

            // 绘制座位位置
            foreach (SeatData seat in generatedSeats)
            {
                // 绘制桌椅位置
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(seat.seatPosition, Vector3.one * 0.8f);

                // 绘制朝向
                Gizmos.color = Color.green;
                Vector3 forward = seat.seatRotation * Vector3.forward;
                Gizmos.DrawRay(seat.seatPosition, forward * 1.5f);

                // 绘制座位编号（可选）
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(seat.seatPosition + Vector3.up * 1.5f, 0.1f);
            }
        }

        [ContextMenu("测试生成6人座位")]
        private void TestGenerate6Seats()
        {
            if (sceneManager == null)
                sceneManager = GetComponent<SceneLayoutManager>();

            if (!isInitialized)
                Initialize(sceneManager);

            GenerateSeats(6);
        }

        #endregion

        /// <summary>
        /// 座位参数数据结构
        /// </summary>
        private struct SeatingParameters
        {
            public float radius;
            public float arcDegrees;
            public float arcRadians;
            public float startAngle;
            public float endAngle;
        }
    }
}