using UnityEngine;
using System.Collections.Generic;

namespace RoomScene.Data
{
    /// <summary>
    /// 玩家模型选择数据 - 在场景间传递玩家的模型选择信息
    /// 使用静态数据和PlayerPrefs确保数据持久性
    /// </summary>
    public static class PlayerModelSelectionData
    {
        private const string PLAYER_MODEL_PREFS_KEY = "PlayerModelSelections";
        private const string PLAYER_COUNT_PREFS_KEY = "PlayerModelSelectionsCount";

        // 静态数据缓存
        private static Dictionary<ushort, int> cachedSelections = new Dictionary<ushort, int>();
        private static bool isDataLoaded = false;

        #region 保存和加载

        /// <summary>
        /// 保存所有玩家的模型选择
        /// </summary>
        public static void SaveSelections(Dictionary<ushort, int> selections)
        {
            if (selections == null || selections.Count == 0)
            {
                Debug.LogWarning("[PlayerModelSelectionData] 没有模型选择数据需要保存");
                return;
            }

            // 更新缓存
            cachedSelections.Clear();
            foreach (var kvp in selections)
            {
                cachedSelections[kvp.Key] = kvp.Value;
            }

            // 保存到PlayerPrefs
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

            Debug.Log($"[PlayerModelSelectionData] 保存了 {selections.Count} 个玩家的模型选择数据");
        }

        /// <summary>
        /// 加载所有玩家的模型选择
        /// </summary>
        public static Dictionary<ushort, int> LoadSelections()
        {
            if (isDataLoaded && cachedSelections.Count > 0)
            {
                Debug.Log($"[PlayerModelSelectionData] 从缓存加载 {cachedSelections.Count} 个模型选择");
                return new Dictionary<ushort, int>(cachedSelections);
            }

            var selections = new Dictionary<ushort, int>();

            int playerCount = PlayerPrefs.GetInt(PLAYER_COUNT_PREFS_KEY, 0);
            if (playerCount == 0)
            {
                Debug.LogWarning("[PlayerModelSelectionData] 没有找到保存的模型选择数据");
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

            // 更新缓存
            cachedSelections.Clear();
            foreach (var kvp in selections)
            {
                cachedSelections[kvp.Key] = kvp.Value;
            }

            isDataLoaded = true;
            Debug.Log($"[PlayerModelSelectionData] 从PlayerPrefs加载了 {selections.Count} 个模型选择");
            return selections;
        }

        /// <summary>
        /// 获取指定玩家的模型选择
        /// </summary>
        public static int GetPlayerModelId(ushort playerId, int defaultModelId = 0)
        {
            // 先检查缓存
            if (cachedSelections.ContainsKey(playerId))
            {
                return cachedSelections[playerId];
            }

            // 如果缓存中没有，尝试加载所有数据
            if (!isDataLoaded)
            {
                LoadSelections();
            }

            return cachedSelections.TryGetValue(playerId, out int modelId) ? modelId : defaultModelId;
        }

        /// <summary>
        /// 设置指定玩家的模型选择
        /// </summary>
        public static void SetPlayerModelId(ushort playerId, int modelId)
        {
            cachedSelections[playerId] = modelId;

            // 立即保存更新的数据
            SaveSelections(cachedSelections);
        }

        /// <summary>
        /// 清除所有模型选择数据
        /// </summary>
        public static void ClearAllSelections()
        {
            cachedSelections.Clear();

            // 清除PlayerPrefs数据
            int playerCount = PlayerPrefs.GetInt(PLAYER_COUNT_PREFS_KEY, 0);
            for (int i = 0; i < playerCount; i++)
            {
                PlayerPrefs.DeleteKey($"{PLAYER_MODEL_PREFS_KEY}_Player_{i}");
                PlayerPrefs.DeleteKey($"{PLAYER_MODEL_PREFS_KEY}_Model_{i}");
            }
            PlayerPrefs.DeleteKey(PLAYER_COUNT_PREFS_KEY);
            PlayerPrefs.Save();

            isDataLoaded = false;
            Debug.Log("[PlayerModelSelectionData] 清除了所有模型选择数据");
        }

        /// <summary>
        /// 检查是否有可用的模型选择数据
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
        /// 获取所有玩家ID
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
        /// 获取调试信息
        /// </summary>
        public static string GetDebugInfo()
        {
            if (!isDataLoaded)
            {
                LoadSelections();
            }

            var info = new System.Text.StringBuilder();
            info.AppendLine($"[PlayerModelSelectionData] 数据状态: 已加载={isDataLoaded}, 玩家数={cachedSelections.Count}");

            foreach (var kvp in cachedSelections)
            {
                info.AppendLine($"  玩家 {kvp.Key} -> 模型 {kvp.Value}");
            }

            return info.ToString();
        }

        #endregion
    }
}