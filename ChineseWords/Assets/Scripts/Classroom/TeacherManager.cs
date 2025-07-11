using UnityEngine;
using System.Collections;
using Classroom;
using Core.Network;

namespace Classroom.Teacher
{
    /// <summary>
    /// 老师管理器 - 简化版本
    /// </summary>
    public class TeacherManager : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private Animator teacherAnimator;
        [SerializeField] private Transform throwPoint;
        [SerializeField] private GameObject chalkPrefab;

        [Header("动作配置")]
        [SerializeField] private float turnSpeed = 2f;
        [SerializeField] private float throwForce = 15f;
        [SerializeField] private float recoverDelay = 2f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 状态管理
        private enum TeacherState { Idle, Targeting, Throwing, Recovering }
        private TeacherState currentState = TeacherState.Idle;

        // 缓存
        private Quaternion initialRotation;
        private ClassroomManager classroomManager;
        private System.Action onActionCompleted;
        private Vector3 currentTargetPosition;
        private bool hasThrown = false; // 防止重复投掷的标志

        #region Unity生命周期

        private void Awake()
        {
            initialRotation = transform.rotation;
            classroomManager = FindObjectOfType<ClassroomManager>();
        }

        private void Start()
        {
            SubscribeToEvents();
            LogDebug("TeacherManager初始化完成");
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region 事件订阅

        private void SubscribeToEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerAnswerResult += OnPlayerAnswerResult;
                LogDebug("已订阅网络事件");
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerAnswerResult -= OnPlayerAnswerResult;
            }
        }

        private void OnPlayerAnswerResult(ushort playerId, bool isCorrect, string answer)
        {
            LogDebug($"收到答题结果: 玩家{playerId}, 正确:{isCorrect}, 答案:{answer}");

            if (isCorrect || currentState != TeacherState.Idle)
            {
                LogDebug($"跳过处理: 正确答案({isCorrect}) 或 忙碌状态({currentState})");
                return;
            }

            LogDebug($"学生{playerId}答错了，准备扔粉笔");
            StartChalkThrowSequence(playerId);
        }

        #endregion

        #region 主要功能

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

        private IEnumerator ExecuteChalkThrowSequence(ushort targetPlayerId)
        {
            LogDebug($"=== 开始扔粉笔序列，目标玩家: {targetPlayerId} ===");

            // 重置投掷标志
            hasThrown = false;

            // 1. 获取目标位置
            Vector3 targetPosition = GetStudentPosition(targetPlayerId);
            if (targetPosition == Vector3.zero)
            {
                LogDebug($"未找到学生{targetPlayerId}的位置，取消动作");
                yield break;
            }

            currentTargetPosition = targetPosition;
            LogDebug($"目标位置: {targetPosition}");

            // 2. 转向目标
            currentState = TeacherState.Targeting;
            yield return StartCoroutine(TurnToTarget(targetPosition));

            // 3. 播放动画并投掷
            currentState = TeacherState.Throwing;
            PlayThrowAnimation();

            // 4. 等待动画完成
            yield return new WaitForSeconds(3.4f);

            // 5. 恢复初始朝向
            currentState = TeacherState.Recovering;
            yield return StartCoroutine(RecoverToInitialRotation());

            // 6. 完成
            currentState = TeacherState.Idle;
            currentTargetPosition = Vector3.zero;
            hasThrown = false;
            onActionCompleted?.Invoke();
            LogDebug("=== 扔粉笔序列完成 ===");
        }

        #endregion

        #region 具体动作实现

        private IEnumerator TurnToTarget(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
                yield return null;
            }

            transform.rotation = targetRotation;
            LogDebug("转向目标完成");
        }

        private void PlayThrowAnimation()
        {
            if (teacherAnimator != null)
            {
                teacherAnimator.SetTrigger("throw");
                LogDebug("播放扔粉笔动画");
            }
        }

        /// <summary>
        /// 动画事件回调 - 由动画系统调用
        /// </summary>
        public void OnThrowChalk()
        {
            LogDebug("动画事件触发 OnThrowChalk");

            if (currentState == TeacherState.Throwing && !hasThrown)
            {
                ThrowChalk();
            }
            else
            {
                LogDebug($"跳过动画事件投掷 - 状态:{currentState}, 已投掷:{hasThrown}");
            }
        }

        private void ThrowChalk()
        {
            if (hasThrown)
            {
                LogDebug("已经投掷过粉笔，跳过重复投掷");
                return;
            }

            hasThrown = true;
            LogDebug("=== 执行粉笔投掷 ===");

            try
            {
                // 生成粉笔
                GameObject chalk = Instantiate(chalkPrefab, throwPoint.position, throwPoint.rotation);
                Rigidbody chalkRb = chalk.GetComponent<Rigidbody>();

                if (chalkRb != null)
                {
                    // 使用简单投掷
                    Vector3 velocity = CalculateSimpleThrowVelocity(throwPoint.position, currentTargetPosition);
                    chalkRb.AddForce(velocity, ForceMode.VelocityChange);
                    LogDebug($"粉笔投掷完成，速度: {velocity}");
                }

                // 自动销毁
                StartCoroutine(DestroyChalkAfterTime(chalk, 10f));
            }
            catch (System.Exception e)
            {
                LogDebug($"投掷粉笔时发生错误: {e.Message}");
                hasThrown = false; // 重置标志以允许重试
            }
        }

        private IEnumerator DestroyChalkAfterTime(GameObject chalk, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (chalk != null)
            {
                Destroy(chalk);
            }
        }

        private IEnumerator RecoverToInitialRotation()
        {
            yield return new WaitForSeconds(recoverDelay);

            while (Quaternion.Angle(transform.rotation, initialRotation) > 1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, initialRotation, turnSpeed * Time.deltaTime);
                yield return null;
            }

            transform.rotation = initialRotation;
            LogDebug("恢复初始朝向完成");
        }

        #endregion

        #region 简单投掷计算

        /// <summary>
        /// 简单投掷速度计算
        /// </summary>
        private Vector3 CalculateSimpleThrowVelocity(Vector3 from, Vector3 to)
        {
            Vector3 direction = (to - from).normalized;
            float distance = Vector3.Distance(from, to);

            // 添加向上的分量模拟抛物线
            direction.y += 0.4f;
            direction = direction.normalized;

            // 根据距离调整速度
            float speed = Mathf.Max(throwForce, distance * 1.5f);

            return direction * speed;
        }

        #endregion

        #region 辅助方法

        private Vector3 GetStudentPosition(ushort playerId)
        {
            if (classroomManager?.SeatBinder == null)
                return Vector3.zero;

            var studentGameObject = classroomManager.SeatBinder.GetPlayerCharacter(playerId);
            return studentGameObject != null ? studentGameObject.transform.position : Vector3.zero;
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[TeacherManager] {message}");
            }
        }

        #endregion
    }
}