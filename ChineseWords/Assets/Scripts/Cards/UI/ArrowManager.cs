using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Cards.Core;
using UI;

namespace Cards.UI
{
    /// <summary>
    /// ��ͷ������ - ����������ͷ���������ں�Ŀ����
    /// </summary>
    public class ArrowManager : MonoBehaviour
    {
        [Header("��ͷԤ����")]
        private GameObject arrowPrefab; // ��CardUIManager���룬����Inspector������

        [Header("Ŀ��������")]
        [SerializeField] private LayerMask uiLayerMask = -1; // UI���㼶
        [SerializeField] private float detectionRadius = 50f; // PlayerConsole���뾶

        [Header("�����������ã���CardUIManagerͬ����")]
        [SerializeField] private float releaseAreaCenterX = 0.5f;
        [SerializeField] private float releaseAreaCenterY = 0.5f;
        [SerializeField] private float releaseAreaWidth = 0.3f;
        [SerializeField] private float releaseAreaHeight = 0.3f;

        [Header("�Ӿ���������")]
        [SerializeField] private Color normalArrowColor = Color.white;
        [SerializeField] private Color validTargetColor = Color.green;
        [SerializeField] private Color invalidTargetColor = Color.gray;
        [SerializeField] private float beChoosenFadeSpeed = 5f; // BeChoosen�����ٶ�

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = false;

        // Ŀ������ö��
        public enum TargetDetectionResult
        {
            None,           // ��Ŀ��
            PlayerConsole,  // ָ�����Console
            CenterArea,     // ָ����������
            Invalid         // ��Ч����
        }

        // ��ͷ״̬
        private bool isArrowActive = false;
        private GameObject currentArrowInstance = null;
        private BezierArrowRenderer currentArrowRenderer = null;
        private Vector2 arrowStartPosition = Vector2.zero;

        // Ŀ�������
        private TargetDetectionResult currentTargetType = TargetDetectionResult.None;
        private ushort currentTargetPlayerId = 0;
        private GameObject currentTargetConsole = null;
        private CardData currentCardData = null;

        // ��������
        private Canvas parentCanvas;
        private NetworkUI networkUI;
        private Camera uiCamera;

        // BeChoosenЧ������
        private Dictionary<GameObject, CanvasGroup> beChoosenCanvasGroups = new Dictionary<GameObject, CanvasGroup>();

        // �¼�
        public System.Action<ushort> OnValidPlayerTargetDetected; // ��⵽��Ч���Ŀ��
        public System.Action OnValidCenterAreaDetected; // ��⵽��Ч��������
        public System.Action OnInvalidTargetDetected; // ��⵽��ЧĿ��
        public System.Action OnNoTargetDetected; // ��Ŀ��

        #region Unity��������

        private void Update()
        {
            if (isArrowActive && currentArrowInstance != null)
            {
                UpdateTargetDetection();
                UpdateArrowVisuals();
                UpdateBeChoosenEffects();
            }
        }

        private void OnDestroy()
        {
            CleanupArrow();
        }

        #endregion

        #region ��ʼ��������

        /// <summary>
        /// ��CardUIManager���õĳ�ʼ������
        /// </summary>
        public void Initialize(Canvas canvas, GameObject arrowPrefabRef)
        {
            // ������������
            parentCanvas = canvas;
            arrowPrefab = arrowPrefabRef;

            // ��֤��Ҫ���
            if (parentCanvas == null)
            {
                LogError("Canvas����Ϊ�գ�");
                return;
            }

            if (arrowPrefab == null)
            {
                LogError("��ͷԤ��������Ϊ�գ�");
                return;
            }

            // ��ȡUI�����
            uiCamera = parentCanvas.worldCamera;

            // ����NetworkUI
            networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI == null)
            {
                LogWarning("δ�ҵ�NetworkUI��������Ŀ��������޷���������");
            }

            LogDebug("ArrowManager��ʼ�����");
        }

        /// <summary>
        /// ��CardUIManager���ã����ü�ͷԤ��������
        /// </summary>
        public void SetArrowPrefab(GameObject prefab)
        {
            arrowPrefab = prefab;
            LogDebug($"��ͷԤ����������: {prefab?.name}");
        }

        /// <summary>
        /// ���ArrowManager�Ƿ�����ȷ��ʼ��
        /// </summary>
        public bool IsInitialized()
        {
            return parentCanvas != null && arrowPrefab != null;
        }

        /// <summary>
        /// ͬ�������������ã���CardUIManager���ã�
        /// </summary>
        public void SyncCenterAreaSettings(float centerX, float centerY, float width, float height)
        {
            releaseAreaCenterX = centerX;
            releaseAreaCenterY = centerY;
            releaseAreaWidth = width;
            releaseAreaHeight = height;

            LogDebug($"��������������ͬ��: Center({centerX}, {centerY}), Size({width}, {height})");
        }

        #endregion

        #region ��ͷ�������ڹ���

        /// <summary>
        /// ��ʼ��ͷĿ��ѡ��
        /// </summary>
        public bool StartArrowTargeting(Vector2 startPosition, CardData cardData)
        {
            if (!IsInitialized())
            {
                LogError("ArrowManagerδ��ȷ��ʼ�����޷���ʼ��ͷĿ��ѡ��");
                return false;
            }

            if (isArrowActive)
            {
                LogWarning("��ͷ�Ѿ����ڼ���״̬���޷��ظ���ʼ");
                return false;
            }

            LogDebug($"��ʼ��ͷĿ��ѡ�� - ���: {startPosition}, ����: {cardData?.cardName}");

            // ����״̬
            arrowStartPosition = startPosition;
            currentCardData = cardData;

            // ������ͷʵ��
            if (!CreateArrowInstance())
            {
                return false;
            }

            // ���ü�ͷ���
            SetArrowStartPosition(startPosition);

            // �����ͷ
            isArrowActive = true;

            return true;
        }

        /// <summary>
        /// ������ͷĿ��ѡ��
        /// </summary>
        public TargetDetectionResult EndArrowTargeting(out ushort targetPlayerId)
        {
            targetPlayerId = 0;

            if (!isArrowActive)
            {
                LogWarning("��ͷδ����޷�����Ŀ��ѡ��");
                return TargetDetectionResult.None;
            }

            LogDebug($"������ͷĿ��ѡ�� - ����Ŀ������: {currentTargetType}");

            // �������ս��
            TargetDetectionResult finalResult = currentTargetType;
            targetPlayerId = currentTargetPlayerId;

            // ����״̬
            CleanupArrow();

            return finalResult;
        }

        /// <summary>
        /// ȡ����ͷĿ��ѡ��
        /// </summary>
        public void CancelArrowTargeting()
        {
            if (!isArrowActive)
            {
                return;
            }

            LogDebug("ȡ����ͷĿ��ѡ��");

            // ����״̬
            CleanupArrow();
        }

        /// <summary>
        /// ������ͷʵ��
        /// </summary>
        private bool CreateArrowInstance()
        {
            if (currentArrowInstance != null)
            {
                LogWarning("��ͷʵ���Ѵ��ڣ��������ʵ��");
                DestroyArrowInstance();
            }

            // ʵ������ͷԤ����
            currentArrowInstance = Instantiate(arrowPrefab, transform);
            currentArrowInstance.name = "ActiveArrow";

            // ��ȡBezierArrowRenderer���
            currentArrowRenderer = currentArrowInstance.GetComponent<BezierArrowRenderer>();
            if (currentArrowRenderer == null)
            {
                LogError("��ͷԤ����ȱ��BezierArrowRenderer�����");
                DestroyArrowInstance();
                return false;
            }

            // ����BezierArrowRenderer��Update�������ֶ����ƣ�
            currentArrowRenderer.enabled = false;

            LogDebug("��ͷʵ�������ɹ�");
            return true;
        }

        /// <summary>
        /// ���ټ�ͷʵ��
        /// </summary>
        private void DestroyArrowInstance()
        {
            if (currentArrowInstance != null)
            {
                Destroy(currentArrowInstance);
                currentArrowInstance = null;
                currentArrowRenderer = null;
                LogDebug("��ͷʵ��������");
            }
        }

        /// <summary>
        /// ���ü�ͷ���λ��
        /// </summary>
        private void SetArrowStartPosition(Vector2 position)
        {
            if (currentArrowInstance != null)
            {
                RectTransform arrowRect = currentArrowInstance.GetComponent<RectTransform>();
                if (arrowRect != null)
                {
                    arrowRect.anchoredPosition = position;
                }
            }
        }

        /// <summary>
        /// �����ͷ���״̬
        /// </summary>
        private void CleanupArrow()
        {
            // ����BeChoosenЧ��
            ClearAllBeChoosenEffects();

            // ���ټ�ͷʵ��
            DestroyArrowInstance();

            // ����״̬
            isArrowActive = false;
            currentTargetType = TargetDetectionResult.None;
            currentTargetPlayerId = 0;
            currentTargetConsole = null;
            currentCardData = null;
            arrowStartPosition = Vector2.zero;

            LogDebug("��ͷ״̬������");
        }

        #endregion

        #region Ŀ����

        /// <summary>
        /// ����Ŀ����
        /// </summary>
        private void UpdateTargetDetection()
        {
            // ��ȡ���λ��
            Vector2 mouseScreenPos = Input.mousePosition;

            // �ȼ��PlayerConsole
            TargetDetectionResult newTargetType = TargetDetectionResult.None;
            ushort newTargetPlayerId = 0;
            GameObject newTargetConsole = null;

            if (DetectPlayerConsoleTarget(mouseScreenPos, out newTargetPlayerId, out newTargetConsole))
            {
                newTargetType = TargetDetectionResult.PlayerConsole;
            }
            else if (DetectCenterAreaTarget(mouseScreenPos))
            {
                newTargetType = TargetDetectionResult.CenterArea;
            }

            // ��֤Ŀ����Ч��
            if (newTargetType != TargetDetectionResult.None)
            {
                if (!ValidateTarget(newTargetType, newTargetPlayerId))
                {
                    newTargetType = TargetDetectionResult.Invalid;
                }
            }

            // ����Ŀ��״̬
            UpdateTargetState(newTargetType, newTargetPlayerId, newTargetConsole);
        }

        /// <summary>
        /// ���PlayerConsoleĿ��
        /// </summary>
        private bool DetectPlayerConsoleTarget(Vector2 screenPosition, out ushort playerId, out GameObject console)
        {
            playerId = 0;
            console = null;

            if (networkUI == null)
            {
                return false;
            }

            // ��ȡ����PlayerConsole
            var playerConsoles = GetAllPlayerConsoles();

            foreach (var consoleData in playerConsoles)
            {
                if (consoleData.console == null) continue;

                // �������Ƿ���Console��Χ��
                if (IsPointInConsole(screenPosition, consoleData.console))
                {
                    playerId = consoleData.playerId;
                    console = consoleData.console;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// �����������Ŀ��
        /// </summary>
        private bool DetectCenterAreaTarget(Vector2 screenPosition)
        {
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 mousePercent = new Vector2(screenPosition.x / screenSize.x, screenPosition.y / screenSize.y);

            // ������������߽�
            float leftBound = releaseAreaCenterX - releaseAreaWidth / 2f;
            float rightBound = releaseAreaCenterX + releaseAreaWidth / 2f;
            float bottomBound = releaseAreaCenterY - releaseAreaHeight / 2f;
            float topBound = releaseAreaCenterY + releaseAreaHeight / 2f;

            // ����Ƿ���������
            return mousePercent.x >= leftBound && mousePercent.x <= rightBound &&
                   mousePercent.y >= bottomBound && mousePercent.y <= topBound;
        }

        /// <summary>
        /// ��֤Ŀ���Ƿ���Ч
        /// </summary>
        private bool ValidateTarget(TargetDetectionResult targetType, ushort playerId)
        {
            if (currentCardData == null)
            {
                return false;
            }

            switch (currentCardData.targetType)
            {
                case TargetType.Self:
                case TargetType.AllPlayers:
                    // �Է��Ϳ���ֻ���ϵ���������
                    return targetType == TargetDetectionResult.CenterArea;

                case TargetType.SinglePlayer:
                case TargetType.AllOthers:
                case TargetType.Random:
                    // ָ���Ϳ��Ʊ���ָ��PlayerConsole
                    return targetType == TargetDetectionResult.PlayerConsole && playerId > 0;

                default:
                    return false;
            }
        }

        /// <summary>
        /// ����Ŀ��״̬
        /// </summary>
        private void UpdateTargetState(TargetDetectionResult newTargetType, ushort newPlayerId, GameObject newConsole)
        {
            // ����Ƿ��б仯
            bool targetChanged = currentTargetType != newTargetType ||
                                currentTargetPlayerId != newPlayerId ||
                                currentTargetConsole != newConsole;

            if (!targetChanged) return;

            // ����״̬
            currentTargetType = newTargetType;
            currentTargetPlayerId = newPlayerId;
            currentTargetConsole = newConsole;

            // ������Ӧ�¼�
            switch (currentTargetType)
            {
                case TargetDetectionResult.PlayerConsole:
                    OnValidPlayerTargetDetected?.Invoke(currentTargetPlayerId);
                    LogDebug($"��⵽��Ч���Ŀ��: {currentTargetPlayerId}");
                    break;

                case TargetDetectionResult.CenterArea:
                    OnValidCenterAreaDetected?.Invoke();
                    LogDebug("��⵽��Ч��������Ŀ��");
                    break;

                case TargetDetectionResult.Invalid:
                    OnInvalidTargetDetected?.Invoke();
                    LogDebug("��⵽��ЧĿ��");
                    break;

                case TargetDetectionResult.None:
                    OnNoTargetDetected?.Invoke();
                    break;
            }
        }

        #endregion

        #region PlayerConsole���

        /// <summary>
        /// PlayerConsole���ݽṹ
        /// </summary>
        private struct PlayerConsoleData
        {
            public ushort playerId;
            public GameObject console;
        }

        /// <summary>
        /// ��ȡ����PlayerConsole
        /// </summary>
        private List<PlayerConsoleData> GetAllPlayerConsoles()
        {
            var consoles = new List<PlayerConsoleData>();

            if (networkUI == null) return consoles;

            // ʹ�÷����ȡNetworkUI��playerUIItems
            var playerUIItemsField = typeof(NetworkUI).GetField("playerUIItems",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (playerUIItemsField != null)
            {
                var playerUIItems = playerUIItemsField.GetValue(networkUI) as System.Collections.IDictionary;
                if (playerUIItems != null)
                {
                    foreach (System.Collections.DictionaryEntry entry in playerUIItems)
                    {
                        var playerId = (ushort)entry.Key;
                        var components = entry.Value;

                        // ��ȡitemObject
                        var itemObjectField = components.GetType().GetField("itemObject");
                        if (itemObjectField != null)
                        {
                            var itemObject = itemObjectField.GetValue(components) as GameObject;
                            if (itemObject != null)
                            {
                                consoles.Add(new PlayerConsoleData
                                {
                                    playerId = playerId,
                                    console = itemObject
                                });
                            }
                        }
                    }
                }
            }

            return consoles;
        }

        /// <summary>
        /// �����Ƿ���Console��Χ��
        /// </summary>
        private bool IsPointInConsole(Vector2 screenPoint, GameObject console)
        {
            RectTransform consoleRect = console.GetComponent<RectTransform>();
            if (consoleRect == null) return false;

            // ת��Ϊ����������м��
            Vector2 localPoint;
            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                consoleRect, screenPoint, uiCamera, out localPoint);

            if (!success) return false;

            // ����Ƿ��ھ��η�Χ��
            Rect rect = consoleRect.rect;
            return rect.Contains(localPoint);
        }

        #endregion

        #region BeChoosenЧ������

        /// <summary>
        /// ����BeChoosenЧ��
        /// </summary>
        private void UpdateBeChoosenEffects()
        {
            // ����֮ǰ��Ч��
            ClearAllBeChoosenEffects();

            // �����ǰָ��PlayerConsole����ʾBeChoosenЧ��
            if (currentTargetType == TargetDetectionResult.PlayerConsole && currentTargetConsole != null)
            {
                ShowBeChoosenEffect(currentTargetConsole);
            }
        }

        /// <summary>
        /// ��ʾBeChoosenЧ��
        /// </summary>
        private void ShowBeChoosenEffect(GameObject console)
        {
            // ����BeChoosenͼ��
            Transform beChoosenTransform = FindChildRecursive(console.transform, "BeChoosen");
            if (beChoosenTransform == null) return;

            GameObject beChoosenObject = beChoosenTransform.gameObject;
            beChoosenObject.SetActive(true);

            // ���ý���Ч��
            CanvasGroup canvasGroup = beChoosenObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = beChoosenObject.AddComponent<CanvasGroup>();
            }

            // ��¼CanvasGroup���ں�������
            if (!beChoosenCanvasGroups.ContainsKey(beChoosenObject))
            {
                beChoosenCanvasGroups[beChoosenObject] = canvasGroup;
            }

            // ���䵽�ɼ�
            StartCoroutine(FadeBeChoosen(canvasGroup, 1f));
        }

        /// <summary>
        /// ��������BeChoosenЧ��
        /// </summary>
        private void ClearAllBeChoosenEffects()
        {
            var keysToRemove = new List<GameObject>();

            foreach (var kvp in beChoosenCanvasGroups)
            {
                if (kvp.Key == null)
                {
                    keysToRemove.Add(kvp.Key);
                    continue;
                }

                // ���䵽����
                StartCoroutine(FadeBeChoosen(kvp.Value, 0f, () => {
                    if (kvp.Key != null)
                    {
                        kvp.Key.SetActive(false);
                    }
                }));
            }

            // ������Ч����
            foreach (var key in keysToRemove)
            {
                beChoosenCanvasGroups.Remove(key);
            }

            beChoosenCanvasGroups.Clear();
        }

        /// <summary>
        /// BeChoosen����Э��
        /// </summary>
        private System.Collections.IEnumerator FadeBeChoosen(CanvasGroup canvasGroup, float targetAlpha, System.Action onComplete = null)
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            float duration = 1f / beChoosenFadeSpeed;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            onComplete?.Invoke();
        }

        #endregion

        #region ��ͷ�Ӿ�����

        /// <summary>
        /// ���¼�ͷ�Ӿ�Ч��
        /// </summary>
        private void UpdateArrowVisuals()
        {
            if (currentArrowRenderer == null) return;

            // ����Ŀ��״̬���ü�ͷ��ɫ
            Color targetColor = GetArrowColorForTarget();

            // TODO: Ӧ����ɫ����ͷ�ڵ�
            // ������Ҫ����shaderʵ����������ɫ
            // Ŀǰ��ʱ��ʵ�֣���shader��ɺ������
        }

        /// <summary>
        /// ����Ŀ�����ͻ�ȡ��ͷ��ɫ
        /// </summary>
        private Color GetArrowColorForTarget()
        {
            switch (currentTargetType)
            {
                case TargetDetectionResult.PlayerConsole:
                case TargetDetectionResult.CenterArea:
                    return validTargetColor;

                case TargetDetectionResult.Invalid:
                    return invalidTargetColor;

                default:
                    return normalArrowColor;
            }
        }

        #endregion

        #region ���߷���

        /// <summary>
        /// �ݹ�����Ӷ���
        /// </summary>
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

        /// <summary>
        /// ��ȡ��ǰ�������Ϣ
        /// </summary>
        public string GetCurrentTargetInfo()
        {
            switch (currentTargetType)
            {
                case TargetDetectionResult.PlayerConsole:
                    return $"���Ŀ��: {currentTargetPlayerId}";
                case TargetDetectionResult.CenterArea:
                    return "��������Ŀ��";
                case TargetDetectionResult.Invalid:
                    return "��ЧĿ��";
                default:
                    return "��Ŀ��";
            }
        }

        #endregion

        #region ���Թ���

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // ������������
            DrawCenterAreaGizmo();

            // ���Ƽ�ͷ���
            if (isArrowActive)
            {
                Gizmos.color = Color.yellow;
                Vector3 worldPos = transform.TransformPoint(arrowStartPosition);
                Gizmos.DrawWireSphere(worldPos, 20f);
            }
        }

        /// <summary>
        /// ������������
        /// </summary>
        private void DrawCenterAreaGizmo()
        {
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);

            float leftBound = (releaseAreaCenterX - releaseAreaWidth / 2f) * screenSize.x;
            float rightBound = (releaseAreaCenterX + releaseAreaWidth / 2f) * screenSize.x;
            float bottomBound = (releaseAreaCenterY - releaseAreaHeight / 2f) * screenSize.y;
            float topBound = (releaseAreaCenterY + releaseAreaHeight / 2f) * screenSize.y;

            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3((leftBound + rightBound) / 2f, (bottomBound + topBound) / 2f, 0f);
            Vector3 size = new Vector3(rightBound - leftBound, topBound - bottomBound, 1f);

            Gizmos.DrawWireCube(center, size);
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[ArrowManager] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[ArrowManager] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[ArrowManager] {message}");
        }

        #endregion

        #region �������Ժͽӿ�

        public bool IsArrowActive => isArrowActive;
        public TargetDetectionResult CurrentTargetType => currentTargetType;
        public ushort CurrentTargetPlayerId => currentTargetPlayerId;

        /// <summary>
        /// ��̬�������� - ��CardUIManager���ô���ArrowManagerʵ��
        /// </summary>
        public static ArrowManager CreateArrowManager(Transform parent, Canvas canvas, GameObject arrowPrefab)
        {
            // ����ArrowManager GameObject
            GameObject arrowManagerObj = new GameObject("ArrowManager");
            arrowManagerObj.transform.SetParent(parent, false);

            // ���ArrowManager���
            ArrowManager arrowManager = arrowManagerObj.AddComponent<ArrowManager>();

            // ��ʼ��
            arrowManager.Initialize(canvas, arrowPrefab);

            return arrowManager;
        }

        #endregion
    }
}