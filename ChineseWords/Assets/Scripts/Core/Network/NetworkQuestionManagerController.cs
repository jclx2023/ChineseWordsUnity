using UnityEngine;
using Core;
using Core.Network;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;

namespace Core.Network
{
    /// <summary>
    /// ͳһ����ͱ�����Ŀ���������
    /// ר�����ڵ����Ͷ���ģʽ����Ŀ���� + ������Ϣ���� + ���ύ
    /// �Ѿ�ͳһ�˵�����Ŀ�����߼�������Host�˷ַ���+ �ظ��Ĺ����������߼�
    /// </summary>
    public class NetworkQuestionManagerController : MonoBehaviour
    {
        [Header("��������������")]
        [SerializeField] private TimerManager timerManager;
        [SerializeField] private PlayerHealthManager hpManager;

        [Header("��������")]
        [SerializeField] private float timeUpDelay = 1f;
        [SerializeField] private bool isMultiplayerMode = false;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static NetworkQuestionManagerController Instance { get; private set; }

        // ��ǰ״̬
        private QuestionManagerBase currentManager;
        private NetworkQuestionData currentNetworkQuestion;
        private bool isMyTurn = false;
        private bool isWaitingForNetworkQuestion = false;
        private bool gameStarted = false;
        private bool isInitialized = false;
        private ushort currentTurnPlayerId = 0;  // ��ǰ�غ����ID
        private bool hasReceivedTurnChange = false;  // �Ƿ��յ��غϱ����Ϣ

        // ��Ŀ����Ȩ�أ��������ڹ�Hostʹ�ã�
        public Dictionary<QuestionType, float> TypeWeights = new Dictionary<QuestionType, float>()
        {

        };

        // �¼�
        public System.Action<bool> OnGameEnded;
        public System.Action<bool> OnAnswerCompleted;

        // ����
        public bool IsMultiplayerMode => isMultiplayerMode;
        public bool IsMyTurn => isMyTurn;
        public bool IsGameStarted => gameStarted;
        public bool IsInitialized => isInitialized;
        public QuestionManagerBase CurrentManager => currentManager;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("NetworkQuestionManagerController �����Ѵ���");
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // ��ʼ�����õ�����������
            InitializeComponents();
        }

        private void Start()
        {
            RegisterNetworkEvents();
            isInitialized = true;
            LogDebug("�����ѳ�ʼ�����ȴ�������ʼָ��");
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
            CleanupCurrentManager();

            if (Instance == this)
                Instance = null;

            LogDebug("NetworkQuestionManagerController ������");
        }

        /// <summary>
        /// ��ʼ���������
        /// </summary>
        private void InitializeComponents()
        {
            LogDebug("��ʼ���������...");

            // ��ȡ����ұ�Ҫ���
            if (timerManager == null)
                timerManager = GetComponent<TimerManager>() ?? FindObjectOfType<TimerManager>();
            if (hpManager == null)
                hpManager = GetComponent<PlayerHealthManager>() ?? FindObjectOfType<PlayerHealthManager>();

            if (timerManager == null)
            {
                Debug.LogError("[NQMC] �Ҳ���TimerManager����");
                return;
            }

            if (hpManager == null)
            {
                Debug.LogError("[NQMC] �Ҳ���PlayerHealthManager����");
                return;
            }

            // Ӧ������
            var cfg = ConfigManager.Instance?.Config;
            if (cfg != null)
            {
                timerManager.ApplyConfig(cfg.timeLimit);
                hpManager.ApplyConfig(cfg.initialHealth, cfg.damagePerWrong);
            }

            // �󶨼�ʱ���¼�
            timerManager.OnTimeUp += HandleTimeUp;

            LogDebug("���������ʼ�����");
        }

        /// <summary>
        /// ע�������¼�
        /// </summary>
        private void RegisterNetworkEvents()
        {
            NetworkManager.OnQuestionReceived += OnNetworkQuestionReceived;
            NetworkManager.OnAnswerResultReceived += OnNetworkAnswerResult;
            NetworkManager.OnPlayerTurnChanged += OnNetworkPlayerTurnChanged;
            NetworkManager.OnDisconnected += OnNetworkDisconnected;

            LogDebug("�����¼���ע��");
        }

        /// <summary>
        /// ȡ��ע�������¼�
        /// </summary>
        private void UnregisterNetworkEvents()
        {
            NetworkManager.OnQuestionReceived -= OnNetworkQuestionReceived;
            NetworkManager.OnAnswerResultReceived -= OnNetworkAnswerResult;
            NetworkManager.OnPlayerTurnChanged -= OnNetworkPlayerTurnChanged;
            NetworkManager.OnDisconnected -= OnNetworkDisconnected;

            LogDebug("�����¼���ȡ��ע��");
        }

        #region �����ӿ� - �����ⲿϵͳ����

        /// <summary>
        /// ��ʼ��Ϸ�������ⲿϵͳ���ã�
        /// </summary>
        /// <param name="multiplayerMode">�Ƿ�Ϊ����ģʽ</param>
        public void StartGame(bool multiplayerMode = false)
        {
            if (!isInitialized)
            {
                Debug.LogError("[NQMC] ����δ��ʼ�����޷���ʼ��Ϸ");
                return;
            }

            isMultiplayerMode = multiplayerMode;
            gameStarted = true;

            LogDebug($"��ʼ��Ϸ - ģʽ: {(isMultiplayerMode ? "����" : "����")}");

            if (isMultiplayerMode)
            {
                StartMultiplayerGame();
            }
            else
            {
                StartSinglePlayerGame();
            }
        }

        /// <summary>
        /// �޸ģ�ֹͣ��Ϸ - ����״̬
        /// </summary>
        public void StopGame()
        {
            LogDebug("ֹͣ��Ϸ");
            gameStarted = false;
            isMyTurn = false;
            isWaitingForNetworkQuestion = false;

            currentDisplayState = QuestionDisplayState.GameEnded;
            hasReceivedTurnChange = false;
            currentTurnPlayerId = 0;

            StopTimer();
            CleanupCurrentManager();
        }

        /// <summary>
        /// ��ͣ��Ϸ�������ⲿϵͳ���ã�
        /// </summary>
        public void PauseGame()
        {
            LogDebug("��ͣ��Ϸ");
            if (timerManager != null)
                timerManager.PauseTimer();
        }

        /// <summary>
        /// �ָ���Ϸ�������ⲿϵͳ���ã�
        /// </summary>
        public void ResumeGame()
        {
            LogDebug("�ָ���Ϸ");
            if (timerManager != null)
                timerManager.ResumeTimer();
        }

        /// <summary>
        /// ǿ�ƿ�ʼ��һ�⣨�����ⲿϵͳ���ã�
        /// </summary>
        public void ForceNextQuestion()
        {
            if (!gameStarted)
            {
                LogDebug("��Ϸδ��ʼ���޷�ǿ����һ��");
                return;
            }

            LogDebug("ǿ�ƿ�ʼ��һ��");

            if (isMultiplayerMode)
            {
                // ����ģʽ�£�ֻ���ֵ��Լ�����ǿ����һ��
                if (isMyTurn)
                {
                    RequestNetworkQuestion();
                }
                else
                {
                    LogDebug("�����ҵĻغϣ��޷�ǿ����һ��");
                }
            }
            else
            {
                // ����ģʽֱ�Ӽ�����һ��
                LoadNextLocalQuestion();
            }
        }

        #endregion

        #region ��Ϸģʽ����

        /// <summary>
        /// ��ʼ������Ϸ - �޸��汾���������ȴ���Ŀ
        /// </summary>
        private void StartMultiplayerGame()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                LogDebug("����ģʽ���ȴ���Ϸ��ʼ�ͻغϷ���");
                currentDisplayState = QuestionDisplayState.WaitingForGame;
                // �޸������������õȴ���Ŀ״̬
                isWaitingForNetworkQuestion = false;
                hasReceivedTurnChange = false;
            }
            else
            {
                Debug.LogError("[NQMC] δ���ӵ����������޷���ʼ������Ϸ");
                OnGameEnded?.Invoke(false);
            }
        }

        /// <summary>
        /// ��ʼ������Ϸ
        /// </summary>
        private void StartSinglePlayerGame()
        {
            LogDebug("����ģʽ��������ʼ��һ��");
            isMyTurn = true;
            StartCoroutine(DelayedFirstQuestion());
        }

        /// <summary>
        /// �ӳٿ�ʼ��һ��
        /// </summary>
        private IEnumerator DelayedFirstQuestion()
        {
            yield return null;
            LoadNextLocalQuestion();
        }

        #endregion

        #region ��Ŀ���� - �򻯰汾

        /// <summary>
        /// ������һ������Ŀ������ģʽ��
        /// </summary>
        private void LoadNextLocalQuestion()
        {
            if (!gameStarted)
            {
                LogDebug("��Ϸ��ֹͣ������������Ŀ");
                return;
            }

            LogDebug("���ر�����Ŀ...");

            // ����ǰ������
            CleanupCurrentManager();

            // ѡ����Ŀ���Ͳ�����������
            var selectedType = SelectRandomTypeByWeight();
            currentManager = CreateQuestionManager(selectedType, false);

            if (currentManager != null)
            {
                // �󶨹�����������ϵͳ
                if (hpManager != null)
                    hpManager.BindManager(currentManager);

                // �󶨴𰸽���¼�
                currentManager.OnAnswerResult += HandleLocalAnswerResult;

                // �ӳټ�����Ŀ
                StartCoroutine(DelayedLoadQuestion());

                LogDebug($"������Ŀ�����������ɹ�: {selectedType}");
            }
            else
            {
                Debug.LogError("[NQMC] �޷�������Ŀ������");
                OnGameEnded?.Invoke(false);
            }
        }

        /// <summary>
        /// ����������Ŀ������ģʽ��
        /// </summary>
        private void RequestNetworkQuestion()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                isWaitingForNetworkQuestion = true;
                NetworkManager.Instance.RequestQuestion();
                LogDebug("����������Ŀ...");
            }
            else
            {
                Debug.LogError("[NQMC] ����δ���ӣ��޷�������Ŀ");
                OnGameEnded?.Invoke(false);
            }
        }

        /// <summary>
        /// ����������Ŀ - �޸��汾
        /// </summary>
        private void LoadNetworkQuestion(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] ������Ŀ����Ϊ��");
                return;
            }

            LogDebug($"����������Ŀ: {networkQuestion.questionType}");

            // ����ǰ������
            CleanupCurrentManager();

            // ����������Ŀ����
            currentNetworkQuestion = networkQuestion;

            // ������Ӧ�Ĺ�����
            currentManager = CreateQuestionManager(networkQuestion.questionType, true);

            if (currentManager != null)
            {
                // �󶨹�����������ϵͳ
                if (hpManager != null)
                    hpManager.BindManager(currentManager);

                // ��������ύ�¼�
                currentManager.OnAnswerResult += HandleNetworkAnswerSubmission;

                // �ӳټ�����Ŀ - �޸���������������
                StartCoroutine(DelayedLoadNetworkQuestion(networkQuestion));

                LogDebug($"������Ŀ�����������ɹ�: {networkQuestion.questionType}");
            }
            else
            {
                Debug.LogError("[NQMC] �޷�Ϊ������Ŀ����������");
            }
        }

        /// <summary>
        /// �ӳټ�����Ŀ - ����ģʽ�����Ĭ��ʱ��֧�֣�
        /// </summary>
        private IEnumerator DelayedLoadQuestion()
        {
            yield return null;
            if (currentManager != null)
            {
                currentManager.LoadQuestion();

                // ����ģʽҲ֧�ֶ�̬ʱ�䣨ͨ��TimerConfig��ȡ��
                float timeLimit = GetLocalTimeLimit();
                if (timeLimit > 0)
                {
                    StartTimerWithDynamicLimit(timeLimit);
                    LogDebug($"������Ŀ�Ѽ��ز���ʼ��ʱ��ʱ������: {timeLimit}��");
                }
                else
                {
                    StartTimer();
                    LogDebug("������Ŀ�Ѽ��ز���ʼ��ʱ��ʹ��Ĭ��ʱ�䣩");
                }
            }
        }
        /// <summary>
        /// ��ȡ����ģʽ��ʱ�����ƣ���TimerConfig��
        /// </summary>
        private float GetLocalTimeLimit()
        {
            try
            {
                // ����е�ǰ�����������Ի�ȡ������
                if (currentManager != null)
                {
                    // ���Ի�ȡ��������������Ϣ
                    var questionTypeField = currentManager.GetType().GetField("questionType",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (questionTypeField != null && questionTypeField.GetValue(currentManager) is QuestionType questionType)
                    {
                        if (TimerConfigManager.Config != null)
                        {
                            float timeLimit = TimerConfigManager.Config.GetTimeLimitForQuestionType(questionType);
                            LogDebug($"��TimerConfig��ȡ��������ʱ������: {questionType} -> {timeLimit}��");
                            return timeLimit;
                        }
                    }
                }

                LogDebug("�޷���ȡ��������ʱ�����ƣ�ʹ��Ĭ������");
                return 0f; // ����0��ʾʹ��Ĭ��ʱ��
            }
            catch (System.Exception e)
            {
                LogDebug($"��ȡ����ʱ������ʧ��: {e.Message}");
                return 0f;
            }
        }

        /// <summary>
        /// �ӳټ���������Ŀ - �޸��汾��֧�ֶ�̬ʱ������
        /// </summary>
        private IEnumerator DelayedLoadNetworkQuestion(NetworkQuestionData networkData)
        {
            yield return null;
            if (currentManager != null)
            {
                // �޸������������Ƿ�֧���������ݼ���
                if (currentManager is NetworkQuestionManagerBase networkManager)
                {
                    // ���������������ר�ü��ط���
                    var loadMethod = networkManager.GetType().GetMethod("LoadNetworkQuestion",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (loadMethod != null)
                    {
                        LogDebug($"ͨ���������������Ŀ���ط���: {networkData.questionType}");
                        loadMethod.Invoke(networkManager, new object[] { networkData });
                    }
                    else
                    {
                        // ���÷��������Թ�������
                        Debug.LogWarning($"[NQMC] δ�ҵ�LoadNetworkQuestion���������Ա��ü��ط�ʽ");
                        currentManager.LoadQuestion();
                    }
                }
                else
                {
                    // ���������������ʹ����ͨ����
                    Debug.LogWarning($"[NQMC] ����������NetworkQuestionManagerBase����: {currentManager.GetType()}");
                    currentManager.LoadQuestion();
                }

                // �ؼ��޸ģ�ʹ��������Ŀ��ʱ������������ʱ��
                StartTimerWithDynamicLimit(networkData.timeLimit);
                LogDebug($"������Ŀ�Ѽ��ز���ʼ��ʱ��ʱ������: {networkData.timeLimit}��");
            }
        }
        /// <summary>
        /// ʹ�ö�̬ʱ������������ʱ��
        /// </summary>
        private void StartTimerWithDynamicLimit(float timeLimit)
        {
            if (timerManager != null)
            {
                // ���TimerManager�Ƿ�֧�ֶ�̬ʱ������
                var setTimeLimitMethod = timerManager.GetType().GetMethod("SetTimeLimit");
                var startTimerWithLimitMethod = timerManager.GetType().GetMethod("StartTimer", new System.Type[] { typeof(float) });

                if (startTimerWithLimitMethod != null)
                {
                    // ����1��ֱ�ӵ��ô�ʱ�������StartTimer����
                    LogDebug($"ʹ�ö�̬ʱ������������ʱ��: {timeLimit}��");
                    startTimerWithLimitMethod.Invoke(timerManager, new object[] { timeLimit });
                }
                else if (setTimeLimitMethod != null)
                {
                    // ����2��������ʱ�����ƣ���������ʱ��
                    LogDebug($"����ʱ�����ƺ�������ʱ��: {timeLimit}��");
                    setTimeLimitMethod.Invoke(timerManager, new object[] { timeLimit });
                    timerManager.StartTimer();
                }
                else
                {
                    // ����3������ͨ�����ù���������
                    TrySetTimerThroughConfig(timeLimit);
                    timerManager.StartTimer();
                }
            }
            else
            {
                Debug.LogError("[NQMC] TimerManager����Ϊ�գ��޷�������ʱ��");
            }
        }
        /// <summary>
        /// ����ͨ��TimerConfig����ʱ������
        /// </summary>
        private void TrySetTimerThroughConfig(float timeLimit)
        {
            try
            {
                LogDebug($"����ͨ�����ù���������ʱ������: {timeLimit}��");

                // ����Ƿ�������ʱ���ýӿ�
                if (TimerConfigManager.Config != null)
                {
                    // ������ʱ���û��޸ĵ�ǰ���õ�ʱ������
                    // ������Ҫ����TimerConfigManager�ľ���ʵ��������

                    // ����1�����TimerConfigManager֧����ʱʱ������
                    var setTempTimeMethod = typeof(TimerConfigManager).GetMethod("SetTemporaryTimeLimit");
                    if (setTempTimeMethod != null)
                    {
                        setTempTimeMethod.Invoke(null, new object[] { timeLimit });
                        LogDebug("ͨ��TimerConfigManager������ʱʱ�����Ƴɹ�");
                        return;
                    }

                    // ����2��ֱ��ͨ������Ӧ�õ�TimerManager
                    var applyConfigMethod = timerManager.GetType().GetMethod("ApplyConfig", new System.Type[] { typeof(float) });
                    if (applyConfigMethod != null)
                    {
                        applyConfigMethod.Invoke(timerManager, new object[] { timeLimit });
                        LogDebug("ͨ��ApplyConfig����ʱ�����Ƴɹ�");
                        return;
                    }
                }

                Debug.LogWarning($"[NQMC] �޷����ö�̬ʱ�����ƣ���ʹ��Ĭ������");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NQMC] ����ʱ������ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ������Ŀ��������ʹ�ù�����
        /// </summary>
        private QuestionManagerBase CreateQuestionManager(QuestionType questionType, bool isNetworkMode)
        {
            // ������������GameObject������Ŀ������
            GameObject managerObj = new GameObject($"{questionType}Manager");

            // ���UI�������
            managerObj.AddComponent<UIEnvironmentMarker>();

            var manager = QuestionManagerFactory.CreateManagerOnGameObject(
                managerObj,
                questionType,
                isNetworkMode,
                false
            );

            return manager;
        }

        /// <summary>
        /// ����ǰ��Ŀ������
        /// </summary>
        private void CleanupCurrentManager()
        {
            if (currentManager != null)
            {
                LogDebug($"������Ŀ������: {currentManager.GetType().Name}");

                // �Ƴ��¼�����
                currentManager.OnAnswerResult -= HandleLocalAnswerResult;
                currentManager.OnAnswerResult -= HandleNetworkAnswerSubmission;

                // ʹ�ù�����ȫ����
                QuestionManagerFactory.DestroyManager(currentManager);
                currentManager = null;
            }
        }

        /// <summary>
        /// ����Ȩ��ѡ�������Ŀ����
        /// </summary>
        private QuestionType SelectRandomTypeByWeight()
        {
            // ����ʹ���µ�Ȩ�ع�����
            try
            {
                return QuestionWeightManager.SelectRandomQuestionType();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Ȩ�ع�����ѡ��ʧ�ܣ�ʹ�þɰ��߼�: {e.Message}");

                // ���˵��ɵ�Ȩ���߼�
                var typeWeights = TypeWeights;
                float total = typeWeights.Values.Sum();
                float r = Random.Range(0, total);
                float acc = 0f;

                foreach (var pair in typeWeights)
                {
                    acc += pair.Value;
                    if (r <= acc)
                        return pair.Key;
                }

                return typeWeights.Keys.First();
            }
        }

        #endregion

        #region ����������

        /// <summary>
        /// �����ش�����������ģʽ��
        /// </summary>
        private void HandleLocalAnswerResult(bool isCorrect)
        {
            LogDebug($"����ģʽ������: {(isCorrect ? "��ȷ" : "����")}");

            StopTimer();

            // ���������仯
            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // ����Ƿ���Ϸ����
                if (hpManager.CurrentHealth <= 0)
                {
                    LogDebug("�����ľ�����Ϸ����");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            // ֪ͨ�������
            OnAnswerCompleted?.Invoke(isCorrect);

            // �ӳټ�����һ��
            Invoke(nameof(LoadNextLocalQuestion), timeUpDelay);
        }

        /// <summary>
        /// �޸ģ�����������ύ
        /// </summary>
        private void HandleNetworkAnswerSubmission(bool isCorrect)
        {
            LogDebug("����ģʽ���ش��⣬�ȴ�������ȷ��");

            // ֻ�����ҵĻغϲŴ�����ύ
            if (currentDisplayState == QuestionDisplayState.MyTurn)
            {
                StopTimer();
            }
            else
            {
                LogDebug("�����ҵĻغϣ����Դ��ύ����");
            }
        }

        /// <summary>
        /// ����ʱ
        /// </summary>
        private void HandleTimeUp()
        {
            LogDebug("���ⳬʱ");

            StopTimer();

            if (isMultiplayerMode)
            {
                // ����ģʽ���ύ�մ𰸱�ʾ��ʱ
                if (isMyTurn && NetworkManager.Instance?.IsConnected == true)
                {
                    NetworkManager.Instance.SubmitAnswer("");
                    LogDebug("��ʱ���ύ�մ�");
                }
            }
            else
            {
                // ����ģʽ��ֱ�Ӵ���ʱ
                if (currentManager != null)
                {
                    currentManager.OnAnswerResult?.Invoke(false);
                }
                else
                {
                    Invoke(nameof(LoadNextLocalQuestion), timeUpDelay);
                }
            }
        }

        #endregion

        #region �����¼�����

        /// <summary>
        /// ���յ�������Ŀ - �޸��汾�����״̬��֤
        /// </summary>
        private void OnNetworkQuestionReceived(NetworkQuestionData question)
        {
            if (!isMultiplayerMode)
            {
                LogDebug("�Ƕ���ģʽ������������Ŀ");
                return;
            }

            LogDebug($"�յ�������Ŀ: {question.questionType}, ʱ������: {question.timeLimit}��");
            LogDebug($"��ǰ״̬: �ҵĻغ�={isMyTurn}, �ȴ���Ŀ={isWaitingForNetworkQuestion}, �غ�ID={currentTurnPlayerId}");

            // �ؼ��޸ģ����ݵ�ǰ״̬������δ�����Ŀ
            switch (currentDisplayState)
            {
                case QuestionDisplayState.MyTurn:
                    // �ֵ��Ҵ��⣺����������Ŀ
                    LogDebug("�ֵ��Ҵ��⣬����������Ŀ");
                    isWaitingForNetworkQuestion = false;
                    LoadNetworkQuestion(question);
                    break;

                case QuestionDisplayState.OtherPlayerTurn:
                    // ������һغϣ���ʾ��Ŀ�������ý���
                    LogDebug($"������һغϣ���ʾ��Ŀ�����ɽ��� (��ǰ�غ����: {currentTurnPlayerId})");
                    LoadNetworkQuestionAsObserver(question);
                    break;

                case QuestionDisplayState.WaitingForGame:
                    // ���ڵȴ���Ϸ��ʼ��������ʱ������
                    LogDebug("�յ���Ŀ����Ϸ״̬Ϊ�ȴ��У����غ�״̬");
                    if (hasReceivedTurnChange && isMyTurn)
                    {
                        LogDebug("�غ�״̬ȷ��Ϊ�ҵĻغϣ�������Ŀ");
                        currentDisplayState = QuestionDisplayState.MyTurn;
                        isWaitingForNetworkQuestion = false;
                        LoadNetworkQuestion(question);
                    }
                    else if (hasReceivedTurnChange && !isMyTurn)
                    {
                        LogDebug("�غ�״̬ȷ��Ϊ������һغϣ��۲�ģʽ������Ŀ");
                        currentDisplayState = QuestionDisplayState.OtherPlayerTurn;
                        LoadNetworkQuestionAsObserver(question);
                    }
                    else
                    {
                        LogDebug("��δ�յ��غϱ����Ϣ��������Ŀ");
                        // ����ѡ�񻺴���Ŀ�����
                        currentNetworkQuestion = question;
                    }
                    break;

                default:
                    LogDebug($"δ֪״̬ {currentDisplayState}��������Ŀ");
                    break;
            }
        }

        /// <summary>
        /// ���յ�����𰸽��
        /// </summary>
        private void OnNetworkAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!isMultiplayerMode)
                return;

            LogDebug($"�յ�������������: {(isCorrect ? "��ȷ" : "����")}");

            // ���������仯
            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // ����Ƿ���Ϸ����
                if (hpManager.CurrentHealth <= 0)
                {
                    LogDebug("�����ľ�����Ϸ����");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            // ��ʾ�𰸷���
            ShowAnswerFeedback(isCorrect, correctAnswer);
            OnAnswerCompleted?.Invoke(isCorrect);
        }


        /// <summary>
        /// ��һغϱ�� - �޸��汾�����״̬����
        /// </summary>
        private void OnNetworkPlayerTurnChanged(ushort playerId)
        {
            if (!isMultiplayerMode)
                return;

            bool wasMyTurn = isMyTurn;
            currentTurnPlayerId = playerId;
            isMyTurn = (playerId == NetworkManager.Instance?.ClientId);
            hasReceivedTurnChange = true;

            LogDebug($"�غϱ��: {(isMyTurn ? "�ֵ�����" : $"�ֵ����{playerId}")}");

            // ������ʾ״̬
            if (isMyTurn)
            {
                currentDisplayState = QuestionDisplayState.MyTurn;
                LogDebug("״̬���Ϊ��MyTurn");

                // �ֵ��Ҵ��� - ��������������Ŀ���ȴ�����������
                if (!wasMyTurn)
                {
                    LogDebug("�ֵ��Ҵ��⣬�ȴ�������������Ŀ");
                    isWaitingForNetworkQuestion = true;
                }
            }
            else
            {
                currentDisplayState = QuestionDisplayState.OtherPlayerTurn;
                LogDebug($"״̬���Ϊ��OtherPlayerTurn (���{playerId})");

                // �������ҵĻغ�
                if (wasMyTurn)
                {
                    StopTimer();
                    isWaitingForNetworkQuestion = false;
                }
            }
        }

        /// <summary>
        /// ����Ͽ�����
        /// </summary>
        private void OnNetworkDisconnected()
        {
            if (isMultiplayerMode && gameStarted)
            {
                Debug.LogWarning("[NQMC] ����Ͽ�����Ϸ������");
                StopTimer();
                OnGameEnded?.Invoke(false);
            }
        }

        #endregion

        #region ��Ŀ�������ӿ�

        /// <summary>
        /// �޸ģ��ύ�� - ���״̬��֤
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            // ���״̬��֤
            if (isMultiplayerMode)
            {
                if (currentDisplayState != QuestionDisplayState.MyTurn)
                {
                    LogDebug($"�����ҵĻغϣ��޷��ύ�𰸡���ǰ״̬: {currentDisplayState}");
                    return;
                }

                if (!isMyTurn)
                {
                    LogDebug("�غ�״̬��ƥ�䣬�޷��ύ��");
                    return;
                }

                if (NetworkManager.Instance?.IsConnected == true)
                {
                    NetworkManager.Instance.SubmitAnswer(answer);
                    LogDebug($"�ύ�����: {answer}");
                }
                else
                {
                    LogDebug("����δ���ӣ��޷��ύ��");
                }
            }
            else if (!isMultiplayerMode && currentManager != null)
            {
                currentManager.CheckAnswer(answer);
                LogDebug($"�ύ���ش�: {answer}");
            }
            else
            {
                LogDebug("�޷��ύ�𰸣�ģʽ��ƥ���״̬�쳣");
            }
        }
        /// <summary>
        /// ��ȡ��ǰ��Ŀ���������ݣ�����Ŀ������ʹ�ã�
        /// </summary>
        public NetworkQuestionData GetCurrentNetworkQuestion()
        {
            return currentNetworkQuestion;
        }

        #endregion

        #region ��������

        /// <summary>
        /// ��ʼ��ʱ����ԭ�з��������ּ����ԣ�
        /// </summary>
        private void StartTimer()
        {
            if (timerManager != null)
            {
                timerManager.StartTimer();
                LogDebug("��ʱ���ѿ�ʼ��ʹ��Ĭ��ʱ�����ƣ�");
            }
        }

        /// <summary>
        /// ʹ��ָ��ʱ�����ƿ�ʼ��ʱ��
        /// </summary>
        private void StartTimer(float timeLimit)
        {
            StartTimerWithDynamicLimit(timeLimit);
        }

        /// <summary>
        /// ֹͣ��ʱ��
        /// </summary>
        private void StopTimer()
        {
            if (timerManager != null)
            {
                timerManager.StopTimer();
                LogDebug("��ʱ����ֹͣ");
            }
        }

        /// <summary>
        /// ��ʾ�𰸷���
        /// </summary>
        private void ShowAnswerFeedback(bool isCorrect, string correctAnswer)
        {
            string feedback = isCorrect ? "�ش���ȷ��" : $"�ش������ȷ���ǣ�{correctAnswer}";
            LogDebug($"���ⷴ��: {feedback}");

            // �����ǰ������֧����ʾ��������������Ӧ����
            if (currentManager != null)
            {
                // ����ͨ�������ӿڵ��ù���������ʾ����
                // ����: if (currentManager is INetworkResultDisplayer displayer)
                //           displayer.ShowNetworkResult(isCorrect, correctAnswer);
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NQMC] {message}");
            }
        }

        #endregion

        #region ״̬���� - �����ֶ�


        // �������ֶκ����״̬ö��
        private enum QuestionDisplayState
        {
            WaitingForGame,     // �ȴ���Ϸ��ʼ
            MyTurn,            // �ֵ��Ҵ���
            OtherPlayerTurn,   // ������һغ�
            GameEnded          // ��Ϸ����
        }

        private QuestionDisplayState currentDisplayState = QuestionDisplayState.WaitingForGame;
        #endregion
        /// <summary>
        /// �������Թ۲���ģʽ����������Ŀ
        /// </summary>
        private void LoadNetworkQuestionAsObserver(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] ������Ŀ����Ϊ��");
                return;
            }

            LogDebug($"�Թ۲���ģʽ����������Ŀ: {networkQuestion.questionType}");

            // ����ǰ������
            CleanupCurrentManager();

            // ����������Ŀ����
            currentNetworkQuestion = networkQuestion;

            // ������Ӧ�Ĺ��������۲�ģʽ��
            currentManager = CreateQuestionManager(networkQuestion.questionType, true);

            if (currentManager != null)
            {
                // ���󶨴��ύ�¼���ֻ��ʾ��Ŀ
                LogDebug("�۲���ģʽ�����󶨴��ύ�¼�");

                // �ӳټ�����Ŀ���۲�ģʽ��
                StartCoroutine(DelayedLoadNetworkQuestionAsObserver(networkQuestion));

                LogDebug($"�۲���ģʽ��Ŀ�����������ɹ�: {networkQuestion.questionType}");
            }
            else
            {
                Debug.LogError("[NQMC] �޷�Ϊ�۲���ģʽ����������");
            }
        }

        /// <summary>
        /// �������ӳټ���������Ŀ���۲���ģʽ��
        /// </summary>
        private IEnumerator DelayedLoadNetworkQuestionAsObserver(NetworkQuestionData networkData)
        {
            yield return null;
            if (currentManager != null)
            {
                // ������Ŀ����������ʱ��
                if (currentManager is NetworkQuestionManagerBase networkManager)
                {
                    var loadMethod = networkManager.GetType().GetMethod("LoadNetworkQuestion",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (loadMethod != null)
                    {
                        LogDebug($"�۲���ģʽ��ͨ���������������Ŀ���ط���: {networkData.questionType}");
                        loadMethod.Invoke(networkManager, new object[] { networkData });
                    }
                    else
                    {
                        Debug.LogWarning($"[NQMC] �۲���ģʽ��δ�ҵ�LoadNetworkQuestion������ʹ����ͨ����");
                        currentManager.LoadQuestion();
                    }
                }
                else
                {
                    LogDebug("�۲���ģʽ��ʹ����ͨ���ط���");
                    currentManager.LoadQuestion();
                }

                // �ؼ����۲���ģʽ�²�������ʱ������������ֻ����ʱ��
                LogDebug($"�۲���ģʽ����Ŀ�Ѽ��أ�������������ʱ��");

                // ��ѡ������ֻ����ʱ��������ʾ����ʱ
                StartObserverTimer(networkData.timeLimit);
            }
        }

        /// <summary>
        /// �����������۲��߼�ʱ����ֻ��ģʽ��
        /// </summary>
        private void StartObserverTimer(float timeLimit)
        {
            if (timerManager != null)
            {
                // ���TimerManager�Ƿ�֧��ֻ��ģʽ
                var startReadOnlyMethod = timerManager.GetType().GetMethod("StartReadOnlyTimer");
                if (startReadOnlyMethod != null)
                {
                    LogDebug($"����ֻ����ʱ��: {timeLimit}��");
                    startReadOnlyMethod.Invoke(timerManager, new object[] { timeLimit });
                }
                else
                {
                    // �����֧��ֻ��ģʽ������ѡ��������ʱ��
                    LogDebug("TimerManager��֧��ֻ��ģʽ��������ʱ������");
                }
            }
        }

        public string GetStatusInfo()
        {
            var info = "=== NQMC ״̬ ===\n";
            info += $"�ѳ�ʼ��: {isInitialized}\n";
            info += $"��Ϸ�ѿ�ʼ: {gameStarted}\n";
            info += $"����ģʽ: {isMultiplayerMode}\n";
            info += $"�ҵĻغ�: {isMyTurn}\n";
            info += $"�ȴ�������Ŀ: {isWaitingForNetworkQuestion}\n";
            info += $"��ʾ״̬: {currentDisplayState}\n";  // ����
            info += $"��ǰ�غ����ID: {currentTurnPlayerId}\n";  // ����
            info += $"���յ��غϱ��: {hasReceivedTurnChange}\n";  // ����
            info += $"��ǰ������: {(currentManager != null ? currentManager.GetType().Name : "��")}\n";

            if (hpManager != null)
                info += $"��ǰ����: {hpManager.CurrentHealth}\n";

            if (timerManager != null)
                info += $"��ʱ��״̬: {(timerManager.IsRunning ? "������" : "��ֹͣ")}\n";

            return info;
        }

        /// <summary>
        /// ��������״̬�������ã�
        /// </summary>
        [ContextMenu("����״̬")]
        public void ResetState()
        {
            if (Application.isPlaying)
            {
                LogDebug("����״̬");
                StopGame();
                CleanupCurrentManager();
                currentNetworkQuestion = null;
                isWaitingForNetworkQuestion = false;
            }
        }
    }
}