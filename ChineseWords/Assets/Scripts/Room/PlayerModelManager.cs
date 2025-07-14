using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RoomScene.PlayerModel;

namespace RoomScene.Manager
{
    /// <summary>
    /// 玩家模型管理器 - 负责模型数据管理和预览实例化
    /// </summary>
    public class PlayerModelManager : MonoBehaviour
    {
        [Header("模型配置")]
        [SerializeField] private PlayerModelData[] availableModels;
        [SerializeField] private int defaultModelId = 0;

        [Header("预览设置")]
        [SerializeField] private Transform previewParent;
        // 移除 previewCamera 和 previewLight，避免意外禁用主摄像机

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 单例
        public static PlayerModelManager Instance { get; private set; }

        // 模型数据映射
        private Dictionary<int, PlayerModelData> modelDataMap = new Dictionary<int, PlayerModelData>();

        // 预览实例缓存
        private Dictionary<int, GameObject> previewInstances = new Dictionary<int, GameObject>();

        // 当前预览的模型
        private GameObject currentPreviewInstance;
        private int currentPreviewModelId = -1;

        // 事件
        public static event System.Action<int> OnModelChanged;

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeModelData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            SetupPreviewEnvironment();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            ClearAllPreviewInstances();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化模型数据
        /// </summary>
        private void InitializeModelData()
        {
            modelDataMap.Clear();

            if (availableModels == null || availableModels.Length == 0)
            {
                Debug.LogError("[PlayerModelManager] 没有配置任何模型数据！");
                return;
            }

            // 建立ID映射
            for (int i = 0; i < availableModels.Length; i++)
            {
                var modelData = availableModels[i];
                if (modelData == null || !modelData.IsValid())
                {
                    Debug.LogWarning($"[PlayerModelManager] 模型数据 {i} 无效，跳过");
                    continue;
                }

                modelDataMap[modelData.modelId] = modelData;
                LogDebug($"注册模型: ID={modelData.modelId}, Name={modelData.modelName}");
            }

            LogDebug($"模型管理器初始化完成，共加载 {modelDataMap.Count} 个模型");
        }

        /// <summary>
        /// 设置预览环境
        /// </summary>
        private void SetupPreviewEnvironment()
        {
            if (previewParent == null)
            {
                // 创建预览容器
                GameObject previewContainer = new GameObject("ModelPreviewContainer");
                previewContainer.transform.SetParent(transform);
                previewParent = previewContainer.transform;
            }

            LogDebug("预览环境设置完成");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取所有可用模型ID
        /// </summary>
        public int[] GetAvailableModelIds()
        {
            return modelDataMap.Keys.OrderBy(id => id).ToArray();
        }

        /// <summary>
        /// 获取模型数据
        /// </summary>
        public PlayerModelData GetModelData(int modelId)
        {
            return modelDataMap.TryGetValue(modelId, out PlayerModelData data) ? data : null;
        }

        /// <summary>
        /// 获取模型名称
        /// </summary>
        public string GetModelName(int modelId)
        {
            var data = GetModelData(modelId);
            return data?.modelName ?? "未知模型";
        }

        /// <summary>
        /// 获取默认模型ID
        /// </summary>
        public int GetDefaultModelId()
        {
            // 优先返回标记为默认的模型
            var defaultModel = availableModels.FirstOrDefault(m => m.isDefault);
            if (defaultModel != null)
            {
                return defaultModel.modelId;
            }

            // 否则返回配置的默认ID或第一个可用模型
            if (modelDataMap.ContainsKey(defaultModelId))
            {
                return defaultModelId;
            }

            return modelDataMap.Keys.FirstOrDefault();
        }

        /// <summary>
        /// 检查模型是否可用
        /// </summary>
        public bool IsModelAvailable(int modelId)
        {
            var data = GetModelData(modelId);
            return data != null && data.isUnlocked;
        }

        /// <summary>
        /// 获取下一个可用模型ID
        /// </summary>
        public int GetNextModelId(int currentModelId)
        {
            var availableIds = GetAvailableModelIds().Where(id => IsModelAvailable(id)).ToArray();
            if (availableIds.Length == 0) return currentModelId;

            int currentIndex = System.Array.IndexOf(availableIds, currentModelId);
            int nextIndex = (currentIndex + 1) % availableIds.Length;
            return availableIds[nextIndex];
        }

        /// <summary>
        /// 获取上一个可用模型ID
        /// </summary>
        public int GetPreviousModelId(int currentModelId)
        {
            var availableIds = GetAvailableModelIds().Where(id => IsModelAvailable(id)).ToArray();
            if (availableIds.Length == 0) return currentModelId;

            int currentIndex = System.Array.IndexOf(availableIds, currentModelId);
            int previousIndex = (currentIndex - 1 + availableIds.Length) % availableIds.Length;
            return availableIds[previousIndex];
        }

        #endregion

        #region 预览管理

        /// <summary>
        /// 显示模型预览
        /// </summary>
        public GameObject ShowModelPreview(int modelId, Transform parentTransform = null)
        {
            var modelData = GetModelData(modelId);
            if (modelData == null)
            {
                Debug.LogWarning($"[PlayerModelManager] 无法找到模型 {modelId}");
                return null;
            }

            // 隐藏当前预览
            HideCurrentPreview();

            // 获取或创建预览实例
            GameObject previewInstance = GetOrCreatePreviewInstance(modelId);
            if (previewInstance == null) return null;

            // 设置父级
            Transform targetParent = parentTransform ?? previewParent;
            previewInstance.transform.SetParent(targetParent, false);

            // 应用预览配置
            ApplyPreviewSettings(previewInstance, modelData);

            // 激活预览
            previewInstance.SetActive(true);
            currentPreviewInstance = previewInstance;
            currentPreviewModelId = modelId;

            // 播放选择动画
            PlaySelectAnimation(previewInstance, modelData);

            LogDebug($"显示模型预览: {modelData.modelName} (ID: {modelId})");
            return previewInstance;
        }

        /// <summary>
        /// 隐藏当前预览
        /// </summary>
        public void HideCurrentPreview()
        {
            if (currentPreviewInstance != null)
            {
                currentPreviewInstance.SetActive(false);
                currentPreviewInstance = null;
                currentPreviewModelId = -1;
            }
        }

        /// <summary>
        /// 获取或创建预览实例
        /// </summary>
        private GameObject GetOrCreatePreviewInstance(int modelId)
        {
            // 检查缓存
            if (previewInstances.TryGetValue(modelId, out GameObject cachedInstance))
            {
                return cachedInstance;
            }

            // 创建新实例
            var modelData = GetModelData(modelId);
            if (modelData == null) return null;

            GameObject previewPrefab = modelData.GetPreviewPrefab();
            if (previewPrefab == null) return null;

            GameObject instance = Instantiate(previewPrefab, previewParent);
            instance.name = $"Preview_{modelData.modelName}_{modelId}";
            instance.SetActive(false);

            // 移除可能的网络组件（预览不需要）
            var networkComponents = instance.GetComponentsInChildren<MonoBehaviour>();
            foreach (var component in networkComponents)
            {
                if (component.GetType().Name.Contains("Network") ||
                    component.GetType().Name.Contains("Photon"))
                {
                    Destroy(component);
                }
            }

            // 缓存实例
            previewInstances[modelId] = instance;

            LogDebug($"创建模型预览实例: {modelData.modelName}");
            return instance;
        }

        /// <summary>
        /// 应用预览设置
        /// </summary>
        private void ApplyPreviewSettings(GameObject instance, PlayerModelData modelData)
        {
            instance.transform.localPosition = modelData.previewPosition;
            instance.transform.localRotation = Quaternion.Euler(modelData.previewRotation);
            instance.transform.localScale = modelData.previewScale;
        }

        /// <summary>
        /// 播放选择动画
        /// </summary>
        private void PlaySelectAnimation(GameObject instance, PlayerModelData modelData)
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null) return;

            if (modelData.selectAnimation != null)
            {
                // 播放选择动画
                animator.speed = modelData.animationSpeed;
                animator.Play(modelData.selectAnimation.name);
            }
            else if (modelData.idleAnimation != null)
            {
                // 播放待机动画
                animator.speed = modelData.animationSpeed;
                animator.Play(modelData.idleAnimation.name);
            }
        }

        /// <summary>
        /// 清空所有预览实例
        /// </summary>
        private void ClearAllPreviewInstances()
        {
            foreach (var instance in previewInstances.Values)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }
            previewInstances.Clear();
            currentPreviewInstance = null;
            currentPreviewModelId = -1;
        }

        #endregion

        #region 游戏模型实例化

        /// <summary>
        /// 为NetworkGameScene创建游戏模型
        /// </summary>
        public GameObject CreateGameModel(int modelId, Vector3 position, Quaternion rotation)
        {
            var modelData = GetModelData(modelId);
            if (modelData == null)
            {
                Debug.LogError($"[PlayerModelManager] 无法创建游戏模型，模型ID {modelId} 不存在");
                return null;
            }

            GameObject gamePrefab = modelData.GetGamePrefab();
            if (gamePrefab == null)
            {
                Debug.LogError($"[PlayerModelManager] 模型 {modelData.modelName} 缺少游戏预制体");
                return null;
            }

            GameObject gameInstance = Instantiate(gamePrefab, position, rotation);
            gameInstance.name = $"GameModel_{modelData.modelName}_{modelId}";

            LogDebug($"创建游戏模型: {modelData.modelName} at {position}");
            return gameInstance;
        }

        #endregion

        #region 辅助方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerModelManager] {message}");
            }
        }

        #endregion
    }
}