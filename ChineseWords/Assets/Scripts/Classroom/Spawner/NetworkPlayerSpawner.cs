using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Classroom.Scene;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// ������������� - ������ݷ����������̬������λ�ͽ�ɫ
    /// �����ˣ�������λ+�������+���ɽ�ɫ��Ȼ��ͬ�������пͻ���
    /// �ͻ��ˣ�����ͬ�����ݣ��ڱ����ؽ���λ�ͽ�ɫ����
    /// ʹ��ͳһ���������������RPCͨ��
    /// </summary>
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private GameObject playerCharacterPrefab; // ��ҽ�ɫԤ���壨��Humanoid������
        [SerializeField] private CircularSeatingSystem seatingSystem; // ��λϵͳ����
        [SerializeField] private float spawnDelay = 1f; // �����ӳ٣��ȴ������ȶ���

        [Header("��ɫ����")]
        [SerializeField] private float characterHeight = 1.8f; // ��ɫ�߶�
        [SerializeField] private Vector3 characterScale = Vector3.one; // ��ɫ����

        [Header("ͬ������")]
        [SerializeField] private float sceneLoadTimeout = 10f; // �������س�ʱʱ��
        [SerializeField] private bool waitForAllPlayersReady = true; // �Ƿ�ȴ��������׼������

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ���-��λ������
        private Dictionary<ushort, int> playerToSeatMap = new Dictionary<ushort, int>(); // PlayerID -> SeatIndex
        private Dictionary<int, ushort> seatToPlayerMap = new Dictionary<int, ushort>(); // SeatIndex -> PlayerID
        private Dictionary<ushort, GameObject> playerCharacters = new Dictionary<ushort, GameObject>(); // PlayerID -> Character GameObject

        // ����״̬
        private bool isInitialized = false;
        private bool hasGeneratedSeats = false;
        private bool hasSpawnedCharacters = false;
        private List<ushort> readyPlayerIds = new List<ushort>(); // ��׼���õ����ID�б�

        // ��������
        public bool IsInitialized => isInitialized;
        public bool HasGeneratedSeats => hasGeneratedSeats;
        public bool HasSpawnedCharacters => hasSpawnedCharacters;
        public int SpawnedCharacterCount => playerCharacters.Count;

        // �¼�
        public static event System.Action<int> OnSeatsGenerated; // ��λ�������
        public static event System.Action<ushort, GameObject> OnCharacterSpawned; // ��ɫ�������
        public static event System.Action OnAllCharactersSpawned; // ���н�ɫ�������

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

            isInitialized = true;
            LogDebug("NetworkPlayerSpawner�ѳ�ʼ��");
        }

        private void Start()
        {
            if (!isInitialized) return;

            // ���������¼�
            SubscribeToNetworkEvents();

            // ������������ڷ����У���ʼ��������
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            {
                StartCoroutine(DelayedSpawnInitialization());
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        #endregion

        #region �����¼�����

        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined += OnPlayerJoined;
                NetworkManager.OnPlayerLeft += OnPlayerLeft;
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnPlayerLeft;
            }
        }

        #endregion

        #region �����ˣ�������λ�ͽ�ɫ

        /// <summary>
        /// �ӳٳ�ʼ����������
        /// </summary>
        private IEnumerator DelayedSpawnInitialization()
        {
            LogDebug("��ʼ�ӳ����ɳ�ʼ��");

            // �ȴ������ȶ�
            yield return new WaitForSeconds(spawnDelay);

            // �ȴ��������׼��������������ã�
            if (waitForAllPlayersReady)
            {
                yield return StartCoroutine(WaitForAllPlayersReady());
            }

            // ִ����������������
            GenerateSeatsAndSpawnCharacters();
        }

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
        /// ������λ��������ҽ�ɫ
        /// </summary>
        public void GenerateSeatsAndSpawnCharacters()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("�������ˣ����������߼�");
                return;
            }

            LogDebug("��ʼ������λ�ͽ�ɫ...");

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

            // 4. ͬ�������пͻ���
            SyncToAllClients();

            OnSeatsGenerated?.Invoke(playerCount);
            LogDebug("��λ�ͽ�ɫ�������");
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
            if (playerCharacterPrefab == null)
            {
                Debug.LogError("[NetworkPlayerSpawner] ��ɫԤ����δ����");
                return;
            }

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
        /// Ϊָ��������ɽ�ɫ
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

            // ���ɽ�ɫ
            GameObject character = Instantiate(playerCharacterPrefab, spawnPosition, spawnRotation);
            character.name = $"Player_{playerId:D2}_Character";
            character.transform.localScale = characterScale;

            // ���ý�ɫ����
            SetupCharacterForPlayer(character, playerId);

            // ��¼��ɫ
            playerCharacters[playerId] = character;

            OnCharacterSpawned?.Invoke(playerId, character);
            LogDebug($"Ϊ��� {playerId} ����λ {seatIndex} ���ɽ�ɫ");
        }

        /// <summary>
        /// ���ý�ɫ����
        /// </summary>
        private void SetupCharacterForPlayer(GameObject character, ushort playerId)
        {
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

                // ������λ����������������
                // ע�⣺���ﲻֱ������CameraMount����PlayerCameraController�Լ����ҽ�ɫ��ͷ�����ص�
                cameraController.SetCameraMount(null, seatIdentifier.GetSeatRotation());
                LogDebug($"Ϊ������� {playerId} ���������������");
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

        #region ����ͬ��

        /// <summary>
        /// ͬ�������пͻ���
        /// </summary>
        private void SyncToAllClients()
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

        #region �����¼�����

        /// <summary>
        /// ��Ҽ��뷿��
        /// </summary>
        private void OnPlayerJoined(ushort playerId)
        {
            LogDebug($"��� {playerId} ���뷿��");

            if (PhotonNetwork.IsMasterClient)
            {
                // ����������λ�ͽ�ɫ
                StartCoroutine(DelayedRegenerateSeatsAndCharacters());
            }
        }

        /// <summary>
        /// ����뿪����
        /// </summary>
        private void OnPlayerLeft(ushort playerId)
        {
            LogDebug($"��� {playerId} �뿪����");

            // �Ƴ���ҽ�ɫ����������λ
            if (playerCharacters.ContainsKey(playerId))
            {
                Destroy(playerCharacters[playerId]);
                playerCharacters.Remove(playerId);
                LogDebug($"���Ƴ���� {playerId} �Ľ�ɫ");
            }

            // ������������������ɣ���ѡ���������������
            // ע�͵��Ա�����λ���䣬֧�ֶ�������
            /*
            if (PhotonNetwork.IsMasterClient)
            {
                StartCoroutine(DelayedRegenerateSeatsAndCharacters());
            }
            */
        }

        /// <summary>
        /// �ӳ�����������λ�ͽ�ɫ
        /// </summary>
        private IEnumerator DelayedRegenerateSeatsAndCharacters()
        {
            yield return new WaitForSeconds(1f); // �ȴ�����״̬�ȶ�
            GenerateSeatsAndSpawnCharacters();
        }

        #endregion

        #region �����ӿ�

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

        #region ��������

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