using UnityEngine;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// ���ͷ�������� - ר�Ŵ���ͷ��������ת
    /// ��������������ƣ�����ͷ���Ŀ��ӻ����ֺ�����ͬ��
    /// ������Ҳ��ƶ�λ�ã�ֻ����ͷ����ת�߼�
    /// </summary>
    public class PlayerHeadController : MonoBehaviour
    {
        [Header("ͷ����������")]
        [SerializeField] private bool enableHeadRotation = true; // ����ͷ����ת
        [SerializeField] private bool enableHeadHorizontalRotation = true; // ����ͷ��ˮƽ��ת
        [SerializeField] private bool enableHeadVerticalRotation = false; // ����ͷ����ֱ��ת������ģʽ��

        [Header("ͷ����ת����")]
        [SerializeField] private float headHorizontalLimit = 90f; // ͷ��ˮƽ��ת���ƣ����Ҹ�90�ȣ�
        [SerializeField] private float headVerticalUpLimit = 30f; // ͷ�����ϽǶ�����
        [SerializeField] private float headVerticalDownLimit = 15f; // ͷ�����½Ƕ�����

        [Header("ͷ��ƽ������")]
        [SerializeField] private float headSmoothSpeed = 8f; // ͷ����תƽ���ٶ�
        [SerializeField] private bool useSmoothing = true; // �Ƿ�ʹ��ƽ��

        [Header("��������")]
        [SerializeField] private string headBoneName = "head"; // ͷ����������
        [SerializeField] private bool autoFindHeadBone = true; // �Զ�����ͷ������

        [Header("����ͬ��")]
        [SerializeField] private bool syncHeadRotation = true; // ͬ��ͷ����ת
        [SerializeField] private float syncThreshold = 3f; // ͬ����ֵ���ȣ�

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = false;

        // ͷ����ת����
        private float targetHeadHorizontalAngle = 0f; // Ŀ��ͷ��ˮƽ�Ƕ�
        private float targetHeadVerticalAngle = 0f; // Ŀ��ͷ����ֱ�Ƕ�
        private float currentHeadHorizontalAngle = 0f; // ��ǰͷ��ˮƽ�Ƕ�
        private float currentHeadVerticalAngle = 0f; // ��ǰͷ����ֱ�Ƕ�

        // ״̬����
        private bool isInitialized = false;
        private bool isLocalPlayer = false;
        private bool isControlEnabled = false;

        // �������
        private Animator characterAnimator;
        private Transform headBone;
        private Quaternion initialHeadRotation; // ͷ����ʼ��ת
        private Quaternion baseHeadRotation; // ͷ��������ת����λ����

        // ����ͬ�����
        private float lastSyncTime = 0f;
        private float lastSentHorizontalAngle = 0f;
        private float lastSentVerticalAngle = 0f;

        // ��������
        public bool IsInitialized => isInitialized;
        public bool IsLocalPlayer => isLocalPlayer;
        public float CurrentHeadHorizontalAngle => currentHeadHorizontalAngle;
        public float CurrentHeadVerticalAngle => currentHeadVerticalAngle;
        public Transform HeadBone => headBone;

        #region Unity��������

        private void Awake()
        {
            // ���ҽ�ɫ������
            characterAnimator = GetComponent<Animator>();
            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }

            LogDebug("PlayerHeadController Awake���");
        }

        private void Start()
        {
            // ��鱾�����״̬
            CheckIfLocalPlayer();

            if (isLocalPlayer) // �༭����Ҳ��ʼ�������ڵ���
            {
                InitializeHeadController();
            }
        }

        private void Update()
        {
            if (!isInitialized) return;

            // ����ͷ����ת�����۱��ػ���Զ����Ҷ���Ҫ��
            UpdateHeadRotation();

            // ������Ҷ��⴦������ͬ�����
            if (isLocalPlayer && syncHeadRotation)
            {
                CheckAndSendHeadRotation();
            }
        }

        #endregion

        #region ��ʼ��

        /// <summary>
        /// ����Ƿ�Ϊ�������
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
                // ���÷�����ͨ����ɫ�����ж�
                isLocalPlayer = name.Contains($"Player_{NetworkManager.Instance?.ClientId:D2}");
            }

            LogDebug($"��鱾�����״̬: {isLocalPlayer}");
        }

        /// <summary>
        /// ��ʼ��ͷ��������
        /// </summary>
        private void InitializeHeadController()
        {
            LogDebug("��ʼ��ʼ��ͷ��������");

            // ����ͷ������
            if (autoFindHeadBone)
            {
                FindHeadBone();
            }

            // ��¼��ʼ״̬
            SetupInitialHeadTransform();

            isInitialized = true;
            isControlEnabled = true;

            LogDebug($"ͷ����������ʼ����ɣ�ͷ������: {headBone.name}");
        }

        /// <summary>
        /// ����ͷ������
        /// </summary>
        private void FindHeadBone()
        {
            if (characterAnimator == null) return;
            headBone = FindBoneRecursive(characterAnimator.transform, headBoneName);
        }

        /// <summary>
        /// �ݹ���ҹ���
        /// </summary>
        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name.ToLower().Contains(boneName.ToLower()))
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindBoneRecursive(parent.GetChild(i), boneName);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// ���ó�ʼͷ���任
        /// </summary>
        private void SetupInitialHeadTransform()
        {
            if (headBone == null) return;

            // ��¼ͷ����ʼ��ת
            initialHeadRotation = headBone.rotation;
            baseHeadRotation = initialHeadRotation;

            // ���ýǶ�
            targetHeadHorizontalAngle = 0f;
            targetHeadVerticalAngle = 0f;
            currentHeadHorizontalAngle = 0f;
            currentHeadVerticalAngle = 0f;

            LogDebug($"��¼ͷ����ʼ��ת: {initialHeadRotation.eulerAngles}");
        }

        #endregion

        #region ͷ����ת����

        /// <summary>
        /// �������������������ת����
        /// </summary>
        /// <param name="horizontalAngle">�ܵ�ˮƽ�Ƕ�</param>
        /// <param name="verticalAngle">�ܵĴ�ֱ�Ƕ�</param>
        /// <returns>����ͷ��ʵ�ʳе��ĽǶ�</returns>
        public Vector2 ReceiveRotationInput(float horizontalAngle, float verticalAngle)
        {
            if (!isLocalPlayer || !isInitialized || !enableHeadRotation)
                return Vector2.zero;

            // ����ͷ��Ӧ�óе�����ת
            float headHorizontal = 0f;
            float headVertical = 0f;

            if (enableHeadHorizontalRotation)
            {
                headHorizontal = Mathf.Clamp(horizontalAngle, -headHorizontalLimit, headHorizontalLimit);
            }

            if (enableHeadVerticalRotation)
            {
                headVertical = Mathf.Clamp(verticalAngle, -headVerticalDownLimit, headVerticalUpLimit);
            }

            // ����Ŀ��Ƕ�
            targetHeadHorizontalAngle = headHorizontal;
            targetHeadVerticalAngle = headVertical;

            return new Vector2(headHorizontal, headVertical);
        }

        /// <summary>
        /// ����ͷ����ת
        /// </summary>
        private void UpdateHeadRotation()
        {
            if (!isInitialized || headBone == null) return;

            // ƽ����ֵ��Ŀ��Ƕ�
            if (useSmoothing)
            {
                float deltaTime = Time.deltaTime;
                float speed = headSmoothSpeed * deltaTime;

                currentHeadHorizontalAngle = Mathf.LerpAngle(currentHeadHorizontalAngle, targetHeadHorizontalAngle, speed);
                currentHeadVerticalAngle = Mathf.LerpAngle(currentHeadVerticalAngle, targetHeadVerticalAngle, speed);
            }
            else
            {
                currentHeadHorizontalAngle = targetHeadHorizontalAngle;
                currentHeadVerticalAngle = targetHeadVerticalAngle;
            }

            // Ӧ����ת��ͷ������
            ApplyHeadRotation();
        }

        /// <summary>
        /// Ӧ��ͷ����ת
        /// </summary>
        private void ApplyHeadRotation()
        {
            if (headBone == null) return;

            // ˮƽ��ת����Y�ᣩ
            Quaternion horizontalRotation = enableHeadHorizontalRotation ?
                Quaternion.AngleAxis(currentHeadHorizontalAngle, Vector3.up) : Quaternion.identity;

            // ��ֱ��ת����X�ᣩ
            Quaternion verticalRotation = enableHeadVerticalRotation ?
                Quaternion.AngleAxis(currentHeadVerticalAngle, Vector3.right) : Quaternion.identity;

            // �����ת��������ת * ˮƽ��ת * ��ֱ��ת
            Quaternion finalHeadRotation = baseHeadRotation * horizontalRotation * verticalRotation;

            // Ӧ�õ�ͷ������
            headBone.rotation = finalHeadRotation;
        }

        /// <summary>
        /// ���û���ͷ����ת����λ����
        /// </summary>
        public void SetBaseHeadRotation(Quaternion baseRotation)
        {
            baseHeadRotation = initialHeadRotation * baseRotation;
            LogDebug($"����ͷ��������ת: {baseRotation.eulerAngles}");
        }

        #endregion

        #region ����ͬ�����

        /// <summary>
        /// ��鲢����ͷ����ת����
        /// </summary>
        private void CheckAndSendHeadRotation()
        {
            // ��ȫ��飺���粻����ʱֹͣ����
            if (!isLocalPlayer || !syncHeadRotation ||
                NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected ||
                Time.time - lastSyncTime < 1f / 15f) // 15Hz����Ƶ��
            {
                return;
            }

            // ����Ƿ��������仯
            float horizontalDelta = Mathf.Abs(currentHeadHorizontalAngle - lastSentHorizontalAngle);
            float verticalDelta = Mathf.Abs(currentHeadVerticalAngle - lastSentVerticalAngle);

            if (horizontalDelta > syncThreshold || verticalDelta > syncThreshold)
            {
                // ����ͷ����ת����
                SendHeadRotationData();

                lastSentHorizontalAngle = currentHeadHorizontalAngle;
                lastSentVerticalAngle = currentHeadVerticalAngle;
                lastSyncTime = Time.time;

                LogDebug($"����ͷ����ת: H={currentHeadHorizontalAngle:F1}��, V={currentHeadVerticalAngle:F1}��");
            }
        }

        /// <summary>
        /// ����ͷ����ת����
        /// </summary>
        private void SendHeadRotationData()
        {
            // ��ȡPlayerNetworkSync�������������
            var networkSync = GetComponent<PlayerNetworkSync>();
            if (networkSync != null && NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                // ֪ͨ����ͬ���������ͷ������
                networkSync.SendHeadRotation(currentHeadHorizontalAngle, currentHeadVerticalAngle);
            }
        }

        /// <summary>
        /// ��������ͷ����ת���ݣ�����Զ����ң�
        /// </summary>
        public void ReceiveNetworkHeadRotation(float horizontalAngle, float verticalAngle)
        {
            if (isLocalPlayer) return;

            targetHeadHorizontalAngle = horizontalAngle;
            targetHeadVerticalAngle = verticalAngle;

            LogDebug($"��������ͷ����ת: H={horizontalAngle:F1}��, V={verticalAngle:F1}��");
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ����/����ͷ������
        /// </summary>
        public void SetHeadControlEnabled(bool enabled)
        {
            isControlEnabled = enabled;
            LogDebug($"ͷ������״̬: {enabled}");
        }

        #endregion

        #region ���Է���

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[PlayerHeadController] {message}");
            }
        }

        #endregion

    }
}