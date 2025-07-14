using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Classroom.Scene;
using Core.Network;
using RoomScene.PlayerModel; // ����PlayerModelData

namespace Classroom.Player
{
    /// <summary>
    /// ������������� - ֧�ָ������ģ��ѡ�����ɸ��Ի���ɫ
    /// ʹ�þ�̬��Դ���أ�������PlayerModelManagerʵ��
    /// </summary>
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private GameObject playerCharacterPrefab; // Ĭ�Ͻ�ɫԤ���壨����ʹ�ã�
        [SerializeField] private CircularSeatingSystem seatingSystem; // ��λϵͳ����
        [SerializeField] private float spawnDelay = 1f; // �����ӳ٣��ȴ������ȶ���

        [Header("��ɫ����")]
        [SerializeField] private float characterHeight = 1.8f; // ��ɫ�߶�
        [SerializeField] private Vector3 characterScale = Vector3.one; // ��ɫ����

        [Header("ģ����Դ����")]
        [SerializeField] private string modelDataResourcesPath = "PlayerModels"; // Resources�ļ�����ģ�����ݵ�·��
        [SerializeField] private int defaultModelId = 0; // Ĭ��ģ��ID

        [Header("ͬ������")]
        [SerializeField] private float sceneLoadTimeout = 10f; // �������س�ʱʱ��
        [SerializeField] private bool waitForAllPlayersReady = true; // �Ƿ�ȴ��������׼������

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ��̬ģ�����ݻ���
        private static Dictionary<int, PlayerModelData> modelDataCache = new Dictionary<int, PlayerModelData>();
        private static bool modelDataLoaded = false;

        // ���-��λ������
        private Dictionary<ushort, int> playerToSeatMap = new Dictionary<ushort, int>(); // PlayerID -> SeatIndex
        private Dictionary<int, ushort> seatToPlayerMap = new Dictionary<int, ushort>(); // SeatIndex -> PlayerID
        private Dictionary<ushort, GameObject> playerCharacters = new Dictionary<ushort, GameObject>(); // PlayerID -> Character GameObject

        // ����״̬
        private bool isInitialized = false;
        private bool hasGeneratedSeats = false;
        private bool hasSpawnedCharacters = false;
        private List<ushort> readyPlayerIds = new List<ushort>(); // ��׼���õ����ID�б�

        // �����¼�����״̬
        private bool networkEventsSubscribed = false;

        // ��������
        public bool IsInitialized => isInitialized;
        public bool HasGeneratedSeats => hasGeneratedSeats;
        public bool HasSpawnedCharacters => hasSpawnedCharacters;
        public int SpawnedCharacterCount => playerCharacters.Count;

        // �¼�
        public static event System.Action<int> OnSeatsGenerated; // ��λ�������
        public static event System.Action<ushort, GameObject> OnCharacterSpawned; // ��ɫ�������
        public static event System.Action OnAllCharactersSpawned; // ���н�ɫ�������
        public static event System.Action<ushort> OnPlayerJoinedEvent; // ��Ҽ����¼���ת����ClassroomManager��
        public static event System.Action<ushort> OnPlayerLeftEvent; // ����뿪�¼���ת����ClassroomManager��

        #region Unity��������

        private void Awake()
        {
            // �Զ�������λϵͳ
            if (seatingSystem == null)
            {
                seatingSystem = FindObjectOfType<CircularSeatingSystem>();
            }

            if (seatingSystem == null)
            {
                Debug.LogError("[NetworkPlayerSpawner] δ�ҵ�CircularSeatingSystem���");
                return;
            }

            // Ԥ����ģ������
            LoadModelDataIfNeeded();

            isInitialized = true;
            LogDebug("NetworkPlayerSpawner�ѳ�ʼ��");
        }

        private void Start()
        {
            if (!isInitialized) return;

            // ֻ���������¼������Զ���ʼ��������
            SubscribeToNetworkEvents();
            LogDebug("NetworkPlayerSpawner�Ѷ��������¼����ȴ�ClassroomManager����");
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        #endregion

        #region ��̬ģ�����ݼ���

        /// <summary>
        /// ����ģ�����ݣ������δ���أ�
        /// </summary>
        private void LoadModelDataIfNeeded()
        {
            if (modelDataLoaded) return;

            try
            {
                LogDebug($"��ʼ��Resources����ģ������: {modelDataResourcesPath}");

                // ��������PlayerModelData�ʲ�
                PlayerModelData[] allModelData = Resources.LoadAll<PlayerModelData>(modelDataResourcesPath);

                if (allModelData == null || allModelData.Length == 0)
                {
                    Debug.LogWarning($"[NetworkPlayerSpawner] δ�� Resources/{modelDataResourcesPath} ���ҵ�PlayerModelData�ʲ�");
                    return;
                }

                // ����IDӳ��
                modelDataCache.Clear();
                foreach (var modelData in allModelData)
                {
                    if (modelData != null && modelData.IsValid())
                    {
                        modelDataCache[modelData.modelId] = modelData;
                        LogDebug($"����ģ������: ID={modelData.modelId}, Name={modelData.modelName}");
                    }
                }

                modelDataLoaded = true;
                LogDebug($"ģ�����ݼ�����ɣ������� {modelDataCache.Count} ��ģ��");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkPlayerSpawner] ����ģ������ʱ��������: {e.Message}");
            }
        }

        /// <summary>
        /// ��ȡģ������
        /// </summary>
        private static PlayerModelData GetModelData(int modelId)
        {
            return modelDataCache.TryGetValue(modelId, out PlayerModelData data) ? data : null;
        }

        /// <summary>
        /// ��ȡģ�͵���ϷԤ����
        /// </summary>
        private static GameObject GetModelGamePrefab(int modelId)
        {
            var modelData = GetModelData(modelId);
            return modelData?.GetGamePrefab();
        }

        #endregion

        #region �����¼�����

        private void SubscribeToNetworkEvents()
        {
            if (networkEventsSubscribed) return;

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined += OnPlayerJoinedInternal;
                NetworkManager.OnPlayerLeft += OnPlayerLeftInternal;
                networkEventsSubscribed = true;
                LogDebug("�Ѷ��������¼�");
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (!networkEventsSubscribed) return;

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnPlayerJoinedInternal;
                NetworkManager.OnPlayerLeft -= OnPlayerLeftInternal;
                networkEventsSubscribed = false;
                LogDebug("��ȡ�����������¼�");
            }
        }

        #endregion

        #region �����ӿ� - ��ClassroomManager����

        /// <summary>
        /// ������λ��������ҽ�ɫ����ClassroomManager���ã�
        /// </summary>
        public void GenerateSeatsAndSpawnCharacters()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("�������ˣ����������߼�");
                return;
            }

            LogDebug("��ʼ������λ�ͽ�ɫ...");

            // ȷ��ģ�������Ѽ���
            LoadModelDataIfNeeded();

            // ��ȡ��ǰ�����������
            var allPlayers = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).ToList();
            int playerCount = allPlayers.Count;

            LogDebug($"��ǰ���������: {playerCount}");

            // ������������
            ClearAllData();

            // 1. ������λ
            seatingSystem.GenerateSeats(playerCount);
            hasGeneratedSeats = true;

            // 2. ������ҵ���λ
            AssignPlayersToSeats(allPlayers);

            // 3. ���ɽ�ɫ
            SpawnAllCharacters();

            OnSeatsGenerated?.Invoke(playerCount);
            LogDebug("��λ�ͽ�ɫ�������");
        }

        /// <summary>
        /// ͬ�������пͻ��ˣ���ClassroomManager���ã�
        /// </summary>
        public void SyncToAllClients()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // ׼��ͬ������
            ushort[] playerIds = playerToSeatMap.Keys.ToArray();
            int[] seatIndices = playerToSeatMap.Values.ToArray();

            // ͨ��NetworkManager����RPC
            NetworkManager.Instance.BroadcastSeatsAndCharacters(playerIds, seatIndices);
            LogDebug($"�����пͻ���ͬ�����ݣ�{playerIds.Length} �����");
        }

        /// <summary>
        /// �ӳٳ�ʼ���������̣���ClassroomManager���ã�
        /// </summary>
        public IEnumerator DelayedSpawnInitializationCoroutine()
        {
            LogDebug("��ʼ�ӳ����ɳ�ʼ��");

            // �ȴ������ȶ�
            yield return new WaitForSeconds(spawnDelay);

            // ȷ��ģ�������Ѽ���
            LoadModelDataIfNeeded();

            // �ȴ��������׼��������������ã�
            if (waitForAllPlayersReady)
            {
                yield return StartCoroutine(WaitForAllPlayersReady());
            }

            LogDebug("�ӳٳ�ʼ����ɣ�׼��������λ�ͽ�ɫ");
        }

        #endregion

        #region ԭ�е������߼������ֲ��䣩

        /// <summary>
        /// �ȴ��������׼������
        /// </summary>
        private IEnumerator WaitForAllPlayersReady()
        {
            LogDebug("�ȴ��������׼������...");

            float timeout = sceneLoadTimeout;
            while (timeout > 0)
            {
                // ����Ƿ�������Ҷ��ѷ���"׼������"�ź�
                if (AreAllPlayersReady())
                {
                    LogDebug("���������׼������");
                    break;
                }

                timeout -= Time.deltaTime;
                yield return null;
            }

            if (timeout <= 0)
            {
                LogDebug("�ȴ����׼����ʱ��ǿ�ƿ�ʼ����");
            }
        }

        /// <summary>
        /// �����������Ƿ�׼������
        /// </summary>
        private bool AreAllPlayersReady()
        {
            // ��ʱ�򻯣���Ϊ�����ڷ����е���Ҷ���׼������
            // ����������Ӹ����ӵ�׼����������߼�
            return PhotonNetwork.PlayerList.Length > 0;
        }

        /// <summary>
        /// ������ҵ���λ
        /// </summary>
        private void AssignPlayersToSeats(List<Photon.Realtime.Player> players)
        {
            LogDebug("��ʼ������ҵ���λ");

            for (int i = 0; i < players.Count; i++)
            {
                ushort playerId = (ushort)players[i].ActorNumber;
                int seatIndex = i;

                // ����˫��ӳ��
                playerToSeatMap[playerId] = seatIndex;
                seatToPlayerMap[seatIndex] = playerId;

                // ռ����λ
                var seatData = seatingSystem.GetSeatData(seatIndex);
                if (seatData?.seatInstance != null)
                {
                    var seatIdentifier = seatData.seatInstance.GetComponent<SeatIdentifier>();
                    if (seatIdentifier != null)
                    {
                        seatIdentifier.OccupySeat(players[i].NickName ?? $"Player_{playerId}");
                    }
                }

                LogDebug($"��� {players[i].NickName} (ID: {playerId}) ���䵽��λ {seatIndex}");
            }

            LogDebug($"��ҷ�����ɣ������� {players.Count} �����");
        }

        /// <summary>
        /// �������н�ɫ
        /// </summary>
        private void SpawnAllCharacters()
        {
            LogDebug("��ʼ�������н�ɫ");

            foreach (var kvp in playerToSeatMap)
            {
                ushort playerId = kvp.Key;
                int seatIndex = kvp.Value;

                SpawnCharacterForPlayer(playerId, seatIndex);
            }

            hasSpawnedCharacters = true;
            OnAllCharactersSpawned?.Invoke();
            LogDebug("���н�ɫ�������");
        }

        /// <summary>
        /// Ϊָ��������ɽ�ɫ - �޸İ棺֧�ָ��Ի�ģ��
        /// </summary>
        private void SpawnCharacterForPlayer(ushort playerId, int seatIndex)
        {
            var seatData = seatingSystem.GetSeatData(seatIndex);
            if (seatData?.seatInstance == null)
            {
                Debug.LogError($"[NetworkPlayerSpawner] ��λ {seatIndex} ������Ч");
                return;
            }

            var seatIdentifier = seatData.seatInstance.GetComponent<SeatIdentifier>();
            if (seatIdentifier == null)
            {
                Debug.LogError($"[NetworkPlayerSpawner] ��λ {seatIndex} ȱ��SeatIdentifier���");
                return;
            }

            // ��ȡ����λ��
            Vector3 spawnPosition = seatIdentifier.GetPlayerSpawnPosition();
            Quaternion spawnRotation = seatIdentifier.GetSeatRotation();
            spawnRotation *= Quaternion.Euler(0, 180, 0);

            // ���ɽ�ɫ��ʹ�ø��Ի�ģ�ͣ�
            GameObject character = CreatePlayerCharacter(playerId, spawnPosition, spawnRotation);
            if (character == null)
            {
                Debug.LogError($"[NetworkPlayerSpawner] Ϊ��� {playerId} ������ɫʧ��");
                return;
            }

            character.name = $"Player_{playerId:D2}_Character";
            character.transform.localScale = characterScale;

            // ���ý�ɫ����
            SetupCharacterForPlayer(character, playerId);

            // ��¼��ɫ
            playerCharacters[playerId] = character;

            OnCharacterSpawned?.Invoke(playerId, character);
            LogDebug($"Ϊ��� {playerId} ����λ {seatIndex} ���ɽ�ɫ��ģ��ID: {GetPlayerModelId(playerId)}��");
        }

        /// <summary>
        /// ������ҽ�ɫ - �޸İ棺ʹ�þ�̬��Դ����
        /// </summary>
        private GameObject CreatePlayerCharacter(ushort playerId, Vector3 position, Quaternion rotation)
        {
            // ��ȡ���ѡ���ģ��ID
            int modelId = GetPlayerModelId(playerId);

            // ����ͨ����̬��Դ���ػ�ȡģ��Ԥ����
            GameObject modelPrefab = GetModelGamePrefab(modelId);
            if (modelPrefab != null)
            {
                GameObject modelCharacter = Instantiate(modelPrefab, position, rotation);
                LogDebug($"ͨ����̬��ԴΪ��� {playerId} ����ģ�� {modelId} �ɹ�");
                return modelCharacter;
            }
            else
            {
                LogDebug($"��̬��Դ��δ�ҵ�ģ�� {modelId}��ʹ��Ĭ��Ԥ����");
            }

            // ���ˣ�ʹ��Ĭ��Ԥ����
            if (playerCharacterPrefab != null)
            {
                GameObject defaultCharacter = Instantiate(playerCharacterPrefab, position, rotation);
                LogDebug($"Ϊ��� {playerId} ʹ��Ĭ��Ԥ���崴����ɫ");
                return defaultCharacter;
            }

            Debug.LogError($"[NetworkPlayerSpawner] �޷�Ϊ��� {playerId} ������ɫ��ȱ��Ĭ��Ԥ����");
            return null;
        }

        /// <summary>
        /// ��ȡ���ģ��ID
        /// </summary>
        private int GetPlayerModelId(ushort playerId)
        {
            // ��NetworkManager��ȡ���ģ��ID
            if (NetworkManager.Instance != null)
            {
                int modelId = NetworkManager.Instance.GetPlayerModelId(playerId);
                LogDebug($"��NetworkManager��ȡ��� {playerId} ģ��ID: {modelId}");
                return modelId;
            }

            // ���ˣ�ʹ��Ĭ��ģ��ID
            LogDebug($"NetworkManager�����ã�Ϊ��� {playerId} ʹ��Ĭ��ģ��ID: {defaultModelId}");
            return defaultModelId;
        }

        /// <summary>
        /// ���ý�ɫ����
        /// </summary>
        private void SetupCharacterForPlayer(GameObject character, ushort playerId)
        {
            LogDebug($"���ý�ɫ {playerId}, ����ClientId: {NetworkManager.Instance.ClientId}, �Ƿ�ƥ��: {playerId == NetworkManager.Instance.ClientId}");

            // ���PlayerCameraController����������ң�
            if (playerId == NetworkManager.Instance.ClientId)
            {
                var cameraController = character.GetComponent<PlayerCameraController>();
                if (cameraController == null)
                {
                    cameraController = character.AddComponent<PlayerCameraController>();
                }

                // ��ȡ��Ӧ����λ��Ϣ���������
                int seatIndex = playerToSeatMap[playerId];
                var seatData = seatingSystem.GetSeatData(seatIndex);
                var seatIdentifier = seatData.seatInstance.GetComponent<SeatIdentifier>();

                // ��ȡ��λԭʼ��ת����Y����ת180��
                Quaternion originalRotation = seatIdentifier.GetSeatRotation();
                Quaternion rotatedRotation = originalRotation * Quaternion.Euler(0, 180, 0);

                // ������ת��ĳ���������������
                cameraController.SetCameraMount(null, rotatedRotation);
                LogDebug($"Ϊ������� {playerId} �������������������Ӧ��180��Y����ת");
            }

            // ���������ɫ������綯��������ͬ���ȣ�
            var networkSync = character.GetComponent<PlayerNetworkSync>();
            if (networkSync == null)
            {
                networkSync = character.AddComponent<PlayerNetworkSync>();
            }
            networkSync.Initialize(playerId);
        }

        #endregion

        #region ����ͬ�������ֲ��䣩

        /// <summary>
        /// ����������ͬ�������ݣ���NetworkManager���ã�
        /// </summary>
        public void ReceiveSeatsAndCharactersData(ushort[] playerIds, int[] seatIndices)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                LogDebug("�����˺���ͬ������");
                return;
            }

            LogDebug($"�ͻ��˽���ͬ�����ݣ�{playerIds.Length} �����");

            StartCoroutine(ProcessSyncData(playerIds, seatIndices));
        }

        /// <summary>
        /// ����ͬ������
        /// </summary>
        private IEnumerator ProcessSyncData(ushort[] playerIds, int[] seatIndices)
        {
            // �ȴ�������λϵͳ׼������
            yield return new WaitUntil(() => seatingSystem.IsInitialized);

            LogDebug("��ʼ����ͬ������");

            // ȷ��ģ�������Ѽ���
            LoadModelDataIfNeeded();

            // ������������
            ClearAllData();

            // ����������λ��ʹ����ͬ�����������
            seatingSystem.GenerateSeats(playerIds.Length);
            hasGeneratedSeats = true;

            // �ؽ�ӳ���ϵ
            for (int i = 0; i < playerIds.Length; i++)
            {
                ushort playerId = playerIds[i];
                int seatIndex = seatIndices[i];

                playerToSeatMap[playerId] = seatIndex;
                seatToPlayerMap[seatIndex] = playerId;

                // ռ����λ
                var seatData = seatingSystem.GetSeatData(seatIndex);
                if (seatData?.seatInstance != null)
                {
                    var seatIdentifier = seatData.seatInstance.GetComponent<SeatIdentifier>();
                    if (seatIdentifier != null)
                    {
                        var playerName = GetPlayerNameById(playerId);
                        seatIdentifier.OccupySeat(playerName);
                    }
                }
            }

            // ���ɽ�ɫ
            SpawnAllCharacters();

            OnSeatsGenerated?.Invoke(playerIds.Length);
            LogDebug("�ͻ�������ͬ�����");
        }

        #endregion

        #region �����¼������޸�Ϊת���¼���

        /// <summary>
        /// ��Ҽ��뷿�䣨�ڲ�����ת����ClassroomManager��
        /// </summary>
        private void OnPlayerJoinedInternal(ushort playerId)
        {
            LogDebug($"��� {playerId} ���뷿�䣬ת���¼���ClassroomManager");

            // �Ƴ���ҽ�ɫ�������Զ���������
            // ת���¼���ClassroomManager����������Ƿ���������
            OnPlayerJoinedEvent?.Invoke(playerId);
        }

        /// <summary>
        /// ����뿪���䣨�ڲ�����ת����ClassroomManager��
        /// </summary>
        private void OnPlayerLeftInternal(ushort playerId)
        {
            LogDebug($"��� {playerId} �뿪����");

            // �Ƴ���ҽ�ɫ����������λ
            if (playerCharacters.ContainsKey(playerId))
            {
                Destroy(playerCharacters[playerId]);
                playerCharacters.Remove(playerId);
                LogDebug($"���Ƴ���� {playerId} �Ľ�ɫ");
            }

            // ת���¼���ClassroomManager����������Ƿ���������
            OnPlayerLeftEvent?.Invoke(playerId);
        }

        #endregion

        #region ������ѯ�ӿڣ����ֲ��䣩

        /// <summary>
        /// ��ȡ��ҵ���λ����
        /// </summary>
        public int GetPlayerSeatIndex(ushort playerId)
        {
            return playerToSeatMap.TryGetValue(playerId, out int seatIndex) ? seatIndex : -1;
        }

        /// <summary>
        /// ��ȡ��ҽ�ɫ����
        /// </summary>
        public GameObject GetPlayerCharacter(ushort playerId)
        {
            return playerCharacters.TryGetValue(playerId, out GameObject character) ? character : null;
        }

        /// <summary>
        /// ��ȡ��ҵ���λ��ʶ��
        /// </summary>
        public SeatIdentifier GetPlayerSeatIdentifier(ushort playerId)
        {
            int seatIndex = GetPlayerSeatIndex(playerId);
            if (seatIndex >= 0)
            {
                var seatData = seatingSystem.GetSeatData(seatIndex);
                return seatData?.seatInstance?.GetComponent<SeatIdentifier>();
            }
            return null;
        }

        #endregion

        #region �������������ֲ��䣩

        /// <summary>
        /// ������������
        /// </summary>
        private void ClearAllData()
        {
            // �������н�ɫ
            foreach (var character in playerCharacters.Values)
            {
                if (character != null)
                {
                    Destroy(character);
                }
            }

            // ����ӳ��
            playerToSeatMap.Clear();
            seatToPlayerMap.Clear();
            playerCharacters.Clear();

            hasGeneratedSeats = false;
            hasSpawnedCharacters = false;

            LogDebug("��������������");
        }

        /// <summary>
        /// ����ID��ȡ�������
        /// </summary>
        private string GetPlayerNameById(ushort playerId)
        {
            var player = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == playerId);
            return player?.NickName ?? $"Player_{playerId}";
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkPlayerSpawner] {message}");
            }
        }

        #endregion
    }
}