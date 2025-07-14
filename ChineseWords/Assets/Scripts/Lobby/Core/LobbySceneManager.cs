using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Core;
using Core.Network;
using Lobby.Data;
using Lobby.UI;
using Photon.Pun;

namespace Lobby.Core
{
    /// <summary>
    /// Lobby�����ܹ����� - ����ʵ�ְ�
    /// ���𳡾��ĳ�ʼ�������ݹ��������¼���Ӧ�ͳ����л�
    /// </summary>
    public class LobbySceneManager : MonoBehaviourPun
    {
        [Header("��������")]
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";
        [SerializeField] private string roomSceneName = "RoomScene";
        [SerializeField] private float roomJoinTransitionDelay = 0.2f;

        [Header("���ݳ־û�")]
        [SerializeField] private bool persistPlayerData = true;
        [SerializeField] private bool autoSavePlayerPrefs = true;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static LobbySceneManager Instance { get; private set; }

        // �������
        private LobbyPlayerData currentPlayerData;
        private LobbyRoomData lastJoinedRoomData; // ����������ķ�������

        // ����״̬
        private bool isInitialized = false;
        private bool isTransitioningToRoom = false;

        // �����¼�״̬
        private bool hasSubscribedToNetworkEvents = false;

        #region Unity��������

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("LobbySceneManager ʵ���Ѵ���");

                // ����Ϊ�־û������Ա��ڳ����л�ʱ��������
                if (persistPlayerData)
                {
                    DontDestroyOnLoad(gameObject);
                    LogDebug("LobbySceneManager ����Ϊ�糡���־û�");
                }
            }
            else
            {
                LogDebug("�����ظ���LobbySceneManagerʵ��");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // ��ʱ���ԣ���֤�¼�����
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.OnRoomJoined += OnRoomJoinedHandler;
                LogDebug("�Ѷ���LobbyNetworkManager.OnRoomJoined�¼�");
            }
            else
            {
                LogDebug("LobbyNetworkManager.InstanceΪ�գ�");
            }
            StartCoroutine(InitializeSceneCoroutine());
        }

        private void OnDestroy()
        {
            // ȡ�����������¼�
            UnsubscribeFromNetworkEvents();

            if (Instance == this)
            {
                Instance = null;
            }

            LogDebug("LobbySceneManager ������");
        }

        #endregion

        #region ������ʼ��

        /// <summary>
        /// ������ʼ��Э��
        /// </summary>
        private IEnumerator InitializeSceneCoroutine()
        {
            LogDebug("��ʼ��ʼ��Lobby����");

            // ��ʼ���������
            InitializePlayerData();

            // �ȴ����������׼������
            yield return StartCoroutine(WaitForNetworkManager());

            NotifyPlayerDataReady();

            // ���������¼�
            SubscribeToNetworkEvents();

            // ��ʼ��UIϵͳ
            InitializeUISystem();

            isInitialized = true;
            LogDebug("Lobby������ʼ�����");
        }

        private void NotifyPlayerDataReady()
        {
            if (LobbyNetworkManager.Instance != null && currentPlayerData != null)
            {
                // ֱ�Ӹ���LobbyNetworkManager�����������
                LobbyNetworkManager.Instance.SetPlayerName(currentPlayerData.playerName);
                LogDebug($"��֪ͨLobbyNetworkManager�����������: '{currentPlayerData.playerName}'");
            }
            else
            {
                LogDebug("�޷�֪ͨLobbyNetworkManager��ʵ�������ڻ��������Ϊ��");
            }
        }

        /// <summary>
        /// ��ʼ���������
        /// </summary>
        private void InitializePlayerData()
        {
            LogDebug("��ʼ���������");

            // �����µ�������ݻ�ӳ־û����ݻָ�
            if (currentPlayerData == null)
            {
                currentPlayerData = new LobbyPlayerData();

                // ��PlayerPrefs���ر�����������
                string savedPlayerName = PlayerPrefs.GetString("PlayerName", "");
                if (string.IsNullOrEmpty(savedPlayerName))
                {
                    currentPlayerData.playerName = GenerateRandomPlayerName();
                    LogDebug($"������������: {currentPlayerData.playerName}");
                }
                else
                {
                    currentPlayerData.playerName = savedPlayerName;
                    LogDebug($"��PlayerPrefs�ָ������: {currentPlayerData.playerName}");
                }

                // ������ҵ�ΨһID
                currentPlayerData.playerId = GetOrCreatePlayerId();

                // �Զ�����
                if (autoSavePlayerPrefs)
                {
                    SavePlayerDataToPrefs();
                }
            }
            else
            {
                LogDebug($"ʹ�������������: {currentPlayerData.playerName}");
            }

            LogDebug($"������ݳ�ʼ����� - ����: {currentPlayerData.playerName}, ID: {currentPlayerData.playerId}");
        }

        /// <summary>
        /// �ȴ����������׼������
        /// </summary>
        private IEnumerator WaitForNetworkManager()
        {
            LogDebug("�ȴ�LobbyNetworkManager׼������...");

            int waitFrames = 0;
            const int maxWaitFrames = 300; // 5�볬ʱ

            while (LobbyNetworkManager.Instance == null && waitFrames < maxWaitFrames)
            {
                yield return null;
                waitFrames++;
            }

            if (LobbyNetworkManager.Instance == null)
            {
                Debug.LogError("[LobbySceneManager] LobbyNetworkManager׼����ʱ������LobbyNetworkManager�Ƿ�����ڳ�����");
            }
            else
            {
                LogDebug($"LobbyNetworkManager׼���������ȴ��� {waitFrames} ֡");
            }
        }

        /// <summary>
        /// ��ʼ��UIϵͳ
        /// </summary>
        private void InitializeUISystem()
        {
            LogDebug("��ʼ��UIϵͳ");

            // ���������ϢUI
            if (LobbyUIManager.Instance != null)
            {
                LobbyUIManager.Instance.UpdatePlayerInfo(currentPlayerData);
                LogDebug("�Ѹ���LobbyUIManager�������Ϣ");
            }
        }

        #endregion

        #region �����¼�����

        /// <summary>
        /// ���������¼�
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            if (hasSubscribedToNetworkEvents)
            {
                LogDebug("�Ѿ������������¼��������ظ�����");
                return;
            }

            if (LobbyNetworkManager.Instance != null)
            {
                // ���ķ�������¼�
                LobbyNetworkManager.Instance.OnRoomJoined += OnRoomJoinedHandler;
                LobbyNetworkManager.Instance.OnRoomCreated += OnRoomCreatedHandler;
                LobbyNetworkManager.Instance.OnConnectionStatusChanged += OnConnectionStatusChangedHandler;

                hasSubscribedToNetworkEvents = true;
                LogDebug("�Ѷ���LobbyNetworkManager�¼�");
            }
            else
            {
                Debug.LogError("[LobbySceneManager] �޷������¼���LobbyNetworkManagerʵ��������");
            }
        }

        /// <summary>
        /// ȡ�����������¼�
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            if (!hasSubscribedToNetworkEvents)
                return;

            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.OnRoomJoined -= OnRoomJoinedHandler;
                LobbyNetworkManager.Instance.OnRoomCreated -= OnRoomCreatedHandler;
                LobbyNetworkManager.Instance.OnConnectionStatusChanged -= OnConnectionStatusChangedHandler;

                LogDebug("��ȡ������LobbyNetworkManager�¼�");
            }

            hasSubscribedToNetworkEvents = false;
        }

        #endregion

        #region �����¼�������

        /// <summary>
        /// �������ɹ��¼������� - ����ʵ��
        /// </summary>
        private void OnRoomJoinedHandler(string roomName, bool success)
        {
            LogDebug($"�յ���������¼� - ����: {roomName}, �ɹ�: {success}");

            if (success)
            {
                LogDebug($"�ɹ����뷿��: {roomName}��׼���л���RoomScene");

                // ���淿����Ϣ����Photon��ȡ�������ݣ�
                CacheCurrentRoomData();

                // ����Photon�������
                SetPhotonPlayerProperties();

                // ��ʼ�����л�����
                StartRoomSceneTransition();
            }
            else
            {
                Debug.LogError($"[LobbySceneManager] ���뷿��ʧ��: {roomName}");

                // ��ʾ������ʾ
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ShowMessage($"���뷿�� '{roomName}' ʧ�ܣ�������");
                }
            }
        }

        /// <summary>
        /// ���䴴���ɹ��¼�������
        /// </summary>
        private void OnRoomCreatedHandler(string roomName, bool success)
        {
            LogDebug($"�յ����䴴���¼� - ����: {roomName}, �ɹ�: {success}");

            if (success)
            {
                LogDebug($"�ɹ���������: {roomName}���ȴ��Զ�����");
                // ���䴴���ɹ�����Զ����룬��OnRoomJoinedHandler�����������
            }
            else
            {
                Debug.LogError($"[LobbySceneManager] ��������ʧ��: {roomName}");

                // ��ʾ������ʾ
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ShowMessage($"�������� '{roomName}' ʧ�ܣ�������");
                }
            }
        }

        /// <summary>
        /// ����״̬�仯�¼�������
        /// </summary>
        private void OnConnectionStatusChangedHandler(bool isConnected)
        {
            LogDebug($"��������״̬�仯: {isConnected}");

            if (!isConnected)
            {
                // ���ӶϿ�ʱ����״̬
                isTransitioningToRoom = false;
                lastJoinedRoomData = null;

                LogDebug("�������ӶϿ������÷������״̬");
            }
        }

        #endregion

        #region ���䳡���л�����

        /// <summary>
        /// ��ʼ���䳡���л�����
        /// </summary>
        private void StartRoomSceneTransition()
        {
            if (isTransitioningToRoom)
            {
                LogDebug("�����л������䳡���������У������ظ��л�");
                return;
            }

            if (SceneTransitionManager.IsTransitioning)
            {
                LogDebug("SceneTransitionManager�����л��������ȴ����");
                return;
            }

            LogDebug("��ʼ���䳡���л�����");
            isTransitioningToRoom = true;

            // ʹ��SceneTransitionManager���а�ȫ�ĳ����л�
            bool transitionStarted = SceneTransitionManager.Instance?.TransitionToScene(
                roomSceneName,
                roomJoinTransitionDelay,
                "LobbySceneManager.OnRoomJoined"
            ) ?? false;

            if (transitionStarted)
            {
                LogDebug($"�����л������������� {roomJoinTransitionDelay} ����л��� {roomSceneName}");

                // ��ʾ�л���ʾ
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ShowMessage($"���ڽ��뷿��...");
                }
            }
            else
            {
                Debug.LogError("[LobbySceneManager] �����л�����ʧ��");
                isTransitioningToRoom = false;

                // ��ʾ������ʾ
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ShowMessage("���뷿��ʧ�ܣ�������");
                }
            }
        }

        /// <summary>
        /// ���浱ǰ��������
        /// </summary>
        private void CacheCurrentRoomData()
        {
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("����Photon�����У��޷����淿������");
                return;
            }

            var currentRoom = PhotonNetwork.CurrentRoom;
            LogDebug($"���淿������: {currentRoom.Name}");

            // ��Photon���䴴��LobbyRoomData
            lastJoinedRoomData = LobbyRoomData.FromPhotonRoom(currentRoom);

            if (lastJoinedRoomData != null)
            {
                LogDebug($"�������ݻ���ɹ� - ����: {lastJoinedRoomData.roomName}, ���: {lastJoinedRoomData.currentPlayers}/{lastJoinedRoomData.maxPlayers}");
            }
            else
            {
                Debug.LogError("[LobbySceneManager] ��Photon���䴴��LobbyRoomDataʧ��");
            }
        }

        /// <summary>
        /// ����Photon�������
        /// </summary>
        private void SetPhotonPlayerProperties()
        {
            if (!PhotonNetwork.InRoom || currentPlayerData == null)
            {
                LogDebug("�޷�����Photon������ԣ����ڷ����л��������Ϊ��");
                return;
            }

            LogDebug($"����Photon�������: {currentPlayerData.playerName}");

            // ��������ǳ�
            PhotonNetwork.LocalPlayer.NickName = currentPlayerData.playerName;

            // �����Զ����������
            var playerProps = new ExitGames.Client.Photon.Hashtable();
            playerProps["playerName"] = currentPlayerData.playerName;
            playerProps["playerId"] = currentPlayerData.playerId;
            playerProps["joinTime"] = System.DateTime.Now.ToBinary();

            PhotonNetwork.LocalPlayer.SetCustomProperties(playerProps);

            LogDebug("Photon��������������");
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ��ȡ��ǰ�������
        /// </summary>
        public LobbyPlayerData GetCurrentPlayerData()
        {
            return currentPlayerData;
        }

        /// <summary>
        /// ��ȡ������ķ�������
        /// </summary>
        public LobbyRoomData GetLastJoinedRoomData()
        {
            return lastJoinedRoomData;
        }

        /// <summary>
        /// ��������ǳ�
        /// </summary>
        public void UpdatePlayerName(string newName)
        {
            if (string.IsNullOrEmpty(newName) || newName.Length < 2)
            {
                LogDebug($"��Ч������ǳ�: '{newName}'");
                return;
            }

            // ��������ǳ�
            newName = newName.Trim();
            if (newName.Length > 20)
            {
                newName = newName.Substring(0, 20);
            }

            string oldName = currentPlayerData.playerName;
            currentPlayerData.playerName = newName;

            // ���浽PlayerPrefs
            if (autoSavePlayerPrefs)
            {
                SavePlayerDataToPrefs();
            }

            // �ؼ���ͬʱ֪ͨLobbyNetworkManager����
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.SetPlayerName(newName);
                LogDebug($"��֪ͨLobbyNetworkManager�����������: '{newName}'");
            }

            LogDebug($"����ǳ��Ѹ���: '{oldName}' -> '{newName}'");

            // ����UI
            if (LobbyUIManager.Instance != null)
            {
                LobbyUIManager.Instance.UpdatePlayerInfo(currentPlayerData);
            }
        }

        /// <summary>
        /// �������˵�
        /// </summary>
        public void BackToMainMenu()
        {
            LogDebug("�������˵�");

            // ����״̬
            isTransitioningToRoom = false;
            lastJoinedRoomData = null;

            // ʹ��SceneTransitionManager�л�����
            bool transitionStarted = SceneTransitionManager.ReturnToMainMenu("LobbySceneManager.BackToMainMenu");

            if (!transitionStarted)
            {
                // ���÷�����ֱ���л�
                Debug.LogWarning("[LobbySceneManager] SceneTransitionManager�л�ʧ�ܣ�ʹ�ñ��÷���");
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }


        #endregion

        #region ���ݳ־û�

        /// <summary>
        /// ����������ݵ�PlayerPrefs
        /// </summary>
        private void SavePlayerDataToPrefs()
        {
            if (currentPlayerData == null) return;

            PlayerPrefs.SetString("PlayerName", currentPlayerData.playerName);
            PlayerPrefs.SetString("PlayerId", currentPlayerData.playerId);
            PlayerPrefs.Save();

            LogDebug($"��������ѱ��浽PlayerPrefs: {currentPlayerData.playerName}");
        }

        /// <summary>
        /// ������������
        /// </summary>
        private string GenerateRandomPlayerName()
        {
            string[] prefixes = { "ѧ��", "���"};
            string prefix = prefixes[Random.Range(0, prefixes.Length)];
            int number = Random.Range(1000, 9999);
            return $"{prefix}{number}";
        }

        /// <summary>
        /// ��ȡ�򴴽����ID
        /// </summary>
        private string GetOrCreatePlayerId()
        {
            string savedId = PlayerPrefs.GetString("PlayerId", "");
            if (string.IsNullOrEmpty(savedId))
            {
                savedId = System.Guid.NewGuid().ToString();
                PlayerPrefs.SetString("PlayerId", savedId);
                PlayerPrefs.Save();
                LogDebug($"�����µ����ID: {savedId}");
            }
            else
            {
                LogDebug($"ʹ���ѱ�������ID: {savedId}");
            }
            return savedId;
        }

        #endregion

        #region ״̬��ѯ�͵���

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LobbySceneManager] {message}");
            }
        }

        #endregion
    }
}