using UnityEngine;
using System.Collections;
using Classroom;
using Core.Network;

namespace Classroom.Teacher
{
    /// <summary>
    /// 老师管理器 - 处理学生答错题时的扔粉笔动作
    /// 功能：转向学生 → 播放动画 → 生成粉笔 → 抛物线飞行 → 命中销毁 → 恢复朝向
    /// </summary>
    public class TeacherManager : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private Animator teacherAnimator;
        [SerializeField] private Transform throwPoint; // 手部位置
        [SerializeField] private GameObject chalkPrefab; // 粉笔预制体

        [Header("动作配置")]
        [SerializeField] private float turnSpeed = 2f; // 转向速度
        [SerializeField] private float throwForce = 15f; // 抛射力度
        [SerializeField] private float throwAngle = 25f; // 抛射角度
        [SerializeField] private float recoverDelay = 2f; // 恢复朝向延迟

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 状态管理
        private enum TeacherState { Idle, Targeting, Throwing, Recovering }
        private TeacherState currentState = TeacherState.Idle;

        // 缓存
        private Quaternion initialRotation;
        private ClassroomManager classroomManager;
        private System.Action onActionCompleted; // 动作完成回调
        private Vector3 currentTargetPosition; // 当前目标位置缓存

        #region Unity生命周期

        private void Awake()
        {
            // 记录初始朝向
            initialRotation = transform.rotation;

            // 获取教室管理器
            classroomManager = FindObjectOfType<ClassroomManager>();

            // 自动查找组件
            AutoFindComponents();
        }

        private void Start()
        {
            // 监听答题结果事件
            SubscribeToEvents();
            LogDebug("TeacherManager初始化完成");
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region 事件订阅

        /// <summary>
        /// 订阅网络事件
        /// </summary>
        private void SubscribeToEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerAnswerResult += OnPlayerAnswerResult;
            }
        }

        /// <summary>
        /// 取消事件订阅
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerAnswerResult -= OnPlayerAnswerResult;
            }
        }

        /// <summary>
        /// 处理玩家答题结果
        /// </summary>
        private void OnPlayerAnswerResult(ushort playerId, bool isCorrect, string answer)
        {
            // 只处理错误答案，且确保当前空闲
            if (isCorrect || currentState != TeacherState.Idle)
                return;

            LogDebug($"学生{playerId}答错了，准备扔粉笔");
            StartChalkThrowSequence(playerId);
        }

        #endregion

        #region 主要功能

        /// <summary>
        /// 开始扔粉笔序列
        /// </summary>
        public void StartChalkThrowSequence(ushort targetPlayerId, System.Action onCompleted = null)
        {
            if (currentState != TeacherState.Idle)
            {
                LogDebug("老师正忙，无法执行新的扔粉笔动作");
                return;
            }

            onActionCompleted = onCompleted;
            StartCoroutine(ExecuteChalkThrowSequence(targetPlayerId));
        }

        /// <summary>
        /// 执行完整的扔粉笔序列
        /// </summary>
        private IEnumerator ExecuteChalkThrowSequence(ushort targetPlayerId)
        {
            LogDebug($"开始对学生{targetPlayerId}执行扔粉笔序列");

            // 1. 获取目标位置
            Vector3 targetPosition = GetStudentPosition(targetPlayerId);
            if (targetPosition == Vector3.zero)
            {
                LogDebug($"未找到学生{targetPlayerId}的位置，取消动作");
                yield break;
            }

            // 缓存目标位置供动画事件使用
            currentTargetPosition = targetPosition;

            // 2. 转向目标
            currentState = TeacherState.Targeting;
            yield return StartCoroutine(TurnToTarget(targetPosition));

            // 3. 播放扔粉笔动画（粉笔投掷由动画事件触发）
            currentState = TeacherState.Throwing;
            PlayThrowAnimation();

            // 4. 等待动画完成（这里用固定时间，实际应该根据动画长度调整）
            yield return new WaitForSeconds(3.4f);

            // 5. 恢复初始朝向
            currentState = TeacherState.Recovering;
            yield return StartCoroutine(RecoverToInitialRotation());

            // 6. 完成
            currentState = TeacherState.Idle;
            currentTargetPosition = Vector3.zero; // 清除缓存
            onActionCompleted?.Invoke();
            LogDebug("扔粉笔序列完成");
        }

        #endregion

        #region 具体动作实现

        /// <summary>
        /// 转向目标
        /// </summary>
        private IEnumerator TurnToTarget(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0; // 保持水平转向

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                                                    turnSpeed * Time.deltaTime);
                yield return null;
            }

            transform.rotation = targetRotation;
            LogDebug("转向目标完成");
        }

        /// <summary>
        /// 播放扔粉笔动画
        /// </summary>
        private void PlayThrowAnimation()
        {
            if (teacherAnimator != null)
            {
                teacherAnimator.SetTrigger("throw");
                LogDebug("播放扔粉笔动画");
            }
        }

        /// <summary>
        /// 动画事件回调 - 在关键帧投掷粉笔
        /// 这个方法会被Animation Event自动调用
        /// </summary>
        public void OnThrowChalk()
        {
            if (currentState == TeacherState.Throwing && currentTargetPosition != Vector3.zero)
            {
                ThrowChalk(currentTargetPosition);
                LogDebug("通过动画事件投掷粉笔");
            }
        }

        /// <summary>
        /// 投掷粉笔
        /// </summary>
        private void ThrowChalk(Vector3 targetPosition)
        {
            if (chalkPrefab == null || throwPoint == null)
            {
                LogDebug("粉笔预制体或投掷点未设置");
                return;
            }

            // 生成粉笔
            GameObject chalk = Instantiate(chalkPrefab, throwPoint.position, throwPoint.rotation);
            Rigidbody chalkRb = chalk.GetComponent<Rigidbody>();

            if (chalkRb != null)
            {
                // 计算抛物线初始速度
                Vector3 velocity = CalculateThrowVelocity(throwPoint.position, targetPosition);
                chalkRb.AddForce(velocity, ForceMode.VelocityChange);

                LogDebug($"粉笔已投掷，初始速度：{velocity}");
            }

            // 确保粉笔有ChalkProjectile脚本用于碰撞检测
            if (chalk.GetComponent<ChalkProjectile>() == null)
            {
                chalk.AddComponent<ChalkProjectile>();
            }
        }

        /// <summary>
        /// 恢复到初始朝向
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
            LogDebug("恢复初始朝向完成");
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 自动查找组件
        /// </summary>
        private void AutoFindComponents()
        {
            if (teacherAnimator == null)
                teacherAnimator = GetComponent<Animator>();

            if (throwPoint == null)
            {
                // 尝试查找手部位置
                throwPoint = transform.Find("ThrowPoint") ??
                           transform.Find("Hand") ??
                           transform.Find("RightHand");

                if (throwPoint == null)
                {
                    // 创建默认投掷点
                    GameObject throwObj = new GameObject("ThrowPoint");
                    throwObj.transform.SetParent(transform);
                    throwObj.transform.localPosition = new Vector3(0.5f, 1.5f, 0.5f);
                    throwPoint = throwObj.transform;
                    LogDebug("自动创建投掷点");
                }
            }
        }

        /// <summary>
        /// 获取学生位置
        /// </summary>
        private Vector3 GetStudentPosition(ushort playerId)
        {
            if (classroomManager?.SeatBinder == null)
                return Vector3.zero;

            var studentGameObject = classroomManager.SeatBinder.GetPlayerCharacter(playerId);
            return studentGameObject != null ? studentGameObject.transform.position : Vector3.zero;
        }

        /// <summary>
        /// 计算抛物线投掷速度
        /// </summary>
        private Vector3 CalculateThrowVelocity(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0;
            float distance = direction.magnitude;

            float radianAngle = throwAngle * Mathf.Deg2Rad;
            float height = to.y - from.y;

            // 抛物线公式计算
            float vY = Mathf.Sqrt(2 * Physics.gravity.magnitude *
                                (height + distance * Mathf.Tan(radianAngle)));
            float vXZ = distance / (vY / Physics.gravity.magnitude +
                                  Mathf.Sqrt(2 * height / Physics.gravity.magnitude));

            Vector3 velocity = direction.normalized * vXZ;
            velocity.y = vY;

            return velocity * (throwForce / 10f); // 缩放系数
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[TeacherManager] {message}");
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 检查当前是否空闲
        /// </summary>
        public bool IsIdle => currentState == TeacherState.Idle;

        /// <summary>
        /// 强制停止当前动作
        /// </summary>
        public void StopCurrentAction()
        {
            StopAllCoroutines();
            currentState = TeacherState.Idle;
            transform.rotation = initialRotation;
            LogDebug("强制停止当前动作");
        }

        #endregion
    }

    #region ChalkProjectile组件

    /// <summary>
    /// 粉笔弹道组件 - 处理碰撞和销毁
    /// </summary>
    public class ChalkProjectile : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private float lifetime = 5f; // 最大存活时间
        [SerializeField] private bool enableDebugLogs = false;

        private void Start()
        {
            // 设置最大存活时间
            Destroy(gameObject, lifetime);
        }

        private void OnTriggerEnter(Collider other)
        {
            // 检查是否命中学生
            if (other.CompareTag("Player") || other.name.Contains("Character"))
            {
                LogDebug($"粉笔命中目标: {other.name}");

                // 可以在这里添加命中特效
                CreateHitEffect();

                // 销毁粉笔
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // 备用碰撞检测
            if (collision.gameObject.CompareTag("Player") ||
                collision.gameObject.name.Contains("Character"))
            {
                LogDebug($"粉笔碰撞目标: {collision.gameObject.name}");
                CreateHitEffect();
                Destroy(gameObject);
            }
            else if (collision.gameObject.CompareTag("Ground") ||
                     collision.gameObject.name.Contains("Floor"))
            {
                // 命中地面也销毁
                LogDebug("粉笔落地");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 创建命中特效（可选）
        /// </summary>
        private void CreateHitEffect()
        {
            // 可以在这里实例化粒子特效或播放音效
            // 例如：粉笔碎裂效果、灰尘飞扬等
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