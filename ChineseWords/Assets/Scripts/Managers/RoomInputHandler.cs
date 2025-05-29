using UnityEngine;
using UnityEngine.SceneManagement;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// �������봦����
    /// ר�Ŵ����䳡���еĿ�ݼ�����
    /// </summary>
    public class RoomInputHandler : MonoBehaviour
    {
        [Header("��ݼ�����")]
        [SerializeField] private KeyCode configUIKey = KeyCode.F1;
        [SerializeField] private KeyCode debugInfoKey = KeyCode.F2;
        [SerializeField] private KeyCode playerListKey = KeyCode.F3;

        [Header("��������")]
        [SerializeField] private string[] validScenes = { "RoomScene", "LobbyScene" };
        [SerializeField] private bool onlyInValidScenes = true;

        [Header("Ȩ������")]
        [SerializeField] private bool requireHostForConfig = true;
        [SerializeField] private bool showInputHints = true;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // �������
        private RoomConfigManager configManager;
        private bool isInitialized = false;

        // ����״̬
        private bool inputEnabled = true;
        private float lastInputTime = 0f;
        private const float INPUT_COOLDOWN = 0.2f; // ��ֹ�ظ�����

        private void Start()
        {
            InitializeInputHandler();
        }

        private void Update()
        {
            if (isInitialized && inputEnabled && CanProcessInput())
            {
                HandleKeyboardInput();
            }
        }

        private void OnEnable()
        {
            // ���ĳ����л��¼�
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            // ȡ������
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// ��ʼ�����봦����
        /// </summary>
        private void InitializeInputHandler()
        {
            LogDebug("��ʼ���������봦����");

            // �������ù�����
            FindConfigManager();

            // ��֤��ǰ����
            if (onlyInValidScenes && !IsInValidScene())
            {
                LogDebug("��ǰ����������Ч�����б��У��������봦��");
                inputEnabled = false;
                return;
            }

            isInitialized = true;
            LogDebug("�������봦������ʼ�����");

            // ��ʾ������ʾ
            if (showInputHints)
            {
                ShowInputHints();
            }
        }

        /// <summary>
        /// �������ù�����
        /// </summary>
        private void FindConfigManager()
        {
            configManager = RoomConfigManager.Instance;

            if (configManager == null)
            {
                configManager = FindObjectOfType<RoomConfigManager>();
            }

            if (configManager == null)
            {
                LogDebug("δ�ҵ�RoomConfigManager������UI���ܽ�������");
            }
            else
            {
                LogDebug("�ҵ�RoomConfigManager");
            }
        }

        /// <summary>
        /// ����Ƿ���Դ�������
        /// </summary>
        private bool CanProcessInput()
        {
            // ���������ȴ
            if (Time.time - lastInputTime < INPUT_COOLDOWN)
                return false;

            // ���UI״̬�����������UI�򿪣�������Ҫ�������룩
            if (configManager != null && configManager.IsUIOpen)
                return false;

            return true;
        }

        /// <summary>
        /// �����������
        /// </summary>
        private void HandleKeyboardInput()
        {
            // F1 - ����UI
            if (Input.GetKeyDown(configUIKey))
            {
                HandleConfigUIToggle();
                lastInputTime = Time.time;
            }

            // F2 - ������Ϣ
            if (Input.GetKeyDown(debugInfoKey))
            {
                HandleDebugInfoToggle();
                lastInputTime = Time.time;
            }

            // F3 - ����б�
            if (Input.GetKeyDown(playerListKey))
            {
                HandlePlayerListToggle();
                lastInputTime = Time.time;
            }
        }

        /// <summary>
        /// ��������UI�л�
        /// </summary>
        private void HandleConfigUIToggle()
        {
            if (configManager == null)
            {
                configManager = FindObjectOfType<RoomConfigManager>();
            }

            if (configManager != null)
            {
                configManager.HandleConfigUIToggle();
            }
            else
            {
                LogDebug("���ù����������ڣ��޷�������UI");
                ShowMessage("���ù��ܲ�����");
            }
        }

        /// <summary>
        /// ���������Ϣ�л�
        /// </summary>
        private void HandleDebugInfoToggle()
        {
            LogDebug($"��⵽ {debugInfoKey} ����");

            // ��ʾ��ǰ��Ϸ״̬��Ϣ
            ShowGameStatusInfo();
        }

        /// <summary>
        /// ��������б��л�
        /// </summary>
        private void HandlePlayerListToggle()
        {
            LogDebug($"��⵽ {playerListKey} ����");

            // ��ʾ��ǰ��������б�
            ShowPlayerListInfo();
        }

        /// <summary>
        /// ��鵱ǰ����Ƿ�Ϊ����
        /// </summary>
        private bool IsCurrentPlayerHost()
        {
            // ���NetworkManager
            if (NetworkManager.Instance != null)
            {
                bool isHost = NetworkManager.Instance.IsHost;
                LogDebug($"NetworkManager��� - IsHost: {isHost}");
                return isHost;
            }

            // ���RoomManager
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;

                // ��ȡ��ǰ���ID - ��Ҫ��NetworkManager�������ط���ȡ
                ushort currentPlayerId = GetCurrentPlayerId();

                bool isRoomHost = (currentPlayerId != 0 && currentPlayerId == room.hostId);
                LogDebug($"RoomManager��� - CurrentPlayerId: {currentPlayerId}, HostId: {room.hostId}, IsHost: {isRoomHost}");

                return isRoomHost;
            }

            // ����ģʽ����
            if (Application.isEditor)
            {
                LogDebug("�༭��ģʽ�������������");
                return true;
            }

            LogDebug("�޷�ȷ��������ݣ�Ĭ�Ͼܾ�");
            return false;
        }

        /// <summary>
        /// ��ȡ��ǰ���ID
        /// </summary>
        private ushort GetCurrentPlayerId()
        {
            // ���ȴ�NetworkManager��ȡ
            if (NetworkManager.Instance != null)
            {
                return NetworkManager.Instance.ClientId;
            }

            // ���NetworkManager�����ã�����������ʽ
            // ���������Ҫ������ľ���ʵ�ֵ���

            LogDebug("�޷���ȡ��ǰ���ID");
            return 0;
        }

        /// <summary>
        /// ����Ƿ�����Ч������
        /// </summary>
        private bool IsInValidScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;

            foreach (string validScene in validScenes)
            {
                if (currentScene.Contains(validScene))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// �������ػص�
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LogDebug($"�����л�: {scene.name}");

            // ������֤�����ͳ�ʼ��
            if (onlyInValidScenes)
            {
                inputEnabled = IsInValidScene();
                LogDebug($"���봦��״̬: {(inputEnabled ? "����" : "����")}");
            }

            // ���²������ù��������������³����У�
            if (inputEnabled)
            {
                FindConfigManager();
            }
        }

        /// <summary>
        /// ��ʾ������ʾ
        /// </summary>
        private void ShowInputHints()
        {
            var hints = $"=== �����ݼ� ===\n";
            hints += $"{configUIKey}: ��Ϸ���� (������)\n";
            hints += $"{debugInfoKey}: ������Ϣ\n";
            hints += $"{playerListKey}: ����б�\n";

            LogDebug(hints);

            // ������������ʾUI��ʾ
            // UIMessageManager.ShowMessage(hints, 5f);
        }

        /// <summary>
        /// ��ʾ��Ϸ״̬��Ϣ
        /// </summary>
        private void ShowGameStatusInfo()
        {
            var info = "=== ��Ϸ״̬��Ϣ ===\n";

            // ��ǰ����
            info += $"��ǰ����: {SceneManager.GetActiveScene().name}\n";

            // ����״̬
            if (NetworkManager.Instance != null)
            {
                info += $"����״̬: {(NetworkManager.Instance.IsConnected ? "������" : "δ����")}\n";
                info += $"�Ƿ�Ϊ����: {NetworkManager.Instance.IsHost}\n";
                info += $"�ͻ���ID: {NetworkManager.Instance.ClientId}\n";
                info += $"���������: {NetworkManager.Instance.ConnectedPlayerCount}\n";
            }
            else
            {
                info += "NetworkManager: δ�ҵ�\n";
            }

            // ����״̬
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                info += $"������: {room.roomName}\n";
                info += $"���������: {room.players.Count}\n";
                info += $"����ID: {room.hostId}\n";
            }
            else
            {
                info += "����״̬: δ���뷿��\n";
            }

            // ���ù�����״̬
            if (configManager != null)
            {
                info += $"���ù�����: �ѳ�ʼ��\n";
                info += $"����UI״̬: {(configManager.IsUIOpen ? "�Ѵ�" : "�ѹر�")}\n";
            }
            else
            {
                info += "���ù�����: δ�ҵ�\n";
            }

            LogDebug(info);
            ShowMessage("������Ϣ�����������̨");
        }

        /// <summary>
        /// ��ʾ����б���Ϣ
        /// </summary>
        private void ShowPlayerListInfo()
        {
            var info = "=== ��ǰ����б� ===\n";

            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;

                if (room.players.Count == 0)
                {
                    info += "������û�����\n";
                }
                else
                {
                    foreach (var player in room.players.Values)
                    {
                        string hostFlag = (player.playerId == room.hostId) ? " [����]" : "";
                        info += $"- {player.playerName} (ID: {player.playerId}){hostFlag}\n";
                    }
                }
            }
            else
            {
                info += "δ�����κη���\n";
            }

            LogDebug(info);
            ShowMessage("����б������������̨");
        }

        /// <summary>
        /// ��ʾ��Ϣ���û�
        /// </summary>
        private void ShowMessage(string message)
        {
            LogDebug($"�û���Ϣ: {message}");

            // ������Լ���ʵ�ʵ�UI��Ϣϵͳ
            // ���磺UIMessageManager.ShowMessage(message, 3f);
            // ���ߣ�ToastManager.Show(message);

            // ��ʱ�ڿ���̨��ʾ
            Debug.Log($"[�û���ʾ] {message}");
        }

        /// <summary>
        /// �������봦��
        /// </summary>
        public void EnableInput()
        {
            inputEnabled = true;
            LogDebug("���봦��������");
        }

        /// <summary>
        /// �������봦��
        /// </summary>
        public void DisableInput()
        {
            inputEnabled = false;
            LogDebug("���봦���ѽ���");
        }

        /// <summary>
        /// �л����봦��״̬
        /// </summary>
        public void ToggleInput()
        {
            inputEnabled = !inputEnabled;
            LogDebug($"���봦����{(inputEnabled ? "����" : "����")}");
        }

        /// <summary>
        /// ��������UI��ݼ�
        /// </summary>
        public void SetConfigUIKey(KeyCode keyCode)
        {
            configUIKey = keyCode;
            LogDebug($"����UI��ݼ�������Ϊ: {keyCode}");
        }

        /// <summary>
        /// ��ȡ��ǰ����״̬
        /// </summary>
        public bool IsInputEnabled()
        {
            return inputEnabled && isInitialized;
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomInputHandler] {message}");
            }
        }

        #region �����ӿ�

        /// <summary>
        /// �ֶ���������UI
        /// </summary>
        public void TriggerConfigUI()
        {
            HandleConfigUIToggle();
        }

        /// <summary>
        /// �ֶ���ʾ������Ϣ
        /// </summary>
        public void TriggerDebugInfo()
        {
            HandleDebugInfoToggle();
        }

        /// <summary>
        /// �ֶ���ʾ����б�
        /// </summary>
        public void TriggerPlayerList()
        {
            HandlePlayerListToggle();
        }

        /// <summary>
        /// ��ȡ��ǰ��ݼ�����
        /// </summary>
        public KeyCode GetConfigUIKey()
        {
            return configUIKey;
        }

        #endregion

        #region ���Է���

#if UNITY_EDITOR
        [ContextMenu("��������UI")]
        public void TestConfigUI()
        {
            TriggerConfigUI();
        }

        [ContextMenu("��ʾ������Ϣ")]
        public void TestDebugInfo()
        {
            TriggerDebugInfo();
        }

        [ContextMenu("��ʾ����б�")]
        public void TestPlayerList()
        {
            TriggerPlayerList();
        }

        [ContextMenu("��ʾ������ʾ")]
        public void TestInputHints()
        {
            ShowInputHints();
        }
#endif

        #endregion
    }
}