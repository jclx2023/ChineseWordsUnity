using UnityEngine;
using System.Collections.Generic;

namespace Classroom.Scene
{
    /// <summary>
    /// 独立的圆弧座位系统 - 可直接挂载使用，所有参数可在编辑器中设置
    /// </summary>
    public class CircularSeatingSystem : MonoBehaviour
    {
        [Header("核心配置")]
        [SerializeField] private GameObject deskChairPrefab; // 桌椅一体预制体
        [SerializeField] private int playerCount = 4; // 玩家数量
        [SerializeField] private bool autoGenerateOnStart = false; // 启动时自动生成

        [Header("圆弧中心设置")]
        [SerializeField] private Vector3 arcCenter = new Vector3(5, 0, 0); // 圆弧中心点（黑板底部或更后方）
        [SerializeField] private Vector3 seatDistributionCenter = Vector3.zero; // 座位分布中心点（Z=0中线）
        [SerializeField] private bool showArcCenterGizmo = true;

        [Header("桌椅尺寸")]
        [SerializeField] private float deskChairWidth = 0.64f; // 桌椅宽度（缩放后0.8倍）
        [SerializeField] private float deskChairDepth = 0.48f; // 桌椅深度（缩放后0.8倍）
        [SerializeField] private float extraSpacing = 0.3f; // 桌椅之间的额外间距

        [Header("教室边界")]
        [SerializeField] private float classroomWidth = 8f; // 教室宽度（Z轴方向）
        [SerializeField] private float classroomDepth = 10f; // 教室深度（X轴方向）
        [SerializeField] private float wallMargin = 0.5f; // 距离墙壁的安全边距
        [SerializeField] private bool autoAdjustForClassroomSize = true; // 自动根据教室尺寸调整
        [Header("圆弧布局配置")]
        [SerializeField] private float baseRadius = 3f; // 基础半径
        [SerializeField] private float radiusPerPlayer = 0.15f; // 每个玩家增加的半径
        [SerializeField] private float maxRadius = 4.5f; // 最大半径限制
        [SerializeField] private float minRadius = 2.5f; // 最小半径限制

        [Header("弧度范围配置")]
        [SerializeField] private float maxArcDegrees = 150f; // 最大弧度角度
        [SerializeField] private float minAngleBetweenSeats = 12f; // 最小角度间距（度）

        [Header("座位高度和旋转")]
        [SerializeField] private float seatHeight = 0f; // 座位高度（Y坐标）
        [SerializeField] private Vector3 chairDefaultDirection = Vector3.back; // 椅子默认朝向（Unity中）
        [SerializeField] private bool lockXZRotation = true; // 锁定X和Z轴旋转，只允许Y轴旋转
        [SerializeField] private float yRotationOffset = 180f; // Y轴旋转偏移（用于微调椅子朝向）

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color arcCenterColor = Color.red;
        [SerializeField] private Color seatCenterColor = Color.green;
        [SerializeField] private Color seatPositionColor = Color.blue;
        [SerializeField] private Color directionRayColor = Color.yellow;

        // 座位数据结构
        [System.Serializable]
        public class SeatData
        {
            public Vector3 seatPosition; // 桌椅一体的位置
            public Quaternion seatRotation; // 桌椅一体的旋转
            public int seatIndex;
            public GameObject seatInstance; // 桌椅一体实例
            public float angleFromCenter; // 从中心线的角度偏移

            public SeatData(int index)
            {
                seatIndex = index;
            }
        }

        // 私有变量
        private List<SeatData> generatedSeats = new List<SeatData>();
        private bool isInitialized = false;

        // 公共属性
        public List<SeatData> GeneratedSeats => generatedSeats;
        public bool IsInitialized => isInitialized;
        public int SeatCount => generatedSeats.Count;
        public Vector3 ArcCenter => arcCenter;
        public Vector3 SeatDistributionCenter => seatDistributionCenter;

        #region Unity生命周期

        private void Awake()
        {
            isInitialized = true;
            LogDebug("CircularSeatingSystem 已初始化（独立模式）");
        }

        private void Start()
        {
            if (autoGenerateOnStart && deskChairPrefab != null)
            {
                GenerateSeats(playerCount);
            }
        }

        #endregion

        #region 座位生成

        /// <summary>
        /// 生成指定数量的座位
        /// </summary>
        /// <param name="count">玩家数量</param>
        public void GenerateSeats(int count)
        {
            if (deskChairPrefab == null)
            {
                LogDebug("桌椅预制体未设置，无法生成座位");
                return;
            }

            if (count <= 0)
            {
                LogDebug("玩家数量无效，无法生成座位");
                return;
            }

            LogDebug($"开始生成{count}个座位...");

            // 清理现有座位
            ClearAllSeats();

            // 计算布局参数
            SeatingParameters parameters = CalculateSeatingParameters(count);

            // 生成座位
            for (int i = 0; i < count; i++)
            {
                SeatData seatData = CreateSeatAtIndex(i, count, parameters);
                generatedSeats.Add(seatData);
            }

            LogDebug($"座位生成完成，共生成{generatedSeats.Count}个座位");
        }

        /// <summary>
        /// 计算座位参数
        /// </summary>
        private SeatingParameters CalculateSeatingParameters(int count)
        {
            SeatingParameters parameters = new SeatingParameters();

            if (autoAdjustForClassroomSize)
            {
                // 根据教室尺寸和玩家数量智能计算参数
                parameters = CalculateOptimalParameters(count);
            }
            else
            {
                // 使用传统的固定参数计算
                parameters = CalculateTraditionalParameters(count);
            }

            LogDebug($"座位参数：半径={parameters.radius:F1}m, 弧度={parameters.arcDegrees:F1}°, 玩家数={count}, 座位间距={parameters.actualSeatSpacing:F1}m");
            return parameters;
        }

        /// <summary>
        /// 根据教室尺寸计算最优参数
        /// </summary>
        private SeatingParameters CalculateOptimalParameters(int count)
        {
            SeatingParameters parameters = new SeatingParameters();

            // 根据桌椅实际尺寸计算最小间距
            float minSeatSpacing = Mathf.Max(deskChairWidth, deskChairDepth) + extraSpacing;

            // 计算可用的教室空间
            float availableWidth = classroomWidth - (wallMargin * 2);
            float availableDepth = classroomDepth - (wallMargin * 2);

            // 最大可用半径（考虑教室的宽度和深度限制）
            float maxUsableRadius = Mathf.Min(availableWidth * 0.5f, availableDepth * 0.5f);

            // 根据玩家数量计算所需的弧长来保证最小间距
            float requiredArcLength = (count - 1) * minSeatSpacing;

            // 尝试不同的弧度角度，找到最优解
            float[] candidateArcs = { 60f, 90f, 120f, 150f, 180f };

            foreach (float arcDegrees in candidateArcs)
            {
                float arcRadians = arcDegrees * Mathf.Deg2Rad;

                // 根据弧长公式计算所需半径：弧长 = 半径 × 弧度
                float requiredRadius = requiredArcLength / arcRadians;

                // 检查是否在合理范围内
                if (requiredRadius >= minRadius && requiredRadius <= maxUsableRadius && requiredRadius <= maxRadius)
                {
                    parameters.radius = requiredRadius;
                    parameters.arcDegrees = arcDegrees;
                    parameters.arcRadians = arcRadians;
                    parameters.startAngle = -arcRadians * 0.5f;
                    parameters.endAngle = arcRadians * 0.5f;

                    // 计算实际座位间距
                    parameters.actualSeatSpacing = (parameters.radius * arcRadians) / Mathf.Max(1, count - 1);

                    LogDebug($"找到最优解：弧度{arcDegrees}°, 半径{requiredRadius:F1}m, 间距{parameters.actualSeatSpacing:F1}m, 最小需求间距{minSeatSpacing:F1}m");
                    return parameters;
                }
            }

            // 如果没有找到理想解，使用折中方案
            LogDebug($"未找到理想解，使用折中方案。所需最小间距：{minSeatSpacing:F1}m");
            return CalculateFallbackParameters(count, maxUsableRadius, minSeatSpacing);
        }

        /// <summary>
        /// 计算折中参案
        /// </summary>
        private SeatingParameters CalculateFallbackParameters(int count, float maxUsableRadius, float minSeatSpacing)
        {
            SeatingParameters parameters = new SeatingParameters();

            // 使用最大可用半径
            parameters.radius = Mathf.Min(maxUsableRadius, maxRadius);

            // 计算能容纳的最大弧度（保证最小间距）
            float maxArcLength = parameters.radius * Mathf.PI; // 半圆弧长
            float maxArcForSpacing = (count - 1) * minSeatSpacing;

            if (maxArcForSpacing <= maxArcLength)
            {
                // 可以保证最小间距
                float requiredArcRadians = maxArcForSpacing / parameters.radius;
                parameters.arcDegrees = Mathf.Min(requiredArcRadians * Mathf.Rad2Deg, 180f);
            }
            else
            {
                // 无法保证最小间距，使用最大弧度
                parameters.arcDegrees = 180f;
                LogDebug($"警告：无法保证{minSeatSpacing:F1}m的最小间距，当前间距约为{(parameters.radius * Mathf.PI) / (count - 1):F1}m");
            }

            parameters.arcRadians = parameters.arcDegrees * Mathf.Deg2Rad;
            parameters.startAngle = -parameters.arcRadians * 0.5f;
            parameters.endAngle = parameters.arcRadians * 0.5f;
            parameters.actualSeatSpacing = (parameters.radius * parameters.arcRadians) / Mathf.Max(1, count - 1);

            return parameters;
        }

        /// <summary>
        /// 传统的固定参数计算（保留原有逻辑）
        /// </summary>
        private SeatingParameters CalculateTraditionalParameters(int count)
        {
            SeatingParameters parameters = new SeatingParameters();

            // 计算半径
            parameters.radius = Mathf.Clamp(
                baseRadius + (count * radiusPerPlayer),
                minRadius,
                maxRadius
            );

            // 计算弧度范围，确保座位间距合理
            float requiredArcForSpacing = minAngleBetweenSeats * (count - 1);
            parameters.arcDegrees = Mathf.Min(maxArcDegrees, requiredArcForSpacing);

            // 如果只有一个人，角度为0
            if (count == 1)
            {
                parameters.arcDegrees = 0;
            }

            // 转换为弧度
            parameters.arcRadians = parameters.arcDegrees * Mathf.Deg2Rad;

            // 计算起始和结束角度（以Z轴为基准，向两侧分布）
            parameters.startAngle = -parameters.arcRadians * 0.5f;
            parameters.endAngle = parameters.arcRadians * 0.5f;

            // 计算实际座位间距
            parameters.actualSeatSpacing = (parameters.radius * parameters.arcRadians) / Mathf.Max(1, count - 1);

            return parameters;
        }

        /// <summary>
        /// 在指定索引创建座位
        /// </summary>
        private SeatData CreateSeatAtIndex(int index, int totalCount, SeatingParameters parameters)
        {
            SeatData seatData = new SeatData(index);

            // 计算当前座位的角度（以Z轴为0°基准）
            float currentAngle;
            if (totalCount == 1)
            {
                currentAngle = 0; // 单人时在中心线上
            }
            else
            {
                float t = (float)index / (totalCount - 1);
                currentAngle = Mathf.Lerp(parameters.startAngle, parameters.endAngle, t);
            }

            seatData.angleFromCenter = currentAngle * Mathf.Rad2Deg;

            // 计算座位位置（真正的圆弧布局：每个座位到圆弧中心距离相等）
            Vector3 offsetFromArcCenter = new Vector3(
                -parameters.radius * Mathf.Cos(currentAngle), // X轴偏移（负号因为圆弧朝向黑板）
                0, // Y轴高度
                parameters.radius * Mathf.Sin(currentAngle)   // Z轴偏移
            );

            seatData.seatPosition = arcCenter + offsetFromArcCenter;
            seatData.seatPosition.y = seatHeight;

            // 计算朝向（面向圆弧中心）
            Vector3 directionToArcCenter = (arcCenter - seatData.seatPosition).normalized;

            // 只保留Y轴旋转分量
            if (lockXZRotation)
            {
                // 计算Y轴旋转角度
                float yRotation = Mathf.Atan2(directionToArcCenter.x, directionToArcCenter.z) * Mathf.Rad2Deg;

                // 应用Y轴旋转偏移
                yRotation += yRotationOffset;

                seatData.seatRotation = Quaternion.Euler(0, yRotation, 0);
            }
            else
            {
                seatData.seatRotation = Quaternion.LookRotation(directionToArcCenter) * Quaternion.Euler(0, yRotationOffset, 0);
            }

            // 创建实际的桌椅对象
            CreatePhysicalSeat(seatData);

            // 调试信息
            LogDebug($"座位{index}: 位置{seatData.seatPosition}, 弧度角度{seatData.angleFromCenter:F1}°, Y旋转{seatData.seatRotation.eulerAngles.y:F1}°, 朝向圆弧中心{arcCenter}");

            return seatData;
        }

        /// <summary>
        /// 创建物理座位对象
        /// </summary>
        private void CreatePhysicalSeat(SeatData seatData)
        {
            if (deskChairPrefab != null)
            {
                seatData.seatInstance = Instantiate(deskChairPrefab, seatData.seatPosition, seatData.seatRotation, transform);
                seatData.seatInstance.name = $"DeskChair_{seatData.seatIndex:D2}";
                foreach (var renderer in seatData.seatInstance.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                // 可选：添加座位标识组件
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
                {
                    if (Application.isPlaying)
                        Destroy(seat.seatInstance);
                    else
                        DestroyImmediate(seat.seatInstance);
                }
            }

            generatedSeats.Clear();
        }

        /// <summary>
        /// 更新座位数量（编辑器用）
        /// </summary>
        public void UpdatePlayerCount(int newCount)
        {
            playerCount = Mathf.Max(1, newCount);
            if (Application.isPlaying || generatedSeats.Count > 0)
            {
                GenerateSeats(playerCount);
            }
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

        #endregion

        #region 调试可视化

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // 绘制教室边界
            if (autoAdjustForClassroomSize)
            {
                Gizmos.color = Color.gray;
                Vector3 classroomSize = new Vector3(classroomDepth, 0.1f, classroomWidth);
                Gizmos.DrawWireCube(Vector3.zero, classroomSize);

                // 绘制安全边距
                Gizmos.color = Color.yellow;
                Vector3 safeAreaSize = new Vector3(classroomDepth - wallMargin * 2, 0.1f, classroomWidth - wallMargin * 2);
                Gizmos.DrawWireCube(Vector3.zero, safeAreaSize);
            }

            // 绘制圆弧中心
            if (showArcCenterGizmo)
            {
                Gizmos.color = arcCenterColor;
                Gizmos.DrawWireSphere(arcCenter, 0.3f);
                Gizmos.DrawWireCube(arcCenter, Vector3.one * 0.1f);
            }

            // 绘制座位分布中心
            Gizmos.color = seatCenterColor;
            Gizmos.DrawWireSphere(seatDistributionCenter, 0.2f);

            // 绘制连接线
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(arcCenter, seatDistributionCenter);

            // 绘制座位位置和朝向
            foreach (SeatData seat in generatedSeats)
            {
                // 绘制座位位置
                Gizmos.color = seatPositionColor;
                Gizmos.DrawWireCube(seat.seatPosition, Vector3.one * 0.4f);

                // 绘制朝向
                Gizmos.color = directionRayColor;
                Vector3 forward = seat.seatRotation * Vector3.forward;
                Gizmos.DrawRay(seat.seatPosition, forward * 1.5f);

                // 绘制到圆弧中心的连线
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(seat.seatPosition, arcCenter);

                // 绘制座位间距（相邻座位之间的连线）
                if (seat.seatIndex < generatedSeats.Count - 1)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(seat.seatPosition, generatedSeats[seat.seatIndex + 1].seatPosition);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // 选中时显示更详细的信息
            Gizmos.color = Color.white;

            // 绘制弧度范围
            if (generatedSeats.Count > 1)
            {
                SeatingParameters parameters = CalculateSeatingParameters(playerCount);

                // 绘制弧度范围线
                Vector3 startDir = new Vector3(0, 0, parameters.radius * Mathf.Sin(parameters.startAngle));
                Vector3 endDir = new Vector3(0, 0, parameters.radius * Mathf.Sin(parameters.endAngle));

                Gizmos.DrawLine(seatDistributionCenter, seatDistributionCenter + startDir);
                Gizmos.DrawLine(seatDistributionCenter, seatDistributionCenter + endDir);
            }
        }

        #endregion

        #region 编辑器接口

        [ContextMenu("生成座位")]
        private void TestGenerateSeats()
        {
            GenerateSeats(playerCount);
        }

        [ContextMenu("清理座位")]
        private void TestClearSeats()
        {
            ClearAllSeats();
        }

        [ContextMenu("重新生成")]
        private void TestRegenerateSeats()
        {
            ClearAllSeats();
            GenerateSeats(playerCount);
        }

        #endregion

        #region 辅助方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CircularSeatingSystem] {message}");
            }
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
            public float actualSeatSpacing; // 实际座位间距
        }
    }
}