using UnityEngine;
using Lobby.Data;

namespace RoomScene.Data
{
    /// <summary>
    /// ����������� - ��չLobbyPlayerData�����ģ��ѡ����
    /// </summary>
    [System.Serializable]
    public class RoomPlayerData
    {
        [Header("������Ϣ")]
        public ushort playerId;
        public string playerName;
        public bool isHost;
        public bool isReady;

        [Header("ģ��ѡ��")]
        public int selectedModelId = 0;          // ѡ���ģ��ID
        public string selectedModelName = "";    // ģ�����ƣ�������ʾ��

        [Header("����״̬")]
        public bool isOnline = true;
        public float lastSyncTime;

        /// <summary>
        /// ��LobbyPlayerData����
        /// </summary>
        public RoomPlayerData(LobbyPlayerData lobbyData, ushort id)
        {
            playerId = id;
            playerName = lobbyData.GetDisplayName();
            selectedModelId = lobbyData.avatarIndex; // ��Lobby��avatarIndexӳ��ΪmodelId
            isHost = false;
            isReady = false;
            isOnline = true;
            lastSyncTime = Time.time;
        }

        /// <summary>
        /// Ĭ�Ϲ��캯��
        /// </summary>
        public RoomPlayerData()
        {
            playerId = 0;
            playerName = "δ֪���";
            selectedModelId = 0;
            selectedModelName = "";
            isHost = false;
            isReady = false;
            isOnline = true;
            lastSyncTime = Time.time;
        }

        /// <summary>
        /// ����ģ��ѡ��
        /// </summary>
        public void SetSelectedModel(int modelId, string modelName)
        {
            selectedModelId = modelId;
            selectedModelName = modelName;
            lastSyncTime = Time.time;
        }

        /// <summary>
        /// ����׼��״̬
        /// </summary>
        public void SetReady(bool ready)
        {
            isReady = ready;
            lastSyncTime = Time.time;
        }

        /// <summary>
        /// ��ȡ������Ϣ
        /// </summary>
        public override string ToString()
        {
            return $"RoomPlayer[ID:{playerId}, Name:{playerName}, Model:{selectedModelId}, Ready:{isReady}]";
        }

        /// <summary>
        /// ת��Ϊ���紫���ʽ
        /// </summary>
        public RoomPlayerNetworkData ToNetworkData()
        {
            return new RoomPlayerNetworkData
            {
                playerId = this.playerId,
                playerName = this.playerName,
                isHost = this.isHost,
                isReady = this.isReady,
                selectedModelId = this.selectedModelId,
                selectedModelName = this.selectedModelName
            };
        }

        /// <summary>
        /// ���������ݹ���
        /// </summary>
        public static RoomPlayerData FromNetworkData(RoomPlayerNetworkData networkData)
        {
            return new RoomPlayerData
            {
                playerId = networkData.playerId,
                playerName = networkData.playerName,
                isHost = networkData.isHost,
                isReady = networkData.isReady,
                selectedModelId = networkData.selectedModelId,
                selectedModelName = networkData.selectedModelName,
                isOnline = true,
                lastSyncTime = Time.time
            };
        }
    }

    /// <summary>
    /// ���紫���õ�������ݽṹ
    /// </summary>
    [System.Serializable]
    public class RoomPlayerNetworkData
    {
        public ushort playerId;
        public string playerName;
        public bool isHost;
        public bool isReady;
        public int selectedModelId;
        public string selectedModelName;
    }
}