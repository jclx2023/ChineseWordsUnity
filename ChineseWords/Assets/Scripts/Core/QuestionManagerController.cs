using UnityEngine;
using Core;
using GameLogic;
using GameLogic.FillBlank;
using GameLogic.TorF;
using GameLogic.Choice;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;

namespace Core
{
    /// <summary>
    /// ��Ŀ���������
    /// - ������Ŀ���͵�ѡ����л�
    /// - ������Ŀ����������������
    /// - Э����ʱ����Ѫ��ϵͳ
    /// - ����������ͳ�ʱ�߼�
    /// </summary>
    public class QuestionManagerController : MonoBehaviour
    {
        [Header("��Ϸ����")]
        [SerializeField] private float defaultTimeLimit = 15f;
        [SerializeField] private int defaultInitialHealth = 3;
        [SerializeField] private int defaultDamagePerWrong = 1;

        [Header("��Ŀ��������")]
        [SerializeField] private float timeUpDelay = 1f;
        [SerializeField] private float questionTransitionDelay = 0.5f;
        [SerializeField] private bool autoStartOnAwake = false;

        [Header("������Ϣ")]
        [SerializeField] private bool enableDebugLog = true;

        // �������
        private QuestionManagerBase currentManager;
        private TimerManager timerManager;
        private PlayerHealthManager healthManager;

        // ��Ϸ״̬
        private bool isGameRunning = false;
        private bool isTransitioning = false;
        private int questionCount = 0;

        // ����Ȩ������
        private Dictionary<QuestionType, float> typeWeights = new Dictionary<QuestionType, float>()
        {
            { QuestionType.IdiomChain, 1f },
            { QuestionType.TextPinyin, 1f },
            { QuestionType.HardFill, 1f },
            { QuestionType.SoftFill, 1f },
            { QuestionType.SentimentTorF, 1f },
            { QuestionType.SimularWordChoice, 1f },
            { QuestionType.UsageTorF, 1f },
            { QuestionType.ExplanationChoice, 1f },
        };

        #region Unity��������

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            SetupGameConfiguration();
            BindEvents();

            if (autoStartOnAwake)
            {
                StartQuestionFlow();
            }
        }

        private void OnDestroy()
        {
            UnbindEvents();
            CleanupCurrentManager();
        }

        #endregion

        #region ��ʼ��

        /// <summary>
        /// ��ʼ���������
        /// </summary>
        private void InitializeComponents()
        {
            timerManager = GetComponent<TimerManager>();
            healthManager = GetComponent<PlayerHealthManager>();

            if (timerManager == null)
            {
                Debug.LogError("[QMC] �Ҳ��� TimerManager ���");
            }

            if (healthManager == null)
            {
                Debug.LogError("[QMC] �Ҳ��� PlayerHealthManager ���");
            }

            LogDebug("�����ʼ�����");
        }

        /// <summary>
        /// ������Ϸ����
        /// </summary>
        private void SetupGameConfiguration()
        {
            // ���Դ� ConfigManager ��ȡ���ã����û����ʹ��Ĭ��ֵ
            float timeLimit = defaultTimeLimit;
            int initialHealth = defaultInitialHealth;
            int damagePerWrong = defaultDamagePerWrong;

            // ����� ConfigManager������ʹ��������
            if (ConfigManager.Instance != null && ConfigManager.Instance.Config != null)
            {
                var config = ConfigManager.Instance.Config;
                timeLimit = config.timeLimit > 0 ? config.timeLimit : defaultTimeLimit;
                initialHealth = config.initialHealth > 0 ? config.initialHealth : defaultInitialHealth;
                damagePerWrong = config.damagePerWrong > 0 ? config.damagePerWrong : defaultDamagePerWrong;

                LogDebug($"ʹ�� ConfigManager ����: ʱ��={timeLimit}, Ѫ��={initialHealth}, ��Ѫ={damagePerWrong}");
            }
            else
            {
                LogDebug($"ʹ��Ĭ������: ʱ��={timeLimit}, Ѫ��={initialHealth}, ��Ѫ={damagePerWrong}");
            }

            // Ӧ�����õ�����������
            if (timerManager != null)
                timerManager.ApplyConfig(timeLimit);

            if (healthManager != null)
                healthManager.ApplyConfig(initialHealth, damagePerWrong);
        }

        /// <summary>
        /// ���¼�
        /// </summary>
        private void BindEvents()
        {
            if (timerManager != null)
            {
                timerManager.OnTimeUp += HandleTimeUp;
                LogDebug("��ʱ���¼������");
            }

            // Ѫ�����������¼���ÿ����Ŀ����������ʱ��
        }

        /// <summary>
        /// ����¼�
        /// </summary>
        private void UnbindEvents()
        {
            if (timerManager != null)
            {
                timerManager.OnTimeUp -= HandleTimeUp;
            }

            if (currentManager != null && currentManager.OnAnswerResult != null)
            {
                currentManager.OnAnswerResult -= HandleAnswerResult;
            }

            LogDebug("�¼�������");
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ��ʼ��Ŀ����
        /// </summary>
        public void StartQuestionFlow()
        {
            if (isGameRunning)
            {
                LogDebug("��Ŀ�����Ѿ�������");
                return;
            }

            LogDebug("��ʼ��Ŀ����");
            isGameRunning = true;
            questionCount = 0;

            StartCoroutine(DelayedFirstQuestion());
        }

        /// <summary>
        /// ֹͣ��Ŀ����
        /// </summary>
        public void StopQuestionFlow()
        {
            if (!isGameRunning)
            {
                LogDebug("��Ŀ����δ������");
                return;
            }

            LogDebug("ֹͣ��Ŀ����");
            isGameRunning = false;

            // ֹͣ��ʱ��
            if (timerManager != null)
                timerManager.StopTimer();

            // ����ǰ��Ŀ������
            CleanupCurrentManager();

            // ֹͣ����Э��
            StopAllCoroutines();
        }

        /// <summary>
        /// ��ͣ��Ŀ����
        /// </summary>
        public void PauseQuestionFlow()
        {
            if (!isGameRunning)
            {
                LogDebug("��Ŀ����δ�����У��޷���ͣ");
                return;
            }

            LogDebug("��ͣ��Ŀ����");

            if (timerManager != null)
                timerManager.PauseTimer();
        }

        /// <summary>
        /// �ָ���Ŀ����
        /// </summary>
        public void ResumeQuestionFlow()
        {
            if (!isGameRunning)
            {
                LogDebug("��Ŀ����δ�����У��޷��ָ�");
                return;
            }

            LogDebug("�ָ���Ŀ����");

            if (timerManager != null)
                timerManager.ResumeTimer();
        }

        /// <summary>
        /// ������ǰ��Ŀ
        /// </summary>
        public void SkipCurrentQuestion()
        {
            if (!isGameRunning || isTransitioning)
            {
                LogDebug("�޷�������Ŀ����Ϸδ���л�����ת����");
                return;
            }

            LogDebug("������ǰ��Ŀ");

            // ֹͣ��ʱ��
            if (timerManager != null)
                timerManager.StopTimer();

            // ֱ�Ӽ�����һ��
            StartCoroutine(DelayedLoadNextQuestion(0f));
        }

        #endregion

        #region ��Ŀ����

        /// <summary>
        /// �ӳټ��ص�һ��
        /// </summary>
        public IEnumerator DelayedFirstQuestion()
        {
            yield return new WaitForEndOfFrame();

            if (isGameRunning)
            {
                LoadNextQuestion();
            }
        }

        /// <summary>
        /// ������һ��
        /// </summary>
        private void LoadNextQuestion()
        {
            if (!isGameRunning)
            {
                LogDebug("��Ϸδ���У�ֹͣ������Ŀ");
                return;
            }

            if (isTransitioning)
            {
                LogDebug("����ת���У�����������Ŀ");
                return;
            }

            isTransitioning = true;
            questionCount++;

            LogDebug($"��ʼ���ص� {questionCount} ��");

            // ����ǰ������
            CleanupCurrentManager();

            // ѡ�����Ͳ�����������
            var selectedType = SelectRandomTypeByWeight();
            currentManager = CreateManager(selectedType);

            if (currentManager == null)
            {
                Debug.LogError("[QMC] ������Ŀ������ʧ��");
                isTransitioning = false;
                return;
            }



            // ��Ѫ��������
            //if (healthManager != null)
            //{
            //    healthManager.BindManager(currentManager);
            //}
            // �󶨴������¼�
            currentManager.OnAnswerResult += HandleAnswerResult;
            // �ӳټ�����Ŀ
            StartCoroutine(DelayedLoadQuestion());
        }

        /// <summary>
        /// �ӳټ�����Ŀ
        /// </summary>
        private IEnumerator DelayedLoadQuestion()
        {
            yield return new WaitForSeconds(questionTransitionDelay);

            if (currentManager != null && isGameRunning)
            {
                currentManager.LoadQuestion();

                // ������ʱ��
                if (timerManager != null)
                {
                    timerManager.StartTimer();
                }

                LogDebug($"�� {questionCount} �������ɣ���ʱ��������");
            }

            isTransitioning = false;
        }

        /// <summary>
        /// �ӳټ�����һ��
        /// </summary>
        private IEnumerator DelayedLoadNextQuestion(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (isGameRunning)
            {
                LoadNextQuestion();
            }
        }

        /// <summary>
        /// ����ǰ��Ŀ������
        /// </summary>
        private void CleanupCurrentManager()
        {
            if (currentManager != null)
            {
                // ����¼�
                currentManager.OnAnswerResult -= HandleAnswerResult;

                // �������
                if (currentManager is Component component)
                {
                    Destroy(component);
                }

                currentManager = null;
                LogDebug("��ǰ��Ŀ������������");
            }
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// ���������
        /// </summary>
        private void HandleAnswerResult(bool isCorrect)
        {
            if (!isGameRunning)
            {
                LogDebug("��Ϸδ���У����Դ�����");
                return;
            }

            LogDebug($"������: {(isCorrect ? "��ȷ" : "����")}");

            // ֹͣ��ʱ��
            if (timerManager != null)
                timerManager.StopTimer();

            // ������Ѫ�����������Զ������Ѫ
            if (!isCorrect && healthManager != null)
            {
                healthManager.HPHandleAnswerResult(false);
            }

            // �����Ϸ�Ƿ����
            if (healthManager != null && healthManager.IsGameOver)
            {
                LogDebug("��Ϸ������ֹͣ��Ŀ����");
                StopQuestionFlow();
                return;
            }

            // �ӳټ�����һ��
            StartCoroutine(DelayedLoadNextQuestion(timeUpDelay));
        }

        /// <summary>
        /// ����ʱ
        /// </summary>
        private void HandleTimeUp()
        {
            if (!isGameRunning)
            {
                LogDebug("��Ϸδ���У����Գ�ʱ�¼�");
                return;
            }

            LogDebug("���ⳬʱ");

            // ֹͣ��ʱ��
            if (timerManager != null)
                timerManager.StopTimer();

            // ���������
            if (currentManager != null)
            {
                currentManager.OnAnswerResult?.Invoke(false);
            }
            else
            {
                // ���û�е�ǰ��������ֱ�Ӵ���ʱ
                HandleAnswerResult(false);
            }
        }

        #endregion

        #region ���͹���

        /// <summary>
        /// ����Ȩ�����ѡ������
        /// </summary>
        private QuestionType SelectRandomTypeByWeight()
        {
            float total = typeWeights.Values.Sum();
            float random = Random.Range(0f, total);
            float accumulator = 0f;

            foreach (var pair in typeWeights)
            {
                accumulator += pair.Value;
                if (random <= accumulator)
                {
                    LogDebug($"ѡ������: {pair.Key}");
                    return pair.Key;
                }
            }

            // fallback
            var fallbackType = typeWeights.Keys.First();
            LogDebug($"ʹ��fallback����: {fallbackType}");
            return fallbackType;
        }

        /// <summary>
        /// ������Ŀ������
        /// </summary>
        private QuestionManagerBase CreateManager(QuestionType type)
        {
            QuestionManagerBase manager = null;

            try
            {
                switch (type)
                {
                    case QuestionType.IdiomChain:
                        manager = gameObject.AddComponent<IdiomChainQuestionManager>();
                        break;
                    case QuestionType.TextPinyin:
                        manager = gameObject.AddComponent<TextPinyinQuestionManager>();
                        break;
                    case QuestionType.HardFill:
                        manager = gameObject.AddComponent<HardFillQuestionManager>();
                        break;
                    case QuestionType.SoftFill:
                        manager = gameObject.AddComponent<SoftFillQuestionManager>();
                        break;
                    case QuestionType.SentimentTorF:
                        manager = gameObject.AddComponent<SentimentTorFQuestionManager>();
                        break;
                    case QuestionType.SimularWordChoice:
                        manager = gameObject.AddComponent<SimularWordChoiceQuestionManager>();
                        break;
                    case QuestionType.UsageTorF:
                        manager = gameObject.AddComponent<UsageTorFQuestionManager>();
                        break;
                    case QuestionType.ExplanationChoice:
                        manager = gameObject.AddComponent<ExplanationChoiceQuestionManager>();
                        break;
                    default:
                        Debug.LogError($"[QMC] δʵ�ֵ�����: {type}");
                        return null;
                }

                LogDebug($"������Ŀ������: {type} -> {manager.GetType().Name}");
                return manager;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QMC] ������Ŀ������ʧ��: {type}, ����: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// ��������Ȩ��
        /// </summary>
        /// <param name="weights">����Ȩ���ֵ�</param>
        public void SetTypeWeights(Dictionary<QuestionType, float> weights)
        {
            if (weights == null || weights.Count == 0)
            {
                Debug.LogWarning("[QMC] ����Ȩ��Ϊ�գ�����ԭ������");
                return;
            }

            typeWeights = new Dictionary<QuestionType, float>(weights);
            LogDebug("����Ȩ���Ѹ���");
        }

        /// <summary>
        /// ��ȡ��ǰ����Ȩ��
        /// </summary>
        public Dictionary<QuestionType, float> GetTypeWeights()
        {
            return new Dictionary<QuestionType, float>(typeWeights);
        }

        #endregion

        #region ��������

        /// <summary>
        /// ��ȡ��ǰ��Ŀ������
        /// </summary>
        public QuestionManagerBase CurrentManager => currentManager;

        /// <summary>
        /// �����Ϸ�Ƿ�������
        /// </summary>
        public bool IsGameRunning => isGameRunning;

        /// <summary>
        /// ����Ƿ�����ת����Ŀ
        /// </summary>
        public bool IsTransitioning => isTransitioning;

        /// <summary>
        /// ��ȡ��ǰ��Ŀ����
        /// </summary>
        public int QuestionCount => questionCount;

        #endregion

        #region ���Թ���

        /// <summary>
        /// ������־���
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[QMC] {message}");
            }
        }

        #endregion

        #region Editor���߷���

#if UNITY_EDITOR
        /// <summary>
        /// �ڱ༭���в��Կ�ʼ��Ϸ
        /// </summary>
        [ContextMenu("���Կ�ʼ��Ϸ")]
        private void TestStartGame()
        {
            StartQuestionFlow();
        }

        /// <summary>
        /// �ڱ༭���в���ֹͣ��Ϸ
        /// </summary>
        [ContextMenu("����ֹͣ��Ϸ")]
        private void TestStopGame()
        {
            StopQuestionFlow();
        }

        /// <summary>
        /// �ڱ༭���в���������Ŀ
        /// </summary>
        [ContextMenu("����������Ŀ")]
        private void TestSkipQuestion()
        {
            SkipCurrentQuestion();
        }
#endif

        #endregion
    }
}