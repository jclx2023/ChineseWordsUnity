using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Classroom.Player;

namespace Cards.UI
{
    /// <summary>
    /// ����չʾUI������ - �ֶ��������ð�
    /// ��������Ƶ��Ӿ�չʾ״̬������ͼ <-> ����չʾ
    /// ֧�ְ�ѹ�¼��������¼�
    /// </summary>
    public class CardDisplayUI : MonoBehaviour
    {
        [Header("�������� (�Զ�����)")]
        [SerializeField] private Transform thumbnailContainer; // ����ͼ���� (�Զ�����)
        [SerializeField] private Transform fanDisplayContainer; // ����չʾ���� (�Զ�����)

        [Header("����ͼ����")]
        [SerializeField] private Vector2 thumbnailPosition = new Vector2(110f, 0f); // ���������ʵ�λ��
        [SerializeField] private float thumbnailRotation = -10f; // ����ͼ������ת�Ƕ�
        [SerializeField] private float thumbnailScale = 0.6f; // ����ͼ���ţ���0.4����0.6��
        [SerializeField] private float thumbnailFanRadius = 100f; // ����ͼ���ΰ뾶����80����100��
        [SerializeField] private float thumbnailFanAngle = 40f; // ����ͼ���νǶȣ���30����40��
        [SerializeField] private int maxThumbnailCards = 3; // �����ʾ3������ͼ

        [Header("����չʾ����")]
        [SerializeField] private float fanRadius = 400f; // ���ΰ뾶�������ÿ��Ƹ�ƽ��
        [SerializeField] private float fanAngleSpread = 40f; // ���νǶȷ�Χ����С�ÿ��Ƹ����ܣ�
        [SerializeField] private float fanCenterOffsetY = 200f; // �������ĳ�����Ļ�ײ��ľ���
        [SerializeField] private float cardOverlapRatio = 0.85f; // ����¶����Ļ�ı�����0.7��ʾ¶��70%��

        [Header("��ͣ��������")]
        [SerializeField] private float hoverScale = 1.2f; // ��ͣʱ�ķŴ���
        [SerializeField] private float hoverAnimationDuration = 0.2f; // ��ͣ����ʱ��
        [SerializeField] private AnimationCurve hoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // ��ͣ��������

        [Header("�л���������")]
        [SerializeField] private float transitionDuration = 0.5f; // ״̬�л�����ʱ��
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("��ѹ�������")]
        [SerializeField] private float pressDetectionThreshold = 0.1f; // ��ѹ�����ֵ���룩

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // չʾ״̬ö��
        public enum DisplayState
        {
            Thumbnail,     // ����ͼ״̬��Ĭ��״̬��
            FanDisplay,    // ����չʾ״̬
            Transitioning, // ת����
            Disabled       // ����״̬�������ڼ䣩
        }

        // ��ǰ״̬
        private DisplayState currentState = DisplayState.Thumbnail;
        private List<GameObject> currentCardUIs = new List<GameObject>();
        private List<GameObject> thumbnailCardUIs = new List<GameObject>(); // ����ͼ����UI�б�
        private List<Vector3> fanPositions = new List<Vector3>();
        private List<Vector3> fanRotations = new List<Vector3>();
        private List<Vector3> thumbnailPositions = new List<Vector3>(); // ����ͼλ��
        private List<Vector3> thumbnailRotations = new List<Vector3>(); // ����ͼ��ת
        private GameObject hoveredCard = null;
        private Coroutine transitionCoroutine = null;
        private Coroutine hoverCoroutine = null;

        // ��ѹ������
        private GameObject pressedCard = null;
        private Vector2 pressStartPosition = Vector2.zero;
        private float pressStartTime = 0f;
        private bool isPressing = false;

        // ��̬������������ĵ�
        private Vector2 dynamicFanCenter;

        // ��ʼ��״̬
        private bool isInitialized = false;

        // �������� - ͨ��CardUIManager����
        private PlayerCameraController playerCameraController;
        private CardUIComponents cardUIComponents;

        // �¼�
        public System.Action<GameObject> OnCardHoverEnter;
        public System.Action<GameObject> OnCardHoverExit;
        public System.Action<GameObject, Vector2> OnCardPressStart;
        public System.Action<GameObject> OnCardPressEnd;

        // ����
        public DisplayState CurrentState => currentState;
        public bool IsInFanDisplay => currentState == DisplayState.FanDisplay;
        public bool IsEnabled => currentState != DisplayState.Disabled;
        public bool CanShowFanDisplay => currentState == DisplayState.Thumbnail && IsEnabled && isInitialized;

        #region ��ʼ��

        /// <summary>
        /// ��CardUIManager���õĳ�ʼ������
        /// </summary>
        public void Initialize(CardUIComponents uiComponents, PlayerCameraController cameraController = null)
        {
            LogDebug("��ʼ��ʼ��CardDisplayUI");

            // ������������
            cardUIComponents = uiComponents;
            playerCameraController = cameraController;

            // ��֤��������
            if (!ValidateContainers())
            {
                LogError("����������Ч���޷���ʼ��");
                return;
            }

            // ��ʼ��UI���
            InitializeUIComponents();

            // ���ó�ʼ״̬Ϊ����ͼ
            SetState(DisplayState.Thumbnail);

            isInitialized = true;
            LogDebug("CardDisplayUI��ʼ�����");
        }

        /// <summary>
        /// ��֤��������
        /// </summary>
        private bool ValidateContainers()
        {
            // �Զ���������
            FindContainers();

            if (thumbnailContainer == null)
            {
                LogError("�Ҳ���ThumbnailContainer����ȷ��CardCanvas�´��ڸ�����");
                return false;
            }

            if (fanDisplayContainer == null)
            {
                LogError("�Ҳ���FanDisplayContainer����ȷ��CardCanvas�´��ڸ�����");
                return false;
            }

            LogDebug("����������֤ͨ��");
            return true;
        }

        /// <summary>
        /// ��������
        /// </summary>
        private void FindContainers()
        {
            // ����CardCanvas
            Canvas cardCanvas = GetComponentInParent<Canvas>();
            if (cardCanvas == null)
            {
                LogError("�Ҳ�����Canvas");
                return;
            }

            // ��CardCanvas�²�������
            thumbnailContainer = cardCanvas.transform.Find("ThumbnailContainer");
            fanDisplayContainer = cardCanvas.transform.Find("FanDisplayContainer");

            if (thumbnailContainer != null)
                LogDebug($"�ҵ�����ͼ����: {thumbnailContainer.name}");

            if (fanDisplayContainer != null)
                LogDebug($"�ҵ�����չʾ����: {fanDisplayContainer.name}");
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUIComponents()
        {
            // ��֤�����㼶
            ValidateContainerHierarchy();

            LogDebug("UI�����ʼ�����");
        }

        /// <summary>
        /// ��֤�����㼶�Ƿ���ȷ
        /// </summary>
        private void ValidateContainerHierarchy()
        {
            Transform parent = transform.parent;
            Canvas parentCanvas = null;

            // ���ϲ���Canvas
            Transform current = parent;
            while (current != null && parentCanvas == null)
            {
                parentCanvas = current.GetComponent<Canvas>();
                current = current.parent;
            }

            if (parentCanvas != null)
            {
                LogDebug($"CardDisplayUI�㼶��֤ͨ����Canvas: {parentCanvas.name}");
            }
            else
            {
                LogWarning("CardDisplayUIδ�ҵ���Canvas�����ܻᵼ��UI��ʾ����");
            }
        }

        #endregion

        #region Unity��������

        private void Update()
        {
            // ����ѹ���
            HandlePressDetection();
        }

        #endregion

        #region ��ѹ���ϵͳ

        /// <summary>
        /// ����ѹ���
        /// </summary>
        private void HandlePressDetection()
        {
            if (currentState != DisplayState.FanDisplay) return;

            // �����갴��
            if (Input.GetMouseButtonDown(0))
            {
                HandleMouseDown();
            }
            // ������̧��
            else if (Input.GetMouseButtonUp(0))
            {
                HandleMouseUp();
            }
        }

        /// <summary>
        /// ������갴��
        /// </summary>
        private void HandleMouseDown()
        {
            // ��ȡ���λ��
            Vector2 mousePosition = Input.mousePosition;

            // ����Ƿ����ڿ�����
            GameObject clickedCard = GetCardUnderMouse(mousePosition);
            if (clickedCard != null)
            {
                StartCardPress(clickedCard, mousePosition);
            }
        }

        /// <summary>
        /// �������̧��
        /// </summary>
        private void HandleMouseUp()
        {
            if (isPressing && pressedCard != null)
            {
                EndCardPress();
            }
        }

        /// <summary>
        /// ��ʼ���ư�ѹ
        /// </summary>
        private void StartCardPress(GameObject card, Vector2 mousePosition)
        {
            pressedCard = card;
            pressStartPosition = mousePosition;
            pressStartTime = Time.time;
            isPressing = true;

            LogDebug($"��ʼ��ѹ����: {card.name}");

            // ת�������Ļ����ΪCanvas����
            Vector2 canvasPosition;
            RectTransform canvasRect = GetCanvasRectTransform();
            if (canvasRect != null)
            {
                Camera uiCamera = GetUICamera();
                bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, mousePosition, uiCamera, out canvasPosition);

                if (success)
                {
                    // ������ѹ��ʼ�¼�
                    OnCardPressStart?.Invoke(card, canvasPosition);
                }
                else
                {
                    LogWarning("�޷�ת��������굽Canvas����");
                    OnCardPressStart?.Invoke(card, mousePosition);
                }
            }
            else
            {
                // ʹ��ԭʼ��Ļ����
                OnCardPressStart?.Invoke(card, mousePosition);
            }
        }

        /// <summary>
        /// �������ư�ѹ
        /// </summary>
        private void EndCardPress()
        {
            if (pressedCard != null)
            {
                LogDebug($"������ѹ����: {pressedCard.name}");

                // ������ѹ�����¼�
                OnCardPressEnd?.Invoke(pressedCard);
            }

            // ����ѹ״̬
            pressedCard = null;
            pressStartPosition = Vector2.zero;
            pressStartTime = 0f;
            isPressing = false;
        }

        /// <summary>
        /// ��ȡ����µĿ���
        /// </summary>
        private GameObject GetCardUnderMouse(Vector2 mousePosition)
        {
            // ʹ��EventSystem����Raycast���
            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = mousePosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            // ���ҵ�һ��ƥ��Ŀ���
            foreach (var result in results)
            {
                GameObject obj = result.gameObject;

                // ����Ƿ��ǵ�ǰ�Ŀ���UI
                if (currentCardUIs.Contains(obj))
                {
                    return obj;
                }

                // ��鸸�����Ƿ��ǿ���UI
                Transform parent = obj.transform.parent;
                while (parent != null)
                {
                    if (currentCardUIs.Contains(parent.gameObject))
                    {
                        return parent.gameObject;
                    }
                    parent = parent.parent;
                }
            }

            return null;
        }

        /// <summary>
        /// ��ȡUI�����
        /// </summary>
        private Camera GetUICamera()
        {
            Canvas canvas = GetCanvasRectTransform()?.GetComponent<Canvas>();
            return canvas?.worldCamera;
        }

        #endregion

        #region ״̬����

        /// <summary>
        /// ����չʾ״̬
        /// </summary>
        private void SetState(DisplayState newState)
        {
            if (currentState == newState) return;

            DisplayState oldState = currentState;
            currentState = newState;

            LogDebug($"״̬�л�: {oldState} -> {newState}");

            // ״̬�л�����
            OnStateChanged(oldState, newState);
        }

        /// <summary>
        /// ״̬�л�����
        /// </summary>
        private void OnStateChanged(DisplayState oldState, DisplayState newState)
        {
            switch (newState)
            {
                case DisplayState.Thumbnail:
                    // ��ʾ����ͼ����������
                    ShowThumbnailMode();
                    break;

                case DisplayState.FanDisplay:
                    // ��ʾ���Σ���������ͼ
                    ShowFanDisplayMode();
                    break;

                case DisplayState.Disabled:
                    // �������н����������ֵ�ǰ��ʾ״̬
                    DisableAllInteractions();
                    break;

                case DisplayState.Transitioning:
                    // ת��״̬������Ҫ���⴦��
                    break;
            }
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ˢ������ͼ��ʾ
        /// </summary>
        public void RefreshThumbnailDisplay(List<CardDisplayData> cardDataList)
        {
            if (!isInitialized)
            {
                LogWarning("CardDisplayUIδ��ʼ�����޷�ˢ������ͼ");
                return;
            }

            if (cardDataList == null || cardDataList.Count == 0)
            {
                LogDebug("��������Ϊ�գ���������ͼ��ʾ");
                ClearThumbnailCards();
                return;
            }

            LogDebug($"ˢ������ͼ��ʾ����������: {cardDataList.Count}");

            // ���´�������ͼ����
            CreateThumbnailCards(cardDataList);

            // �����ǰ������ͼ״̬��ȷ����ʾ
            if (currentState == DisplayState.Thumbnail)
            {
                ShowThumbnailMode();
            }
        }

        /// <summary>
        /// ��ʾ����չʾ
        /// </summary>
        public bool ShowFanDisplayWithCards(List<CardDisplayData> cardDataList)
        {
            if (!CanShowFanDisplay)
            {
                LogWarning($"�޷���ʾ����չʾ - ��ǰ״̬: {currentState}, �ѳ�ʼ��: {isInitialized}");
                return false;
            }

            if (cardDataList == null || cardDataList.Count == 0)
            {
                LogWarning("���������б�Ϊ�գ��޷���ʾ����չʾ");
                return false;
            }

            LogDebug($"��ʼ��ʾ����չʾ����������: {cardDataList.Count}");

            // ���㶯̬�������ģ�����Ӧ��Ļ�ײ���
            CalculateDynamicFanCenter();

            // ��������UI
            CreateCardUIs(cardDataList);

            // ��������λ��
            CalculateFanPositions(cardDataList.Count);

            // �л�״̬
            SetState(DisplayState.Transitioning);

            // ��ʼת������
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            transitionCoroutine = StartCoroutine(TransitionToFanDisplay());

            // �������������
            SetCameraControl(false);

            return true;
        }

        /// <summary>
        /// ��������չʾ������ʹ�ú���ã�
        /// </summary>
        public bool UpdateFanDisplayWithCards(List<CardDisplayData> cardDataList)
        {
            if (currentState != DisplayState.FanDisplay)
            {
                LogWarning("��ǰ��������չʾ״̬���޷���������չʾ");
                return false;
            }

            if (cardDataList == null || cardDataList.Count == 0)
            {
                LogDebug("��������Ϊ�գ���������ͼ״̬");
                ReturnToThumbnail();
                return true;
            }

            LogDebug($"��������չʾ���µĿ�������: {cardDataList.Count}");

            // ���¼��㶯̬��������
            CalculateDynamicFanCenter();

            // ����ǰ���ο���
            ClearFanDisplayCards();

            // �����µĿ���UI
            CreateCardUIs(cardDataList);

            // ���¼�������λ��
            CalculateFanPositions(cardDataList.Count);

            // ֱ�����õ�����λ�ã��޶�������Ϊ�Ǹ��£�
            ShowFanDisplayMode();

            // ͬʱ��������ͼ��Ϊ������׼����
            CreateThumbnailCards(cardDataList);

            LogDebug("����չʾ�������");
            return true;
        }

        /// <summary>
        /// ��������ͼ״̬
        /// </summary>
        public void ReturnToThumbnail()
        {
            if (currentState != DisplayState.FanDisplay)
            {
                LogWarning("��ǰ��������չʾ״̬���޷���������ͼ");
                return;
            }

            LogDebug("������չʾ��������ͼ");

            SetState(DisplayState.Transitioning);

            // ��ʼ���ض���
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            transitionCoroutine = StartCoroutine(TransitionToThumbnail());

            // �ָ����������
            SetCameraControl(true);
        }

        /// <summary>
        /// ��������/����״̬
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (currentState == DisplayState.Disabled)
                {
                    SetState(DisplayState.Thumbnail);
                    LogDebug("����CardDisplayUI");
                }
            }
            else
            {
                if (currentState != DisplayState.Disabled)
                {
                    // ����ѹ״̬
                    if (isPressing)
                    {
                        EndCardPress();
                    }

                    SetState(DisplayState.Disabled);
                    LogDebug("����CardDisplayUI");
                }
            }
        }

        /// <summary>
        /// ǿ���л�������ͼ״̬����CardUIManager���ã�
        /// </summary>
        public void ForceToThumbnailState()
        {
            LogDebug("ǿ���л�������ͼ״̬");

            // ֹͣ���ж���
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            if (hoverCoroutine != null)
            {
                StopCoroutine(hoverCoroutine);
                hoverCoroutine = null;
            }

            // ����ѹ״̬
            if (isPressing)
            {
                EndCardPress();
            }

            // �������ο���
            ClearFanDisplayCards();

            // �ָ����������
            SetCameraControl(true);

            // �л�������ͼ״̬
            SetState(DisplayState.Thumbnail);
        }

        /// <summary>
        /// ���ؿ�����ʾ
        /// </summary>
        public void HideCardDisplay()
        {
            LogDebug("���ؿ�����ʾ��ǿ�ƻص�����ͼ״̬��");
            ForceToThumbnailState();
        }

        /// <summary>
        /// ��ʾ������ʾ
        /// </summary>
        public void ShowCardDisplay(List<CardDisplayData> cardDataList)
        {
            LogDebug("��ʾ������ʾ��ˢ������ͼ��");
            RefreshThumbnailDisplay(cardDataList);
        }

        /// <summary>
        /// ��ȫ������ʾ���������ʱ���ã�
        /// </summary>
        public void ClearAllDisplays()
        {
            LogDebug("����������ʾ");

            // ֹͣ����Э��
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            if (hoverCoroutine != null)
            {
                StopCoroutine(hoverCoroutine);
            }

            // ����ѹ״̬
            if (isPressing)
            {
                EndCardPress();
            }

            // ��������UI
            ClearThumbnailCards();
            ClearFanDisplayCards();

            // �ָ����������
            SetCameraControl(true);
        }

        #endregion

        #region ��̬λ�ü���

        /// <summary>
        /// ���㶯̬�������ĵ�
        /// </summary>
        private void CalculateDynamicFanCenter()
        {
            // ��ȡCanvas��RectTransform
            RectTransform canvasRect = GetCanvasRectTransform();
            if (canvasRect == null)
            {
                LogWarning("�޷���ȡCanvas RectTransform��ʹ��Ĭ��λ��");
                dynamicFanCenter = new Vector2(0f, -400f);
                return;
            }

            // ��ȡ��Ļ�ߴ磨��Canvas����ϵ�У�
            Rect canvasSize = canvasRect.rect;
            float screenHeight = canvasSize.height;

            // ������������λ��
            // X�᣺��Ļ����
            float centerX = 0f;

            // Y�᣺��Ļ�ײ� + ����ƫ�ƣ���������������Ļ��
            // ���Ƹ߶�279��ϣ��¶��70%��������Ҫ�������ڵײ��·�һ������
            float cardHeight = 279f; // ����ʵ�ʸ߶�
            float visibleCardHeight = cardHeight * cardOverlapRatio; // ���ƿɼ�����
            float hiddenCardHeight = cardHeight - visibleCardHeight; // ���Ʊ��ڵ�����

            // ��������Y���� = ��Ļ�ײ� - ����ƫ�ƾ���
            float centerY = -screenHeight / 2f - fanCenterOffsetY;

            dynamicFanCenter = new Vector2(centerX, centerY);

            LogDebug($"��Ļ�߶�: {screenHeight}, ���ƿɼ��߶�: {visibleCardHeight}");
        }

        /// <summary>
        /// ��ȡCanvas��RectTransform
        /// </summary>
        private RectTransform GetCanvasRectTransform()
        {
            // ���ϲ���Canvas
            Transform current = transform;
            while (current != null)
            {
                Canvas canvas = current.GetComponent<Canvas>();
                if (canvas != null)
                {
                    return canvas.GetComponent<RectTransform>();
                }
                current = current.parent;
            }
            return null;
        }

        #endregion

        #region ����ͼ��ʾ

        /// <summary>
        /// ��������ͼ����
        /// </summary>
        private void CreateThumbnailCards(List<CardDisplayData> cardDataList)
        {

            // ������������ͼ
            ClearThumbnailCards();

            // ��������ͼ����
            int thumbnailCount = Mathf.Min(cardDataList.Count, maxThumbnailCards);

            // ��������ͼλ��
            CalculateThumbnailPositions(thumbnailCount);

            // ��������ͼUI
            for (int i = 0; i < thumbnailCount; i++)
            {
                GameObject thumbnailCard = cardUIComponents.CreateCardUI(cardDataList[i], thumbnailContainer);
                if (thumbnailCard != null)
                {
                    // ��������ͼλ�ú���ת
                    RectTransform rectTransform = thumbnailCard.GetComponent<RectTransform>();
                    if (rectTransform != null && i < thumbnailPositions.Count)
                    {
                        rectTransform.anchoredPosition = thumbnailPositions[i];
                        rectTransform.localEulerAngles = thumbnailRotations[i];
                        rectTransform.localScale = Vector3.one; // �����Ѿ�������
                    }

                    thumbnailCardUIs.Add(thumbnailCard);
                }
            }

            LogDebug($"������ {thumbnailCardUIs.Count} ������ͼ����");
        }

        /// <summary>
        /// ��������ͼλ�ã�С�������У�
        /// </summary>
        private void CalculateThumbnailPositions(int cardCount)
        {
            thumbnailPositions.Clear();
            thumbnailRotations.Clear();

            if (cardCount == 0) return;

            // ����Ƕȼ��
            float angleStep = cardCount > 1 ? thumbnailFanAngle / (cardCount - 1) : 0f;
            float startAngle = -thumbnailFanAngle / 2f;

            for (int i = 0; i < cardCount; i++)
            {
                float angle = startAngle + (angleStep * i);
                float radians = angle * Mathf.Deg2Rad;

                // ����λ�ã����������ͼ������
                Vector3 position = new Vector3(
                    Mathf.Sin(radians) * thumbnailFanRadius,
                    Mathf.Cos(radians) * thumbnailFanRadius,
                    0f
                );

                // ������ת
                Vector3 rotation = new Vector3(0, 0, -angle);

                thumbnailPositions.Add(position);
                thumbnailRotations.Add(rotation);
            }

            LogDebug($"������ {cardCount} ������ͼ������λ��");
        }

        /// <summary>
        /// ��ʾ����ͼģʽ
        /// </summary>
        private void ShowThumbnailMode()
        {
            // ��ʾ����ͼ����
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(true);

                // ȷ��͸��������
                CanvasGroup thumbnailCanvasGroup = thumbnailContainer.GetComponent<CanvasGroup>();
                if (thumbnailCanvasGroup != null)
                {
                    thumbnailCanvasGroup.alpha = 1f;
                }
            }

            // ��������չʾ
            if (fanDisplayContainer != null)
            {
                fanDisplayContainer.gameObject.SetActive(false);
            }

            LogDebug("����ͼģʽ��ʾ���");
        }

        /// <summary>
        /// ��������ͼ����
        /// </summary>
        private void ClearThumbnailCards()
        {
            if (thumbnailCardUIs.Count > 0)
            {
                foreach (var thumbnailCard in thumbnailCardUIs)
                {
                    if (thumbnailCard != null && cardUIComponents != null)
                    {
                        cardUIComponents.DestroyCardUI(thumbnailCard);
                    }
                }
                thumbnailCardUIs.Clear();
            }

            thumbnailPositions.Clear();
            thumbnailRotations.Clear();
        }

        #endregion

        #region ����չʾ

        /// <summary>
        /// ��������UI
        /// </summary>
        private void CreateCardUIs(List<CardDisplayData> cardDataList)
        {

            // �������п���
            ClearFanDisplayCards();

            // �����¿���
            currentCardUIs = cardUIComponents.CreateMultipleCardUI(cardDataList, fanDisplayContainer);

            // Ϊÿ�ſ��������ͣ���
            foreach (var cardUI in currentCardUIs)
            {
                AddHoverDetection(cardUI);
            }

            LogDebug($"������ {currentCardUIs.Count} �����ο���UI");
        }

        /// <summary>
        /// ��������λ��
        /// </summary>
        private void CalculateFanPositions(int cardCount)
        {
            fanPositions.Clear();
            fanRotations.Clear();

            if (cardCount == 0) return;

            // ����Ƕȼ��
            float angleStep = cardCount > 1 ? fanAngleSpread / (cardCount - 1) : 0f;
            float startAngle = -fanAngleSpread / 2f;

            for (int i = 0; i < cardCount; i++)
            {
                float angle = startAngle + (angleStep * i);
                float radians = angle * Mathf.Deg2Rad;

                // ʹ�ö�̬�������������
                Vector3 position = new Vector3(
                    dynamicFanCenter.x + Mathf.Sin(radians) * fanRadius,
                    dynamicFanCenter.y + Mathf.Cos(radians) * fanRadius,
                    0f
                );

                // ������ת
                Vector3 rotation = new Vector3(0, 0, -angle);

                fanPositions.Add(position);
                fanRotations.Add(rotation);
            }

            LogDebug($"������ {cardCount} �ſ��Ƶ�����λ�ã����ĵ�: {dynamicFanCenter}");
        }

        /// <summary>
        /// ��ʾ����չʾģʽ
        /// </summary>
        private void ShowFanDisplayMode()
        {
            // ��������ͼ
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(false);
            }

            // ��ʾ���ο���
            if (fanDisplayContainer != null)
            {
                fanDisplayContainer.gameObject.SetActive(true);
            }

            // �������ο���λ��
            for (int i = 0; i < currentCardUIs.Count && i < fanPositions.Count; i++)
            {
                GameObject cardUI = currentCardUIs[i];
                RectTransform rectTransform = cardUI.GetComponent<RectTransform>();

                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = fanPositions[i];
                    rectTransform.localEulerAngles = fanRotations[i];
                    rectTransform.localScale = Vector3.one;
                }

                cardUI.SetActive(true);
            }

            LogDebug("����չʾģʽ��ʾ���");
        }

        /// <summary>
        /// ��������չʾ����
        /// </summary>
        private void ClearFanDisplayCards()
        {
            if (currentCardUIs.Count > 0 && cardUIComponents != null)
            {
                cardUIComponents.DestroyMultipleCardUI(currentCardUIs);
                currentCardUIs.Clear();
            }

            fanPositions.Clear();
            fanRotations.Clear();
            hoveredCard = null;
        }

        #endregion

        #region ��������

        /// <summary>
        /// ת��������չʾ�Ķ���
        /// </summary>
        private IEnumerator TransitionToFanDisplay()
        {
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;
                float curveValue = transitionCurve.Evaluate(t);

                // ����ÿ�ſ��ƴӶ�Ӧ����ͼλ�õ�����λ��
                for (int i = 0; i < currentCardUIs.Count && i < fanPositions.Count; i++)
                {
                    GameObject cardUI = currentCardUIs[i];
                    RectTransform rectTransform = cardUI.GetComponent<RectTransform>();

                    if (rectTransform != null)
                    {
                        // ������ʼλ�ã���Ӧ������ͼλ�ã�
                        Vector3 startPos;
                        Vector3 startRot;
                        Vector3 startScale;

                        if (i < thumbnailPositions.Count)
                        {
                            // �ж�Ӧ������ͼλ��
                            Vector3 thumbnailPos3D = new Vector3(thumbnailPosition.x, thumbnailPosition.y, 0f);
                            startPos = thumbnailPos3D + thumbnailPositions[i] * thumbnailScale;
                            startRot = thumbnailRotations[i];
                            startScale = Vector3.one * thumbnailScale;
                        }
                        else
                        {
                            // ��������ͼ�����Ŀ��ƴ����Ŀ�ʼ
                            startPos = new Vector3(thumbnailPosition.x, thumbnailPosition.y, 0f);
                            startRot = Vector3.zero;
                            startScale = Vector3.one * thumbnailScale;
                        }

                        // λ�ò�ֵ
                        Vector3 targetPos = fanPositions[i];
                        rectTransform.anchoredPosition = Vector3.Lerp(startPos, targetPos, curveValue);

                        // ��ת��ֵ
                        Vector3 targetRot = fanRotations[i];
                        rectTransform.localEulerAngles = Vector3.Lerp(startRot, targetRot, curveValue);

                        // ���Ų�ֵ
                        rectTransform.localScale = Vector3.Lerp(startScale, Vector3.one, curveValue);
                    }

                    cardUI.SetActive(true);
                }

                // ����ͼ��������
                if (thumbnailContainer != null && t > 0.3f)
                {
                    CanvasGroup thumbnailCanvasGroup = thumbnailContainer.GetComponent<CanvasGroup>();
                    if (thumbnailCanvasGroup == null)
                    {
                        thumbnailCanvasGroup = thumbnailContainer.gameObject.AddComponent<CanvasGroup>();
                    }
                    thumbnailCanvasGroup.alpha = 1f - ((t - 0.3f) / 0.7f);
                }

                yield return null;
            }

            // ��������ͼ
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(false);
            }

            SetState(DisplayState.FanDisplay);
            LogDebug("ת��������չʾ�������");
        }

        /// <summary>
        /// ת��������ͼ�Ķ���
        /// </summary>
        private IEnumerator TransitionToThumbnail()
        {
            float elapsed = 0f;

            // ��¼���ε�����λ��
            List<Vector3> startPositions = new List<Vector3>();
            List<Vector3> startRotations = new List<Vector3>();

            for (int i = 0; i < currentCardUIs.Count; i++)
            {
                RectTransform rectTransform = currentCardUIs[i].GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    startPositions.Add(rectTransform.anchoredPosition);
                    startRotations.Add(rectTransform.localEulerAngles);
                }
            }

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;
                float curveValue = transitionCurve.Evaluate(t);

                // ����ÿ�ſ��ƴ�����λ�ûص���Ӧ������ͼλ��
                for (int i = 0; i < currentCardUIs.Count && i < startPositions.Count; i++)
                {
                    GameObject cardUI = currentCardUIs[i];
                    RectTransform rectTransform = cardUI.GetComponent<RectTransform>();

                    if (rectTransform != null)
                    {
                        // ����Ŀ��λ�ã���Ӧ������ͼλ�ã�
                        Vector3 targetPos;
                        Vector3 targetRot;
                        Vector3 targetScale;

                        if (i < thumbnailPositions.Count)
                        {
                            // �ж�Ӧ������ͼλ��
                            Vector3 thumbnailPos3D = new Vector3(thumbnailPosition.x, thumbnailPosition.y, 0f);
                            targetPos = thumbnailPos3D + thumbnailPositions[i] * thumbnailScale;
                            targetRot = thumbnailRotations[i];
                            targetScale = Vector3.one * thumbnailScale;
                        }
                        else
                        {
                            // ��������ͼ�����Ŀ��ƻص�����
                            targetPos = new Vector3(thumbnailPosition.x, thumbnailPosition.y, 0f);
                            targetRot = Vector3.zero;
                            targetScale = Vector3.one * thumbnailScale;
                        }

                        // λ�ò�ֵ
                        rectTransform.anchoredPosition = Vector3.Lerp(startPositions[i], targetPos, curveValue);

                        // ��ת��ֵ
                        rectTransform.localEulerAngles = Vector3.Lerp(startRotations[i], targetRot, curveValue);

                        // ���Ų�ֵ
                        rectTransform.localScale = Vector3.Lerp(Vector3.one, targetScale, curveValue);
                    }
                }

                yield return null;
            }

            // �������ο���UI
            ClearFanDisplayCards();

            // ������ʾ����ͼ
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(true);

                // �ָ�����ͼ͸����
                CanvasGroup thumbnailCanvasGroup = thumbnailContainer.GetComponent<CanvasGroup>();
                if (thumbnailCanvasGroup != null)
                {
                    thumbnailCanvasGroup.alpha = 1f;
                }
            }

            SetState(DisplayState.Thumbnail);
            LogDebug("ת��������ͼ�������");
        }

        #endregion

        #region ��ͣ����

        /// <summary>
        /// Ϊ���������ͣ���
        /// </summary>
        private void AddHoverDetection(GameObject cardUI)
        {
            // ���EventTrigger���
            EventTrigger eventTrigger = cardUI.GetComponent<EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = cardUI.AddComponent<EventTrigger>();
            }

            // �������¼�
            EventTrigger.Entry pointerEnter = new EventTrigger.Entry();
            pointerEnter.eventID = EventTriggerType.PointerEnter;
            pointerEnter.callback.AddListener((eventData) => OnCardPointerEnter(cardUI));
            eventTrigger.triggers.Add(pointerEnter);

            // ����뿪�¼�
            EventTrigger.Entry pointerExit = new EventTrigger.Entry();
            pointerExit.eventID = EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((eventData) => OnCardPointerExit(cardUI));
            eventTrigger.triggers.Add(pointerExit);
        }

        /// <summary>
        /// �����������¼�
        /// </summary>
        private void OnCardPointerEnter(GameObject cardUI)
        {
            if (currentState != DisplayState.FanDisplay) return;

            hoveredCard = cardUI;

            // ��ʼ��ͣ����
            if (hoverCoroutine != null)
            {
                StopCoroutine(hoverCoroutine);
            }
            hoverCoroutine = StartCoroutine(HoverScaleAnimation(cardUI, hoverScale));

            // �����¼�
            OnCardHoverEnter?.Invoke(cardUI);

        }

        /// <summary>
        /// ��������뿪�¼�
        /// </summary>
        private void OnCardPointerExit(GameObject cardUI)
        {
            if (currentState != DisplayState.FanDisplay) return;

            if (hoveredCard == cardUI)
            {
                hoveredCard = null;
            }

            // ��ʼ��ԭ����
            if (hoverCoroutine != null)
            {
                StopCoroutine(hoverCoroutine);
            }
            hoverCoroutine = StartCoroutine(HoverScaleAnimation(cardUI, 1f));

            // �����¼�
            OnCardHoverExit?.Invoke(cardUI);

        }

        /// <summary>
        /// ��ͣ���Ŷ���
        /// </summary>
        private IEnumerator HoverScaleAnimation(GameObject cardUI, float targetScale)
        {
            if (cardUI == null) yield break;

            RectTransform rectTransform = cardUI.GetComponent<RectTransform>();
            if (rectTransform == null) yield break;

            Vector3 startScale = rectTransform.localScale;
            Vector3 endScale = Vector3.one * targetScale;
            float elapsed = 0f;

            while (elapsed < hoverAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / hoverAnimationDuration;
                float curveValue = hoverCurve.Evaluate(t);

                rectTransform.localScale = Vector3.Lerp(startScale, endScale, curveValue);

                yield return null;
            }

            rectTransform.localScale = endScale;
        }

        /// <summary>
        /// �������н���
        /// </summary>
        private void DisableAllInteractions()
        {
            // ����ѹ״̬
            if (isPressing)
            {
                EndCardPress();
            }

            LogDebug("�������н���");
        }

        #endregion

        #region ���������

        /// <summary>
        /// ���������
        /// </summary>
        private void SetCameraControl(bool enabled)
        {
            if (playerCameraController != null)
            {
                playerCameraController.SetControlEnabled(enabled);
                LogDebug($"���������: {(enabled ? "����" : "����")}");
            }
            else
            {
                LogWarning("����������������ã��޷����������");
            }
        }

        #endregion

        #region ���߷���

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardDisplayUI] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardDisplayUI] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[CardDisplayUI] {message}");
        }

        #endregion

        #region Unity��������

        private void OnDestroy()
        {
            // ����������Դ
            ClearAllDisplays();

            LogDebug("CardDisplayUI�����٣���Դ������");
        }

        #endregion
    }
}