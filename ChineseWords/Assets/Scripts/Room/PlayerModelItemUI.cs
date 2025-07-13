using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RoomScene.Data;
using RoomScene.Manager;
using Core.Network;

namespace UI
{
    /// <summary>
    /// ���ģ��UI�� - ͳһActionButton�汾
    /// ��ʾ��3Dģ��Ԥ�� + ģ���л���ť + �����Ϣ + ͳһ�ж���ť
    /// ActionButton����������ͺͱ���/Զ����ʾ��ͬ����
    /// </summary>
    public class PlayerModelItemUI : MonoBehaviour
    {
        [Header("ģ��Ԥ������")]
        [SerializeField] private Transform modelPreviewContainer;
        [SerializeField] private Button previousModelButton;
        [SerializeField] private Button nextModelButton;

        [Header("�����Ϣ����")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text hostIndicatorText;          // �δ����ʶ�ı�
        [SerializeField] private Image playerBackgroundImage;

        [Header("�ж���������")]
        [SerializeField] private Button actionButton;                // ͳһ���ж���ť
        [SerializeField] private TMP_Text actionButtonText;          // ��ť�ı�

        [Header("��ɫ����")]
        [SerializeField] private Color hostBackgroundColor = new Color(1f, 0.8f, 0.2f, 0.3f);
        [SerializeField] private Color readyBackgroundColor = new Color(0.2f, 1f, 0.2f, 0.2f);
        [SerializeField] private Color notReadyBackgroundColor = new Color(1f, 0.2f, 0.2f, 0.2f);

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ��������
        private ushort playerId;
        private RoomPlayerData playerData;
        private RoomUIController parentController;
        private GameObject currentModelPreview;
        private bool isLocalPlayer;

        // ״̬
        private bool isInitialized = false;

        #region ��ʼ��

        /// <summary>
        /// ��ʼ�����UI��
        /// </summary>
        public void Initialize(ushort id, RoomPlayerData data, RoomUIController controller)
        {
            playerId = id;
            playerData = data;
            parentController = controller;
            isLocalPlayer = (playerId == NetworkManager.Instance?.ClientId);

            SetupUIComponents();
            UpdateDisplay(playerData);
            ShowModelPreview(playerData.selectedModelId);

            isInitialized = true;
            LogDebug($"��ʼ�����UI��: {playerData.playerName} (�������: {isLocalPlayer})");
        }

        /// <summary>
        /// ����UI���
        /// </summary>
        private void SetupUIComponents()
        {
            // ����ģ���л���ť����������ҿ��ã�
            if (previousModelButton != null)
            {
                previousModelButton.onClick.RemoveAllListeners();
                previousModelButton.onClick.AddListener(OnPreviousModelClicked);
                previousModelButton.gameObject.SetActive(isLocalPlayer);
            }

            if (nextModelButton != null)
            {
                nextModelButton.onClick.RemoveAllListeners();
                nextModelButton.onClick.AddListener(OnNextModelClicked);
                nextModelButton.gameObject.SetActive(isLocalPlayer);
            }

            // ����ͳһ���ж���ť
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionButtonClicked);
                UpdateActionButtonState();
            }

            // ȷ��Ԥ����������
            if (modelPreviewContainer == null)
            {
                GameObject previewObj = new GameObject("ModelPreviewContainer");
                previewObj.transform.SetParent(transform, false);
                modelPreviewContainer = previewObj.transform;
            }
        }

        #endregion

        #region ��ʾ����

        /// <summary>
        /// ������ʾ����
        /// </summary>
        public void UpdateDisplay(RoomPlayerData data)
        {
            if (data == null) return;

            playerData = data;

            // �����������
            if (playerNameText != null)
            {
                string displayName = playerData.playerName;
                if (isLocalPlayer)
                {
                    displayName += " (��)";
                }
                playerNameText.text = displayName;
            }

            // ���¿δ����ʶ
            UpdateHostIndicator();

            // ���±�����ɫ
            UpdateBackgroundColor();

            // ����ͳһ�ж���ť״̬
            UpdateActionButtonState();

            // ����ģ��Ԥ�������ģ��ID�����仯��
            if (currentModelPreview == null ||
                playerData.selectedModelId != GetCurrentPreviewModelId())
            {
                ShowModelPreview(playerData.selectedModelId);
            }

            LogDebug($"���������ʾ: {playerData.playerName}, ģ��: {playerData.selectedModelId}, ׼��: {playerData.isReady}");
        }

        /// <summary>
        /// ���¿δ����ʶ
        /// </summary>
        private void UpdateHostIndicator()
        {
            if (hostIndicatorText != null)
            {
                if (playerData.isHost)
                {
                    hostIndicatorText.text = "�δ���";
                    hostIndicatorText.color = Color.yellow;
                    hostIndicatorText.gameObject.SetActive(true);
                }
                else
                {
                    hostIndicatorText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// ���±�����ɫ
        /// </summary>
        private void UpdateBackgroundColor()
        {
            if (playerBackgroundImage == null) return;

            Color backgroundColor;

            if (playerData.isHost)
            {
                backgroundColor = hostBackgroundColor;
            }
            else if (playerData.isReady)
            {
                backgroundColor = readyBackgroundColor;
            }
            else
            {
                backgroundColor = notReadyBackgroundColor;
            }

            playerBackgroundImage.color = backgroundColor;
        }

        /// <summary>
        /// ����ͳһ�ж���ť״̬
        /// </summary>
        private void UpdateActionButtonState()
        {
            if (actionButton == null) return;

            // actionButtonʼ����ʾ�������ݺͽ����Բ�ͬ
            actionButton.gameObject.SetActive(true);

            if (isLocalPlayer)
            {
                // ������ҵİ�ť
                if (playerData.isHost)
                {
                    // ������ʾ"��ʼ��Ϸ"
                    if (actionButtonText != null)
                    {
                        actionButtonText.text = "��ʼ��Ϸ";
                    }

                    // ������ť�Ƿ�ɵ��ȡ�����Ƿ������˶�׼������
                    actionButton.interactable = CanStartGame();
                }
                else
                {
                    // ��ͨ�����ʾ"׼��"/"ȡ��׼��"
                    if (actionButtonText != null)
                    {
                        actionButtonText.text = playerData.isReady ? "ȡ��׼��" : "׼��";
                    }

                    actionButton.interactable = true;
                }
            }
            else
            {
                // ������ҵİ�ť - ����״̬��ʾ�����ɽ���
                actionButton.interactable = false;

                if (actionButtonText != null)
                {
                    if (playerData.isHost)
                    {
                        // ����������ʱ��ʼ����ʾ"��׼��"
                        actionButtonText.text = "��׼��";
                    }
                    else
                    {
                        // ��������ͨ���ʱ����ʾ��ʵ��׼��״̬
                        actionButtonText.text = playerData.isReady ? "��׼��" : "δ׼��";
                    }
                }
            }
        }

        /// <summary>
        /// ����Ƿ���Կ�ʼ��Ϸ����������ťʹ�ã�
        /// </summary>
        private bool CanStartGame()
        {
            if (parentController == null) return false;

            // ͨ���������������Ϸ��ʼ����
            return parentController.CanStartGame();
        }

        #endregion

        #region ģ��Ԥ��

        /// <summary>
        /// ��ʾģ��Ԥ��
        /// </summary>
        private void ShowModelPreview(int modelId)
        {
            if (PlayerModelManager.Instance == null) return;

            // �����ǰԤ��
            ClearCurrentPreview();

            // ������Ԥ��
            currentModelPreview = PlayerModelManager.Instance.ShowModelPreview(modelId, modelPreviewContainer);

            LogDebug($"��ʾģ��Ԥ��: ID {modelId}");
        }

        /// <summary>
        /// �����ǰԤ��
        /// </summary>
        private void ClearCurrentPreview()
        {
            if (currentModelPreview != null)
            {
                currentModelPreview.SetActive(false);
                currentModelPreview = null;
            }
        }

        /// <summary>
        /// ��ȡ��ǰԤ����ģ��ID
        /// </summary>
        private int GetCurrentPreviewModelId()
        {
            return playerData?.selectedModelId ?? 0;
        }

        #endregion

        #region ��ť�¼�

        /// <summary>
        /// ��һ��ģ�Ͱ�ť���
        /// </summary>
        private void OnPreviousModelClicked()
        {
            if (!isLocalPlayer || PlayerModelManager.Instance == null) return;

            int currentModelId = playerData.selectedModelId;
            int previousModelId = PlayerModelManager.Instance.GetPreviousModelId(currentModelId);

            if (previousModelId != currentModelId)
            {
                ChangeModel(previousModelId);
            }
        }

        /// <summary>
        /// ��һ��ģ�Ͱ�ť���
        /// </summary>
        private void OnNextModelClicked()
        {
            if (!isLocalPlayer || PlayerModelManager.Instance == null) return;

            int currentModelId = playerData.selectedModelId;
            int nextModelId = PlayerModelManager.Instance.GetNextModelId(currentModelId);

            if (nextModelId != currentModelId)
            {
                ChangeModel(nextModelId);
            }
        }

        /// <summary>
        /// ͳһ���ж���ť���
        /// </summary>
        private void OnActionButtonClicked()
        {
            if (!isLocalPlayer) return; // ֻ�б�������ܵ���Լ��İ�ť

            if (playerData.isHost)
            {
                // ���������ʼ��Ϸ��ʵ���߼��ɸ�����������
                if (parentController != null)
                {
                    parentController.OnPlayerReadyStateChanged(playerId, true); // ��true��ʾ��ʼ��Ϸ
                }
                LogDebug("���������ʼ��Ϸ��ť");
            }
            else
            {
                // ��ͨ����л�׼��״̬
                bool newReadyState = !playerData.isReady;

                if (parentController != null)
                {
                    parentController.OnPlayerReadyStateChanged(playerId, newReadyState);
                }

                LogDebug($"�������׼����ť: {playerData.isReady} -> {newReadyState}");
            }
        }

        /// <summary>
        /// �ı�ģ��
        /// </summary>
        private void ChangeModel(int newModelId)
        {
            if (!PlayerModelManager.Instance.IsModelAvailable(newModelId))
            {
                LogDebug($"ģ�� {newModelId} ������");
                return;
            }

            // ���±�������
            string modelName = PlayerModelManager.Instance.GetModelName(newModelId);
            playerData.SetSelectedModel(newModelId, modelName);

            // ����Ԥ��
            ShowModelPreview(newModelId);

            // ֪ͨ�����������ᴥ������ͬ����
            if (parentController != null)
            {
                parentController.OnPlayerModelSelectionChanged(playerId, newModelId);
            }

            LogDebug($"�ı�ģ�͵�: {modelName} (ID: {newModelId})");
        }

        #endregion

        #region ��������

        private void OnDestroy()
        {
            ClearCurrentPreview();
        }

        #endregion

        #region ��������

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerModelItemUI_{playerId}] {message}");
            }
        }

        #endregion
    }
}