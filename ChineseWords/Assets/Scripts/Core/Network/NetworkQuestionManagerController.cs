using UnityEngine;
using Core;
using Core.Network;
using System.Collections;
using Managers;
using Photon.Pun;

namespace Core.Network
{
    /// <summary>
    /// ������Ŀ��������� - ������ģʽ��
    /// ר�����ڶ���ģʽ����Ŀ���� + ������Ϣ���� + ���ύ
    /// ���Ƴ�����ģʽ��רע��Photon������Ϸ����
    /// </summary>
    public class NetworkQuestionManagerController : MonoBehaviourPun
    {
        [Header("��������������")]
        [SerializeField] private TimerManager timerManager;

        [Header("��������")]
        [SerializeField] private float timeUpDelay = 1f;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static NetworkQuestionManagerController Instance { get; private set; }

        // ��ǰ״̬
        private QuestionManagerBase currentManager;
        private NetworkQuestionData currentNetworkQuestion;
        private bool isMyTurn = false;
        private bool gameStarted = false;
        private bool isInitialized = false;
        private ushort currentTurnPlayerId = 0;
        private bool hasReceivedTurnChange = false;

        // �¼�
        public System.Action<bool> OnGameEnded;
        public System.Action<bool> OnAnswerCompleted;

        // ����
        public bool IsMyTurn => isMyTurn;
        public bool IsGameStarted => gameStarted;
        public bool IsInitialized => isInitialized;
        public QuestionManagerBase CurrentManager => currentManager;

        // ״̬����ö��
        private enum QuestionDisplayState
        {
            WaitingForGame,     // �ȴ���Ϸ��ʼ
            MyTurn,            // �ֵ��Ҵ���
            OtherPlayerTurn,   // ������һغ�
            GameEnded          // ��Ϸ����
        }

        private QuestionDisplayState currentDisplayState = QuestionDisplayState.WaitingForGame;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("NetworkQuestionManagerController (�����˰�) �����Ѵ���");
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            InitializeComponents();
        }

        private void Start()
        {
            isInitialized = true;
            LogDebug("�����˰�NQMC��ʼ����ɣ��ȴ��ⲿ����ָ��");
        }

        private void Update()
        {
            // ��������״̬�仯
            if (gameStarted)
            {
                if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
                {
                    LogDebug("��⵽Photon���ӶϿ���ֹͣ��Ϸ");
                    StopGame();
                    OnGameEnded?.Invoke(false);
                }
            }
        }

        private void OnDestroy()
        {
            CleanupCurrentManager();

            if (Instance == this)
                Instance = null;

            LogDebug("NetworkQuestionManagerController (�����˰�) ������");
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

            if (timerManager == null)
            {
                Debug.LogError("[NQMC] �Ҳ���TimerManager����");
                return;
            }


            // Ӧ������
            var cfg = ConfigManager.Instance?.Config;
            if (cfg != null)
            {
                timerManager.ApplyConfig(cfg.timeLimit);
            }

            // �󶨼�ʱ���¼�
            timerManager.OnTimeUp += HandleTimeUp;

            LogDebug("���������ʼ�����");
        }

        #region �����ӿ� - �����ⲿϵͳ����

        /// <summary>
        /// ��ʼ������Ϸ�������ⲿϵͳ���ã�
        /// </summary>
        public void StartGame(bool multiplayerMode = true)
        {
            if (!isInitialized)
            {
                Debug.LogError("[NQMC] ����δ��ʼ�����޷���ʼ��Ϸ");
                return;
            }

            // �����Ѻ��ԣ���Զ�Ƕ���ģʽ
            gameStarted = true;
            LogDebug("��ʼ������Ϸ");

            StartMultiplayerGame();
        }

        /// <summary>
        /// ֹͣ��Ϸ - ����״̬
        /// </summary>
        public void StopGame()
        {
            LogDebug("ֹͣ��Ϸ");
            gameStarted = false;
            isMyTurn = false;

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

        #endregion

        #region ������Ϸ����

        /// <summary>
        /// ��ʼ������Ϸ
        /// </summary>
        private void StartMultiplayerGame()
        {
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            {
                LogDebug("����ģʽ���ȴ�HostGameManager����غϺͷ�����Ŀ");
                currentDisplayState = QuestionDisplayState.WaitingForGame;
                hasReceivedTurnChange = false;
            }
            else
            {
                Debug.LogError("[NQMC] δ���ӵ�Photon���䣬�޷���ʼ������Ϸ");
                OnGameEnded?.Invoke(false);
            }
        }

        #endregion

        #region ������Ŀ����

        /// <summary>
        /// ����������Ŀ - �ҵĻغ�
        /// </summary>
        private void LoadNetworkQuestion(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] ������Ŀ����Ϊ��");
                return;
            }

            LogDebug($"����������Ŀ: {networkQuestion.questionType}");

            CleanupCurrentManager();
            currentNetworkQuestion = networkQuestion;

            currentManager = CreateQuestionManager(networkQuestion.questionType, true);

            if (currentManager != null)
            {

                currentManager.OnAnswerResult += HandleNetworkAnswerSubmission;

                StartCoroutine(DelayedLoadNetworkQuestion(networkQuestion));
                LogDebug($"������Ŀ�����������ɹ�: {networkQuestion.questionType}");
            }
            else
            {
                Debug.LogError("[NQMC] �޷�Ϊ������Ŀ����������");
            }
        }

        /// <summary>
        /// �Թ۲���ģʽ����������Ŀ - ������һغ�
        /// </summary>
        private void LoadNetworkQuestionAsObserver(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] ������Ŀ����Ϊ��");
                return;
            }

            LogDebug($"�Թ۲���ģʽ����������Ŀ: {networkQuestion.questionType}");

            CleanupCurrentManager();
            currentNetworkQuestion = networkQuestion;

            currentManager = CreateQuestionManager(networkQuestion.questionType, true);

            if (currentManager != null)
            {
                // �۲���ģʽ�����󶨴��ύ�¼�
                LogDebug("�۲���ģʽ�����󶨴��ύ�¼�");

                StartCoroutine(DelayedLoadNetworkQuestionAsObserver(networkQuestion));
                LogDebug($"�۲���ģʽ��Ŀ�����������ɹ�: {networkQuestion.questionType}");
            }
            else
            {
                Debug.LogError("[NQMC] �޷�Ϊ�۲���ģʽ����������");
            }
        }

        /// <summary>
        /// �ӳټ���������Ŀ - �ҵĻغ�
        /// </summary>
        private IEnumerator DelayedLoadNetworkQuestion(NetworkQuestionData networkData)
        {
            yield return null;
            if (currentManager != null)
            {
                LoadQuestionWithNetworkData(networkData);
                StartTimerWithDynamicLimit(networkData.timeLimit);
                LogDebug($"������Ŀ�Ѽ��ز���ʼ��ʱ��ʱ������: {networkData.timeLimit}��");
            }
        }

        /// <summary>
        /// �ӳټ���������Ŀ���۲���ģʽ��
        /// </summary>
        private IEnumerator DelayedLoadNetworkQuestionAsObserver(NetworkQuestionData networkData)
        {
            yield return null;
            if (currentManager != null)
            {
                LoadQuestionWithNetworkData(networkData);
                StartObserverTimer(networkData.timeLimit);
                LogDebug($"�۲���ģʽ����Ŀ�Ѽ��أ�����ֻ����ʱ��");
            }
        }

        /// <summary>
        /// ʹ���������ݼ�����Ŀ
        /// </summary>
        private void LoadQuestionWithNetworkData(NetworkQuestionData networkData)
        {
            if (currentManager is NetworkQuestionManagerBase networkManager)
            {
                var loadMethod = networkManager.GetType().GetMethod("LoadNetworkQuestion",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (loadMethod != null)
                {
                    LogDebug($"ͨ���������������Ŀ���ط���: {networkData.questionType}");
                    loadMethod.Invoke(networkManager, new object[] { networkData });
                }
                else
                {
                    Debug.LogWarning($"[NQMC] δ�ҵ�LoadNetworkQuestion������ʹ�ñ��ü��ط�ʽ");
                    currentManager.LoadQuestion();
                }
            }
            else
            {
                Debug.LogWarning($"[NQMC] ����������NetworkQuestionManagerBase����: {currentManager.GetType()}");
                currentManager.LoadQuestion();
            }
        }

        /// <summary>
        /// ʹ�ö�̬ʱ������������ʱ��
        /// </summary>
        private void StartTimerWithDynamicLimit(float timeLimit)
        {
            if (timerManager != null)
            {
                var startTimerWithLimitMethod = timerManager.GetType().GetMethod("StartTimer", new System.Type[] { typeof(float) });
                var setTimeLimitMethod = timerManager.GetType().GetMethod("SetTimeLimit");

                if (startTimerWithLimitMethod != null)
                {
                    LogDebug($"ʹ�ö�̬ʱ������������ʱ��: {timeLimit}��");
                    startTimerWithLimitMethod.Invoke(timerManager, new object[] { timeLimit });
                }
                else if (setTimeLimitMethod != null)
                {
                    LogDebug($"����ʱ�����ƺ�������ʱ��: {timeLimit}��");
                    setTimeLimitMethod.Invoke(timerManager, new object[] { timeLimit });
                    timerManager.StartTimer();
                }
                else
                {
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
        /// �����۲��߼�ʱ����ֻ��ģʽ��
        /// </summary>
        private void StartObserverTimer(float timeLimit)
        {
            if (timerManager != null)
            {
                var startReadOnlyMethod = timerManager.GetType().GetMethod("StartReadOnlyTimer");
                if (startReadOnlyMethod != null)
                {
                    LogDebug($"����ֻ����ʱ��: {timeLimit}��");
                    startReadOnlyMethod.Invoke(timerManager, new object[] { timeLimit });
                }
                else
                {
                    LogDebug("TimerManager��֧��ֻ��ģʽ��������ʱ������");
                }
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

                if (TimerConfigManager.Config != null)
                {
                    var setTempTimeMethod = typeof(TimerConfigManager).GetMethod("SetTemporaryTimeLimit");
                    if (setTempTimeMethod != null)
                    {
                        setTempTimeMethod.Invoke(null, new object[] { timeLimit });
                        LogDebug("ͨ��TimerConfigManager������ʱʱ�����Ƴɹ�");
                        return;
                    }

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
            GameObject managerObj = new GameObject($"{questionType}Manager");
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

                currentManager.OnAnswerResult -= HandleNetworkAnswerSubmission;
                QuestionManagerFactory.DestroyManager(currentManager);
                currentManager = null;
            }
        }

        #endregion

        #region ����������

        /// <summary>
        /// ����������ύ
        /// </summary>
        private void HandleNetworkAnswerSubmission(bool isCorrect)
        {
            LogDebug("����ģʽ���ش��⣬�ȴ�������ȷ��");

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

            if (isMyTurn && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            {
                if (NetworkManager.Instance != null)
                {
                    NetworkManager.Instance.SubmitAnswer("");
                    LogDebug("��ʱ���ύ�մ�");
                }
            }
        }

        #endregion

        #region �����¼����� - ��NetworkManager�������

        /// <summary>
        /// ���յ�������Ŀ
        /// </summary>
        public void OnNetworkQuestionReceived(NetworkQuestionData question)
        {
            LogDebug($"�յ�������Ŀ: {question.questionType}, ʱ������: {question.timeLimit}��");
            LogDebug($"��ǰ״̬: �ҵĻغ�={isMyTurn}, �غ�ID={currentTurnPlayerId}");

            switch (currentDisplayState)
            {
                case QuestionDisplayState.MyTurn:
                    LogDebug("�ֵ��Ҵ��⣬����������Ŀ");
                    LoadNetworkQuestion(question);
                    break;

                case QuestionDisplayState.OtherPlayerTurn:
                    LogDebug($"������һغϣ���ʾ��Ŀ�����ɽ��� (��ǰ�غ����: {currentTurnPlayerId})");
                    LoadNetworkQuestionAsObserver(question);
                    break;

                case QuestionDisplayState.WaitingForGame:
                    LogDebug("�յ���Ŀ����Ϸ״̬Ϊ�ȴ��У����غ�״̬");
                    if (hasReceivedTurnChange && isMyTurn)
                    {
                        LogDebug("�غ�״̬ȷ��Ϊ�ҵĻغϣ�������Ŀ");
                        currentDisplayState = QuestionDisplayState.MyTurn;
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
        public void OnNetworkAnswerResult(bool isCorrect, string correctAnswer)
        {
            LogDebug($"�յ�������������: {(isCorrect ? "��ȷ" : "����")}");

            ShowAnswerFeedback(isCorrect, correctAnswer);
            OnAnswerCompleted?.Invoke(isCorrect);
        }

        /// <summary>
        /// ��һغϱ��
        /// </summary>
        public void OnNetworkPlayerTurnChanged(ushort playerId)
        {
            bool wasMyTurn = isMyTurn;
            currentTurnPlayerId = playerId;
            isMyTurn = (playerId == PhotonNetwork.LocalPlayer.ActorNumber);
            hasReceivedTurnChange = true;

            LogDebug($"�غϱ��: {(isMyTurn ? "�ֵ�����" : $"�ֵ����{playerId}")}");

            if (isMyTurn)
            {
                currentDisplayState = QuestionDisplayState.MyTurn;
                LogDebug("״̬���Ϊ��MyTurn");

                if (!wasMyTurn)
                {
                    LogDebug("�ֵ��Ҵ��⣬�ȴ�HostGameManager������Ŀ");
                }
            }
            else
            {
                currentDisplayState = QuestionDisplayState.OtherPlayerTurn;
                LogDebug($"״̬���Ϊ��OtherPlayerTurn (���{playerId})");

                if (wasMyTurn)
                {
                    StopTimer();
                }
            }
        }

        #endregion

        #region ��Ŀ�������ӿ�

        /// <summary>
        /// �ύ��
        /// </summary>
        public void SubmitAnswer(string answer)
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

            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
                LogDebug($"�ύ�����: {answer}");
            }
            else
            {
                LogDebug("����δ���ӻ�NetworkManager�����ã��޷��ύ��");
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
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NQMC-Pure] {message}");
            }
        }

        #endregion

        #region ״̬��ѯ�͵���

        /// <summary>
        /// ��ȡ״̬��Ϣ
        /// </summary>
        public string GetStatusInfo()
        {
            var info = "=== NQMC (�����˰�) ״̬ ===\n";
            info += $"�ѳ�ʼ��: {isInitialized}\n";
            info += $"��Ϸ�ѿ�ʼ: {gameStarted}\n";
            info += $"�ҵĻغ�: {isMyTurn}\n";
            info += $"��ʾ״̬: {currentDisplayState}\n";
            info += $"��ǰ�غ����ID: {currentTurnPlayerId}\n";
            info += $"���յ��غϱ��: {hasReceivedTurnChange}\n";
            info += $"��ǰ������: {(currentManager != null ? currentManager.GetType().Name : "��")}\n";

            // Photon����״̬
            info += $"Photon����: {PhotonNetwork.IsConnected}\n";
            info += $"�ڷ�����: {PhotonNetwork.InRoom}\n";
            info += $"�ҵ�ActorNumber: {(PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0)}\n";
            info += $"�Ƿ�MasterClient: {PhotonNetwork.IsMasterClient}\n";

            if (timerManager != null)
                info += $"��ʱ��״̬: {(timerManager.IsRunning ? "������" : "��ֹͣ")}\n";

            return info;
        }

        /// <summary>
        /// ����״̬�������ã�
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
            }
        }

        /// <summary>
        /// ��ʾ��ǰ״̬�������ã�
        /// </summary>
        [ContextMenu("��ʾ��ǰ״̬")]
        public void ShowCurrentStatus()
        {
            if (Application.isPlaying)
            {
                Debug.Log(GetStatusInfo());
            }
        }

        #endregion
    }
}