using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Core.Network;
using Photon.Pun;
using Core;

namespace UI.GameExit
{
    /// <summary>
    /// ��Ϸ�˳�������
    /// ����NetworkGameScene�е��˳�����
    /// ���ܣ�ESC���������˳�ȷ�ϡ�����������
    /// </summary>
    public class GameExitManager : MonoBehaviour
    {
        [Header("�˳�����")]
        [SerializeField] private KeyCode exitHotkey = KeyCode.Escape;
        [SerializeField] private bool enableExitDuringGame = true;
        [SerializeField] private float exitConfirmTimeout = 10f;

        [Header("UI����")]
        [SerializeField] private GameObject exitConfirmDialog;
        [SerializeField] private Button confirmExitButton;
        [SerializeField] private Button cancelExitButton;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private TMP_Text countdownText;

        [Header("�˳�Ŀ��")]
        [SerializeField] private string targetScene = "RoomScene";
        [SerializeField] private bool returnToMainMenu = false;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ״̬����
        private bool isExitDialogOpen = false;
        private bool isExiting = false;
        private float countdownTimer = 0f;
        private bool isCountdownActive = false;

        // ����״̬����
        private bool wasHost = false;
        private bool wasInGame = false;

        #region Unity��������

        private void Start()
        {
            InitializeExitManager();
        }

        private void Update()
        {
            HandleInput();
            UpdateCountdown();
        }

        private void OnDestroy()
        {
            CleanupExitManager();
        }

        #endregion

        #region ��ʼ��������

        /// <summary>
        /// ��ʼ���˳�������
        /// </summary>
        private void InitializeExitManager()
        {
            LogDebug("��ʼ����Ϸ�˳�������");

            // ��֤����
            if (!IsInGameScene())
            {
                LogDebug("������Ϸ�����У������˳�������");
                this.enabled = false;
                return;
            }

            // ����UI
            SetupUI();

            // �����ʼ״̬
            CacheNetworkState();

            LogDebug("��Ϸ�˳���������ʼ�����");
        }

        /// <summary>
        /// ����UI���
        /// </summary>
        private void SetupUI()
        {
            // �����˳��Ի������û��Ԥ��Ļ���
            if (exitConfirmDialog == null)
            {
                CreateExitDialog();
            }

            // ���ð�ť�¼�
            if (confirmExitButton != null)
            {
                confirmExitButton.onClick.RemoveAllListeners();
                confirmExitButton.onClick.AddListener(ConfirmExit);
            }

            if (cancelExitButton != null)
            {
                cancelExitButton.onClick.RemoveAllListeners();
                cancelExitButton.onClick.AddListener(CancelExit);
            }

            // ��ʼ���ضԻ���
            if (exitConfirmDialog != null)
            {
                exitConfirmDialog.SetActive(false);
            }

            LogDebug("UI����������");
        }

        /// <summary>
        /// �����˳��Ի��򣨱��÷�����
        /// </summary>
        private void CreateExitDialog()
        {
            LogDebug("����Ĭ���˳��Ի���");

            // ������Զ�̬����һ���򵥵��˳��Ի���
            // ���ߴ�Resources����Ԥ����
            // ��ʱ������������Ԥ���UI
        }

        /// <summary>
        /// ��������״̬
        /// </summary>
        private void CacheNetworkState()
        {
            if (NetworkManager.Instance != null)
            {
                wasHost = NetworkManager.Instance.IsHost;
                LogDebug($"��������״̬ - �Ƿ�Host: {wasHost}");
            }

            if (HostGameManager.Instance != null)
            {
                wasInGame = HostGameManager.Instance.IsGameInProgress;
                LogDebug($"������Ϸ״̬ - ��Ϸ������: {wasInGame}");
            }
        }

        /// <summary>
        /// �����˳�������
        /// </summary>
        private void CleanupExitManager()
        {
            // ȡ�����а�ť����
            if (confirmExitButton != null)
                confirmExitButton.onClick.RemoveAllListeners();

            if (cancelExitButton != null)
                cancelExitButton.onClick.RemoveAllListeners();

            LogDebug("��Ϸ�˳�������������");
        }

        #endregion

        #region ���봦��

        /// <summary>
        /// �����û�����
        /// </summary>
        private void HandleInput()
        {
            if (isExiting) return;

            // ����˳��ȼ�
            if (Input.GetKeyDown(exitHotkey))
            {
                HandleExitRequest();
            }
        }

        /// <summary>
        /// �����˳�����
        /// </summary>
        private void HandleExitRequest()
        {
            LogDebug($"��⵽�˳����� - ��ǰ״̬: �Ի�����={isExitDialogOpen}");

            if (isExitDialogOpen)
            {
                // �Ի����ѿ������ر���
                CancelExit();
            }
            else
            {
                // ��ʾ�˳�ȷ��
                ShowExitConfirmation();
            }
        }

        #endregion

        #region �˳�ȷ�϶Ի���

        /// <summary>
        /// ��ʾ�˳�ȷ�϶Ի���
        /// </summary>
        public void ShowExitConfirmation()
        {
            if (isExitDialogOpen || isExiting) return;

            LogDebug("��ʾ�˳�ȷ�϶Ի���");

            // ����˳�����
            if (!CanExit())
            {
                LogDebug("��ǰ״̬�������˳�");
                return;
            }

            // ��ʾ�Ի���
            if (exitConfirmDialog != null)
            {
                exitConfirmDialog.SetActive(true);
                isExitDialogOpen = true;

                // ����ȷ���ı�
                UpdateConfirmText();

                // ��������ʱ
                StartCountdown();

                LogDebug("�˳�ȷ�϶Ի�������ʾ");
            }
            else
            {
                LogDebug("�˳��Ի���UI�����ڣ�ֱ��ִ���˳�");
                ConfirmExit();
            }
        }

        /// <summary>
        /// ����ȷ���ı�
        /// </summary>
        private void UpdateConfirmText()
        {
            if (confirmText == null) return;

            string message = "ȷ��Ҫ�˳���Ϸ��\n";

            // ���ݵ�ǰ״̬���˵��
            if (wasInGame)
            {
                if (wasHost)
                {
                    message += "ע�⣺��Ϊ�����˳���������ǰ��Ϸ��";
                }
                else
                {
                    message += "�˳����޷����¼��뵱ǰ��Ϸ��";
                }
            }
            else
            {
                message += "�����ص����䳡����";
            }

            confirmText.text = message;
        }

        /// <summary>
        /// ��������ʱ
        /// </summary>
        private void StartCountdown()
        {
            countdownTimer = exitConfirmTimeout;
            isCountdownActive = true;
            LogDebug($"�����˳�ȷ�ϵ���ʱ: {exitConfirmTimeout}��");
        }

        /// <summary>
        /// ���µ���ʱ��ʾ
        /// </summary>
        private void UpdateCountdown()
        {
            if (!isCountdownActive) return;

            countdownTimer -= Time.deltaTime;

            // ���µ���ʱ�ı�
            if (countdownText != null)
            {
                int seconds = Mathf.CeilToInt(countdownTimer);
                countdownText.text = $"�Զ�ȡ��: {seconds}��";
            }

            // ����ʱ�������Զ�ȡ��
            if (countdownTimer <= 0)
            {
                LogDebug("�˳�ȷ�ϵ���ʱ�������Զ�ȡ��");
                CancelExit();
            }
        }

        /// <summary>
        /// ֹͣ����ʱ
        /// </summary>
        private void StopCountdown()
        {
            isCountdownActive = false;
            countdownTimer = 0f;
        }

        #endregion

        #region �˳�����

        /// <summary>
        /// ȷ���˳�
        /// </summary>
        public void ConfirmExit()
        {
            if (isExiting) return;

            LogDebug("�û�ȷ���˳���Ϸ");

            isExiting = true;
            HideExitDialog();

            // ִ���˳��߼�
            StartExitProcess();
        }

        /// <summary>
        /// ȡ���˳�
        /// </summary>
        public void CancelExit()
        {
            LogDebug("ȡ���˳���Ϸ");

            HideExitDialog();
        }

        /// <summary>
        /// �����˳��Ի���
        /// </summary>
        private void HideExitDialog()
        {
            if (exitConfirmDialog != null)
            {
                exitConfirmDialog.SetActive(false);
            }

            isExitDialogOpen = false;
            StopCountdown();
        }

        /// <summary>
        /// ��ʼ�˳�����
        /// </summary>
        private void StartExitProcess()
        {
            LogDebug("��ʼ�˳�����");

            // �����Host����Ϸ�����У���Ҫ���⴦��
            if (wasHost && wasInGame)
            {
                HandleHostExitDuringGame();
            }
            else
            {
                // ��ͨ�˳�����
                HandleNormalExit();
            }
        }

        /// <summary>
        /// ����Host����Ϸ���˳�
        /// </summary>
        private void HandleHostExitDuringGame()
        {
            LogDebug("����Host��Ϸ���˳�");

            try
            {
                // ֪ͨHostGameManager������Ϸ
                if (HostGameManager.Instance != null)
                {
                    HostGameManager.Instance.ForceEndGame("�����˳���Ϸ");
                }

                // �ӳ�ִ���˳������������һ��ʱ�������Ϸ������Ϣ
                Invoke(nameof(ExecuteExit), 2f);
            }
            catch (System.Exception e)
            {
                LogDebug($"Host�˳�����ʧ��: {e.Message}");
                ExecuteExit(); // �����˳�
            }
        }

        /// <summary>
        /// ������ͨ�˳�
        /// </summary>
        private void HandleNormalExit()
        {
            LogDebug("������ͨ����˳�");

            // �����������˳���֪ͨHostGameManager
            if (HostGameManager.Instance != null && NetworkManager.Instance != null)
            {
                ushort myPlayerId = NetworkManager.Instance.ClientId;
                HostGameManager.Instance.RequestReturnToRoom(myPlayerId, "��������˳�");
            }

            // ֱ��ִ���˳�
            ExecuteExit();
        }

        /// <summary>
        /// ִ��ʵ�ʵ��˳�����
        /// </summary>
        private void ExecuteExit()
        {
            LogDebug("ִ��ʵ���˳�����");

            try
            {
                if (returnToMainMenu)
                {
                    ReturnToMainMenu();
                }
                else
                {
                    ReturnToRoom();
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"�˳�ִ��ʧ��: {e.Message}");
                // ���÷�����ֱ���л�����
                SceneManager.LoadScene("RoomScene");
            }
        }

        /// <summary>
        /// ���ط���
        /// </summary>
        private void ReturnToRoom()
        {
            LogDebug($"���ط��䳡��: {targetScene}");

            // ʹ��SceneTransitionManager��̬����
            bool success = SceneTransitionManager.SwitchToGameScene("GameExitManager");
            if (!success)
            {
                // ���÷�����ֱ���л�����
                LogDebug("SceneTransitionManager�л�ʧ�ܣ�ʹ�ñ��÷���");
                SceneManager.LoadScene(targetScene);
            }
        }

        /// <summary>
        /// �������˵�
        /// </summary>
        private void ReturnToMainMenu()
        {
            LogDebug("�������˵�");

            // ���뿪Photon����
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }

            // ʹ��SceneTransitionManager��̬����
            bool success = SceneTransitionManager.ReturnToMainMenu("GameExitManager");
            if (!success)
            {
                LogDebug("SceneTransitionManager�������˵�ʧ�ܣ�ʹ�ñ��÷���");
                SceneManager.LoadScene("MainMenu");
            }
        }

        #endregion

        #region �������

        /// <summary>
        /// ����Ƿ�����˳�
        /// </summary>
        private bool CanExit()
        {
            // �������
            if (!enableExitDuringGame)
            {
                LogDebug("��Ϸ���˳������ѽ���");
                return false;
            }

            // �������״̬
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("����Photon�����У������˳�");
                return true;
            }

            // ������״̬�������Ҫ��
            // ���磺���������ʱ�����˳�����������Ҫȷ��

            LogDebug("�˳��������ͨ��");
            return true;
        }

        /// <summary>
        /// ����Ƿ�����Ϸ������
        /// </summary>
        private bool IsInGameScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            return currentScene.Contains("NetworkGameScene") || currentScene.Contains("GameScene");
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// �ⲿ���ã������˳���Ϸ
        /// </summary>
        public void RequestExit()
        {
            LogDebug("�յ��ⲿ�˳�����");
            ShowExitConfirmation();
        }

        /// <summary>
        /// �ⲿ���ã�ǿ���˳�����ȷ�ϣ�
        /// </summary>
        public void ForceExit()
        {
            LogDebug("�յ�ǿ���˳�����");
            if (!isExiting)
            {
                ConfirmExit();
            }
        }

        /// <summary>
        /// ����˳��������Ƿ��Ծ
        /// </summary>
        public bool IsActive()
        {
            return this.enabled && !isExiting;
        }

        /// <summary>
        /// ����˳��Ի����Ƿ���
        /// </summary>
        public bool IsExitDialogOpen()
        {
            return isExitDialogOpen;
        }

        #endregion

        #region ���Է���

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[GameExitManager] {message}");
            }
        }

        #endregion
    }
}