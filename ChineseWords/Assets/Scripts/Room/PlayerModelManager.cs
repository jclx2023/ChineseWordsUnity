using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RoomScene.PlayerModel;

namespace RoomScene.Manager
{
    /// <summary>
    /// 玩家模型管理器 - 修复版，移除预览缓存机制
    /// 负责模型数据管理和预览实例化，每次创建独立的预览实例
    /// </summary>
    public class PlayerModelManager : MonoBehaviour
    {
        [Header("模型配置")]
        [SerializeField] private PlayerModelData[] availableModels;
        [SerializeField] private int defaultModelId = 0;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 单例
        public static PlayerModelManager Instance { get; private set; }

        // 模型数据映射
        private Dictionary<int, PlayerModelData> modelDataMap = new Dictionary<int, PlayerModelData>();

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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
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
        /// 显示模型预览 - 每次创建新实例
        /// </summary>
        public GameObject ShowModelPreview(int modelId, Transform parentTransform)
        {
            var modelData = GetModelData(modelId);

            // 创建新的预览实例
            GameObject previewInstance = CreatePreviewInstance(modelId);
            if (previewInstance == null) return null;

            // 设置父级
            previewInstance.transform.SetParent(parentTransform, false);

            // 应用预览配置
            ApplyPreviewSettings(previewInstance, modelData);

            // 激活预览
            previewInstance.SetActive(true);

            // 播放选择动画
            PlaySelectAnimation(previewInstance, modelData);

            LogDebug($"创建模型预览: {modelData.modelName} (ID: {modelId}) 到容器: {parentTransform.name}");
            return previewInstance;
        }

        /// <summary>
        /// 创建预览实例
        /// </summary>
        private GameObject CreatePreviewInstance(int modelId)
        {
            var modelData = GetModelData(modelId);
            if (modelData == null) return null;

            GameObject previewPrefab = modelData.GetPreviewPrefab();
            if (previewPrefab == null)
            {
                Debug.LogWarning($"[PlayerModelManager] 模型 {modelData.modelName} 没有预览预制体");
                return null;
            }

            // 创建新实例
            GameObject instance = Instantiate(previewPrefab);
            instance.name = $"Preview_{modelData.modelName}_{modelId}_{System.Guid.NewGuid().ToString("N")[..8]}";
            instance.SetActive(false);

            LogDebug($"创建预览实例: {instance.name}");
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