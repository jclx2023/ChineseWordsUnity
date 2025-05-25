using UnityEngine;
using Core.Network;
using UI;

namespace Core
{
    /// <summary>
    /// ����ģʽ������
    /// ������Ϸģʽ�Զ�����/������Ӧ���
    /// </summary>
    public class SceneModeController : MonoBehaviour
    {
        [Header("�������")]
        [SerializeField] private GameObject networkManager;
        [SerializeField] private GameObject hostGameManager;
        [SerializeField] private GameObject networkCanvas;
        [SerializeField] private GameObject gameCanvas;
        [SerializeField] private NetworkUI networkUI;

        [Header("��������")]
        [SerializeField] private bool showDebugLogs = true;

        private void Start()
        {
            ConfigureSceneBasedOnGameMode();
        }

        /// <summary>
        /// ������Ϸģʽ���ó���
        /// </summary>
        private void ConfigureSceneBasedOnGameMode()
        {
            var gameMode = MainMenuManager.SelectedGameMode;
            LogDebug($"���ó���ģʽ: {gameMode}");

            switch (gameMode)
            {
                case MainMenuManager.GameMode.SinglePlayer:
                    ConfigureSinglePlayerMode();
                    break;

                case MainMenuManager.GameMode.Host:
                    ConfigureHostMode();
                    break;

                case MainMenuManager.GameMode.Client:
                    ConfigureClientMode();
                    break;

                default:
                    Debug.LogWarning($"δ֪��Ϸģʽ: {gameMode}��ʹ�õ���ģʽ����");
                    ConfigureSinglePlayerMode();
                    break;
            }
        }

        /// <summary>
        /// ���õ���ģʽ
        /// </summary>
        private void ConfigureSinglePlayerMode()
        {
            LogDebug("���õ���ģʽ");

            // �������
            SetGameObjectActive(networkManager, false);
            SetGameObjectActive(hostGameManager, false);

            // UI���
            SetGameObjectActive(networkCanvas, false);
            SetGameObjectActive(gameCanvas, true);

            // ��������UI
            if (networkUI != null)
                networkUI.HideNetworkPanel();
        }

        /// <summary>
        /// ��������ģʽ
        /// </summary>
        private void ConfigureHostMode()
        {
            LogDebug("��������ģʽ");

            // �������
            SetGameObjectActive(networkManager, true);
            SetGameObjectActive(hostGameManager, true);

            // UI���
            SetGameObjectActive(networkCanvas, true);
            SetGameObjectActive(gameCanvas, true);

            // ��ʾ����UI
            if (networkUI != null)
                networkUI.ShowNetworkPanel();
        }

        /// <summary>
        /// ���ÿͻ���ģʽ
        /// </summary>
        private void ConfigureClientMode()
        {
            LogDebug("���ÿͻ���ģʽ");

            // �������
            SetGameObjectActive(networkManager, true);
            SetGameObjectActive(hostGameManager, false);

            // UI���
            SetGameObjectActive(networkCanvas, true);
            SetGameObjectActive(gameCanvas, true);

            // ��ʾ����UI
            if (networkUI != null)
                networkUI.ShowNetworkPanel();
        }

        /// <summary>
        /// ��ȫ����GameObject����״̬
        /// </summary>
        private void SetGameObjectActive(GameObject obj, bool active)
        {
            if (obj != null)
            {
                obj.SetActive(active);
                LogDebug($"���� {obj.name} ����״̬: {active}");
            }
            else
            {
                Debug.LogWarning($"GameObject����Ϊ�գ��޷����ü���״̬");
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[SceneModeController] {message}");
            }
        }

        /// <summary>
        /// ��̬�л�ģʽ����������ʱ���ԣ�
        /// </summary>
        [ContextMenu("�������ó���")]
        public void ReconfigureScene()
        {
            ConfigureSceneBasedOnGameMode();
        }
    }
}