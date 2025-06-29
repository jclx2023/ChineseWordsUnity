using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Cards.Core;
using Cards.Player;
using Core.Network;

namespace Cards.UI
{
    /// <summary>
    /// ����UI������ - ��������
    /// ����ͳһ������UIϵͳ��Э����ģ����Ϊ�����������״̬����
    /// </summary>
    public class CardUIManager : MonoBehaviour
    {
        [Header("Canvas����")]
        [SerializeField] private Canvas cardUICanvas; // �����Ŀ���UI Canvas
        [SerializeField] private int canvasSortingOrder = 105; // Canvas�㼶

        [Header("������ã���ѡ���������Զ�����/������")]
        [SerializeField] private CardUIComponents cardUIComponents; // �����������ѡ��
        [SerializeField] private CardDisplayUI cardDisplayUI; // չʾ����������ѡ��

        [Header("��������")]
        [SerializeField] private KeyCode cardDisplayTriggerKey = KeyCode.E; // ����չʾ������

        [Header("��ק���� - �ͷ�������Ļ�ٷֱȣ�")]
        [SerializeField] private float releaseAreaCenterX = 0.5f; // 50%
        [SerializeField] private float releaseAreaCenterY = 0.5f; // 50%
        [SerializeField] private float releaseAreaWidth = 0.3f; // 30%
        [SerializeField] private float releaseAreaHeight = 0.3f; // 30%

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showReleaseArea = false; // ��ʾ�ͷ�����

        // ����ʵ��
        public static CardUIManager Instance { get; private set; }

        // UI״̬
        public enum UIState
        {
            Hidden,           // ����״̬
            Thumbnail,        // ����ͼ״̬
            FanDisplay,       // ����չʾ״̬
            Dragging,         // ��ק״̬
            Disabled          // ����״̬�������ڼ䣩
        }

        private UIState currentUIState = UIState.Hidden;
        private bool isInitialized = false;
        private bool isMyTurn = false;
        private bool canUseCards = true;

        // ��������
        private List<CardDisplayData> currentHandCards = new List<CardDisplayData>();
        private GameObject draggedCard = null;
        private Vector3 dragOffset = Vector3.zero;

        // ��ק��أ�TODO: ��ͷ���ƣ�
        private bool isDragging = false;
        private Vector3 dragStartPosition;

        // �¼�
        public System.Action<int, int> OnCardUseRequested; // cardId, targetPlayerId
        public System.Action OnCardUIOpened;
        public System.Action OnCardUIClosed;

        // ����
        public UIState CurrentState => currentUIState;
        public bool IsCardUIVisible => currentUIState != UIState.Hidden && currentUIState != UIState.Disabled;
        public bool CanOpenCardUI => currentUIState == UIState.Hidden && !isMyTurn && canUseCards;

        #region Unity��������

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeManager();
            }
            else
            {
                LogDebug("�����ظ���CardUIManagerʵ�������ٵ�ǰʵ��");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            StartCoroutine(DelayedInitialization());
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            HandleInput();
            HandleDragging();
        }

        #endregion

        #region ��ʼ��

        /// <summary>
        /// ��ʼ��������
        /// </summary>
        private void InitializeManager()
        {
            // ����Canvas
            SetupCanvas();

            // ���һ򴴽����
            FindOrCreateComponents();

            LogDebug("CardUIManager��ʼ����ʼ");
        }

        /// <summary>
        /// �ӳٳ�ʼ��
        /// </summary>
        private IEnumerator DelayedInitialization()
        {
            // �ȴ�����ϵͳ��ʼ�����
            yield return new WaitForSeconds(0.5f);

            // ��֤ϵͳ����
            if (!ValidateSystemDependencies())
            {
                LogError("ϵͳ������֤ʧ�ܣ�CardUIManager�޷���������");
                yield break;
            }

            // ��ʼ�����
            isInitialized = true;
            LogDebug("CardUIManager��ʼ�����");

            // ��ʾ����ͼ
            ShowThumbnail();
        }

        /// <summary>
        /// ����Canvas
        /// </summary>
        private void SetupCanvas()
        {
            if (cardUICanvas == null)
            {
                // ��������Canvas
                GameObject canvasObject = new GameObject("CardUICanvas");
                canvasObject.transform.SetParent(transform, false);

                cardUICanvas = canvasObject.AddComponent<Canvas>();
                cardUICanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cardUICanvas.sortingOrder = canvasSortingOrder;

                // ���CanvasScaler��������
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                // ���GraphicRaycaster����UI����
                canvasObject.AddComponent<GraphicRaycaster>();

                LogDebug("�����˶�����CardUICanvas");
            }
            else
            {
                // ��������Canvas
                cardUICanvas.sortingOrder = canvasSortingOrder;
            }
        }

        /// <summary>
        /// ���һ򴴽����
        /// </summary>
        private void FindOrCreateComponents()
        {
            // ����CardUIComponents������ʹ��ȫ�ֵ�����
            if (cardUIComponents == null)
            {
                cardUIComponents = CardUIComponents.Instance;
                if (cardUIComponents == null)
                {
                    // ���û��ȫ��ʵ�������һ򴴽�����ʵ��
                    cardUIComponents = FindObjectOfType<CardUIComponents>();
                    if (cardUIComponents == null)
                    {
                        GameObject componentsObject = new GameObject("CardUIComponents");
                        componentsObject.transform.SetParent(transform, false);
                        cardUIComponents = componentsObject.AddComponent<CardUIComponents>();
                        LogDebug("�������µ�CardUIComponentsʵ��");
                    }
                    else
                    {
                        LogDebug("�ҵ��˳����е�CardUIComponentsʵ��");
                    }
                }
                else
                {
                    LogDebug("ʹ��ȫ��CardUIComponents����");
                }
            }

            // ���һ򴴽�CardDisplayUI
            if (cardDisplayUI == null)
            {
                cardDisplayUI = GetComponentInChildren<CardDisplayUI>();
                if (cardDisplayUI == null)
                {
                    GameObject displayObject = new GameObject("CardDisplayUI");
                    displayObject.transform.SetParent(cardUICanvas.transform, false);
                    cardDisplayUI = displayObject.AddComponent<CardDisplayUI>();
                    LogDebug("�������µ�CardDisplayUIʵ��");
                }
                else
                {
                    LogDebug("�ҵ����Ӷ����е�CardDisplayUIʵ��");
                }
            }
        }

        /// <summary>
        /// ��֤ϵͳ����
        /// </summary>
        private bool ValidateSystemDependencies()
        {
            bool isValid = true;

            if (cardUIComponents == null)
            {
                LogError("CardUIComponentsδ�ҵ�");
                isValid = false;
            }

            if (cardDisplayUI == null)
            {
                LogError("CardDisplayUIδ�ҵ�");
                isValid = false;
            }

            if (CardSystemManager.Instance == null)
            {
                LogError("CardSystemManagerδ�ҵ�");
                isValid = false;
            }

            return isValid;
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// �����¼�
        /// </summary>
        private void SubscribeToEvents()
        {
            // ���Ļغϱ仯�¼�
            NetworkManager.OnPlayerTurnChanged += OnPlayerTurnChanged;

            // ���Ŀ�����ʾUI�¼�
            if (cardDisplayUI != null)
            {
                cardDisplayUI.OnCardSelected += OnCardSelected;
                cardDisplayUI.OnCardHoverEnter += OnCardHoverEnter;
                cardDisplayUI.OnCardHoverExit += OnCardHoverExit;
            }

            LogDebug("�¼��������");
        }

        /// <summary>
        /// ȡ�������¼�
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // ȡ�����Ļغϱ仯�¼�
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerTurnChanged -= OnPlayerTurnChanged;
            }

            // ȡ�����Ŀ�����ʾUI�¼�
            if (cardDisplayUI != null)
            {
                cardDisplayUI.OnCardSelected -= OnCardSelected;
                cardDisplayUI.OnCardHoverEnter -= OnCardHoverEnter;
                cardDisplayUI.OnCardHoverExit -= OnCardHoverExit;
            }

            LogDebug("ȡ���¼�����");
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// ����غϱ仯�¼�
        /// </summary>
        private void OnPlayerTurnChanged(ushort newTurnPlayerId)
        {
            // ����Ƿ����ҵĻغ�
            bool wasMyTurn = isMyTurn;
            isMyTurn = (NetworkManager.Instance != null && NetworkManager.Instance.ClientId == newTurnPlayerId);

            LogDebug($"�غϱ仯 - ��ǰ���: {newTurnPlayerId}, ���ҵĻغ�: {isMyTurn}");

            // ����ֵ��ҵĻغϣ��Զ��رտ���UI
            if (isMyTurn && IsCardUIVisible)
            {
                LogDebug("�ֵ��ҵĻغϣ��Զ��رտ���UI");
                HideCardUI();
            }

            // ����UI״̬
            UpdateUIAvailability();
        }

        /// <summary>
        /// ������ѡ���¼�
        /// </summary>
        private void OnCardSelected(GameObject cardUI)
        {
            if (currentUIState != UIState.FanDisplay) return;

            // ��ʼ��ק
            StartDragging(cardUI);
        }

        /// <summary>
        /// ��������ͣ����
        /// </summary>
        private void OnCardHoverEnter(GameObject cardUI)
        {
            // TODO: ���������ͣ��ʾ��Ϣ
            LogDebug($"������ͣ����: {cardUI.name}");
        }

        /// <summary>
        /// ��������ͣ�뿪
        /// </summary>
        private void OnCardHoverExit(GameObject cardUI)
        {
            LogDebug($"������ͣ�뿪: {cardUI.name}");
        }

        #endregion

        #region ���봦��

        /// <summary>
        /// ��������
        /// </summary>
        private void HandleInput()
        {
            if (!isInitialized) return;

            // E���л�����UI
            if (Input.GetKeyDown(cardDisplayTriggerKey))
            {
                HandleCardDisplayToggle();
            }

            // ESC���رտ���UI
            if (Input.GetKeyDown(KeyCode.Escape) && IsCardUIVisible)
            {
                HideCardUI();
            }
        }

        /// <summary>
        /// ������չʾ�л�
        /// </summary>
        private void HandleCardDisplayToggle()
        {
            switch (currentUIState)
            {
                case UIState.Hidden:
                    if (CanOpenCardUI)
                    {
                        ShowCardUI();
                    }
                    else
                    {
                        LogDebug($"�޷��򿪿���UI - �ҵĻغ�: {isMyTurn}, ����״̬: {canUseCards}");
                    }
                    break;

                case UIState.Thumbnail:
                    ShowFanDisplay();
                    break;

                case UIState.FanDisplay:
                    HideCardUI();
                    break;

                case UIState.Disabled:
                    LogDebug("����UI��ǰ������");
                    break;
            }
        }

        #endregion

        #region ��ק����

        /// <summary>
        /// ��ʼ��ק
        /// </summary>
        private void StartDragging(GameObject cardUI)
        {
            if (cardUI == null) return;

            draggedCard = cardUI;
            isDragging = true;
            dragStartPosition = cardUI.transform.position;

            // �������ƫ��
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            dragOffset = cardUI.transform.position - mouseWorldPos;

            // �л�����ק״̬
            SetUIState(UIState.Dragging);

            // TODO: ������ק��ͷ

            LogDebug($"��ʼ��ק����: {cardUI.name}");
        }

        /// <summary>
        /// ������ק
        /// </summary>
        private void HandleDragging()
        {
            if (!isDragging || draggedCard == null) return;

            // ���̧��ʱ������ק
            if (Input.GetMouseButtonUp(0))
            {
                EndDragging();
                return;
            }

            // TODO: ������ק��ͷλ��
            // ������ʱ���ƶ����Ʊ���ֻ���Ƽ�ͷ
            UpdateDragArrow();
        }

        /// <summary>
        /// ������ק��ͷ (TODO)
        /// </summary>
        private void UpdateDragArrow()
        {
            // TODO: ʵ�ּ�ͷ�����߼�
            // ��ͷ�ӿ���λ����������ʽָ�����λ��
        }

        /// <summary>
        /// ������ק
        /// </summary>
        private void EndDragging()
        {
            if (!isDragging || draggedCard == null) return;

            // ����ͷ�λ��
            bool isInReleaseArea = IsMouseInReleaseArea();

            if (isInReleaseArea)
            {
                // ���ͷ������ڣ�����ʹ�ÿ���
                TryUseCard(draggedCard);
            }
            else
            {
                LogDebug("��ק�ͷ�λ����Ч��ȡ��ʹ�ÿ���");
            }

            // ������ק״̬
            CleanupDragging();
        }

        /// <summary>
        /// ������ק״̬
        /// </summary>
        private void CleanupDragging()
        {
            isDragging = false;
            draggedCard = null;
            dragOffset = Vector3.zero;

            // TODO: ������ק��ͷ

            // ���ص�����չʾ״̬
            SetUIState(UIState.FanDisplay);

            LogDebug("��ק״̬������");
        }

        /// <summary>
        /// �������Ƿ����ͷ�������
        /// </summary>
        private bool IsMouseInReleaseArea()
        {
            Vector2 mouseScreenPos = Input.mousePosition;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);

            // ת��Ϊ��Ļ�ٷֱ�
            Vector2 mousePercent = new Vector2(mouseScreenPos.x / screenSize.x, mouseScreenPos.y / screenSize.y);

            // �����ͷ�����߽�
            float leftBound = releaseAreaCenterX - releaseAreaWidth / 2f;
            float rightBound = releaseAreaCenterX + releaseAreaWidth / 2f;
            float bottomBound = releaseAreaCenterY - releaseAreaHeight / 2f;
            float topBound = releaseAreaCenterY + releaseAreaHeight / 2f;

            // ����Ƿ���������
            bool inArea = mousePercent.x >= leftBound && mousePercent.x <= rightBound &&
                         mousePercent.y >= bottomBound && mousePercent.y <= topBound;

            LogDebug($"���λ�ü�� - �ٷֱ�: {mousePercent}, ���ͷ�������: {inArea}");
            return inArea;
        }

        #endregion

        #region ����ʹ��

        /// <summary>
        /// ����ʹ�ÿ���
        /// </summary>
        private void TryUseCard(GameObject cardUI)
        {
            if (cardUI == null) return;

            // ��ȡ����ID
            CardUIIdentifier identifier = cardUI.GetComponent<CardUIIdentifier>();
            if (identifier == null)
            {
                LogError("����UIȱ�ٱ�ʶ������޷�ʹ��");
                return;
            }

            int cardId = identifier.cardId;
            LogDebug($"����ʹ�ÿ���: {identifier.cardName} (ID: {cardId})");

            // ��֤����ʹ������
            if (!ValidateCardUsage(cardId))
            {
                LogWarning($"���� {identifier.cardName} ʹ������������");
                return;
            }

            // TODO: �����ָ���Ϳ��ƣ���Ҫѡ��Ŀ��
            // ĿǰĬ��ʹ�� -1 ��ΪĿ�꣨�Է��Ϳ��ƻ���Ŀ�꣩
            int targetPlayerId = -1;

            // ��������ʹ������
            OnCardUseRequested?.Invoke(cardId, targetPlayerId);

            LogDebug($"����ʹ�������ѷ���: {identifier.cardName}");

            // �رտ���UI
            HideCardUI();
        }

        /// <summary>
        /// ��֤����ʹ������
        /// </summary>
        private bool ValidateCardUsage(int cardId)
        {
            // ʹ��CardUtilities�е���֤�߼�
            if (CardSystemManager.Instance?.cardConfig == null)
            {
                LogError("CardSystemManager�����ò�����");
                return false;
            }

            // ���ҿ�������
            var cardData = CardSystemManager.Instance.cardConfig.AllCards.Find(c => c.cardId == cardId);
            if (cardData == null)
            {
                LogError($"δ�ҵ���������: {cardId}");
                return false;
            }

            // ��ȡ���״̬������򻯴���ʵ��Ӧ�ô�PlayerCardManager��ȡ��
            // TODO: ������ʵ�����״̬��֤
            bool isMyTurnForValidation = false; // �ǻغ�ʱʹ�ÿ���

            // ʹ��CardUtilities��֤
            return Cards.Core.CardUtilities.Validator.ValidateGameState(true) &&
                   !isMyTurnForValidation; // �򻯵Ļغ���֤
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ��ʾ����UI�����ⲿ���ã�
        /// </summary>
        public void ShowCardUI()
        {
            if (!CanOpenCardUI)
            {
                LogWarning("��ǰ�޷��򿪿���UI");
                return;
            }

            LogDebug("��ʾ����UI");

            // ˢ����������
            RefreshHandCards();

            // ��ʾ����ͼ�������������ݣ�
            ShowThumbnail();

            // �����¼�
            OnCardUIOpened?.Invoke();
        }

        /// <summary>
        /// ���ؿ���UI
        /// </summary>
        public void HideCardUI()
        {
            LogDebug("���ؿ���UI");

            // ������ק״̬
            if (isDragging)
            {
                CleanupDragging();
            }

            // ����չʾUI
            if (cardDisplayUI != null)
            {
                cardDisplayUI.HideCardDisplay();
            }

            // ����״̬
            SetUIState(UIState.Hidden);

            // �����¼�
            OnCardUIClosed?.Invoke();
        }

        /// <summary>
        /// ��ʾ����չʾ
        /// </summary>
        public void ShowFanDisplay()
        {
            if (currentUIState != UIState.Thumbnail)
            {
                LogWarning("��ǰ״̬����Thumbnail���޷���ʾ����չʾ");
                return;
            }

            LogDebug("��ʾ����չʾ");

            // ˢ����������
            RefreshHandCards();

            // ��ʾ����չʾ
            if (cardDisplayUI != null && cardDisplayUI.ShowFanDisplayWithCards(currentHandCards))
            {
                SetUIState(UIState.FanDisplay);
            }
        }

        /// <summary>
        /// ǿ�ƽ��ÿ���UI�������ڼ���ã�
        /// </summary>
        public void DisableCardUI()
        {
            LogDebug("ǿ�ƽ��ÿ���UI");

            // �����ǰ����ʾ��������
            if (IsCardUIVisible)
            {
                HideCardUI();
            }

            SetUIState(UIState.Disabled);
        }

        /// <summary>
        /// ���ÿ���UI
        /// </summary>
        public void EnableCardUI()
        {
            LogDebug("���ÿ���UI");

            if (currentUIState == UIState.Disabled)
            {
                SetUIState(UIState.Hidden);
                UpdateUIAvailability();
            }
        }

        #endregion

        #region ���ݹ���

        /// <summary>
        /// ˢ����������
        /// </summary>
        private void RefreshHandCards()
        {
            currentHandCards.Clear();

            // TODO: ��PlayerCardManager��ȡ��ʵ����������
            // ������ʹ��ģ������
            if (CardSystemManager.Instance?.cardConfig != null)
            {
                // ģ���ȡǰ5�ſ�����Ϊ���ƣ�ʵ�������Ӧ�ô�������ƻ�ȡ��
                var allCards = CardSystemManager.Instance.cardConfig.AllCards;
                for (int i = 0; i < Mathf.Min(5, allCards.Count); i++)
                {
                    currentHandCards.Add(new CardDisplayData(allCards[i]));
                }
            }

            LogDebug($"ˢ������������ɣ���ǰ��������: {currentHandCards.Count}");
        }

        #endregion

        #region ״̬����

        /// <summary>
        /// ����UI״̬
        /// </summary>
        private void SetUIState(UIState newState)
        {
            if (currentUIState == newState) return;

            UIState oldState = currentUIState;
            currentUIState = newState;

            LogDebug($"UI״̬�л�: {oldState} -> {newState}");
        }

        /// <summary>
        /// ��ʾ����ͼ
        /// </summary>
        private void ShowThumbnail()
        {
            if (cardDisplayUI != null && currentHandCards.Count > 0)
            {
                cardDisplayUI.ShowCardDisplay(currentHandCards);
                SetUIState(UIState.Thumbnail);
            }
            else
            {
                LogWarning("�޷���ʾ����ͼ��cardDisplayUIΪ�ջ���������Ϊ��");
            }
        }

        /// <summary>
        /// ����UI������
        /// </summary>
        private void UpdateUIAvailability()
        {
            canUseCards = !isMyTurn; // �ǻغ�ʱ����ʹ�ÿ���

            LogDebug($"UI�����Ը��� - �ҵĻغ�: {isMyTurn}, ���ÿ���: {canUseCards}");
        }

        #endregion

        #region ���Թ���

        /// <summary>
        /// ��ȡϵͳ״̬��Ϣ
        /// </summary>
        public string GetSystemStatus()
        {
            return $"CardUIManager״̬:\n" +
                   $"- ��ʼ��: {isInitialized}\n" +
                   $"- UI״̬: {currentUIState}\n" +
                   $"- �ҵĻغ�: {isMyTurn}\n" +
                   $"- ���ÿ���: {canUseCards}\n" +
                   $"- ��������: {currentHandCards.Count}\n" +
                   $"- ��ק��: {isDragging}";
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardUIManager] {message}");
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardUIManager] {message}");
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[CardUIManager] {message}");
        }

        #endregion

        #region Unity�༭������

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showReleaseArea) return;

            // �����ͷ�����
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize.x > 0 && screenSize.y > 0)
            {
                Vector2 areaSize = new Vector2(
                    screenSize.x * releaseAreaWidth,
                    screenSize.y * releaseAreaHeight
                );

                Vector2 areaCenter = new Vector2(
                    screenSize.x * releaseAreaCenterX,
                    screenSize.y * releaseAreaCenterY
                );

                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);

                // ת����Ļ���굽��������
                if (Camera.main != null)
                {
                    Vector3 worldCenter = Camera.main.ScreenToWorldPoint(new Vector3(areaCenter.x, areaCenter.y, 10f));
                    Vector3 worldSize = new Vector3(areaSize.x / 100f, areaSize.y / 100f, 1f);

                    Gizmos.DrawCube(worldCenter, worldSize);
                }
            }
        }
#endif

        #endregion

        #region ������Դ

        private void OnDestroy()
        {
            // ȡ���¼�����
            UnsubscribeFromEvents();

            // ������ק״̬
            if (isDragging)
            {
                CleanupDragging();
            }

            LogDebug("CardUIManager�����٣���Դ������");
        }

        #endregion
    }
}