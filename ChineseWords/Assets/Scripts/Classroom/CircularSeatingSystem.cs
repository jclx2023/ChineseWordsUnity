using UnityEngine;
using System.Collections.Generic;

namespace Classroom.Scene
{
    /// <summary>
    /// ������Բ����λϵͳ - ��ֱ�ӹ���ʹ�ã����в������ڱ༭��������
    /// </summary>
    public class CircularSeatingSystem : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private GameObject deskChairPrefab; // ����һ��Ԥ����
        [SerializeField] private int playerCount = 4; // �������
        [SerializeField] private bool autoGenerateOnStart = false; // ����ʱ�Զ�����

        [Header("Բ����������")]
        [SerializeField] private Vector3 arcCenter = new Vector3(5, 0, 0); // Բ�����ĵ㣨�ڰ�ײ�����󷽣�
        [SerializeField] private Vector3 seatDistributionCenter = Vector3.zero; // ��λ�ֲ����ĵ㣨Z=0���ߣ�
        [SerializeField] private bool showArcCenterGizmo = true;

        [Header("���γߴ�")]
        [SerializeField] private float deskChairWidth = 0.64f; // ���ο�ȣ����ź�0.8����
        [SerializeField] private float deskChairDepth = 0.48f; // ������ȣ����ź�0.8����
        [SerializeField] private float extraSpacing = 0.3f; // ����֮��Ķ�����

        [Header("���ұ߽�")]
        [SerializeField] private float classroomWidth = 8f; // ���ҿ�ȣ�Z�᷽��
        [SerializeField] private float classroomDepth = 10f; // ������ȣ�X�᷽��
        [SerializeField] private float wallMargin = 0.5f; // ����ǽ�ڵİ�ȫ�߾�
        [SerializeField] private bool autoAdjustForClassroomSize = true; // �Զ����ݽ��ҳߴ����
        [Header("Բ����������")]
        [SerializeField] private float baseRadius = 3f; // �����뾶
        [SerializeField] private float radiusPerPlayer = 0.15f; // ÿ��������ӵİ뾶
        [SerializeField] private float maxRadius = 4.5f; // ���뾶����
        [SerializeField] private float minRadius = 2.5f; // ��С�뾶����

        [Header("���ȷ�Χ����")]
        [SerializeField] private float maxArcDegrees = 150f; // ��󻡶ȽǶ�
        [SerializeField] private float minAngleBetweenSeats = 12f; // ��С�Ƕȼ�ࣨ�ȣ�

        [Header("��λ�߶Ⱥ���ת")]
        [SerializeField] private float seatHeight = 0f; // ��λ�߶ȣ�Y���꣩
        [SerializeField] private Vector3 chairDefaultDirection = Vector3.back; // ����Ĭ�ϳ���Unity�У�
        [SerializeField] private bool lockXZRotation = true; // ����X��Z����ת��ֻ����Y����ת
        [SerializeField] private float yRotationOffset = 180f; // Y����תƫ�ƣ�����΢�����ӳ���

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color arcCenterColor = Color.red;
        [SerializeField] private Color seatCenterColor = Color.green;
        [SerializeField] private Color seatPositionColor = Color.blue;
        [SerializeField] private Color directionRayColor = Color.yellow;

        // ��λ���ݽṹ
        [System.Serializable]
        public class SeatData
        {
            public Vector3 seatPosition; // ����һ���λ��
            public Quaternion seatRotation; // ����һ�����ת
            public int seatIndex;
            public GameObject seatInstance; // ����һ��ʵ��
            public float angleFromCenter; // �������ߵĽǶ�ƫ��

            public SeatData(int index)
            {
                seatIndex = index;
            }
        }

        // ˽�б���
        private List<SeatData> generatedSeats = new List<SeatData>();
        private bool isInitialized = false;

        // ��������
        public List<SeatData> GeneratedSeats => generatedSeats;
        public bool IsInitialized => isInitialized;
        public int SeatCount => generatedSeats.Count;
        public Vector3 ArcCenter => arcCenter;
        public Vector3 SeatDistributionCenter => seatDistributionCenter;

        #region Unity��������

        private void Awake()
        {
            isInitialized = true;
            LogDebug("CircularSeatingSystem �ѳ�ʼ��������ģʽ��");
        }

        private void Start()
        {
            if (autoGenerateOnStart && deskChairPrefab != null)
            {
                GenerateSeats(playerCount);
            }
        }

        #endregion

        #region ��λ����

        /// <summary>
        /// ����ָ����������λ
        /// </summary>
        /// <param name="count">�������</param>
        public void GenerateSeats(int count)
        {
            if (deskChairPrefab == null)
            {
                LogDebug("����Ԥ����δ���ã��޷�������λ");
                return;
            }

            if (count <= 0)
            {
                LogDebug("���������Ч���޷�������λ");
                return;
            }

            LogDebug($"��ʼ����{count}����λ...");

            // ����������λ
            ClearAllSeats();

            // ���㲼�ֲ���
            SeatingParameters parameters = CalculateSeatingParameters(count);

            // ������λ
            for (int i = 0; i < count; i++)
            {
                SeatData seatData = CreateSeatAtIndex(i, count, parameters);
                generatedSeats.Add(seatData);
            }

            LogDebug($"��λ������ɣ�������{generatedSeats.Count}����λ");
        }

        /// <summary>
        /// ������λ����
        /// </summary>
        private SeatingParameters CalculateSeatingParameters(int count)
        {
            SeatingParameters parameters = new SeatingParameters();

            if (autoAdjustForClassroomSize)
            {
                // ���ݽ��ҳߴ������������ܼ������
                parameters = CalculateOptimalParameters(count);
            }
            else
            {
                // ʹ�ô�ͳ�Ĺ̶���������
                parameters = CalculateTraditionalParameters(count);
            }

            LogDebug($"��λ�������뾶={parameters.radius:F1}m, ����={parameters.arcDegrees:F1}��, �����={count}, ��λ���={parameters.actualSeatSpacing:F1}m");
            return parameters;
        }

        /// <summary>
        /// ���ݽ��ҳߴ�������Ų���
        /// </summary>
        private SeatingParameters CalculateOptimalParameters(int count)
        {
            SeatingParameters parameters = new SeatingParameters();

            // ��������ʵ�ʳߴ������С���
            float minSeatSpacing = Mathf.Max(deskChairWidth, deskChairDepth) + extraSpacing;

            // ������õĽ��ҿռ�
            float availableWidth = classroomWidth - (wallMargin * 2);
            float availableDepth = classroomDepth - (wallMargin * 2);

            // �����ð뾶�����ǽ��ҵĿ�Ⱥ�������ƣ�
            float maxUsableRadius = Mathf.Min(availableWidth * 0.5f, availableDepth * 0.5f);

            // �������������������Ļ�������֤��С���
            float requiredArcLength = (count - 1) * minSeatSpacing;

            // ���Բ�ͬ�Ļ��ȽǶȣ��ҵ����Ž�
            float[] candidateArcs = { 60f, 90f, 120f, 150f, 180f };

            foreach (float arcDegrees in candidateArcs)
            {
                float arcRadians = arcDegrees * Mathf.Deg2Rad;

                // ���ݻ�����ʽ��������뾶������ = �뾶 �� ����
                float requiredRadius = requiredArcLength / arcRadians;

                // ����Ƿ��ں���Χ��
                if (requiredRadius >= minRadius && requiredRadius <= maxUsableRadius && requiredRadius <= maxRadius)
                {
                    parameters.radius = requiredRadius;
                    parameters.arcDegrees = arcDegrees;
                    parameters.arcRadians = arcRadians;
                    parameters.startAngle = -arcRadians * 0.5f;
                    parameters.endAngle = arcRadians * 0.5f;

                    // ����ʵ����λ���
                    parameters.actualSeatSpacing = (parameters.radius * arcRadians) / Mathf.Max(1, count - 1);

                    LogDebug($"�ҵ����Ž⣺����{arcDegrees}��, �뾶{requiredRadius:F1}m, ���{parameters.actualSeatSpacing:F1}m, ��С������{minSeatSpacing:F1}m");
                    return parameters;
                }
            }

            // ���û���ҵ�����⣬ʹ�����з���
            LogDebug($"δ�ҵ�����⣬ʹ�����з�����������С��ࣺ{minSeatSpacing:F1}m");
            return CalculateFallbackParameters(count, maxUsableRadius, minSeatSpacing);
        }

        /// <summary>
        /// �������вΰ�
        /// </summary>
        private SeatingParameters CalculateFallbackParameters(int count, float maxUsableRadius, float minSeatSpacing)
        {
            SeatingParameters parameters = new SeatingParameters();

            // ʹ�������ð뾶
            parameters.radius = Mathf.Min(maxUsableRadius, maxRadius);

            // ���������ɵ���󻡶ȣ���֤��С��ࣩ
            float maxArcLength = parameters.radius * Mathf.PI; // ��Բ����
            float maxArcForSpacing = (count - 1) * minSeatSpacing;

            if (maxArcForSpacing <= maxArcLength)
            {
                // ���Ա�֤��С���
                float requiredArcRadians = maxArcForSpacing / parameters.radius;
                parameters.arcDegrees = Mathf.Min(requiredArcRadians * Mathf.Rad2Deg, 180f);
            }
            else
            {
                // �޷���֤��С��࣬ʹ����󻡶�
                parameters.arcDegrees = 180f;
                LogDebug($"���棺�޷���֤{minSeatSpacing:F1}m����С��࣬��ǰ���ԼΪ{(parameters.radius * Mathf.PI) / (count - 1):F1}m");
            }

            parameters.arcRadians = parameters.arcDegrees * Mathf.Deg2Rad;
            parameters.startAngle = -parameters.arcRadians * 0.5f;
            parameters.endAngle = parameters.arcRadians * 0.5f;
            parameters.actualSeatSpacing = (parameters.radius * parameters.arcRadians) / Mathf.Max(1, count - 1);

            return parameters;
        }

        /// <summary>
        /// ��ͳ�Ĺ̶��������㣨����ԭ���߼���
        /// </summary>
        private SeatingParameters CalculateTraditionalParameters(int count)
        {
            SeatingParameters parameters = new SeatingParameters();

            // ����뾶
            parameters.radius = Mathf.Clamp(
                baseRadius + (count * radiusPerPlayer),
                minRadius,
                maxRadius
            );

            // ���㻡�ȷ�Χ��ȷ����λ������
            float requiredArcForSpacing = minAngleBetweenSeats * (count - 1);
            parameters.arcDegrees = Mathf.Min(maxArcDegrees, requiredArcForSpacing);

            // ���ֻ��һ���ˣ��Ƕ�Ϊ0
            if (count == 1)
            {
                parameters.arcDegrees = 0;
            }

            // ת��Ϊ����
            parameters.arcRadians = parameters.arcDegrees * Mathf.Deg2Rad;

            // ������ʼ�ͽ����Ƕȣ���Z��Ϊ��׼��������ֲ���
            parameters.startAngle = -parameters.arcRadians * 0.5f;
            parameters.endAngle = parameters.arcRadians * 0.5f;

            // ����ʵ����λ���
            parameters.actualSeatSpacing = (parameters.radius * parameters.arcRadians) / Mathf.Max(1, count - 1);

            return parameters;
        }

        /// <summary>
        /// ��ָ������������λ
        /// </summary>
        private SeatData CreateSeatAtIndex(int index, int totalCount, SeatingParameters parameters)
        {
            SeatData seatData = new SeatData(index);

            // ���㵱ǰ��λ�ĽǶȣ���Z��Ϊ0���׼��
            float currentAngle;
            if (totalCount == 1)
            {
                currentAngle = 0; // ����ʱ����������
            }
            else
            {
                float t = (float)index / (totalCount - 1);
                currentAngle = Mathf.Lerp(parameters.startAngle, parameters.endAngle, t);
            }

            seatData.angleFromCenter = currentAngle * Mathf.Rad2Deg;

            // ������λλ�ã�������Բ�����֣�ÿ����λ��Բ�����ľ�����ȣ�
            Vector3 offsetFromArcCenter = new Vector3(
                -parameters.radius * Mathf.Cos(currentAngle), // X��ƫ�ƣ�������ΪԲ������ڰ壩
                0, // Y��߶�
                parameters.radius * Mathf.Sin(currentAngle)   // Z��ƫ��
            );

            seatData.seatPosition = arcCenter + offsetFromArcCenter;
            seatData.seatPosition.y = seatHeight;

            // ���㳯������Բ�����ģ�
            Vector3 directionToArcCenter = (arcCenter - seatData.seatPosition).normalized;

            // ֻ����Y����ת����
            if (lockXZRotation)
            {
                // ����Y����ת�Ƕ�
                float yRotation = Mathf.Atan2(directionToArcCenter.x, directionToArcCenter.z) * Mathf.Rad2Deg;

                // Ӧ��Y����תƫ��
                yRotation += yRotationOffset;

                seatData.seatRotation = Quaternion.Euler(0, yRotation, 0);
            }
            else
            {
                seatData.seatRotation = Quaternion.LookRotation(directionToArcCenter) * Quaternion.Euler(0, yRotationOffset, 0);
            }

            // ����ʵ�ʵ����ζ���
            CreatePhysicalSeat(seatData);

            // ������Ϣ
            LogDebug($"��λ{index}: λ��{seatData.seatPosition}, ���ȽǶ�{seatData.angleFromCenter:F1}��, Y��ת{seatData.seatRotation.eulerAngles.y:F1}��, ����Բ������{arcCenter}");

            return seatData;
        }

        /// <summary>
        /// ����������λ����
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

                // ��ѡ�������λ��ʶ���
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
                LogDebug($"�޷�������λ{seatData.seatIndex}������Ԥ����δ����");
            }
        }

        #endregion

        #region ��λ����

        /// <summary>
        /// ����������λ
        /// </summary>
        public void ClearAllSeats()
        {
            LogDebug($"����{generatedSeats.Count}����λ");

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
        /// ������λ�������༭���ã�
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
        /// ��ȡָ����������λ����
        /// </summary>
        public SeatData GetSeatData(int index)
        {
            if (index >= 0 && index < generatedSeats.Count)
                return generatedSeats[index];
            return null;
        }

        #endregion

        #region ���Կ��ӻ�

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // ���ƽ��ұ߽�
            if (autoAdjustForClassroomSize)
            {
                Gizmos.color = Color.gray;
                Vector3 classroomSize = new Vector3(classroomDepth, 0.1f, classroomWidth);
                Gizmos.DrawWireCube(Vector3.zero, classroomSize);

                // ���ư�ȫ�߾�
                Gizmos.color = Color.yellow;
                Vector3 safeAreaSize = new Vector3(classroomDepth - wallMargin * 2, 0.1f, classroomWidth - wallMargin * 2);
                Gizmos.DrawWireCube(Vector3.zero, safeAreaSize);
            }

            // ����Բ������
            if (showArcCenterGizmo)
            {
                Gizmos.color = arcCenterColor;
                Gizmos.DrawWireSphere(arcCenter, 0.3f);
                Gizmos.DrawWireCube(arcCenter, Vector3.one * 0.1f);
            }

            // ������λ�ֲ�����
            Gizmos.color = seatCenterColor;
            Gizmos.DrawWireSphere(seatDistributionCenter, 0.2f);

            // ����������
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(arcCenter, seatDistributionCenter);

            // ������λλ�úͳ���
            foreach (SeatData seat in generatedSeats)
            {
                // ������λλ��
                Gizmos.color = seatPositionColor;
                Gizmos.DrawWireCube(seat.seatPosition, Vector3.one * 0.4f);

                // ���Ƴ���
                Gizmos.color = directionRayColor;
                Vector3 forward = seat.seatRotation * Vector3.forward;
                Gizmos.DrawRay(seat.seatPosition, forward * 1.5f);

                // ���Ƶ�Բ�����ĵ�����
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(seat.seatPosition, arcCenter);

                // ������λ��ࣨ������λ֮������ߣ�
                if (seat.seatIndex < generatedSeats.Count - 1)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(seat.seatPosition, generatedSeats[seat.seatIndex + 1].seatPosition);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // ѡ��ʱ��ʾ����ϸ����Ϣ
            Gizmos.color = Color.white;

            // ���ƻ��ȷ�Χ
            if (generatedSeats.Count > 1)
            {
                SeatingParameters parameters = CalculateSeatingParameters(playerCount);

                // ���ƻ��ȷ�Χ��
                Vector3 startDir = new Vector3(0, 0, parameters.radius * Mathf.Sin(parameters.startAngle));
                Vector3 endDir = new Vector3(0, 0, parameters.radius * Mathf.Sin(parameters.endAngle));

                Gizmos.DrawLine(seatDistributionCenter, seatDistributionCenter + startDir);
                Gizmos.DrawLine(seatDistributionCenter, seatDistributionCenter + endDir);
            }
        }

        #endregion

        #region �༭���ӿ�

        [ContextMenu("������λ")]
        private void TestGenerateSeats()
        {
            GenerateSeats(playerCount);
        }

        [ContextMenu("������λ")]
        private void TestClearSeats()
        {
            ClearAllSeats();
        }

        [ContextMenu("��������")]
        private void TestRegenerateSeats()
        {
            ClearAllSeats();
            GenerateSeats(playerCount);
        }

        #endregion

        #region ��������

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CircularSeatingSystem] {message}");
            }
        }

        #endregion

        /// <summary>
        /// ��λ�������ݽṹ
        /// </summary>
        private struct SeatingParameters
        {
            public float radius;
            public float arcDegrees;
            public float arcRadians;
            public float startAngle;
            public float endAngle;
            public float actualSeatSpacing; // ʵ����λ���
        }
    }
}