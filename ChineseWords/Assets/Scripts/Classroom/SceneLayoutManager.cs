using UnityEngine;
using System.Collections.Generic;

namespace Classroom.Scene
{
    /// <summary>
    /// �������ֹ����� - ������ҳ��������岼�ֳ�ʼ��
    /// �׶�һ��������������������ԣ�
    /// </summary>
    public class SceneLayoutManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private Transform blackboardTransform;
        [SerializeField] private Vector3 blackboardPosition = new Vector3(0, 0, 10);
        [SerializeField] private Vector3 blackboardRotationOffset = new Vector3(0, 90, 0); // �ڰ������תƫ��
        [SerializeField] private Vector3 classroomCenter = Vector3.zero;

        [Header("Ԥ��������")]
        [SerializeField] private GameObject deskChairPrefab; // ����һ��Ԥ����

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool autoInitializeOnStart = true;

        // �������
        private CircularSeatingSystem seatingSystem;

        // ����ģʽ
        public static SceneLayoutManager Instance { get; private set; }

        // ��������
        public Transform BlackboardTransform => blackboardTransform;
        public Vector3 BlackboardPosition => blackboardPosition;
        public Vector3 ClassroomCenter => classroomCenter;
        public bool IsInitialized { get; private set; }

        #region Unity��������

        private void Awake()
        {
            // ������ʼ��
            if (Instance == null)
            {
                Instance = this;
                LogDebug("SceneLayoutManager �����Ѵ���");
            }
            else
            {
                LogDebug("�����ظ���SceneLayoutManager�����ٵ�ǰ����");
                Destroy(gameObject);
                return;
            }

            // ��ʼ�����
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
                LogDebug("SceneLayoutManager ����������");
            }
        }

        #endregion

        #region ��ʼ������

        /// <summary>
        /// ��ʼ�����
        /// </summary>
        private void InitializeComponents()
        {
            // ��ȡ�򴴽�Բ����λϵͳ
            seatingSystem = GetComponent<CircularSeatingSystem>();
            if (seatingSystem == null)
            {
                seatingSystem = gameObject.AddComponent<CircularSeatingSystem>();
                LogDebug("�Զ���� CircularSeatingSystem ���");
            }

            LogDebug("SceneLayoutManager �����ʼ�����");
        }

        /// <summary>
        /// ��ʼ����������
        /// </summary>
        public void InitializeScene()
        {
            if (IsInitialized)
            {
                LogDebug("�����Ѿ���ʼ���������ظ���ʼ��");
                return;
            }

            LogDebug("��ʼ��ʼ�����ҳ���...");

            // 1. ���úڰ�λ�ã������Ѵ����ڳ����У�
            SetupBlackboard();

            // 2. ��ʼ����λϵͳ
            InitializeSeatingSystem();

            IsInitialized = true;
            LogDebug("���ҳ�����ʼ�����");
        }

        #endregion

        #region �������÷���

        /// <summary>
        /// ���úڰ�λ�ú����ã������Ѵ����ڳ����У�
        /// </summary>
        private void SetupBlackboard()
        {
            // ���û���ֶ�ָ���ڰ�Transform�������Զ�����
            if (blackboardTransform == null)
            {
                blackboardTransform = FindBlackboardInScene();
            }

            // �������û�ҵ�������һ������ڰ�λ��
            if (blackboardTransform == null)
            {
                GameObject virtualBlackboard = new GameObject("VirtualBlackboard");
                virtualBlackboard.transform.position = blackboardPosition;
                blackboardTransform = virtualBlackboard.transform;
                LogDebug("��������ڰ�λ��");
            }
            else
            {
                // ���ºڰ�λ��
                blackboardPosition = blackboardTransform.position;

                // Ӧ�úڰ���תƫ��
                if (blackboardRotationOffset != Vector3.zero)
                {
                    blackboardTransform.rotation *= Quaternion.Euler(blackboardRotationOffset);
                    LogDebug($"Ӧ�úڰ���תƫ��: {blackboardRotationOffset}");
                }

                LogDebug($"�ڰ�λ�������ã�{blackboardPosition}");
            }
        }

        /// <summary>
        /// �ڳ����в��Һڰ�
        /// </summary>
        private Transform FindBlackboardInScene()
        {
            // ����ͨ�����Ʋ���
            string[] possibleNames = { "Blackboard", "BlackBoard", "�ڰ�", "Board" };

            foreach (string name in possibleNames)
            {
                GameObject found = GameObject.Find(name);
                if (found != null)
                {
                    LogDebug($"�ҵ��ڰ����{found.name}");
                    return found.transform;
                }
            }

            // ����ͨ����ǩ����
            GameObject blackboardByTag = GameObject.FindWithTag("Blackboard");
            if (blackboardByTag != null)
            {
                LogDebug($"ͨ����ǩ�ҵ��ڰ壺{blackboardByTag.name}");
                return blackboardByTag.transform;
            }

            LogDebug("δ�ڳ������ҵ��ڰ����");
            return null;
        }

        /// <summary>
        /// ��ʼ����λϵͳ
        /// </summary>
        private void InitializeSeatingSystem()
        {
            if (seatingSystem != null)
            {
                seatingSystem.Initialize(this);
                LogDebug("��λϵͳ��ʼ�����");
            }
            else
            {
                LogDebug("��λϵͳ���δ�ҵ����޷���ʼ��");
            }
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ����ָ�����������Σ������ã�
        /// </summary>
        /// <param name="playerCount">�������</param>
        public void GenerateSeatsForTesting(int playerCount)
        {
            if (!IsInitialized)
            {
                InitializeScene();
            }

            if (seatingSystem != null)
            {
                seatingSystem.GenerateSeats(playerCount);
                LogDebug($"Ϊ{playerCount}�����������λ������ģʽ��");
            }
        }

        /// <summary>
        /// �����������ɵ���λ
        /// </summary>
        public void ClearAllSeats()
        {
            if (seatingSystem != null)
            {
                seatingSystem.ClearAllSeats();
                LogDebug("������������λ");
            }
        }

        /// <summary>
        /// ���³�ʼ������
        /// </summary>
        public void ReinitializeScene()
        {
            LogDebug("���³�ʼ������");

            IsInitialized = false;
            ClearAllSeats();

            InitializeScene();
        }

        #endregion

        #region ���Է���

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

            // ���ƽ�������
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(classroomCenter, 0.5f);

            // ���ƺڰ�λ��
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(blackboardPosition, new Vector3(3, 2, 0.2f));

            // ���ƺڰ嵽�������ĵ�����
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(classroomCenter, blackboardPosition);
        }

        [ContextMenu("���Գ�ʼ������")]
        private void TestInitializeScene()
        {
            InitializeScene();
        }

        [ContextMenu("��������4����λ")]
        private void TestGenerate4Seats()
        {
            GenerateSeatsForTesting(4);
        }

        [ContextMenu("��������8����λ")]
        private void TestGenerate8Seats()
        {
            GenerateSeatsForTesting(8);
        }

        [ContextMenu("��������12����λ")]
        private void TestGenerate12Seats()
        {
            GenerateSeatsForTesting(12);
        }

        [ContextMenu("����������λ")]
        private void TestClearSeats()
        {
            ClearAllSeats();
        }

        #endregion
    }
}