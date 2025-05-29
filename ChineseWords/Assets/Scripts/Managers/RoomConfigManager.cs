using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// ����淿�����ù�����
    /// רע�ڻ���������UI������
    /// </summary>
    public class RoomConfigManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private RoomGameConfig defaultConfig;

        [Header("UI����")]
        [SerializeField] private GameObject configUIPrefab;
        [SerializeField] private KeyCode configUIHotkey = KeyCode.F1;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ����ʵ��
        public static RoomConfigManager Instance { get; private set; }

        // ��ǰ����
        private RoomGameConfig currentConfig;
        private RoomGameConfig runtimeConfig; // ����ʱ��ʱ����

        // UI���
        private GameObject configUIInstance;
        private RoomConfigUI configUIComponent;
        private bool isUIOpen = false;

        // ����
        public RoomGameConfig CurrentConfig => currentConfig;
        public bool IsUIOpen => isUIOpen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("RoomConfigManager �����Ѵ���");

                // ������DontDestroyOnLoad���������ڵ�ǰ����
                LoadDefaultConfig();
            }
            else if (Instance != this)
            {
                LogDebug("�����ظ���RoomConfigManagerʵ��");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            CloseConfigUI();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// ����Ĭ������
        /// </summary>
        private void LoadDefaultConfig()
        {
            LogDebug("����Ĭ������");

            if (defaultConfig != null)
            {
                currentConfig = defaultConfig.CreateCopy();
                LogDebug($"ʹ��Ԥ��Ĭ������: {currentConfig.ConfigName}");
            }
            else
            {
                // ���Դ�Resources����
                var resourceConfig = Resources.Load<RoomGameConfig>("RoomGameConfig");
                if (resourceConfig != null)
                {
                    currentConfig = resourceConfig.CreateCopy();
                    LogDebug($"��Resources��������: {currentConfig.ConfigName}");
                }
                else
                {
                    currentConfig = ScriptableObject.CreateInstance<RoomGameConfig>();
                    LogDebug("����Ĭ������");
                }
            }

            runtimeConfig = currentConfig.CreateCopy();
        }

        /// <summary>
        /// ��鵱ǰ����Ƿ�Ϊ����
        /// </summary>
        private bool IsCurrentPlayerHost()
        {
            // ���NetworkManager
            if (NetworkManager.Instance != null)
            {
                return NetworkManager.Instance.IsHost;
            }

            // ���RoomManager
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                ushort currentPlayerId = GetCurrentPlayerId();
                return currentPlayerId != 0 && currentPlayerId == room.hostId;
            }

            // ����ģʽ����
            return Application.isEditor;
        }

        /// <summary>
        /// ��ȡ��ǰ���ID
        /// </summary>
        private ushort GetCurrentPlayerId()
        {
            if (NetworkManager.Instance != null)
            {
                return NetworkManager.Instance.ClientId;
            }
            return 0;
        }

        /// <summary>
        /// ����Ƿ��ڷ��䳡��
        /// </summary>
        private bool IsInRoomScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            return currentScene.Contains("Room") || currentScene.Contains("Lobby");
        }

        /// <summary>
        /// ������Ȩ��
        /// </summary>
        private bool CheckPermissions()
        {
            if (!IsInRoomScene())
            {
                LogDebug("��ǰ���ڷ��䳡�����޷�������");
                return false;
            }

            if (!IsCurrentPlayerHost())
            {
                LogDebug("ֻ�з������Դ�����UI");
                return false;
            }

            return true;
        }

        /// <summary>
        /// ��������UI����
        /// </summary>
        public void HandleConfigUIToggle()
        {
            LogDebug($"��⵽ {configUIHotkey} ����");

            if (!CheckPermissions())
            {
                return;
            }

            if (isUIOpen)
            {
                CloseConfigUI();
            }
            else
            {
                OpenConfigUI();
            }
        }

        /// <summary>
        /// ������UI
        /// </summary>
        public void OpenConfigUI()
        {
            if (isUIOpen)
            {
                LogDebug("����UI�Ѿ���");
                return;
            }

            LogDebug("������UI");

            if (configUIInstance == null)
            {
                CreateConfigUIInstance();
            }

            if (configUIInstance != null)
            {
                configUIInstance.SetActive(true);
                isUIOpen = true;

                if (configUIComponent != null)
                {
                    configUIComponent.Initialize(this, runtimeConfig);
                }

                LogDebug($"����UI�Ѵ� - ����: {configUIInstance.scene.name}");
            }
            else
            {
                LogDebug("����UI����ʧ��");
            }
        }

        /// <summary>
        /// �ر�����UI
        /// </summary>
        public void CloseConfigUI()
        {
            if (!isUIOpen)
                return;

            LogDebug("�ر�����UI");

            if (configUIInstance != null)
            {
                configUIInstance.SetActive(false);
            }

            isUIOpen = false;
        }

        /// <summary>
        /// ��������UIʵ�� - ȷ���ڵ�ǰ������
        /// </summary>
        private void CreateConfigUIInstance()
        {
            LogDebug("��������UIʵ��");

            if (configUIPrefab == null)
            {
                configUIPrefab = Resources.Load<GameObject>("RoomConfigUI");
                if (configUIPrefab == null)
                {
                    configUIPrefab = Resources.Load<GameObject>("UI/RoomConfigUI");
                }
            }

            if (configUIPrefab != null)
            {
                // �ؼ��޸����ڵ�ǰ������Canvas�´���UI
                configUIInstance = CreateUIInCurrentScene();

                if (configUIInstance != null)
                {
                    configUIComponent = configUIInstance.GetComponent<RoomConfigUI>();
                    if (configUIComponent == null)
                    {
                        LogDebug("��������UIԤ����ȱ��RoomConfigUI���");
                    }

                    configUIInstance.SetActive(false);
                    LogDebug($"����UIʵ�������ɹ� - ����: {configUIInstance.scene.name}");
                }
            }
            else
            {
                LogDebug("�����޷���������UIԤ����");
            }
        }

        /// <summary>
        /// �ڵ�ǰ�����д���UI������DontDestroyOnLoad���⣩
        /// </summary>
        private GameObject CreateUIInCurrentScene()
        {
            LogDebug("�ڵ�ǰ�����д���UI");

            // ����1��ֱ��ʵ������Unity���Զ����ڵ�ǰ����
            var uiInstance = Instantiate(configUIPrefab);

            // ����2�����UI��Ȼ�ܵ�DontDestroy��ǿ���ƻص�ǰ����
            if (uiInstance.scene.name == "DontDestroyOnLoad")
            {
                LogDebug("UI��������DontDestroyOnLoad��ǿ���ƻص�ǰ����");
                MoveToCurrentScene(uiInstance);
            }

            // ȷ��Canvas������ȷ
            var canvas = uiInstance.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
            }

            return uiInstance;
        }

        /// <summary>
        /// ǿ�ƽ������ƶ�����ǰ����
        /// </summary>
        private void MoveToCurrentScene(GameObject obj)
        {
            var currentScene = SceneManager.GetActiveScene();
            var rootObjects = currentScene.GetRootGameObjects();

            if (rootObjects.Length > 0)
            {
                // ��ʱ��Ϊ������ĳ��������Ӷ���Ȼ����Ϊnull
                obj.transform.SetParent(rootObjects[0].transform);
                obj.transform.SetParent(null);

                LogDebug($"�������ƶ�����ǰ����: {obj.scene.name}");
            }
        }

        /// <summary>
        /// Ӧ�����ø���
        /// </summary>
        public void ApplyConfigChanges()
        {
            LogDebug("Ӧ�����ø���");

            if (runtimeConfig != null && runtimeConfig.ValidateConfig())
            {
                currentConfig = runtimeConfig.CreateCopy();
                currentConfig.ClearDirty();
                LogDebug("������Ӧ��");
            }
        }

        /// <summary>
        /// ����ΪĬ������
        /// </summary>
        public void ResetToDefault()
        {
            LogDebug("����ΪĬ������");
            runtimeConfig.ResetToDefault();
        }

        /// <summary>
        /// ��ȡ���ݸ���Ϸ����������
        /// </summary>
        public RoomGameConfig GetGameSceneConfig()
        {
            return currentConfig?.CreateCopy();
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomConfigManager] {message}");
            }
        }

        #region ���Է���

        [ContextMenu("ǿ�ƴ�����UI")]
        public void ForceOpenConfigUI()
        {
            LogDebug("ǿ�ƴ�����UI������ģʽ��");
            OpenConfigUI();
        }

        [ContextMenu("��ʾ��ǰ����")]
        public void ShowCurrentConfig()
        {
            Debug.Log(currentConfig?.GetConfigSummary() ?? "����δ����");
        }

        [ContextMenu("�����򵥲���UI")]
        public void CreateSimpleTestUI()
        {
            // ��������UI
            if (configUIInstance != null)
            {
                DestroyImmediate(configUIInstance);
                configUIInstance = null;
            }

            // ������򵥵Ĳ���UI
            var testUI = new GameObject("SimpleTestUI");
            var canvas = testUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            testUI.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // ��ɫ����
            var panel = new GameObject("TestPanel");
            panel.transform.SetParent(testUI.transform, false);
            var image = panel.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.red;
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 300);
            rect.anchoredPosition = Vector2.zero;

            // �ı�
            var textObj = new GameObject("TestText");
            textObj.transform.SetParent(panel.transform, false);
            var text = textObj.AddComponent<UnityEngine.UI.Text>();
            text.text = "����UI����";
            text.color = Color.white;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            configUIInstance = testUI;
            isUIOpen = true;

            LogDebug($"����UI������� - ����: {testUI.scene.name}");
        }

        #endregion
    }
}