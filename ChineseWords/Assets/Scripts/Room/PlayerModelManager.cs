using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RoomScene.PlayerModel;

namespace RoomScene.Manager
{
    /// <summary>
    /// ���ģ�͹����� - ����ģ�����ݹ����Ԥ��ʵ����
    /// </summary>
    public class PlayerModelManager : MonoBehaviour
    {
        [Header("ģ������")]
        [SerializeField] private PlayerModelData[] availableModels;
        [SerializeField] private int defaultModelId = 0;

        [Header("Ԥ������")]
        [SerializeField] private Transform previewParent;
        // �Ƴ� previewCamera �� previewLight��������������������

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ����
        public static PlayerModelManager Instance { get; private set; }

        // ģ������ӳ��
        private Dictionary<int, PlayerModelData> modelDataMap = new Dictionary<int, PlayerModelData>();

        // Ԥ��ʵ������
        private Dictionary<int, GameObject> previewInstances = new Dictionary<int, GameObject>();

        // ��ǰԤ����ģ��
        private GameObject currentPreviewInstance;
        private int currentPreviewModelId = -1;

        // �¼�
        public static event System.Action<int> OnModelChanged;

        #region Unity��������

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

        #region ��ʼ��

        /// <summary>
        /// ��ʼ��ģ������
        /// </summary>
        private void InitializeModelData()
        {
            modelDataMap.Clear();

            if (availableModels == null || availableModels.Length == 0)
            {
                Debug.LogError("[PlayerModelManager] û�������κ�ģ�����ݣ�");
                return;
            }

            // ����IDӳ��
            for (int i = 0; i < availableModels.Length; i++)
            {
                var modelData = availableModels[i];
                if (modelData == null || !modelData.IsValid())
                {
                    Debug.LogWarning($"[PlayerModelManager] ģ������ {i} ��Ч������");
                    continue;
                }

                modelDataMap[modelData.modelId] = modelData;
                LogDebug($"ע��ģ��: ID={modelData.modelId}, Name={modelData.modelName}");
            }

            LogDebug($"ģ�͹�������ʼ����ɣ������� {modelDataMap.Count} ��ģ��");
        }

        /// <summary>
        /// ����Ԥ������
        /// </summary>
        private void SetupPreviewEnvironment()
        {
            if (previewParent == null)
            {
                // ����Ԥ������
                GameObject previewContainer = new GameObject("ModelPreviewContainer");
                previewContainer.transform.SetParent(transform);
                previewParent = previewContainer.transform;
            }

            LogDebug("Ԥ�������������");
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ��ȡ���п���ģ��ID
        /// </summary>
        public int[] GetAvailableModelIds()
        {
            return modelDataMap.Keys.OrderBy(id => id).ToArray();
        }

        /// <summary>
        /// ��ȡģ������
        /// </summary>
        public PlayerModelData GetModelData(int modelId)
        {
            return modelDataMap.TryGetValue(modelId, out PlayerModelData data) ? data : null;
        }

        /// <summary>
        /// ��ȡģ������
        /// </summary>
        public string GetModelName(int modelId)
        {
            var data = GetModelData(modelId);
            return data?.modelName ?? "δ֪ģ��";
        }

        /// <summary>
        /// ��ȡĬ��ģ��ID
        /// </summary>
        public int GetDefaultModelId()
        {
            // ���ȷ��ر��ΪĬ�ϵ�ģ��
            var defaultModel = availableModels.FirstOrDefault(m => m.isDefault);
            if (defaultModel != null)
            {
                return defaultModel.modelId;
            }

            // ���򷵻����õ�Ĭ��ID���һ������ģ��
            if (modelDataMap.ContainsKey(defaultModelId))
            {
                return defaultModelId;
            }

            return modelDataMap.Keys.FirstOrDefault();
        }

        /// <summary>
        /// ���ģ���Ƿ����
        /// </summary>
        public bool IsModelAvailable(int modelId)
        {
            var data = GetModelData(modelId);
            return data != null && data.isUnlocked;
        }

        /// <summary>
        /// ��ȡ��һ������ģ��ID
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
        /// ��ȡ��һ������ģ��ID
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

        #region Ԥ������

        /// <summary>
        /// ��ʾģ��Ԥ��
        /// </summary>
        public GameObject ShowModelPreview(int modelId, Transform parentTransform = null)
        {
            var modelData = GetModelData(modelId);
            if (modelData == null)
            {
                Debug.LogWarning($"[PlayerModelManager] �޷��ҵ�ģ�� {modelId}");
                return null;
            }

            // ���ص�ǰԤ��
            HideCurrentPreview();

            // ��ȡ�򴴽�Ԥ��ʵ��
            GameObject previewInstance = GetOrCreatePreviewInstance(modelId);
            if (previewInstance == null) return null;

            // ���ø���
            Transform targetParent = parentTransform ?? previewParent;
            previewInstance.transform.SetParent(targetParent, false);

            // Ӧ��Ԥ������
            ApplyPreviewSettings(previewInstance, modelData);

            // ����Ԥ��
            previewInstance.SetActive(true);
            currentPreviewInstance = previewInstance;
            currentPreviewModelId = modelId;

            // ����ѡ�񶯻�
            PlaySelectAnimation(previewInstance, modelData);

            LogDebug($"��ʾģ��Ԥ��: {modelData.modelName} (ID: {modelId})");
            return previewInstance;
        }

        /// <summary>
        /// ���ص�ǰԤ��
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
        /// ��ȡ�򴴽�Ԥ��ʵ��
        /// </summary>
        private GameObject GetOrCreatePreviewInstance(int modelId)
        {
            // ��黺��
            if (previewInstances.TryGetValue(modelId, out GameObject cachedInstance))
            {
                return cachedInstance;
            }

            // ������ʵ��
            var modelData = GetModelData(modelId);
            if (modelData == null) return null;

            GameObject previewPrefab = modelData.GetPreviewPrefab();
            if (previewPrefab == null) return null;

            GameObject instance = Instantiate(previewPrefab, previewParent);
            instance.name = $"Preview_{modelData.modelName}_{modelId}";
            instance.SetActive(false);

            // �Ƴ����ܵ����������Ԥ������Ҫ��
            var networkComponents = instance.GetComponentsInChildren<MonoBehaviour>();
            foreach (var component in networkComponents)
            {
                if (component.GetType().Name.Contains("Network") ||
                    component.GetType().Name.Contains("Photon"))
                {
                    Destroy(component);
                }
            }

            // ����ʵ��
            previewInstances[modelId] = instance;

            LogDebug($"����ģ��Ԥ��ʵ��: {modelData.modelName}");
            return instance;
        }

        /// <summary>
        /// Ӧ��Ԥ������
        /// </summary>
        private void ApplyPreviewSettings(GameObject instance, PlayerModelData modelData)
        {
            instance.transform.localPosition = modelData.previewPosition;
            instance.transform.localRotation = Quaternion.Euler(modelData.previewRotation);
            instance.transform.localScale = modelData.previewScale;
        }

        /// <summary>
        /// ����ѡ�񶯻�
        /// </summary>
        private void PlaySelectAnimation(GameObject instance, PlayerModelData modelData)
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null) return;

            if (modelData.selectAnimation != null)
            {
                // ����ѡ�񶯻�
                animator.speed = modelData.animationSpeed;
                animator.Play(modelData.selectAnimation.name);
            }
            else if (modelData.idleAnimation != null)
            {
                // ���Ŵ�������
                animator.speed = modelData.animationSpeed;
                animator.Play(modelData.idleAnimation.name);
            }
        }

        /// <summary>
        /// �������Ԥ��ʵ��
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

        #region ��Ϸģ��ʵ����

        /// <summary>
        /// ΪNetworkGameScene������Ϸģ��
        /// </summary>
        public GameObject CreateGameModel(int modelId, Vector3 position, Quaternion rotation)
        {
            var modelData = GetModelData(modelId);
            if (modelData == null)
            {
                Debug.LogError($"[PlayerModelManager] �޷�������Ϸģ�ͣ�ģ��ID {modelId} ������");
                return null;
            }

            GameObject gamePrefab = modelData.GetGamePrefab();
            if (gamePrefab == null)
            {
                Debug.LogError($"[PlayerModelManager] ģ�� {modelData.modelName} ȱ����ϷԤ����");
                return null;
            }

            GameObject gameInstance = Instantiate(gamePrefab, position, rotation);
            gameInstance.name = $"GameModel_{modelData.modelName}_{modelId}";

            LogDebug($"������Ϸģ��: {modelData.modelName} at {position}");
            return gameInstance;
        }

        #endregion

        #region ��������

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