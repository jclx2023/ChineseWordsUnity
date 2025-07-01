using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Classroom.Player;

namespace Cards.UI
{
    /// <summary>
    /// ����չʾUI������ - �ع���
    /// ��������Ƶ��Ӿ�չʾ״̬������ͼ <-> ����չʾ
    /// �Ƴ�Hidden״̬����Ϊ��CardUIManager��ȫ���Ƶ������
    /// </summary>
    public class CardDisplayUI : MonoBehaviour
    {
        [Header("����ͼ����")]
        [SerializeField] private Transform thumbnailContainer; // ����ͼ����
        [SerializeField] private Vector2 thumbnailPosition = new Vector2(90f, 0f); // ���������ʵ�λ��
        [SerializeField] private float thumbnailRotation = -10f; // ����ͼ������ת�Ƕ�
        [SerializeField] private float thumbnailScale = 0.4f; // ����ͼ����
        [SerializeField] private float thumbnailFanRadius = 80f; // ����ͼ���ΰ뾶
        [SerializeField] private float thumbnailFanAngle = 30f; // ����ͼ���νǶ�
        [SerializeField] private int maxThumbnailCards = 3; // �����ʾ3������ͼ

        [Header("����չʾ����")]
        [SerializeField] private Transform fanDisplayContainer; // ����չʾ����
        [SerializeField] private float fanRadius = 400f; // ���ΰ뾶
        [SerializeField] private float fanAngleSpread = 60f; // ���νǶȷ�Χ
        [SerializeField] private Vector2 fanCenter = new Vector2(0f, -200f); // �������ĵ�

        [Header("��ͣ��������")]
        [SerializeField] private float hoverScale = 1.2f; // ��ͣʱ�ķŴ���
        [SerializeField] private float hoverAnimationDuration = 0.2f; // ��ͣ����ʱ��
        [SerializeField] private AnimationCurve hoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // ��ͣ��������

        [Header("�л���������")]
        [SerializeField] private float transitionDuration = 0.5f; // ״̬�л�����ʱ��
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // չʾ״̬ö�� - �Ƴ�Hidden״̬
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

        // ��ʼ��״̬
        private bool isInitialized = false;

        // �������� - ͨ��CardUIManager����
        private PlayerCameraController playerCameraController;
        private CardUIComponents cardUIComponents;

        // �¼�
        public System.Action<GameObject> OnCardHoverEnter;
        public System.Action<GameObject> OnCardHoverExit;
        public System.Action<GameObject> OnCardSelected;

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

            // ��ʼ��UI���
            InitializeUIComponents();

            // ���ó�ʼ״̬Ϊ����ͼ
            SetState(DisplayState.Thumbnail);

            isInitialized = true;
            LogDebug("CardDisplayUI��ʼ�����");
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUIComponents()
        {
            // ȷ��CardDisplayUI������RectTransform
            RectTransform selfRect = GetComponent<RectTransform>();
            if (selfRect == null)
            {
                selfRect = gameObject.AddComponent<RectTransform>();
                selfRect.anchorMin = Vector2.zero;
                selfRect.anchorMax = Vector2.one;
                selfRect.sizeDelta = Vector2.zero;
                selfRect.anchoredPosition = Vector2.zero;
            }

            // ȷ������չʾ������������ȷ����
            if (fanDisplayContainer == null)
            {
                GameObject container = new GameObject("FanDisplayContainer");
                container.transform.SetParent(transform, false);

                // ���RectTransform���
                RectTransform containerRect = container.AddComponent<RectTransform>();
                containerRect.anchorMin = Vector2.zero;
                containerRect.anchorMax = Vector2.one;
                containerRect.sizeDelta = Vector2.zero;
                containerRect.anchoredPosition = Vector2.zero;

                fanDisplayContainer = container.transform;
                LogDebug($"��������չʾ������������: {container.transform.parent?.name}");
            }

            // ȷ������ͼ������������ȷ����
            if (thumbnailContainer == null)
            {
                GameObject thumbContainer = new GameObject("ThumbnailContainer");
                thumbContainer.transform.SetParent(transform, false);

                // ��������ͼ������RectTransform
                RectTransform thumbRect = thumbContainer.AddComponent<RectTransform>();
                thumbRect.anchoredPosition = thumbnailPosition;
                thumbRect.localScale = Vector3.one * thumbnailScale;
                thumbRect.localEulerAngles = new Vector3(0, 0, thumbnailRotation); // Ӧ����ת
                // ����ê��ͳߴ�
                thumbRect.anchorMin = Vector2.zero;
                thumbRect.anchorMax = Vector2.zero;
                thumbRect.sizeDelta = new Vector2(400, 300); // ��һ������ĳߴ�

                thumbnailContainer = thumbContainer.transform;
                LogDebug($"��������ͼ������������: {thumbContainer.transform.parent?.name}, λ��: {thumbRect.anchoredPosition}, ��ת: {thumbRect.localEulerAngles.z}��");
            }

            // ��֤�����㼶
            ValidateContainerHierarchy();

            LogDebug("UI�����ʼ�����");
        }

        /// <summary>
        /// ��֤�����㼶�Ƿ���ȷ
        /// </summary>
        private void ValidateContainerHierarchy()
        {
            // ���CardDisplayUI�ĸ�����
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

            // ��������Ĳ㼶·��
            string hierarchyPath = GetHierarchyPath(transform);
            LogDebug($"CardDisplayUI�㼶·��: {hierarchyPath}");
        }

        /// <summary>
        /// ��ȡ����Ĳ㼶·��
        /// </summary>
        private string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            Transform current = target.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
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

            // �������ο���
            ClearFanDisplayCards();

            // �ָ����������
            SetCameraControl(true);

            // �л�������ͼ״̬
            SetState(DisplayState.Thumbnail);
        }

        /// <summary>
        /// ���ؿ�����ʾ�����ݽӿڣ�ʵ������ǿ�ƻص�����ͼ״̬��
        /// </summary>
        public void HideCardDisplay()
        {
            LogDebug("���ؿ�����ʾ��ǿ�ƻص�����ͼ״̬��");
            ForceToThumbnailState();
        }

        /// <summary>
        /// ��ʾ������ʾ�����ݽӿڣ�ʵ������ˢ������ͼ��
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

            // ��������UI
            ClearThumbnailCards();
            ClearFanDisplayCards();

            // �ָ����������
            SetCameraControl(true);
        }

        #endregion

        #region ����ͼ��ʾ

        /// <summary>
        /// ��������ͼ����
        /// </summary>
        private void CreateThumbnailCards(List<CardDisplayData> cardDataList)
        {
            if (cardUIComponents == null)
            {
                LogError("CardUIComponentsδ���ã��޷���������ͼ");
                return;
            }

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
            if (cardUIComponents == null)
            {
                LogError("CardUIComponentsδ���ã��޷��������ο���");
                return;
            }

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

                // ����λ��
                Vector3 position = new Vector3(
                    fanCenter.x + Mathf.Sin(radians) * fanRadius,
                    fanCenter.y + Mathf.Cos(radians) * fanRadius,
                    0f
                );

                // ������ת
                Vector3 rotation = new Vector3(0, 0, -angle);

                fanPositions.Add(position);
                fanRotations.Add(rotation);
            }

            LogDebug($"������ {cardCount} �ſ��Ƶ�����λ��");
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

            // ������¼�
            EventTrigger.Entry pointerClick = new EventTrigger.Entry();
            pointerClick.eventID = EventTriggerType.PointerClick;
            pointerClick.callback.AddListener((eventData) => OnCardPointerClick(cardUI));
            eventTrigger.triggers.Add(pointerClick);
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

            LogDebug($"������ͣ����: {cardUI.name}");
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

            LogDebug($"������ͣ�뿪: {cardUI.name}");
        }

        /// <summary>
        /// ���Ƶ���¼�
        /// </summary>
        private void OnCardPointerClick(GameObject cardUI)
        {
            if (currentState != DisplayState.FanDisplay) return;

            // ����ѡ���¼�
            OnCardSelected?.Invoke(cardUI);

            LogDebug($"���Ʊ�ѡ��: {cardUI.name}");
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
            // ������������ӽ��ý������߼�
            // ��������CanvasGroup��interactable = false
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