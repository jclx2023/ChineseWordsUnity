using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Core.Network;
using Core;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace UI
{
    /// <summary>
    /// 简化版游戏内玩家列表UI
    /// 专注于显示游戏进行中的玩家状态
    /// </summary>
    public class NetworkUI : MonoBehaviour
    {
        [Header("游戏玩家列表面板")]
        [SerializeField] private GameObject gamePlayerListPanel;
        [SerializeField] private Transform playerListContent;
        [SerializeField] private GameObject playerListItemPrefab;
        [SerializeField] private TMP_Text gameProgressText;
        [SerializeField] private TMP_Text currentTurnPlayerText;

        [Header("UI配置")]
        [SerializeField] private float healthBarAnimationSpeed = 2f;
        [SerializeField] private Color currentTurnPlayerColor = Color.yellow;
        [SerializeField] private Color normalPlayerColor = Color.white;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

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

            public float HealthPercentage => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        }

        // 玩家列表项组件
        [System.Serializable]
        public class PlayerListItemComponents
        {
            public GameObject itemObject;
            public Image backgroundImage;
            public GameObject turnIndicator;
            public TMP_Text playerNameText;
            public TMP_Text roleText;
            public Slider healthBar;
            public TMP_Text healthText;
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

        /// <summary>
        /// 延迟初始化
        /// </summary>
        private IEnumerator DelayedInitialization()
        {
            yield return new WaitForSeconds(0.5f);

            if (gamePlayerListPanel != null)
                gamePlayerListPanel.SetActive(true);

            isInitialized = true;

            // 如果已经在游戏中，立即同步状态
            if (HostGameManager.Instance != null && HostGameManager.Instance.IsGameInProgress)
            {
                SyncFromHostGameManager();
            }
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            gamePlayerStates = new Dictionary<ushort, GamePlayerUIState>();
            playerUIItems = new Dictionary<ushort, PlayerListItemComponents>();

            LogDebug("NetworkUI 初始化完成");
        }
        /// <summary>
        /// 处理玩家状态同步回调
        /// </summary>
        public void OnPlayerStateSyncReceived(ushort playerId, string playerName, bool isHost, int currentHealth, int maxHealth, bool isAlive)
        {
            LogDebug($"同步玩家状态: {playerName} (ID:{playerId}) HP:{currentHealth}/{maxHealth}");

            // 创建或更新玩家状态
            if (!gamePlayerStates.ContainsKey(playerId))
            {
                gamePlayerStates[playerId] = new GamePlayerUIState();
            }

            var playerState = gamePlayerStates[playerId];
            playerState.playerId = playerId;
            playerState.playerName = playerName;
            playerState.isHost = isHost;
            playerState.currentHealth = currentHealth;
            playerState.maxHealth = maxHealth;
            playerState.isAlive = isAlive;
            playerState.isCurrentTurn = (playerId == currentTurnPlayerId);

            // 立即更新UI
            UpdateGamePlayerListUI();
        }
        #endregion

        #region 网络事件注册

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

        #region 玩家列表UI管理

        private void UpdateGamePlayerListUI()
        {
            UpdateGameProgress();
            UpdateCurrentTurnPlayer();
            UpdatePlayerListItems();
        }

        /// <summary>
        /// 更新游戏进度信息
        /// </summary>
        private void UpdateGameProgress()
        {
            if (gameProgressText == null) return;

            int aliveCount = gamePlayerStates.Values.Count(p => p.isAlive);
            int totalCount = gamePlayerStates.Count;
            string progressInfo = $"第 {currentQuestionNumber} 题 | 存活玩家: {aliveCount}/{totalCount}";

            gameProgressText.text = progressInfo;
        }

        private void UpdateCurrentTurnPlayer()
        {
            if (currentTurnPlayerText == null) return;

            string turnInfo = "";
            if (currentTurnPlayerId != 0 && gamePlayerStates.ContainsKey(currentTurnPlayerId))
            {
                var currentPlayer = gamePlayerStates[currentTurnPlayerId];
                turnInfo = $"当前回合: {currentPlayer.playerName}";
            }
            else
            {
                turnInfo = "等待开始...";
            }

            currentTurnPlayerText.text = turnInfo;
        }

        /// <summary>
        /// 更新玩家列表项
        /// </summary>
        private void UpdatePlayerListItems()
        {
            if (playerListContent == null) return;

            // 创建或更新玩家UI项
            foreach (var playerState in gamePlayerStates.Values)
            {
                if (!playerUIItems.ContainsKey(playerState.playerId))
                {
                    CreatePlayerUIItem(playerState.playerId);
                }
                UpdatePlayerUIItem(playerState.playerId);
            }

            // 移除已离开玩家的UI项
            var playersToRemove = playerUIItems.Keys.Where(id => !gamePlayerStates.ContainsKey(id)).ToList();
            foreach (var playerId in playersToRemove)
            {
                RemovePlayerUIItem(playerId);
            }
        }

        /// <summary>
        /// 创建玩家UI项
        /// </summary>
        private void CreatePlayerUIItem(ushort playerId)
        {
            if (playerListItemPrefab == null || playerListContent == null)
            {
                LogDebug("无法创建玩家UI项：预制体或容器为空");
                return;
            }

            GameObject itemObj = Instantiate(playerListItemPrefab, playerListContent);
            itemObj.name = $"PlayerItem_{playerId}";

            // 查找组件
            PlayerListItemComponents components = new PlayerListItemComponents
            {
                itemObject = itemObj,
                backgroundImage = itemObj.GetComponent<Image>(),
                turnIndicator = FindChildByName(itemObj, "TurnIndicator"),
                playerNameText = FindComponentInChildren<TMP_Text>(itemObj, "PlayerName"),
                roleText = FindComponentInChildren<TMP_Text>(itemObj, "Role"),
                healthBar = FindComponentInChildren<Slider>(itemObj, "HealthBar"),
                healthText = FindComponentInChildren<TMP_Text>(itemObj, "HealthText")
            };

            playerUIItems[playerId] = components;
            LogDebug($"创建玩家UI项: {playerId}");
        }

        /// <summary>
        /// 更新玩家UI项
        /// </summary>
        private void UpdatePlayerUIItem(ushort playerId)
        {
            if (!playerUIItems.ContainsKey(playerId) || !gamePlayerStates.ContainsKey(playerId))
                return;

            var components = playerUIItems[playerId];
            var playerState = gamePlayerStates[playerId];

            // 更新玩家名称
            if (components.playerNameText != null)
            {
                components.playerNameText.text = playerState.playerName;
            }

            // 更新角色标识
            if (components.roleText != null)
            {
                components.roleText.text = playerState.isHost ? "Host" : "";
            }

            // 更新回合指示器
            if (components.turnIndicator != null)
            {
                components.turnIndicator.SetActive(playerState.isCurrentTurn);
            }

            // 更新背景颜色
            if (components.backgroundImage != null)
            {
                components.backgroundImage.color = playerState.isCurrentTurn ? currentTurnPlayerColor : normalPlayerColor;
            }

            // 更新血量条
            UpdateHealthBar(components, playerState);

            // 更新存活状态（透明度）
            SetItemAlpha(components, playerState.isAlive ? 1f : 0.5f);
        }

        /// <summary>
        /// 更新血量条
        /// </summary>
        private void UpdateHealthBar(PlayerListItemComponents components, GamePlayerUIState playerState)
        {
            if (components.healthBar != null)
            {
                float targetValue = playerState.HealthPercentage;
                StartCoroutine(AnimateHealthBar(components.healthBar, targetValue));
            }

            if (components.healthText != null)
            {
                components.healthText.text = $"{playerState.currentHealth}/{playerState.maxHealth}";
            }
        }

        /// <summary>
        /// 血量条动画协程
        /// </summary>
        private IEnumerator AnimateHealthBar(Slider healthBar, float targetValue)
        {
            float startValue = healthBar.value;
            float elapsed = 0f;
            float duration = 1f / healthBarAnimationSpeed;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                healthBar.value = Mathf.Lerp(startValue, targetValue, t);
                yield return null;
            }

            healthBar.value = targetValue;
        }

        /// <summary>
        /// 设置UI项的透明度
        /// </summary>
        private void SetItemAlpha(PlayerListItemComponents components, float alpha)
        {
            if (components.itemObject == null) return;

            CanvasGroup canvasGroup = components.itemObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = components.itemObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = alpha;
        }

        /// <summary>
        /// 移除玩家UI项
        /// </summary>
        private void RemovePlayerUIItem(ushort playerId)
        {
            if (!playerUIItems.ContainsKey(playerId)) return;

            var components = playerUIItems[playerId];
            if (components.itemObject != null)
            {
                Destroy(components.itemObject);
            }

            playerUIItems.Remove(playerId);
            LogDebug($"移除玩家UI项: {playerId}");
        }

        /// <summary>
        /// 清空所有玩家UI项
        /// </summary>
        private void ClearPlayerUIItems()
        {
            foreach (var components in playerUIItems.Values)
            {
                if (components.itemObject != null)
                {
                    Destroy(components.itemObject);
                }
            }
            playerUIItems.Clear();
        }

        #endregion

        #region 数据同步

        /// <summary>
        /// 从HostGameManager同步玩家状态（Host端）
        /// </summary>
        public void SyncFromHostGameManager()
        {
            if (HostGameManager.Instance == null) return;

            LogDebug("从HostGameManager同步玩家状态");

            // 通过反射获取PlayerStateManager
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

            // 同步当前回合玩家
            currentTurnPlayerId = hostManager.CurrentTurnPlayer;

            UpdateGamePlayerListUI();
        }

        /// <summary>
        /// 从HostGameManager的玩家状态同步数据
        /// </summary>
        private void SyncPlayerStatesFromHostManager(System.Collections.IDictionary hostPlayerStates)
        {
            gamePlayerStates.Clear();

            foreach (System.Collections.DictionaryEntry entry in hostPlayerStates)
            {
                var playerId = (ushort)entry.Key;
                var hostPlayerState = entry.Value;

                // 通过反射获取字段值
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

            LogDebug($"同步了 {gamePlayerStates.Count} 个玩家状态");
        }

        /// <summary>
        /// 判断是否为Host玩家
        /// </summary>
        private bool IsHostPlayer(ushort playerId)
        {
            return NetworkManager.Instance?.IsHostPlayer(playerId) ?? false;
        }

        #endregion

        #region 网络消息处理（使用Riptide Client消息回调）

        public void OnHealthUpdateReceived(ushort playerId, int newHealth, int maxHealth)
        {
            if (gamePlayerStates.ContainsKey(playerId))
            {
                var playerState = gamePlayerStates[playerId];
                playerState.currentHealth = newHealth;
                playerState.maxHealth = maxHealth;
                playerState.isAlive = newHealth > 0;

                UpdatePlayerUIItem(playerId);
                LogDebug($"玩家{playerId}血量更新: {newHealth}/{maxHealth}");
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

            LogDebug($"回合切换到玩家: {newTurnPlayerId}");
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
            LogDebug($"游戏进度更新: 第{questionNumber}题, 回合玩家{turnPlayerId}");
        }
        /// <summary>
        /// 处理玩家答题结果回调
        /// </summary>
        public void OnPlayerAnswerResultReceived(ushort playerId, bool isCorrect, string answer)
        {
            LogDebug($"玩家{playerId}答题结果: {(isCorrect ? "正确" : "错误")} - {answer}");

            // 如果需要在UI中显示答题结果，可以在这里处理
            // 比如更新玩家的答题状态图标等
        }
        /// <summary>
        /// 处理游戏开始回调
        /// </summary>
        public void OnGameStartReceived(int totalPlayerCount, int alivePlayerCount, ushort firstTurnPlayerId)
        {
            LogDebug($"游戏开始: 总玩家{totalPlayerCount}, 存活{alivePlayerCount}, 首回合玩家{firstTurnPlayerId}");

            // 确保UI显示
            if (gamePlayerListPanel != null)
                gamePlayerListPanel.SetActive(true);

            // 更新当前回合玩家
            currentTurnPlayerId = firstTurnPlayerId;

            // 更新所有玩家的回合状态
            foreach (var playerState in gamePlayerStates.Values)
            {
                playerState.isCurrentTurn = (playerState.playerId == currentTurnPlayerId);
            }

            UpdateGamePlayerListUI();
        }

        #endregion

        #region 基础网络事件处理

        private void OnPlayerJoined(ushort playerId)
        {
            LogDebug($"玩家加入: {playerId}");

            // 创建基本的玩家状态
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
            LogDebug($"玩家离开: {playerId}");

            if (gamePlayerStates.ContainsKey(playerId))
            {
                gamePlayerStates.Remove(playerId);
                RemovePlayerUIItem(playerId);
                UpdateGamePlayerListUI();
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 在子对象中查找指定名称的GameObject
        /// </summary>
        private GameObject FindChildByName(GameObject parent, string name)
        {
            Transform found = parent.transform.Find(name);
            return found != null ? found.gameObject : null;
        }

        /// <summary>
        /// 修复的组件查找方法 - 支持递归查找
        /// </summary>
        private T FindComponentInChildren<T>(GameObject parent, string childName) where T : Component
        {
            // 递归查找指定名称的子对象
            Transform foundChild = FindChildRecursive(parent.transform, childName);

            if (foundChild != null)
            {
                T component = foundChild.GetComponent<T>();
                if (component != null)
                {
                    LogDebug($"递归找到组件: {childName} -> {GetTransformPath(foundChild)}");
                    return component;
                }
                else
                {
                    LogDebug($"找到子对象 {childName} 但没有 {typeof(T).Name} 组件");
                }
            }
            else
            {
                LogDebug($"未找到名为 {childName} 的子对象");
            }

            return null;
        }

        /// <summary>
        /// 递归查找子对象
        /// </summary>
        private Transform FindChildRecursive(Transform parent, string name)
        {
            // 检查直接子对象
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
            }

            // 递归检查子对象的子对象
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                Transform found = FindChildRecursive(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }


        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkUI] {message}");
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 显示游戏玩家列表
        /// </summary>
        public void ShowGamePlayerList()
        {
            if (gamePlayerListPanel != null)
                gamePlayerListPanel.SetActive(true);
        }

        /// <summary>
        /// 隐藏游戏玩家列表
        /// </summary>
        public void HideGamePlayerList()
        {
            if (gamePlayerListPanel != null)
                gamePlayerListPanel.SetActive(false);
        }

        /// <summary>
        /// 手动刷新玩家状态
        /// </summary>
        public void RefreshPlayerStates()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                SyncFromHostGameManager();
            }
            UpdateGamePlayerListUI();
        }

        /// <summary>
        /// 获取玩家状态信息（调试用）
        /// </summary>
        public string GetPlayerStatesInfo()
        {
            string info = "=== 玩家状态信息 ===\n";
            info += $"游戏玩家数: {gamePlayerStates.Count}\n";
            info += $"当前回合玩家: {currentTurnPlayerId}\n";
            info += $"当前题目: {currentQuestionNumber}\n\n";

            foreach (var playerState in gamePlayerStates.Values)
            {
                info += $"- {playerState.playerName} (ID:{playerState.playerId}) ";
                info += $"血量:{playerState.currentHealth}/{playerState.maxHealth} ";
                info += $"存活:{playerState.isAlive} 回合:{playerState.isCurrentTurn}\n";
            }

            return info;
        }

        #endregion

        #region 兼容性接口（保持原有引用不变）

        /// <summary>
        /// 显示网络状态面板（兼容方法）
        /// </summary>
        public void ShowNetworkPanel()
        {
            ShowGamePlayerList();
        }

        /// <summary>
        /// 隐藏网络状态面板（兼容方法）
        /// </summary>
        public void HideNetworkPanel()
        {
            HideGamePlayerList();
        }

        /// <summary>
        /// 切换网络面板显示状态（兼容方法）
        /// </summary>
        public void ToggleNetworkPanel()
        {
            if (gamePlayerListPanel != null)
            {
                gamePlayerListPanel.SetActive(!gamePlayerListPanel.activeSelf);
            }
        }

        #endregion

        #region 定期更新

        /// <summary>
        /// 定期检查和更新
        /// </summary>
        private void Update()
        {
            if (!isInitialized) return;

            // 如果是Host端，定期同步状态（防止不一致）
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                if (Time.frameCount % 300 == 0) // 每5秒同步一次
                {
                    SyncFromHostGameManager();
                }
            }
        }

        #endregion

        #region 调试方法

#if UNITY_EDITOR
        [ContextMenu("显示玩家状态")]
        private void DebugShowPlayerStates()
        {
            Debug.Log(GetPlayerStatesInfo());
        }

        [ContextMenu("刷新玩家状态")]
        private void DebugRefreshPlayerStates()
        {
            RefreshPlayerStates();
        }

        [ContextMenu("从HostGameManager同步")]
        private void DebugSyncFromHost()
        {
            SyncFromHostGameManager();
        }
        /// <summary>
        /// 完整分析预制体结构
        /// </summary>
        [ContextMenu("完整分析预制体")]
        private void AnalyzePrefabCompletely()
        {
            if (playerListItemPrefab == null)
            {
                LogDebug("预制体为空！");
                return;
            }

            LogDebug("=== 完整预制体分析 ===");

            // 实例化预制体
            GameObject tempObj = Instantiate(playerListItemPrefab);
            tempObj.name = "AnalysisTemp";

            LogDebug($"根对象名称: '{tempObj.name}'");
            LogDebug($"根对象组件: {string.Join(", ", tempObj.GetComponents<Component>().Select(c => c.GetType().Name))}");

            // 递归分析所有子对象
            AnalyzeTransformRecursive(tempObj.transform, 0);

            // 分析所有相关组件
            LogDebug("\n=== 所有TextMeshPro组件 ===");
            var allTexts = tempObj.GetComponentsInChildren<TMP_Text>(true); // 包括非激活的
            for (int i = 0; i < allTexts.Length; i++)
            {
                var text = allTexts[i];
                LogDebug($"[{i}] GameObject: '{text.gameObject.name}' | 激活: {text.gameObject.activeInHierarchy} | 路径: {GetTransformPath(text.transform)}");
            }

            LogDebug("\n=== 所有Slider组件 ===");
            var allSliders = tempObj.GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < allSliders.Length; i++)
            {
                var slider = allSliders[i];
                LogDebug($"[{i}] GameObject: '{slider.gameObject.name}' | 激活: {slider.gameObject.activeInHierarchy} | 路径: {GetTransformPath(slider.transform)}");
            }

            LogDebug("\n=== 所有Image组件 ===");
            var allImages = tempObj.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < allImages.Length; i++)
            {
                var image = allImages[i];
                LogDebug($"[{i}] GameObject: '{image.gameObject.name}' | 激活: {image.gameObject.activeInHierarchy} | 路径: {GetTransformPath(image.transform)}");
            }

            Destroy(tempObj);
        }

        /// <summary>
        /// 递归分析Transform层级
        /// </summary>
        private void AnalyzeTransformRecursive(Transform transform, int depth)
        {
            string indent = new string(' ', depth * 2);

            var components = transform.GetComponents<Component>();
            string componentList = string.Join(", ", components.Select(c => c.GetType().Name));

            LogDebug($"{indent}├─ '{transform.name}' | 激活: {transform.gameObject.activeInHierarchy} | 组件: [{componentList}]");

            if (depth < 5) // 防止无限递归
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    AnalyzeTransformRecursive(transform.GetChild(i), depth + 1);
                }
            }
        }
#endif
        /// <summary>
        /// 获取Transform的完整路径
        /// </summary>
        private string GetTransformPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
        #endregion
    }
}