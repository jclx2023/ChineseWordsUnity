using UnityEngine;
using Core.Network;

namespace Core
{
    /// <summary>
    /// �������������
    /// �������л�ʱ������ظ�����
    /// </summary>
    public class SceneCleanupController : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        private void Awake()
        {
            CleanupDuplicateNetworkComponents();
        }

        /// <summary>
        /// �����ظ����������
        /// </summary>
        private void CleanupDuplicateNetworkComponents()
        {
            LogDebug("��ʼ�����ظ����������...");

            // ���NetworkManager
            CleanupDuplicateNetworkManagers();

            // ���RoomManager
            CleanupDuplicateRoomManagers();

            // ���HostGameManager
            CleanupDuplicateHostGameManagers();

            LogDebug("��������������");
        }

        /// <summary>
        /// �����ظ���NetworkManager��˫��ܹ������������������Host��
        /// </summary>
        private void CleanupDuplicateNetworkManagers()
        {
            var allNetworkManagers = FindObjectsOfType<NetworkManager>(true);
            LogDebug($"���� {allNetworkManagers.Length} ��NetworkManagerʵ��");

            if (allNetworkManagers.Length <= 1)
            {
                LogDebug("NetworkManager��������");
                return;
            }

            if (allNetworkManagers.Length == 2)
            {
                LogDebug("����2��NetworkManager��ʵʩ˫��ܹ�����");

                NetworkManager serverManager = null;  // ID=0, ��������
                NetworkManager playerHostManager = null;  // ID=1, ���Host

                // ʶ��ͱ������������
                foreach (var nm in allNetworkManagers)
                {
                    bool isInDontDestroy = IsInDontDestroyOnLoad(nm.gameObject);
                    LogDebug($"NetworkManager: {nm.name}, ClientId: {nm.ClientId}, IsHost: {nm.IsHost}, IsConnected: {nm.IsConnected}, InDontDestroy: {isInDontDestroy}");

                    if (nm.ClientId == 0)
                    {
                        serverManager = nm;
                        LogDebug($"ʶ��Ϊ������Manager: {nm.name} (ClientId: {nm.ClientId})");
                    }
                    else if (nm.ClientId == 1 || (nm.IsConnected && isInDontDestroy))
                    {
                        playerHostManager = nm;
                        LogDebug($"ʶ��Ϊ���Host Manager: {nm.name} (ClientId: {nm.ClientId})");
                    }
                }

                // ����˫��ܹ�
                if (serverManager != null && playerHostManager != null)
                {
                    ConfigureDualLayerArchitecture(serverManager, playerHostManager);
                }
                else
                {
                    LogDebug("�޷�ʶ������������Host������ԭ���߼�");
                    CleanupDuplicateNetworkManagersOriginal();
                }
            }
            else
            {
                LogDebug("NetworkManager��������2����ʹ��ԭ�������߼�");
                CleanupDuplicateNetworkManagersOriginal();
            }
        }

        /// <summary>
        /// ����˫��ܹ�
        /// </summary>
        private void ConfigureDualLayerArchitecture(NetworkManager serverManager, NetworkManager playerHostManager)
        {
            LogDebug("����˫��ܹ����������� + ��Ҳ�");

            // ���÷������� (ID=0)
            ConfigureServerLayer(serverManager);

            // ������Ҳ� (ID=1) 
            ConfigurePlayerHostLayer(playerHostManager);

            // ����ȫ������
            SetGlobalNetworkManagerReference(playerHostManager);

            LogDebug("˫��ܹ��������");
        }

        /// <summary>
        /// ���÷������� (ID=0)
        /// </summary>
        private void ConfigureServerLayer(NetworkManager serverManager)
        {
            LogDebug("���÷������� (ID=0)");

            // �������㣺������Ϸ�߼���������UIչʾ
            var serverObj = serverManager.gameObject;

            // ��ӷ��������
            var serverMarker = serverObj.GetComponent<ServerLayerMarker>();
            if (serverMarker == null)
            {
                serverMarker = serverObj.AddComponent<ServerLayerMarker>();
            }

            // ȷ���������㱣�ֻ�Ծ������ʾUI
            serverObj.SetActive(true);

            LogDebug("���������������");
        }

        /// <summary>
        /// �������Host�� (ID=1)
        /// </summary>
        private void ConfigurePlayerHostLayer(NetworkManager playerHostManager)
        {
            LogDebug("�������Host�� (ID=1)");

            // ���Host�㣺������Ϸ����ʾUI
            var playerHostObj = playerHostManager.gameObject;

            // ������Host���
            var playerHostMarker = playerHostObj.GetComponent<PlayerHostLayerMarker>();
            if (playerHostMarker == null)
            {
                playerHostMarker = playerHostObj.AddComponent<PlayerHostLayerMarker>();
            }

            // ȷ�����Host�㱣�ֻ�Ծ
            playerHostObj.SetActive(true);

            LogDebug("���Host���������");
        }

        /// <summary>
        /// ����ȫ��NetworkManager����Ϊ���Host
        /// </summary>
        private void SetGlobalNetworkManagerReference(NetworkManager playerHostManager)
        {
            LogDebug("����ȫ��NetworkManager����Ϊ���Host");

            // ȷ��NetworkManager.Instanceָ�����Host
            UpdateNetworkManagerInstance(playerHostManager);
        }

        /// <summary>
        /// ԭ�е������߼�����Ϊ���ã�
        /// </summary>
        private void CleanupDuplicateNetworkManagersOriginal()
        {
            var allNetworkManagers = FindObjectsOfType<NetworkManager>(true);

            NetworkManager keepInstance = null;
            var duplicates = new System.Collections.Generic.List<NetworkManager>();

            // ���ȱ���Host
            foreach (var nm in allNetworkManagers)
            {
                if (nm.IsHost && keepInstance == null)
                {
                    keepInstance = nm;
                    LogDebug($"����Host NetworkManager: {nm.name} (ClientId: {nm.ClientId})");
                }
                else
                {
                    duplicates.Add(nm);
                }
            }

            // ���û��Host���������ӵ�
            if (keepInstance == null)
            {
                foreach (var nm in allNetworkManagers)
                {
                    if (nm.IsConnected)
                    {
                        keepInstance = nm;
                        duplicates.Remove(nm);
                        LogDebug($"�������ӵ�NetworkManager: {nm.name} (ClientId: {nm.ClientId})");
                        break;
                    }
                }
            }

            // �����ظ�ʵ��
            foreach (var duplicate in duplicates)
            {
                LogDebug($"�����ظ�NetworkManager: {duplicate.name} (ClientId: {duplicate.ClientId})");
                Destroy(duplicate.gameObject);
            }
        }

        /// <summary>
        /// ����NetworkManager��������
        /// </summary>
        private void UpdateNetworkManagerInstance(NetworkManager newInstance)
        {
            // ����ҪNetworkManager���ṩһ������Instance�ķ���
            // ����ͨ������������˽���ֶ�
            try
            {
                var field = typeof(NetworkManager).GetField("Instance",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(null, newInstance);
                    LogDebug($"�ɹ�����NetworkManager.Instance��: {newInstance.name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"����NetworkManager.Instanceʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// �����ظ���RoomManager
        /// </summary>
        private void CleanupDuplicateRoomManagers()
        {
            var allRoomManagers = FindObjectsOfType<RoomManager>(true);
            LogDebug($"���� {allRoomManagers.Length} ��RoomManagerʵ��");

            if (allRoomManagers.Length <= 1) return;

            // ����DontDestroyOnLoad�е�ʵ��
            RoomManager keepInstance = null;
            foreach (var rm in allRoomManagers)
            {
                if (IsInDontDestroyOnLoad(rm.gameObject))
                {
                    keepInstance = rm;
                    break;
                }
            }

            // �����ظ�ʵ��
            foreach (var rm in allRoomManagers)
            {
                if (rm != keepInstance)
                {
                    LogDebug($"�����ظ�RoomManager: {rm.name}");
                    Destroy(rm.gameObject);
                }
            }
        }

        /// <summary>
        /// �����ظ���HostGameManager
        /// </summary>
        private void CleanupDuplicateHostGameManagers()
        {
            var allHostManagers = FindObjectsOfType<HostGameManager>(true);
            LogDebug($"���� {allHostManagers.Length} ��HostGameManagerʵ��");

            if (allHostManagers.Length <= 1) return;

            // ������ǰ�����е�ʵ������DontDestroyOnLoad��
            HostGameManager keepInstance = null;
            foreach (var hm in allHostManagers)
            {
                if (!IsInDontDestroyOnLoad(hm.gameObject))
                {
                    keepInstance = hm;
                    break;
                }
            }

            // �����ظ�ʵ��
            foreach (var hm in allHostManagers)
            {
                if (hm != keepInstance)
                {
                    LogDebug($"�����ظ�HostGameManager: {hm.name}");
                    Destroy(hm.gameObject);
                }
            }
        }

        /// <summary>
        /// ���GameObject�Ƿ���DontDestroyOnLoad��
        /// </summary>
        private bool IsInDontDestroyOnLoad(GameObject obj)
        {
            if (obj == null) return false;

            GameObject temp = new GameObject("TempChecker");
            DontDestroyOnLoad(temp);
            bool isInDontDestroy = obj.scene == temp.scene;
            DestroyImmediate(temp);

            return isInDontDestroy;
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneCleanup] {message}");
            }
        }

        /// <summary>
        /// ��ʾ��ǰ�������״̬
        /// </summary>
        [ContextMenu("��ʾ�������״̬")]
        public void ShowNetworkComponentStatus()
        {
            LogDebug("=== �������״̬ ===");

            var networkManagers = FindObjectsOfType<NetworkManager>(true);
            LogDebug($"NetworkManagerʵ����: {networkManagers.Length}");
            foreach (var nm in networkManagers)
            {
                LogDebug($"  - {nm.name}: ClientId={nm.ClientId}, Connected={nm.IsConnected}, DontDestroy={IsInDontDestroyOnLoad(nm.gameObject)}");
            }

            var roomManagers = FindObjectsOfType<RoomManager>(true);
            LogDebug($"RoomManagerʵ����: {roomManagers.Length}");
            foreach (var rm in roomManagers)
            {
                LogDebug($"  - {rm.name}: DontDestroy={IsInDontDestroyOnLoad(rm.gameObject)}");
            }

            var hostManagers = FindObjectsOfType<HostGameManager>(true);
            LogDebug($"HostGameManagerʵ����: {hostManagers.Length}");
            foreach (var hm in hostManagers)
            {
                LogDebug($"  - {hm.name}: DontDestroy={IsInDontDestroyOnLoad(hm.gameObject)}");
            }

            if (NetworkManager.Instance != null)
            {
                LogDebug($"��ǰNetworkManager.Instance: {NetworkManager.Instance.name} (ClientId: {NetworkManager.Instance.ClientId})");
            }
        }
    }
}