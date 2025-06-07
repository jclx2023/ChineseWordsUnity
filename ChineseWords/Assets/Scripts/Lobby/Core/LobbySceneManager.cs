using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Core.Network;
using Lobby.Data;
using Lobby.UI;

namespace Lobby.Core
{
    /// <summary>
    /// Lobby�����ܹ�����
    /// ���𳡾��ĳ�ʼ�������ݹ���ͳ����л�
    /// </summary>
    public class LobbySceneManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";
        [SerializeField] private string roomSceneName = "RoomScene";

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static LobbySceneManager Instance { get; private set; }

        // �������
        private LobbyPlayerData currentPlayerData;

        // ����״̬
        private bool isInitialized = false;

        #region Unity��������

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("LobbySceneManager ʵ���Ѵ���");
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
            StartCoroutine(InitializeSceneCoroutine());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
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

            // ��ʼ��UIϵͳ
            InitializeUISystem();

            isInitialized = true;
            LogDebug("Lobby������ʼ�����");
        }

        /// <summary>
        /// ��ʼ���������
        /// </summary>
        private void InitializePlayerData()
        {
            currentPlayerData = new LobbyPlayerData();

            // ��PlayerPrefs�����������
            string savedPlayerName = PlayerPrefs.GetString("PlayerName", "");
            if (string.IsNullOrEmpty(savedPlayerName))
            {
                currentPlayerData.playerName = $"���{Random.Range(1000, 9999)}";
            }
            else
            {
                currentPlayerData.playerName = savedPlayerName;
            }

            LogDebug($"������ݳ�ʼ�����: {currentPlayerData.playerName}");
        }

        /// <summary>
        /// �ȴ����������׼������
        /// </summary>
        private IEnumerator WaitForNetworkManager()
        {
            LogDebug("�ȴ����������׼������...");

            int waitFrames = 0;
            const int maxWaitFrames = 300; // 5�볬ʱ

            while (LobbyNetworkManager.Instance == null && waitFrames < maxWaitFrames)
            {
                yield return null;
                waitFrames++;
            }

            if (LobbyNetworkManager.Instance == null)
            {
                Debug.LogError("[LobbySceneManager] ���������׼����ʱ");
            }
            else
            {
                LogDebug($"���������׼���������ȴ��� {waitFrames} ֡");
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
            }
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
        /// ��������ǳ�
        /// </summary>
        public void UpdatePlayerName(string newName)
        {
            if (string.IsNullOrEmpty(newName) || newName.Length < 2)
            {
                LogDebug("��Ч������ǳ�");
                return;
            }

            currentPlayerData.playerName = newName;
            PlayerPrefs.SetString("PlayerName", newName);
            PlayerPrefs.Save();

            LogDebug($"����ǳ��Ѹ���: {newName}");
        }

        /// <summary>
        /// �������˵�
        /// </summary>
        public void BackToMainMenu()
        {
            LogDebug("�������˵�");

            // �Ͽ���������
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.Disconnect();
            }

            // �л�����
            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>
        /// ���뷿�䳡����Ϊ�����׶�Ԥ����
        /// </summary>
        public void EnterRoomScene(LobbyRoomData roomData)
        {
            LogDebug($"���뷿�䳡��: {roomData.roomName}");

            // TODO: �׶�3ʵ�� - ���ݷ������ݵ�RoomScene
            // Ŀǰ��ʱֻ�����־
            LogDebug("���뷿�书�ܽ��ں����׶�ʵ��");
        }

        /// <summary>
        /// ��鳡���Ƿ��ѳ�ʼ��
        /// </summary>
        public bool IsSceneInitialized()
        {
            return isInitialized;
        }

        #endregion

        #region ���Է���

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

        [ContextMenu("��ʾ����״̬")]
        public void ShowSceneStatus()
        {
            string status = "=== Lobby����״̬ ===\n";
            status += $"�����ѳ�ʼ��: {isInitialized}\n";
            status += $"��ǰ���: {currentPlayerData?.playerName ?? "δ����"}\n";
            status += $"���������: {(LobbyNetworkManager.Instance != null ? "����" : "δ����")}\n";
            status += $"UI������: {(LobbyUIManager.Instance != null ? "����" : "δ����")}";

            LogDebug(status);
        }

        #endregion
    }
}