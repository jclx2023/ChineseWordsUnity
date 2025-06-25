using UnityEngine;
using System.Collections.Generic;

namespace Classroom.Scene
{
    /// <summary>
    /// Բ����λϵͳ - ������㲢��������ڰ��Բ��״���β���
    /// ���ڼ�����ϵͳ��λ�÷����㷨
    /// </summary>
    public class CircularSeatingSystem : MonoBehaviour
    {
        [Header("Բ����������")]
        [SerializeField] private float baseRadius = 3f; // �����뾶������8x10�׽��ң�
        [SerializeField] private float radiusPerPlayer = 0.15f; // ÿ��������ӵİ뾶
        [SerializeField] private float maxRadius = 4.5f; // ���뾶���ƣ����������ұ߽磩
        [SerializeField] private float minRadius = 2.5f; // ��С�뾶����

        [Header("���ȷ�Χ����")]
        [SerializeField] private float smallGroupArc = 120f; // 2-4�˻���
        [SerializeField] private float mediumGroupArc = 150f; // 5-8�˻���
        [SerializeField] private float largeGroupArc = 180f; // 9+�˻���

        [Header("��λƫ������")]
        [SerializeField] private float seatHeight = 0f; // ��λ�߶�ƫ�ƣ�0��ʾ���أ�
        [SerializeField] private Vector3 seatRotationOffset = new Vector3(0, 180, 0); // ��λ��תƫ�ƣ�����������ڰ壩
        [SerializeField] private bool debugChairDirection = true; // �������ӳ���
        [SerializeField] private bool enableGroundDetection = true; // ���õ�����
        [SerializeField] private LayerMask groundLayerMask = 1; // ����㼶����

        [Header("�������")]
        [SerializeField] private float minAngleBetweenSeats = 12f; // ��С�Ƕȼ�ࣨ�ȣ�
        [SerializeField] private float distanceFromBlackboard = 1.5f; // ����ڰ����С���루������Ҵ�С��

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color gizmoColor = Color.cyan;

        // ��λ���ݽṹ
        [System.Serializable]
        public class SeatData
        {
            public Vector3 seatPosition; // ����һ���λ��
            public Quaternion seatRotation; // ����һ�����ת
            public int seatIndex;
            public GameObject seatInstance; // ����һ��ʵ��

            public SeatData(int index)
            {
                seatIndex = index;
            }
        }

        // ˽�б���
        private SceneLayoutManager sceneManager;
        private List<SeatData> generatedSeats = new List<SeatData>();
        private Vector3 arcCenter;
        private bool isInitialized = false;

        // ��������
        public List<SeatData> GeneratedSeats => generatedSeats;
        public bool IsInitialized => isInitialized;
        public int SeatCount => generatedSeats.Count;

        #region ��ʼ��

        /// <summary>
        /// ��ʼ����λϵͳ
        /// </summary>
        /// <param name="layoutManager">�������ֹ���������</param>
        public void Initialize(SceneLayoutManager layoutManager)
        {
            sceneManager = layoutManager;
            CalculateArcCenter();
            isInitialized = true;
            LogDebug("CircularSeatingSystem ��ʼ�����");
        }

        /// <summary>
        /// ����Բ�����ĵ�
        /// </summary>
        private void CalculateArcCenter()
        {
            if (sceneManager != null)
            {
                Vector3 blackboardPos = sceneManager.BlackboardPosition;
                Vector3 classroomCenter = sceneManager.ClassroomCenter;

                // ���8x10�׽��ҵ��Ż�����
                // �ڰ���(5, 1.5, 0)������������(0, 0, 0)
                // Բ������Ӧ���ںڰ�ǰ���ʵ�����

                Vector3 directionFromBoard;
                if (Vector3.Distance(blackboardPos, classroomCenter) < 0.1f)
                {
                    // ����ڰ�ͽ��������غϣ�Ĭ�Ϻڰ�����-Z����
                    directionFromBoard = Vector3.back;
                }
                else
                {
                    directionFromBoard = (classroomCenter - blackboardPos).normalized;
                }

                arcCenter = blackboardPos + directionFromBoard * distanceFromBlackboard;

                // ȷ��Բ�������ڽ��ҷ�Χ�ڣ�8x10�׽��ұ߽��飩
                arcCenter.x = Mathf.Clamp(arcCenter.x, -3.5f, 3.5f); // 8�׿�ȵ�һ���ȥ��ȫ�߾�
                arcCenter.z = Mathf.Clamp(arcCenter.z, -4.5f, 4.5f); // 10����ȵ�һ���ȥ��ȫ�߾�

                LogDebug($"Բ�����ļ�����ɣ�{arcCenter}���ڰ�λ�ã�{blackboardPos}");
            }
        }

        #endregion

        #region ��λ����

        /// <summary>
        /// ����ָ����������λ
        /// </summary>
        /// <param name="playerCount">�������</param>
        public void GenerateSeats(int playerCount)
        {
            if (!isInitialized)
            {
                LogDebug("��λϵͳδ��ʼ�����޷�������λ");
                return;
            }

            if (playerCount <= 0)
            {
                LogDebug("���������Ч���޷�������λ");
                return;
            }

            LogDebug($"��ʼ����{playerCount}����λ...");

            // ����������λ
            ClearAllSeats();

            // ���㲼�ֲ���
            SeatingParameters parameters = CalculateSeatingParameters(playerCount);

            // ������λ
            for (int i = 0; i < playerCount; i++)
            {
                SeatData seatData = CreateSeatAtIndex(i, playerCount, parameters);
                generatedSeats.Add(seatData);
            }

            LogDebug($"��λ������ɣ�������{generatedSeats.Count}����λ");
        }

        /// <summary>
        /// ������λ����
        /// </summary>
        private SeatingParameters CalculateSeatingParameters(int playerCount)
        {
            SeatingParameters parameters = new SeatingParameters();

            // ����뾶�����8x10�׽����Ż���
            parameters.radius = Mathf.Clamp(
                baseRadius + (playerCount * radiusPerPlayer),
                minRadius,
                maxRadius
            );

            // ���㻡�ȷ�Χ�������صĽǶ����ã�
            if (playerCount <= 3)
                parameters.arcDegrees = 90f;  // С�������
            else if (playerCount <= 6)
                parameters.arcDegrees = 120f; // ��������
            else if (playerCount <= 10)
                parameters.arcDegrees = 150f; // ������չ
            else
                parameters.arcDegrees = 180f; // �����

            // ת��Ϊ����
            parameters.arcRadians = parameters.arcDegrees * Mathf.Deg2Rad;

            // ������ʼ�ͽ����Ƕ�
            parameters.startAngle = -parameters.arcRadians * 0.5f;
            parameters.endAngle = parameters.arcRadians * 0.5f;

            // ��֤��С�Ƕȼ��
            float actualAngleStep = parameters.arcDegrees / Mathf.Max(1, playerCount - 1);
            if (actualAngleStep < minAngleBetweenSeats && playerCount > 1)
            {
                LogDebug($"�Ƕȼ���С({actualAngleStep:F1}��)���������ȷ�Χ");
                parameters.arcDegrees = minAngleBetweenSeats * (playerCount - 1);
                parameters.arcDegrees = Mathf.Min(parameters.arcDegrees, 180f); // ������180��
                parameters.arcRadians = parameters.arcDegrees * Mathf.Deg2Rad;
                parameters.startAngle = -parameters.arcRadians * 0.5f;
                parameters.endAngle = parameters.arcRadians * 0.5f;
            }

            LogDebug($"��λ�������뾶={parameters.radius:F1}m, ����={parameters.arcDegrees:F1}��, �����={playerCount}");
            return parameters;
        }

        /// <summary>
        /// ��ָ������������λ
        /// </summary>
        private SeatData CreateSeatAtIndex(int index, int totalCount, SeatingParameters parameters)
        {
            SeatData seatData = new SeatData(index);

            // ���㵱ǰ��λ�ĽǶ�
            float currentAngle;
            if (totalCount == 1)
            {
                currentAngle = 0; // ����ʱ����ڰ�
            }
            else
            {
                float t = (float)index / (totalCount - 1);
                currentAngle = Mathf.Lerp(parameters.startAngle, parameters.endAngle, t);
            }

            // ��������һ��Ļ���λ��
            Vector3 basePosition = CalculatePositionOnArc(currentAngle, parameters.radius);

            // ������͸߶ȵ���
            seatData.seatPosition = CalculateGroundPosition(basePosition);

            // ���㳯����������ڰ壩
            Vector3 directionToBlackboard = (sceneManager.BlackboardPosition - seatData.seatPosition).normalized;
            seatData.seatRotation = Quaternion.LookRotation(directionToBlackboard);

            // Ӧ����תƫ��
            if (seatRotationOffset != Vector3.zero)
            {
                seatData.seatRotation *= Quaternion.Euler(seatRotationOffset);
            }

            // ������Ϣ
            if (debugChairDirection)
            {
                Vector3 chairForward = seatData.seatRotation * Vector3.forward;
                LogDebug($"��λ{index}: λ��{seatData.seatPosition}, ����ڰ巽��{directionToBlackboard}, ����ǰ��{chairForward}");
            }

            // ����ʵ�ʵ����ζ���
            CreatePhysicalSeat(seatData);

            return seatData;
        }

        /// <summary>
        /// ����Բ���ϵ�λ��
        /// </summary>
        private Vector3 CalculatePositionOnArc(float angle, float radius)
        {
            float x = arcCenter.x + radius * Mathf.Sin(angle);
            float z = arcCenter.z + radius * Mathf.Cos(angle);
            return new Vector3(x, arcCenter.y, z);
        }

        /// <summary>
        /// ��������λ��
        /// </summary>
        private Vector3 CalculateGroundPosition(Vector3 basePosition)
        {
            Vector3 finalPosition = basePosition;

            if (enableGroundDetection)
            {
                // ���Ϸ��������߼�����
                Vector3 rayStart = basePosition + Vector3.up * 5f; // ��5�׸߶ȿ�ʼ���

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f, groundLayerMask))
                {
                    // �ҵ����棬����λ�����ڵ�����
                    finalPosition.y = hit.point.y + seatHeight;
                    LogDebug($"������ɹ���{hit.point.y:F2}, ��λ�߶ȣ�{finalPosition.y:F2}");
                }
                else
                {
                    // û�м�⵽���棬ʹ��Ĭ�ϸ߶�
                    finalPosition.y = seatHeight;
                    LogDebug($"δ��⵽���棬ʹ��Ĭ�ϸ߶ȣ�{finalPosition.y:F2}");
                }
            }
            else
            {
                // ��ʹ�õ����⣬ֱ�����ø߶�
                finalPosition.y = seatHeight;
            }

            return finalPosition;
        }

        /// <summary>
        /// ����������λ����
        /// </summary>
        private void CreatePhysicalSeat(SeatData seatData)
        {
            // ��������һ�����
            GameObject deskChairPrefab = GetDeskChairPrefab();
            if (sceneManager != null && deskChairPrefab != null)
            {
                seatData.seatInstance = Instantiate(deskChairPrefab, seatData.seatPosition, seatData.seatRotation);
                seatData.seatInstance.name = $"DeskChair_{seatData.seatIndex:D2}";

                // ��ѡ��Ϊ��λ��ӱ�ʶ��������ں������Һ͹���
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
                    DestroyImmediate(seat.seatInstance);
            }

            generatedSeats.Clear();
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

        /// <summary>
        /// ��ȡ����ָ��λ���������λ
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

        #region ��������

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

        #region ���Կ��ӻ�

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !isInitialized) return;

            Gizmos.color = gizmoColor;

            // ����Բ������
            Gizmos.DrawWireSphere(arcCenter, 0.3f);

            // ������λλ��
            foreach (SeatData seat in generatedSeats)
            {
                // ��������λ��
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(seat.seatPosition, Vector3.one * 0.8f);

                // ���Ƴ���
                Gizmos.color = Color.green;
                Vector3 forward = seat.seatRotation * Vector3.forward;
                Gizmos.DrawRay(seat.seatPosition, forward * 1.5f);

                // ������λ��ţ���ѡ��
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(seat.seatPosition + Vector3.up * 1.5f, 0.1f);
            }
        }

        [ContextMenu("��������6����λ")]
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
        /// ��λ�������ݽṹ
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