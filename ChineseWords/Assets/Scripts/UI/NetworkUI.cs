using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core.Network;
using Core;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace UI
{
    /// <summary>
    /// 简化版NetworkUI - 只动态生成PlayerConsole
    /// PlayerList为预设的静态UI
    /// 添加了对外接口供ArrowManager调用
    /// </summary>
    public class NetworkUI : MonoBehaviour
    {
        [Header("静态PlayerList引用")]
        [SerializeField] private GameObject playerListPanel;           // 预设的PlayerList面板
        [SerializeField] private Transform consolesContainer;          // Consoles容器（PlayerList的子对象）

        [Header("动态Console预制体")]
        [SerializeField] private GameObject playerConsolePrefab;       // PlayerConsole预制体

        [Header("UI配置")]
        [SerializeField] private float playerConsoleHeight = 78f;      // 单个PlayerConsole高度
        [SerializeField] private Vector3 consolePositionOffset = new Vector3(-1f, 0f, 0f); // Console位置偏移

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs;

        // 玩家UI状态数据
        [System.Serializable]
        public class GamePlayerUIState
        {
            public ushort playerId;
            public string playerName;
            public bool isHost;
            public int currentHealth;
            public int maxHealth;
            public bool isAlive;
            public bool isCurrentTurn;
        }

        // 玩家Console组件（新结构）
        [System.Serializable]
        public class PlayerListItemComponents
        {
            public GameObject itemObject;

            // 6个Image组件
            public Image backImage;           // 底色
            public Image edgeImage;           // 边框
            public Image beChoosenImage;      // 鼠标悬停效果（默认禁用）
            public Image playerRoundImage;    // 回合指示器
            public Image iconImage;           // 玩家头像
            public Image heartImage;          // 血量标志

            // 3个Text组件
            public TMP_Text iconText;         // 玩家名称首字母
            public TMP_Text hpText;           // 当前血量
            public TMP_Text nameText;         // 玩家名称
        }

        // ✅ 新增：对外公开的PlayerConsole信息结构
        [System.Serializable]
        public class PlayerConsoleInfo
        {
            public ushort playerId;
            public string playerName;
            public bool isAlive;
            public bool isCurrentTurn;
            public GameObject consoleObject;
            public Image beChoosenImage;      // 直接暴露BeChoosen Image
            public RectTransform consoleRect; // Console的RectTransform
        }

        // 私有字段
        private Dictionary<ushort, GamePlayerUIState> gamePlayerStates;
        private Dictionary<ushort, PlayerListItemComponents> playerUIItems;
        private ushort currentTurnPlayerId;
        private int currentQuestionNumber;
        private bool isInitialized = false;

        #region Unity生命周期

        private void Start()
        {
            InitializeUI();
            RegisterNetworkEvents();
            StartCoroutine(DelayedInitialization());
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
            ClearPlayerUIItems();
        }

        #endregion

        #region 初始化

        private IEnumerator DelayedInitialization()
        {
            yield return new WaitForSeconds(0.5f);
            isInitialized = true;

            if (HostGameManager.Instance != null && HostGameManager.Instance.IsGameInProgress)
            {
                ShowGamePlayerList();
                SyncFromHostGameManager();
            }
        }

        private void InitializeUI()
        {
            gamePlayerStates = new Dictionary<ushort, GamePlayerUIState>();
            playerUIItems = new Dictionary<ushort, PlayerListItemComponents>();

            // 验证必要的引用
            if (playerListPanel == null)
            {
                Debug.LogError("[NetworkUI] PlayerList面板未分配！");
            }

            if (consolesContainer == null)
            {
                Debug.LogError("[NetworkUI] Consoles容器未分配！");
            }

            if (playerConsolePrefab == null)
            {
                Debug.LogError("[NetworkUI] PlayerConsole预制体未分配！");
            }

            LogDebug("NetworkUI 初始化完成");
        }

        public void OnPlayerStateSyncReceived(ushort playerId, string playerName, bool isHost, int currentHealth, int maxHealth, bool isAlive)
        {
            LogDebug($"同步玩家状态: {playerName} (ID:{playerId}) HP:{currentHealth}/{maxHealth}");

            // 确保PlayerList显示
            ShowGamePlayerList();

            if (!gamePlayerStates.ContainsKey(playerId))
                gamePlayerStates[playerId] = new GamePlayerUIState();

            var playerState = gamePlayerStates[playerId];
            playerState.playerId = playerId;
            playerState.playerName = playerName;
            playerState.isHost = isHost;
            playerState.currentHealth = currentHealth;
            playerState.maxHealth = maxHealth;
            playerState.isAlive = isAlive;
            playerState.isCurrentTurn = (playerId == currentTurnPlayerId);

            UpdateGamePlayerListUI();
        }

        #endregion

        #region 网络事件

        private void RegisterNetworkEvents()
        {
            NetworkManager.OnPlayerJoined += OnPlayerJoined;
            NetworkManager.OnPlayerLeft += OnPlayerLeft;
        }

        private void UnregisterNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnPlayerLeft;
            }
        }

        #endregion

        #region UI更新

        private void UpdateGamePlayerListUI()
        {
            UpdatePlayerListItems();
            UpdateConsolesHeight();
        }

        /// <summary>
        /// 更新Consoles容器高度
        /// </summary>
        private void UpdateConsolesHeight()
        {
            if (consolesContainer == null) return;

            int playerCount = gamePlayerStates.Count;
            float totalHeight = playerCount * playerConsoleHeight;

            RectTransform consolesRect = consolesContainer.GetComponent<RectTransform>();
            if (consolesRect != null)
            {
                Vector2 sizeDelta = consolesRect.sizeDelta;
                sizeDelta.y = totalHeight;
                consolesRect.sizeDelta = sizeDelta;

                LogDebug($"更新Consoles高度: {playerCount} 个玩家 x {playerConsoleHeight} = {totalHeight}");
            }
        }

        private void UpdatePlayerListItems()
        {
            if (consolesContainer == null) return;

            // 创建或更新玩家UI
            foreach (var playerState in gamePlayerStates.Values)
            {
                if (!playerUIItems.ContainsKey(playerState.playerId))
                {
                    CreatePlayerUIItem(playerState.playerId);
                }
                UpdatePlayerUIItem(playerState.playerId);
            }

            // 移除已离开玩家
            var playersToRemove = playerUIItems.Keys.Where(id => !gamePlayerStates.ContainsKey(id)).ToList();
            foreach (var playerId in playersToRemove)
            {
                RemovePlayerUIItem(playerId);
            }
        }

        private void CreatePlayerUIItem(ushort playerId)
        {
            if (playerConsolePrefab == null || consolesContainer == null) return;

            GameObject itemObj = Instantiate(playerConsolePrefab, consolesContainer);
            itemObj.name = $"PlayerConsole_{playerId}";

            // 设置RectTransform
            RectTransform itemRect = itemObj.GetComponent<RectTransform>();
            if (itemRect != null)
            {
                // 设置锚点为顶部中心
                itemRect.anchorMin = new Vector2(0.5f, 1f);
                itemRect.anchorMax = new Vector2(0.5f, 1f);
                itemRect.pivot = new Vector2(0.5f, 1f);

                // 计算Y位置（按创建顺序从上往下）
                int index = playerUIItems.Count;
                float yPosition = -index * playerConsoleHeight;
                itemRect.anchoredPosition = new Vector2(consolePositionOffset.x, yPosition);

                // 确保尺寸正确
                itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, playerConsoleHeight);
            }

            // 查找新结构的组件
            PlayerListItemComponents components = new PlayerListItemComponents
            {
                itemObject = itemObj,
                backImage = FindComponentInChildren<Image>(itemObj, "back"),
                edgeImage = FindComponentInChildren<Image>(itemObj, "Edge"),
                beChoosenImage = FindComponentInChildren<Image>(itemObj, "BeChoosen"),
                playerRoundImage = FindComponentInChildren<Image>(itemObj, "PlayerRound"),
                iconImage = FindComponentInChildren<Image>(itemObj, "Icon"),
                heartImage = FindComponentInChildren<Image>(itemObj, "Heart"),
                iconText = FindComponentInChildren<TMP_Text>(itemObj, "IconText"),
                hpText = FindComponentInChildren<TMP_Text>(itemObj, "HPText"),
                nameText = FindComponentInChildren<TMP_Text>(itemObj, "NameText")
            };

            playerUIItems[playerId] = components;

            // 确保BeChoosen默认禁用
            if (components.beChoosenImage != null)
                components.beChoosenImage.gameObject.SetActive(false);

            LogDebug($"创建玩家Console: {playerId}，位置: {itemRect?.anchoredPosition}");
        }

        private void UpdatePlayerUIItem(ushort playerId)
        {
            if (!playerUIItems.ContainsKey(playerId) || !gamePlayerStates.ContainsKey(playerId))
                return;

            var components = playerUIItems[playerId];
            var playerState = gamePlayerStates[playerId];

            // 更新玩家名称
            if (components.nameText != null)
                components.nameText.text = playerState.playerName;

            // 更新头像文字（首字母）
            if (components.iconText != null)
            {
                string firstChar = string.IsNullOrEmpty(playerState.playerName) ? "?" : playerState.playerName[0].ToString();
                components.iconText.text = firstChar;
            }

            // 更新血量显示
            if (components.hpText != null)
                components.hpText.text = playerState.currentHealth.ToString();

            // 更新回合状态
            if (components.playerRoundImage != null)
                components.playerRoundImage.gameObject.SetActive(playerState.isCurrentTurn);

            // 更新存活状态（透明度）
            SetItemAlpha(components, playerState.isAlive ? 1f : 0.5f);
        }

        private void SetItemAlpha(PlayerListItemComponents components, float alpha)
        {
            if (components.itemObject == null) return;

            CanvasGroup canvasGroup = components.itemObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = components.itemObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = alpha;
        }

        private void RemovePlayerUIItem(ushort playerId)
        {
            if (!playerUIItems.ContainsKey(playerId)) return;

            var components = playerUIItems[playerId];
            if (components.itemObject != null)
                Destroy(components.itemObject);

            playerUIItems.Remove(playerId);
            LogDebug($"移除玩家Console: {playerId}");

            // 重新排列剩余的Console位置
            RearrangePlayerConsoles();
        }

        /// <summary>
        /// 重新排列玩家Console位置
        /// </summary>
        private void RearrangePlayerConsoles()
        {
            int index = 0;
            foreach (var kvp in playerUIItems)
            {
                var components = kvp.Value;
                if (components.itemObject != null)
                {
                    RectTransform itemRect = components.itemObject.GetComponent<RectTransform>();
                    if (itemRect != null)
                    {
                        float yPosition = -index * playerConsoleHeight;
                        itemRect.anchoredPosition = new Vector2(consolePositionOffset.x, yPosition);
                    }
                }
                index++;
            }
            LogDebug($"重新排列{index}个PlayerConsole");
        }

        private void ClearPlayerUIItems()
        {
            foreach (var components in playerUIItems.Values)
            {
                if (components.itemObject != null)
                    Destroy(components.itemObject);
            }
            playerUIItems.Clear();
        }

        #endregion

        #region 对外公共接口（供ArrowManager调用）
        /// <summary>
        /// 根据玩家ID获取PlayerConsole信息
        /// </summary>
        public PlayerConsoleInfo GetPlayerConsoleInfo(ushort playerId)
        {
            if (!playerUIItems.ContainsKey(playerId) || !gamePlayerStates.ContainsKey(playerId))
            {
                return null;
            }

            var components = playerUIItems[playerId];
            var playerState = gamePlayerStates[playerId];

            if (components.itemObject == null)
            {
                return null;
            }

            return new PlayerConsoleInfo
            {
                playerId = playerId,
                playerName = playerState.playerName,
                isAlive = playerState.isAlive,
                isCurrentTurn = playerState.isCurrentTurn,
                consoleObject = components.itemObject,
                beChoosenImage = components.beChoosenImage,
                consoleRect = components.itemObject.GetComponent<RectTransform>()
            };
        }

        /// <summary>
        /// 显示指定玩家的BeChoosen效果
        /// </summary>
        public bool ShowPlayerBeChosenEffect(ushort playerId)
        {
            var consoleInfo = GetPlayerConsoleInfo(playerId);
            if (consoleInfo == null || consoleInfo.beChoosenImage == null)
            {
                LogDebug($"无法显示玩家{playerId}的BeChoosen效果：Console或Image不存在");
                return false;
            }

            consoleInfo.beChoosenImage.gameObject.SetActive(true);
            LogDebug($"显示玩家{playerId}的BeChoosen效果");
            return true;
        }

        /// <summary>
        /// 隐藏指定玩家的BeChoosen效果
        /// </summary>
        public bool HidePlayerBeChosenEffect(ushort playerId)
        {
            var consoleInfo = GetPlayerConsoleInfo(playerId);
            if (consoleInfo == null || consoleInfo.beChoosenImage == null)
            {
                return false;
            }

            consoleInfo.beChoosenImage.gameObject.SetActive(false);
            LogDebug($"隐藏玩家{playerId}的BeChoosen效果");
            return true;
        }

        /// <summary>
        /// 检查点是否在指定PlayerConsole范围内
        /// </summary>
        public bool IsPointInPlayerConsole(Vector2 screenPoint, ushort playerId, Camera uiCamera = null)
        {
            var consoleInfo = GetPlayerConsoleInfo(playerId);
            if (consoleInfo == null || consoleInfo.consoleRect == null)
            {
                return false;
            }

            // 转换为本地坐标进行检测
            Vector2 localPoint;
            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                consoleInfo.consoleRect, screenPoint, uiCamera, out localPoint);

            if (!success) return false;

            // 检查是否在矩形范围内
            Rect rect = consoleInfo.consoleRect.rect;
            return rect.Contains(localPoint);
        }

        /// <summary>
        /// 检查点是否在任意PlayerConsole范围内
        /// </summary>
        public ushort GetPlayerConsoleAtPoint(Vector2 screenPoint, Camera uiCamera = null)
        {
            foreach (var kvp in playerUIItems)
            {
                ushort playerId = kvp.Key;
                if (IsPointInPlayerConsole(screenPoint, playerId, uiCamera))
                {
                    return playerId;
                }
            }
            return 0; // 未找到
        }

        #endregion

        #region 数据同步

        public void SyncFromHostGameManager()
        {
            if (HostGameManager.Instance == null) return;

            var hostManager = HostGameManager.Instance;
            var playerStateManagerField = typeof(HostGameManager).GetField("playerStateManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (playerStateManagerField != null)
            {
                var playerStateManager = playerStateManagerField.GetValue(hostManager);
                if (playerStateManager != null)
                {
                    var getAllStatesMethod = playerStateManager.GetType().GetMethod("GetAllPlayerStates");
                    if (getAllStatesMethod != null)
                    {
                        var allStates = getAllStatesMethod.Invoke(playerStateManager, null) as System.Collections.IDictionary;
                        if (allStates != null)
                        {
                            SyncPlayerStatesFromHostManager(allStates);
                        }
                    }
                }
            }

            currentTurnPlayerId = hostManager.CurrentTurnPlayer;
            UpdateGamePlayerListUI();
        }

        private void SyncPlayerStatesFromHostManager(System.Collections.IDictionary hostPlayerStates)
        {
            gamePlayerStates.Clear();

            foreach (System.Collections.DictionaryEntry entry in hostPlayerStates)
            {
                var playerId = (ushort)entry.Key;
                var hostPlayerState = entry.Value;

                var playerIdField = hostPlayerState.GetType().GetField("playerId");
                var playerNameField = hostPlayerState.GetType().GetField("playerName");
                var healthField = hostPlayerState.GetType().GetField("health");
                var maxHealthField = hostPlayerState.GetType().GetField("maxHealth");
                var isAliveField = hostPlayerState.GetType().GetField("isAlive");

                if (playerIdField != null && playerNameField != null)
                {
                    var uiState = new GamePlayerUIState
                    {
                        playerId = (ushort)playerIdField.GetValue(hostPlayerState),
                        playerName = (string)playerNameField.GetValue(hostPlayerState),
                        isHost = IsHostPlayer(playerId),
                        currentHealth = healthField != null ? (int)healthField.GetValue(hostPlayerState) : 100,
                        maxHealth = maxHealthField != null ? (int)maxHealthField.GetValue(hostPlayerState) : 100,
                        isAlive = isAliveField != null ? (bool)isAliveField.GetValue(hostPlayerState) : true,
                        isCurrentTurn = (playerId == currentTurnPlayerId)
                    };

                    gamePlayerStates[playerId] = uiState;
                }
            }
        }

        private bool IsHostPlayer(ushort playerId)
        {
            return NetworkManager.Instance?.IsHostPlayer(playerId) ?? false;
        }

        #endregion

        #region 网络消息处理

        public void OnHealthUpdateReceived(ushort playerId, int newHealth, int maxHealth)
        {
            if (gamePlayerStates.ContainsKey(playerId))
            {
                var playerState = gamePlayerStates[playerId];
                playerState.currentHealth = newHealth;
                playerState.maxHealth = maxHealth;
                playerState.isAlive = newHealth > 0;

                UpdatePlayerUIItem(playerId);
            }
        }

        public void OnTurnChangedReceived(ushort newTurnPlayerId)
        {
            foreach (var playerState in gamePlayerStates.Values)
            {
                playerState.isCurrentTurn = (playerState.playerId == newTurnPlayerId);
            }

            currentTurnPlayerId = newTurnPlayerId;
            UpdateGamePlayerListUI();
        }

        public void OnGameProgressReceived(int questionNumber, int alivePlayerCount, ushort turnPlayerId)
        {
            currentQuestionNumber = questionNumber;
            currentTurnPlayerId = turnPlayerId;

            foreach (var playerState in gamePlayerStates.Values)
            {
                playerState.isCurrentTurn = (playerState.playerId == currentTurnPlayerId);
            }

            UpdateGamePlayerListUI();
        }

        public void OnPlayerAnswerResultReceived(ushort playerId, bool isCorrect, string answer)
        {
            LogDebug($"玩家{playerId}答题结果: {(isCorrect ? "正确" : "错误")} - {answer}");
        }

        public void OnGameStartReceived(int totalPlayerCount, int alivePlayerCount, ushort firstTurnPlayerId)
        {
            ShowGamePlayerList();

            currentTurnPlayerId = firstTurnPlayerId;

            foreach (var playerState in gamePlayerStates.Values)
            {
                playerState.isCurrentTurn = (playerState.playerId == currentTurnPlayerId);
            }

            UpdateGamePlayerListUI();
        }

        #endregion

        #region 基础网络事件

        private void OnPlayerJoined(ushort playerId)
        {
            if (!gamePlayerStates.ContainsKey(playerId))
            {
                var playerState = new GamePlayerUIState
                {
                    playerId = playerId,
                    playerName = $"玩家{playerId}",
                    isHost = IsHostPlayer(playerId),
                    currentHealth = 100,
                    maxHealth = 100,
                    isAlive = true,
                    isCurrentTurn = false
                };

                gamePlayerStates[playerId] = playerState;
                UpdateGamePlayerListUI();
            }
        }

        private void OnPlayerLeft(ushort playerId)
        {
            if (gamePlayerStates.ContainsKey(playerId))
            {
                gamePlayerStates.Remove(playerId);
                RemovePlayerUIItem(playerId);
                UpdateGamePlayerListUI();
            }
        }

        #endregion

        #region 工具方法

        private T FindComponentInChildren<T>(GameObject parent, string childName) where T : Component
        {
            Transform foundChild = FindChildRecursive(parent.transform, childName);
            return foundChild != null ? foundChild.GetComponent<T>() : null;
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                Transform found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[NetworkUI] {message}");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 供外部查询玩家存活状态
        /// </summary>
        public bool IsPlayerAlive(ushort playerId)
        {
            if (gamePlayerStates.ContainsKey(playerId))
            {
                return gamePlayerStates[playerId].isAlive;
            }
            return false; // 玩家不存在时返回false
        }

        /// <summary>
        /// 检查是否包含指定玩家
        /// </summary>
        public bool ContainsPlayer(ushort playerId)
        {
            return gamePlayerStates.ContainsKey(playerId);
        }

        /// <summary>
        /// 显示玩家列表
        /// </summary>
        public void ShowGamePlayerList()
        {
            if (playerListPanel != null)
                playerListPanel.SetActive(true);
        }

        /// <summary>
        /// 隐藏玩家列表
        /// </summary>
        public void HideGamePlayerList()
        {
            if (playerListPanel != null)
                playerListPanel.SetActive(false);
        }

        /// <summary>
        /// 检查玩家列表是否显示
        /// </summary>
        public bool IsPlayerListActive()
        {
            return playerListPanel != null && playerListPanel.activeInHierarchy;
        }

        // 兼容性接口
        public void ShowNetworkPanel() => ShowGamePlayerList();
        public void HideNetworkPanel() => HideGamePlayerList();
        public void ToggleNetworkPanel()
        {
            if (IsPlayerListActive())
                HideGamePlayerList();
            else
                ShowGamePlayerList();
        }

        #endregion
    }
}