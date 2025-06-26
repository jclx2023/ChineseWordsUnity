using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Classroom.Scene;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// ��λ-��Ұ��� - �����������λ��ӳ���ϵ
    /// �ṩ��λ��ѯ����Ҷ�λ�ȹ��ܣ���CircularSeatingSystemЭͬ����
    /// </summary>
    public class SeatingPlayerBinder : MonoBehaviour
    {
        [Header("�������")]
        [SerializeField] private CircularSeatingSystem seatingSystem;
        [SerializeField] private NetworkPlayerSpawner playerSpawner;

        [Header("������")]
        [SerializeField] private bool autoBindOnStart = true;
        [SerializeField] private bool maintainBindingOnPlayerLeave = true; // ����뿪ʱ������λ��

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showBindingGizmos = true;

        // ������
        private Dictionary<ushort, SeatBinding> playerBindings = new Dictionary<ushort, SeatBinding>();
        private Dictionary<int, SeatBinding> seatBindings = new Dictionary<int, SeatBinding>();
        private bool isInitialized = false;

        // ��λ�����ݽṹ
        [System.Serializable]
        public class SeatBinding
        {
            public ushort playerId;
            public int seatIndex;
            public SeatIdentifier seatIdentifier;
            public GameObject playerCharacter;
            public string playerName;
            public bool isOccupied;
            public bool isActive; // ����Ƿ�����

            public SeatBinding(ushort id, int seat, SeatIdentifier identifier)
            {
                playerId = id;
                seatIndex = seat;
                seatIdentifier = identifier;
                isOccupied = true;
                isActive = true;
            }
        }

        // ��������
        public bool IsInitialized => isInitialized;
        public int TotalSeats => seatBindings.Count;
        public int OccupiedSeats => seatBindings.Values.Count(b => b.isOccupied);
        public int ActivePlayers => playerBindings.Values.Count(b => b.isActive);

        // �¼�
        public static event System.Action<SeatBinding> OnPlayerBoundToSeat;
        public static event System.Action<ushort, int> OnPlayerUnboundFromSeat;
        public static event System.Action<SeatBinding> OnPlayerActivated;
        public static event System.Action<SeatBinding> OnPlayerDeactivated;

        #region Unity��������

        private void Awake()
        {
            // �Զ��������
            if (seatingSystem == null)
                seatingSystem = FindObjectOfType<CircularSeatingSystem>();

            if (playerSpawner == null)
                playerSpawner = FindObjectOfType<NetworkPlayerSpawner>();

            if (seatingSystem == null)
            {
                Debug.LogError("[SeatingPlayerBinder] δ�ҵ�CircularSeatingSystem���");
                return;
            }
        }

        private void Start()
        {
            if (autoBindOnStart && seatingSystem != null)
            {
                Initialize();
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region �¼�����

        private void SubscribeToEvents()
        {
            if (playerSpawner != null)
            {
                NetworkPlayerSpawner.OnCharacterSpawned += OnCharacterSpawned;
                NetworkPlayerSpawner.OnSeatsGenerated += OnSeatsGenerated;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerLeft += OnPlayerLeft;
                NetworkManager.OnPlayerJoined += OnPlayerJoined;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (playerSpawner != null)
            {
                NetworkPlayerSpawner.OnCharacterSpawned -= OnCharacterSpawned;
                NetworkPlayerSpawner.OnSeatsGenerated -= OnSeatsGenerated;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerLeft -= OnPlayerLeft;
                NetworkManager.OnPlayerJoined -= OnPlayerJoined;
            }
        }

        #endregion

        #region ��ʼ��

        /// <summary>
        /// ��ʼ����λ����
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                LogDebug("�ѳ�ʼ���������ظ���ʼ��");
                return;
            }

            LogDebug("��ʼ��ʼ����λ����");

            // ������������
            ClearAllBindings();

            // �ȴ���λϵͳ׼������
            if (!seatingSystem.IsInitialized)
            {
                LogDebug("�ȴ���λϵͳ��ʼ��...");
                StartCoroutine(WaitForSeatingSystemReady());
                return;
            }

            // ������ʼ��
            BuildInitialBindings();

            isInitialized = true;
            LogDebug("��λ������ʼ�����");
        }

        /// <summary>
        /// �ȴ���λϵͳ׼������
        /// </summary>
        private System.Collections.IEnumerator WaitForSeatingSystemReady()
        {
            yield return new WaitUntil(() => seatingSystem.IsInitialized);
            BuildInitialBindings();
            isInitialized = true;
            LogDebug("��λ�����ӳٳ�ʼ�����");
        }

        /// <summary>
        /// ������ʼ�󶨹�ϵ
        /// </summary>
        private void BuildInitialBindings()
        {
            var generatedSeats = seatingSystem.GeneratedSeats;
            LogDebug($"������ʼ�󶨹�ϵ����λ����: {generatedSeats.Count}");

            for (int i = 0; i < generatedSeats.Count; i++)
            {
                var seatData = generatedSeats[i];
                if (seatData.seatInstance != null)
                {
                    var seatIdentifier = seatData.seatInstance.GetComponent<SeatIdentifier>();
                    if (seatIdentifier != null)
                    {
                        // ��������λ��
                        CreateEmptySeatBinding(i, seatIdentifier);
                    }
                }
            }
        }

        /// <summary>
        /// ��������λ��
        /// </summary>
        private void CreateEmptySeatBinding(int seatIndex, SeatIdentifier seatIdentifier)
        {
            var binding = new SeatBinding(0, seatIndex, seatIdentifier)
            {
                isOccupied = false,
                isActive = false,
                playerName = ""
            };

            seatBindings[seatIndex] = binding;
            LogDebug($"��������λ��: ��λ {seatIndex}");
        }

        #endregion

        #region �󶨹���

        /// <summary>
        /// ����ҵ���λ
        /// </summary>
        public bool BindPlayerToSeat(ushort playerId, int seatIndex, string playerName = "")
        {
            if (!IsValidSeatIndex(seatIndex))
            {
                LogDebug($"��Ч����λ����: {seatIndex}");
                return false;
            }

            if (IsPlayerBound(playerId))
            {
                LogDebug($"��� {playerId} �Ѱ���λ���Ƚ��");
                UnbindPlayer(playerId);
            }

            var seatBinding = seatBindings[seatIndex];
            var seatIdentifier = seatBinding.seatIdentifier;

            // ���°���Ϣ
            seatBinding.playerId = playerId;
            seatBinding.isOccupied = true;
            seatBinding.isActive = true;
            seatBinding.playerName = !string.IsNullOrEmpty(playerName) ? playerName : $"Player_{playerId}";

            // ռ����λ
            seatIdentifier.OccupySeat(seatBinding.playerName);

            // ����˫��ӳ��
            playerBindings[playerId] = seatBinding;

            OnPlayerBoundToSeat?.Invoke(seatBinding);
            LogDebug($"��� {playerId} ({seatBinding.playerName}) �󶨵���λ {seatIndex}");

            return true;
        }

        /// <summary>
        /// ������
        /// </summary>
        public bool UnbindPlayer(ushort playerId)
        {
            if (!IsPlayerBound(playerId))
            {
                LogDebug($"��� {playerId} δ���κ���λ");
                return false;
            }

            var binding = playerBindings[playerId];
            int seatIndex = binding.seatIndex;

            // �Ƴ���Ұ�
            playerBindings.Remove(playerId);

            if (maintainBindingOnPlayerLeave)
            {
                // ������λ�������Ϊ�ǻ�Ծ
                binding.isActive = false;
                binding.playerCharacter = null;
                LogDebug($"��� {playerId} ����λ {seatIndex} �����λ����");
            }
            else
            {
                // ��ȫ�ͷ���λ
                binding.playerId = 0;
                binding.isOccupied = false;
                binding.isActive = false;
                binding.playerName = "";
                binding.playerCharacter = null;
                binding.seatIdentifier.ReleaseSeat();
                LogDebug($"��� {playerId} ����λ {seatIndex} �����λ�ͷ�");
            }

            OnPlayerUnboundFromSeat?.Invoke(playerId, seatIndex);
            return true;
        }

        /// <summary>
        /// ���¼�����ң����ڶ���������
        /// </summary>
        public bool ReactivatePlayer(ushort playerId, GameObject playerCharacter = null)
        {
            var binding = GetPlayerBinding(playerId);
            if (binding == null)
            {
                LogDebug($"��� {playerId} û�б�������λ��");
                return false;
            }

            binding.isActive = true;
            binding.playerCharacter = playerCharacter;

            // ����ռ����λ
            binding.seatIdentifier.OccupySeat(binding.playerName);

            OnPlayerActivated?.Invoke(binding);
            LogDebug($"��� {playerId} ���¼����λ {binding.seatIndex}");

            return true;
        }

        /// <summary>
        /// ͣ����ң���������λ��
        /// </summary>
        public bool DeactivatePlayer(ushort playerId)
        {
            if (!IsPlayerBound(playerId))
            {
                return false;
            }

            var binding = playerBindings[playerId];
            binding.isActive = false;
            binding.playerCharacter = null;

            OnPlayerDeactivated?.Invoke(binding);
            LogDebug($"��� {playerId} ��ͣ�ã���λ {binding.seatIndex} ����");

            return true;
        }

        #endregion

        #region ��ѯ�ӿ�

        /// <summary>
        /// ��ȡ��ҵ���λ��
        /// </summary>
        public SeatBinding GetPlayerBinding(ushort playerId)
        {
            return playerBindings.TryGetValue(playerId, out SeatBinding binding) ? binding : null;
        }

        /// <summary>
        /// ��ȡ��λ�İ���Ϣ
        /// </summary>
        public SeatBinding GetSeatBinding(int seatIndex)
        {
            return seatBindings.TryGetValue(seatIndex, out SeatBinding binding) ? binding : null;
        }

        /// <summary>
        /// ��ȡ��ҵ���λ����
        /// </summary>
        public int GetPlayerSeatIndex(ushort playerId)
        {
            var binding = GetPlayerBinding(playerId);
            return binding?.seatIndex ?? -1;
        }

        /// <summary>
        /// ��ȡ��λ�����ID
        /// </summary>
        public ushort GetSeatPlayerId(int seatIndex)
        {
            var binding = GetSeatBinding(seatIndex);
            return binding?.playerId ?? 0;
        }

        /// <summary>
        /// ��ȡ��ҵ���λ��ʶ��
        /// </summary>
        public SeatIdentifier GetPlayerSeatIdentifier(ushort playerId)
        {
            var binding = GetPlayerBinding(playerId);
            return binding?.seatIdentifier;
        }

        /// <summary>
        /// ��ȡ��ҵĽ�ɫ����
        /// </summary>
        public GameObject GetPlayerCharacter(ushort playerId)
        {
            var binding = GetPlayerBinding(playerId);
            return binding?.playerCharacter;
        }

        /// <summary>
        /// �������Ƿ��Ѱ���λ
        /// </summary>
        public bool IsPlayerBound(ushort playerId)
        {
            return playerBindings.ContainsKey(playerId);
        }

        /// <summary>
        /// �������Ƿ��Ծ
        /// </summary>
        public bool IsPlayerActive(ushort playerId)
        {
            var binding = GetPlayerBinding(playerId);
            return binding?.isActive ?? false;
        }

        /// <summary>
        /// �����λ�Ƿ�ռ��
        /// </summary>
        public bool IsSeatOccupied(int seatIndex)
        {
            var binding = GetSeatBinding(seatIndex);
            return binding?.isOccupied ?? false;
        }

        /// <summary>
        /// ��ȡ���л�Ծ��ҵİ�
        /// </summary>
        public List<SeatBinding> GetActivePlayerBindings()
        {
            return playerBindings.Values.Where(b => b.isActive).ToList();
        }

        /// <summary>
        /// ��ȡ���п�����λ������
        /// </summary>
        public List<int> GetAvailableSeatIndices()
        {
            return seatBindings.Values.Where(b => !b.isOccupied).Select(b => b.seatIndex).ToList();
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// ��λ��������¼�
        /// </summary>
        private void OnSeatsGenerated(int seatCount)
        {
            LogDebug($"�յ���λ�����¼�����λ��: {seatCount}");

            if (!isInitialized)
            {
                Initialize();
            }
            else
            {
                // ���½�����
                BuildInitialBindings();
            }
        }

        /// <summary>
        /// ��ɫ��������¼�
        /// </summary>
        private void OnCharacterSpawned(ushort playerId, GameObject character)
        {
            LogDebug($"�յ���ɫ�����¼�: ��� {playerId}");

            // ���°��еĽ�ɫ����
            var binding = GetPlayerBinding(playerId);
            if (binding != null)
            {
                binding.playerCharacter = character;
                LogDebug($"������� {playerId} �Ľ�ɫ����");
            }
            else
            {
                LogDebug($"���棺��� {playerId} û�ж�Ӧ����λ��");
            }
        }

        /// <summary>
        /// ����뿪�¼�
        /// </summary>
        private void OnPlayerLeft(ushort playerId)
        {
            LogDebug($"��� {playerId} �뿪����");

            if (maintainBindingOnPlayerLeave)
            {
                DeactivatePlayer(playerId);
            }
            else
            {
                UnbindPlayer(playerId);
            }
        }

        /// <summary>
        /// ��Ҽ����¼�
        /// </summary>
        private void OnPlayerJoined(ushort playerId)
        {
            LogDebug($"��� {playerId} ���뷿��");

            // ����Ƿ��б�������λ��
            var existingBinding = GetPlayerBinding(playerId);
            if (existingBinding != null && !existingBinding.isActive)
            {
                LogDebug($"��� {playerId} �б�����λ {existingBinding.seatIndex}��׼�����¼���");
                // �ȴ���ɫ���ɺ��ټ���
            }
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// �������а�
        /// </summary>
        public void ClearAllBindings()
        {
            LogDebug("����������λ��");

            // �ͷ�������λ
            foreach (var binding in seatBindings.Values)
            {
                binding.seatIdentifier?.ReleaseSeat();
            }

            playerBindings.Clear();
            seatBindings.Clear();
        }

        /// <summary>
        /// ǿ���ؽ��󶨹�ϵ
        /// </summary>
        [ContextMenu("�ؽ��󶨹�ϵ")]
        public void RebuildBindings()
        {
            LogDebug("ǿ���ؽ��󶨹�ϵ");

            ClearAllBindings();
            BuildInitialBindings();
        }

        /// <summary>
        /// ��ȡ��״̬��Ϣ
        /// </summary>
        public string GetBindingStatus()
        {
            var activePlayers = GetActivePlayerBindings();
            var availableSeats = GetAvailableSeatIndices();

            string status = $"=== ��λ��״̬ ===\n";
            status += $"����λ��: {TotalSeats}\n";
            status += $"��ռ��: {OccupiedSeats}\n";
            status += $"��Ծ���: {ActivePlayers}\n";
            status += $"������λ: {availableSeats.Count}\n\n";

            status += "��Ծ����б�:\n";
            foreach (var binding in activePlayers)
            {
                status += $"  ��λ {binding.seatIndex}: ��� {binding.playerId} ({binding.playerName})\n";
            }

            return status;
        }

        #endregion

        #region ��������

        /// <summary>
        /// �����λ�����Ƿ���Ч
        /// </summary>
        private bool IsValidSeatIndex(int seatIndex)
        {
            return seatIndex >= 0 && seatBindings.ContainsKey(seatIndex);
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SeatingPlayerBinder] {message}");
            }
        }

        #endregion

        #region ���Է���

        [ContextMenu("��ʾ���а�����")]
        public void ShowAllBindingDetails()
        {
            string details = "=== ��ϸ����Ϣ ===\n";

            foreach (var kvp in seatBindings)
            {
                var binding = kvp.Value;
                details += $"��λ {binding.seatIndex}: ";

                if (binding.isOccupied)
                {
                    details += $"��� {binding.playerId} ({binding.playerName}) ";
                    details += $"- {(binding.isActive ? "��Ծ" : "����")}";
                }
                else
                {
                    details += "����";
                }

                details += "\n";
            }

            Debug.Log(details);
        }

        #endregion

    }
}