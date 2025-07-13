using UnityEngine;
using System.Collections.Generic;

namespace RoomScene.Data
{
    /// <summary>
    /// ���ģ��ѡ������ - �ڳ����䴫����ҵ�ģ��ѡ����Ϣ
    /// ʹ�þ�̬���ݺ�PlayerPrefsȷ�����ݳ־���
    /// </summary>
    public static class PlayerModelSelectionData
    {
        private const string PLAYER_MODEL_PREFS_KEY = "PlayerModelSelections";
        private const string PLAYER_COUNT_PREFS_KEY = "PlayerModelSelectionsCount";

        // ��̬���ݻ���
        private static Dictionary<ushort, int> cachedSelections = new Dictionary<ushort, int>();
        private static bool isDataLoaded = false;

        #region ����ͼ���

        /// <summary>
        /// ����������ҵ�ģ��ѡ��
        /// </summary>
        public static void SaveSelections(Dictionary<ushort, int> selections)
        {
            if (selections == null || selections.Count == 0)
            {
                Debug.LogWarning("[PlayerModelSelectionData] û��ģ��ѡ��������Ҫ����");
                return;
            }

            // ���»���
            cachedSelections.Clear();
            foreach (var kvp in selections)
            {
                cachedSelections[kvp.Key] = kvp.Value;
            }

            // ���浽PlayerPrefs
            PlayerPrefs.SetInt(PLAYER_COUNT_PREFS_KEY, selections.Count);

            int index = 0;
            foreach (var kvp in selections)
            {
                PlayerPrefs.SetInt($"{PLAYER_MODEL_PREFS_KEY}_Player_{index}", kvp.Key);
                PlayerPrefs.SetInt($"{PLAYER_MODEL_PREFS_KEY}_Model_{index}", kvp.Value);
                index++;
            }

            PlayerPrefs.Save();
            isDataLoaded = true;

            Debug.Log($"[PlayerModelSelectionData] ������ {selections.Count} ����ҵ�ģ��ѡ������");
        }

        /// <summary>
        /// ����������ҵ�ģ��ѡ��
        /// </summary>
        public static Dictionary<ushort, int> LoadSelections()
        {
            if (isDataLoaded && cachedSelections.Count > 0)
            {
                Debug.Log($"[PlayerModelSelectionData] �ӻ������ {cachedSelections.Count} ��ģ��ѡ��");
                return new Dictionary<ushort, int>(cachedSelections);
            }

            var selections = new Dictionary<ushort, int>();

            int playerCount = PlayerPrefs.GetInt(PLAYER_COUNT_PREFS_KEY, 0);
            if (playerCount == 0)
            {
                Debug.LogWarning("[PlayerModelSelectionData] û���ҵ������ģ��ѡ������");
                return selections;
            }

            for (int i = 0; i < playerCount; i++)
            {
                ushort playerId = (ushort)PlayerPrefs.GetInt($"{PLAYER_MODEL_PREFS_KEY}_Player_{i}", 0);
                int modelId = PlayerPrefs.GetInt($"{PLAYER_MODEL_PREFS_KEY}_Model_{i}", 0);

                if (playerId > 0)
                {
                    selections[playerId] = modelId;
                }
            }

            // ���»���
            cachedSelections.Clear();
            foreach (var kvp in selections)
            {
                cachedSelections[kvp.Key] = kvp.Value;
            }

            isDataLoaded = true;
            Debug.Log($"[PlayerModelSelectionData] ��PlayerPrefs������ {selections.Count} ��ģ��ѡ��");
            return selections;
        }

        /// <summary>
        /// ��ȡָ����ҵ�ģ��ѡ��
        /// </summary>
        public static int GetPlayerModelId(ushort playerId, int defaultModelId = 0)
        {
            // �ȼ�黺��
            if (cachedSelections.ContainsKey(playerId))
            {
                return cachedSelections[playerId];
            }

            // ���������û�У����Լ�����������
            if (!isDataLoaded)
            {
                LoadSelections();
            }

            return cachedSelections.TryGetValue(playerId, out int modelId) ? modelId : defaultModelId;
        }

        /// <summary>
        /// ����ָ����ҵ�ģ��ѡ��
        /// </summary>
        public static void SetPlayerModelId(ushort playerId, int modelId)
        {
            cachedSelections[playerId] = modelId;

            // ����������µ�����
            SaveSelections(cachedSelections);
        }

        /// <summary>
        /// �������ģ��ѡ������
        /// </summary>
        public static void ClearAllSelections()
        {
            cachedSelections.Clear();

            // ���PlayerPrefs����
            int playerCount = PlayerPrefs.GetInt(PLAYER_COUNT_PREFS_KEY, 0);
            for (int i = 0; i < playerCount; i++)
            {
                PlayerPrefs.DeleteKey($"{PLAYER_MODEL_PREFS_KEY}_Player_{i}");
                PlayerPrefs.DeleteKey($"{PLAYER_MODEL_PREFS_KEY}_Model_{i}");
            }
            PlayerPrefs.DeleteKey(PLAYER_COUNT_PREFS_KEY);
            PlayerPrefs.Save();

            isDataLoaded = false;
            Debug.Log("[PlayerModelSelectionData] ���������ģ��ѡ������");
        }

        /// <summary>
        /// ����Ƿ��п��õ�ģ��ѡ������
        /// </summary>
        public static bool HasSelectionData()
        {
            if (isDataLoaded)
            {
                return cachedSelections.Count > 0;
            }

            return PlayerPrefs.GetInt(PLAYER_COUNT_PREFS_KEY, 0) > 0;
        }

        /// <summary>
        /// ��ȡ�������ID
        /// </summary>
        public static ushort[] GetAllPlayerIds()
        {
            if (!isDataLoaded)
            {
                LoadSelections();
            }

            var playerIds = new ushort[cachedSelections.Count];
            cachedSelections.Keys.CopyTo(playerIds, 0);
            return playerIds;
        }

        /// <summary>
        /// ��ȡ������Ϣ
        /// </summary>
        public static string GetDebugInfo()
        {
            if (!isDataLoaded)
            {
                LoadSelections();
            }

            var info = new System.Text.StringBuilder();
            info.AppendLine($"[PlayerModelSelectionData] ����״̬: �Ѽ���={isDataLoaded}, �����={cachedSelections.Count}");

            foreach (var kvp in cachedSelections)
            {
                info.AppendLine($"  ��� {kvp.Key} -> ģ�� {kvp.Value}");
            }

            return info.ToString();
        }

        #endregion
    }
}