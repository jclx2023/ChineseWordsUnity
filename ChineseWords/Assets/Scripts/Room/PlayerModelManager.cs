using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RoomScene.PlayerModel;

namespace RoomScene.Manager
{
    /// <summary>
    /// ���ģ�͹����� - �޸��棬�Ƴ�Ԥ���������
    /// ����ģ�����ݹ����Ԥ��ʵ������ÿ�δ���������Ԥ��ʵ��
    /// </summary>
    public class PlayerModelManager : MonoBehaviour
    {
        [Header("ģ������")]
        [SerializeField] private PlayerModelData[] availableModels;
        [SerializeField] private int defaultModelId = 0;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ����
        public static PlayerModelManager Instance { get; private set; }

        // ģ������ӳ��
        private Dictionary<int, PlayerModelData> modelDataMap = new Dictionary<int, PlayerModelData>();

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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
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
        /// ��ʾģ��Ԥ�� - ÿ�δ�����ʵ��
        /// </summary>
        public GameObject ShowModelPreview(int modelId, Transform parentTransform)
        {
            var modelData = GetModelData(modelId);

            // �����µ�Ԥ��ʵ��
            GameObject previewInstance = CreatePreviewInstance(modelId);
            if (previewInstance == null) return null;

            // ���ø���
            previewInstance.transform.SetParent(parentTransform, false);

            // Ӧ��Ԥ������
            ApplyPreviewSettings(previewInstance, modelData);

            // ����Ԥ��
            previewInstance.SetActive(true);

            // ����ѡ�񶯻�
            PlaySelectAnimation(previewInstance, modelData);

            LogDebug($"����ģ��Ԥ��: {modelData.modelName} (ID: {modelId}) ������: {parentTransform.name}");
            return previewInstance;
        }

        /// <summary>
        /// ����Ԥ��ʵ��
        /// </summary>
        private GameObject CreatePreviewInstance(int modelId)
        {
            var modelData = GetModelData(modelId);
            if (modelData == null) return null;

            GameObject previewPrefab = modelData.GetPreviewPrefab();
            if (previewPrefab == null)
            {
                Debug.LogWarning($"[PlayerModelManager] ģ�� {modelData.modelName} û��Ԥ��Ԥ����");
                return null;
            }

            // ������ʵ��
            GameObject instance = Instantiate(previewPrefab);
            instance.name = $"Preview_{modelData.modelName}_{modelId}_{System.Guid.NewGuid().ToString("N")[..8]}";
            instance.SetActive(false);

            LogDebug($"����Ԥ��ʵ��: {instance.name}");
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