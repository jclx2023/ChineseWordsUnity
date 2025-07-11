using UnityEngine;
using System.Collections;
using Classroom;
using Core.Network;

namespace Classroom.Teacher
{
    /// <summary>
    /// ��ʦ������ - ����ѧ�������ʱ���ӷ۱ʶ���
    /// ���ܣ�ת��ѧ�� �� ���Ŷ��� �� ���ɷ۱� �� �����߷��� �� �������� �� �ָ�����
    /// </summary>
    public class TeacherManager : MonoBehaviour
    {
        [Header("�������")]
        [SerializeField] private Animator teacherAnimator;
        [SerializeField] private Transform throwPoint; // �ֲ�λ��
        [SerializeField] private GameObject chalkPrefab; // �۱�Ԥ����

        [Header("��������")]
        [SerializeField] private float turnSpeed = 2f; // ת���ٶ�
        [SerializeField] private float throwForce = 15f; // ��������
        [SerializeField] private float throwAngle = 25f; // ����Ƕ�
        [SerializeField] private float recoverDelay = 2f; // �ָ������ӳ�

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ״̬����
        private enum TeacherState { Idle, Targeting, Throwing, Recovering }
        private TeacherState currentState = TeacherState.Idle;

        // ����
        private Quaternion initialRotation;
        private ClassroomManager classroomManager;
        private System.Action onActionCompleted; // ������ɻص�
        private Vector3 currentTargetPosition; // ��ǰĿ��λ�û���

        #region Unity��������

        private void Awake()
        {
            // ��¼��ʼ����
            initialRotation = transform.rotation;

            // ��ȡ���ҹ�����
            classroomManager = FindObjectOfType<ClassroomManager>();

            // �Զ��������
            AutoFindComponents();
        }

        private void Start()
        {
            // �����������¼�
            SubscribeToEvents();
            LogDebug("TeacherManager��ʼ�����");
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// ���������¼�
        /// </summary>
        private void SubscribeToEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerAnswerResult += OnPlayerAnswerResult;
            }
        }

        /// <summary>
        /// ȡ���¼�����
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerAnswerResult -= OnPlayerAnswerResult;
            }
        }

        /// <summary>
        /// ������Ҵ�����
        /// </summary>
        private void OnPlayerAnswerResult(ushort playerId, bool isCorrect, string answer)
        {
            // ֻ�������𰸣���ȷ����ǰ����
            if (isCorrect || currentState != TeacherState.Idle)
                return;

            LogDebug($"ѧ��{playerId}����ˣ�׼���ӷ۱�");
            StartChalkThrowSequence(playerId);
        }

        #endregion

        #region ��Ҫ����

        /// <summary>
        /// ��ʼ�ӷ۱�����
        /// </summary>
        public void StartChalkThrowSequence(ushort targetPlayerId, System.Action onCompleted = null)
        {
            if (currentState != TeacherState.Idle)
            {
                LogDebug("��ʦ��æ���޷�ִ���µ��ӷ۱ʶ���");
                return;
            }

            onActionCompleted = onCompleted;
            StartCoroutine(ExecuteChalkThrowSequence(targetPlayerId));
        }

        /// <summary>
        /// ִ���������ӷ۱�����
        /// </summary>
        private IEnumerator ExecuteChalkThrowSequence(ushort targetPlayerId)
        {
            LogDebug($"��ʼ��ѧ��{targetPlayerId}ִ���ӷ۱�����");

            // 1. ��ȡĿ��λ��
            Vector3 targetPosition = GetStudentPosition(targetPlayerId);
            if (targetPosition == Vector3.zero)
            {
                LogDebug($"δ�ҵ�ѧ��{targetPlayerId}��λ�ã�ȡ������");
                yield break;
            }

            // ����Ŀ��λ�ù������¼�ʹ��
            currentTargetPosition = targetPosition;

            // 2. ת��Ŀ��
            currentState = TeacherState.Targeting;
            yield return StartCoroutine(TurnToTarget(targetPosition));

            // 3. �����ӷ۱ʶ������۱�Ͷ���ɶ����¼�������
            currentState = TeacherState.Throwing;
            PlayThrowAnimation();

            // 4. �ȴ�������ɣ������ù̶�ʱ�䣬ʵ��Ӧ�ø��ݶ������ȵ�����
            yield return new WaitForSeconds(3.4f);

            // 5. �ָ���ʼ����
            currentState = TeacherState.Recovering;
            yield return StartCoroutine(RecoverToInitialRotation());

            // 6. ���
            currentState = TeacherState.Idle;
            currentTargetPosition = Vector3.zero; // �������
            onActionCompleted?.Invoke();
            LogDebug("�ӷ۱��������");
        }

        #endregion

        #region ���嶯��ʵ��

        /// <summary>
        /// ת��Ŀ��
        /// </summary>
        private IEnumerator TurnToTarget(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0; // ����ˮƽת��

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                                                    turnSpeed * Time.deltaTime);
                yield return null;
            }

            transform.rotation = targetRotation;
            LogDebug("ת��Ŀ�����");
        }

        /// <summary>
        /// �����ӷ۱ʶ���
        /// </summary>
        private void PlayThrowAnimation()
        {
            if (teacherAnimator != null)
            {
                teacherAnimator.SetTrigger("throw");
                LogDebug("�����ӷ۱ʶ���");
            }
        }

        /// <summary>
        /// �����¼��ص� - �ڹؼ�֡Ͷ���۱�
        /// ��������ᱻAnimation Event�Զ�����
        /// </summary>
        public void OnThrowChalk()
        {
            if (currentState == TeacherState.Throwing && currentTargetPosition != Vector3.zero)
            {
                ThrowChalk(currentTargetPosition);
                LogDebug("ͨ�������¼�Ͷ���۱�");
            }
        }

        /// <summary>
        /// Ͷ���۱�
        /// </summary>
        private void ThrowChalk(Vector3 targetPosition)
        {
            if (chalkPrefab == null || throwPoint == null)
            {
                LogDebug("�۱�Ԥ�����Ͷ����δ����");
                return;
            }

            // ���ɷ۱�
            GameObject chalk = Instantiate(chalkPrefab, throwPoint.position, throwPoint.rotation);
            Rigidbody chalkRb = chalk.GetComponent<Rigidbody>();

            if (chalkRb != null)
            {
                // ���������߳�ʼ�ٶ�
                Vector3 velocity = CalculateThrowVelocity(throwPoint.position, targetPosition);
                chalkRb.AddForce(velocity, ForceMode.VelocityChange);

                LogDebug($"�۱���Ͷ������ʼ�ٶȣ�{velocity}");
            }

            // ȷ���۱���ChalkProjectile�ű�������ײ���
            if (chalk.GetComponent<ChalkProjectile>() == null)
            {
                chalk.AddComponent<ChalkProjectile>();
            }
        }

        /// <summary>
        /// �ָ�����ʼ����
        /// </summary>
        private IEnumerator RecoverToInitialRotation()
        {
            yield return new WaitForSeconds(recoverDelay);

            while (Quaternion.Angle(transform.rotation, initialRotation) > 1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, initialRotation,
                                                    turnSpeed * Time.deltaTime);
                yield return null;
            }

            transform.rotation = initialRotation;
            LogDebug("�ָ���ʼ�������");
        }

        #endregion

        #region ��������

        /// <summary>
        /// �Զ��������
        /// </summary>
        private void AutoFindComponents()
        {
            if (teacherAnimator == null)
                teacherAnimator = GetComponent<Animator>();

            if (throwPoint == null)
            {
                // ���Բ����ֲ�λ��
                throwPoint = transform.Find("ThrowPoint") ??
                           transform.Find("Hand") ??
                           transform.Find("RightHand");

                if (throwPoint == null)
                {
                    // ����Ĭ��Ͷ����
                    GameObject throwObj = new GameObject("ThrowPoint");
                    throwObj.transform.SetParent(transform);
                    throwObj.transform.localPosition = new Vector3(0.5f, 1.5f, 0.5f);
                    throwPoint = throwObj.transform;
                    LogDebug("�Զ�����Ͷ����");
                }
            }
        }

        /// <summary>
        /// ��ȡѧ��λ��
        /// </summary>
        private Vector3 GetStudentPosition(ushort playerId)
        {
            if (classroomManager?.SeatBinder == null)
                return Vector3.zero;

            var studentGameObject = classroomManager.SeatBinder.GetPlayerCharacter(playerId);
            return studentGameObject != null ? studentGameObject.transform.position : Vector3.zero;
        }

        /// <summary>
        /// ����������Ͷ���ٶ�
        /// </summary>
        private Vector3 CalculateThrowVelocity(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0;
            float distance = direction.magnitude;

            float radianAngle = throwAngle * Mathf.Deg2Rad;
            float height = to.y - from.y;

            // �����߹�ʽ����
            float vY = Mathf.Sqrt(2 * Physics.gravity.magnitude *
                                (height + distance * Mathf.Tan(radianAngle)));
            float vXZ = distance / (vY / Physics.gravity.magnitude +
                                  Mathf.Sqrt(2 * height / Physics.gravity.magnitude));

            Vector3 velocity = direction.normalized * vXZ;
            velocity.y = vY;

            return velocity * (throwForce / 10f); // ����ϵ��
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[TeacherManager] {message}");
            }
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ��鵱ǰ�Ƿ����
        /// </summary>
        public bool IsIdle => currentState == TeacherState.Idle;

        /// <summary>
        /// ǿ��ֹͣ��ǰ����
        /// </summary>
        public void StopCurrentAction()
        {
            StopAllCoroutines();
            currentState = TeacherState.Idle;
            transform.rotation = initialRotation;
            LogDebug("ǿ��ֹͣ��ǰ����");
        }

        #endregion
    }

    #region ChalkProjectile���

    /// <summary>
    /// �۱ʵ������ - ������ײ������
    /// </summary>
    public class ChalkProjectile : MonoBehaviour
    {
        [Header("����")]
        [SerializeField] private float lifetime = 5f; // �����ʱ��
        [SerializeField] private bool enableDebugLogs = false;

        private void Start()
        {
            // ���������ʱ��
            Destroy(gameObject, lifetime);
        }

        private void OnTriggerEnter(Collider other)
        {
            // ����Ƿ�����ѧ��
            if (other.CompareTag("Player") || other.name.Contains("Character"))
            {
                LogDebug($"�۱�����Ŀ��: {other.name}");

                // �������������������Ч
                CreateHitEffect();

                // ���ٷ۱�
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // ������ײ���
            if (collision.gameObject.CompareTag("Player") ||
                collision.gameObject.name.Contains("Character"))
            {
                LogDebug($"�۱���ײĿ��: {collision.gameObject.name}");
                CreateHitEffect();
                Destroy(gameObject);
            }
            else if (collision.gameObject.CompareTag("Ground") ||
                     collision.gameObject.name.Contains("Floor"))
            {
                // ���е���Ҳ����
                LogDebug("�۱����");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// ����������Ч����ѡ��
        /// </summary>
        private void CreateHitEffect()
        {
            // ����������ʵ����������Ч�򲥷���Ч
            // ���磺�۱�����Ч�����ҳ������
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[ChalkProjectile] {message}");
            }
        }
    }

    #endregion
}