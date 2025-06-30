using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Cards.UI
{
    /// <summary>
    /// ����UI������� - Ԥ����ģʽ
    /// ����ʵ��������Ԥ���岢��������
    /// </summary>
    public class CardUIComponents : MonoBehaviour
    {
        [Header("Ԥ��������")]
        [SerializeField] private GameObject cardUIPrefab; // ����UIԤ����

        // Ԥ������Դ·��
        private const string CARD_UI_PREFAB_PATH = "Prefabs/UI/CardUIPrefab";

        [Header("�ߴ�����")]
        [SerializeField] private Vector2 baseCardSize = new Vector2(200f, 279f);
        [SerializeField] private float scaleFactor = 1f; // ȫ����������

        [Header("��������")]
        [SerializeField] private float baseTitleFontSize = 18f; // �������������С
        [SerializeField] private float baseDescriptionFontSize = 12f; // �������������С
        [SerializeField] private bool enableResponsiveFonts = true; // ������Ӧʽ����

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ����ʵ��
        public static CardUIComponents Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("CardUIComponentsʵ���Ѵ���");

                // ��̬����Ԥ����
                LoadCardUIPrefab();
            }
            else
            {
                LogDebug("�����ظ���CardUIComponentsʵ�������ٵ�ǰʵ��");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // ��֤Ԥ��������
            if (!ValidateResources())
            {
                LogError("CardUIComponents��Դ��֤ʧ�ܣ�����Ԥ��������");
            }
        }

        /// <summary>
        /// ��Resources�ļ��м��ؿ���UIԤ����
        /// </summary>
        private void LoadCardUIPrefab()
        {
            cardUIPrefab = Resources.Load<GameObject>(CARD_UI_PREFAB_PATH);
        }



        /// <summary>
        /// ���������Ŀ���UI GameObject
        /// </summary>
        public GameObject CreateCardUI(CardDisplayData cardData, Transform parent = null)
        {
            if (cardData == null || !cardData.IsValid())
            {
                LogError("��Ч�Ŀ������ݣ��޷�����UI");
                return null;
            }

            // ȷ��Ԥ�����Ѽ���
            if (cardUIPrefab == null)
            {
                LogWarning("Ԥ����Ϊ�գ��������¼���");
                LoadCardUIPrefab();

                if (cardUIPrefab == null)
                {
                    LogError("����UIԤ�������ʧ�ܣ��޷�����UI");
                    return null;
                }
            }

            LogDebug($"��ʼ��������UI: {cardData.cardName}");

            // ʵ����Ԥ����
            GameObject cardInstance = Instantiate(cardUIPrefab, parent);

            // ��������
            cardInstance.name = $"Card_{cardData.cardId}_{cardData.cardName}";

            // Ӧ�óߴ�����
            ApplyCardSize(cardInstance);

            // ���ÿ�������
            ConfigureCardUI(cardInstance, cardData);

            LogDebug($"����UI�������: {cardData.cardName}");
            return cardInstance;
        }

        /// <summary>
        /// ���ÿ���UI�����ݺ����
        /// </summary>
        private void ConfigureCardUI(GameObject cardInstance, CardDisplayData cardData)
        {
            // ���ÿ��Ʊ�ʶ���
            ConfigureCardIdentifier(cardInstance, cardData);

            // ���ÿ�������
            ConfigureCardFace(cardInstance, cardData);

            // ���ÿ��Ʊ���
            ConfigureCardTitle(cardInstance, cardData);

            // ���ÿ�������
            ConfigureCardDescription(cardInstance, cardData);

            LogDebug($"����UI�������: {cardData.cardName}");
        }

        /// <summary>
        /// ���ÿ��Ʊ�ʶ���
        /// </summary>
        private void ConfigureCardIdentifier(GameObject cardInstance, CardDisplayData cardData)
        {
            CardUIIdentifier identifier = cardInstance.GetComponent<CardUIIdentifier>();
            if (identifier == null)
            {
                identifier = cardInstance.AddComponent<CardUIIdentifier>();
            }

            identifier.cardId = cardData.cardId;
            identifier.cardName = cardData.cardName;
        }

        /// <summary>
        /// ���ÿ�������
        /// </summary>
        private void ConfigureCardFace(GameObject cardInstance, CardDisplayData cardData)
        {
            // ���ҿ�������Image���
            Image faceImage = FindComponentInChildren<Image>(cardInstance, "CardFace");

            if (faceImage != null)
            {
                faceImage.sprite = cardData.cardFaceSprite;
                faceImage.color = cardData.backgroundColor;

                // ���û������ͼ����ʾ����ɫ
                if (cardData.cardFaceSprite == null)
                {
                    LogWarning($"���� {cardData.cardName} û������ͼ������ʾ��ɫ����");
                }
            }
            else
            {
                LogWarning($"δ�ҵ�CardFace���������: {cardData.cardName}");
            }
        }

        /// <summary>
        /// ���ÿ��Ʊ���
        /// </summary>
        private void ConfigureCardTitle(GameObject cardInstance, CardDisplayData cardData)
        {
            TextMeshProUGUI titleText = FindComponentInChildren<TextMeshProUGUI>(cardInstance, "CardTitle");

            if (titleText != null)
            {
                titleText.text = cardData.cardName;

                // Ӧ����Ӧʽ�����С
                if (enableResponsiveFonts)
                {
                    titleText.fontSize = baseTitleFontSize * scaleFactor;
                }
            }
            else
            {
                LogWarning($"δ�ҵ�CardTitle���������: {cardData.cardName}");
            }
        }

        /// <summary>
        /// ���ÿ�������
        /// </summary>
        private void ConfigureCardDescription(GameObject cardInstance, CardDisplayData cardData)
        {
            TextMeshProUGUI descriptionText = FindComponentInChildren<TextMeshProUGUI>(cardInstance, "CardDescription");

            if (descriptionText != null)
            {
                descriptionText.text = cardData.GetFormattedDescription();

                // Ӧ����Ӧʽ�����С
                if (enableResponsiveFonts)
                {
                    descriptionText.fontSize = baseDescriptionFontSize * scaleFactor;
                }
            }
            else
            {
                LogWarning($"δ�ҵ�CardDescription���������: {cardData.cardName}");
            }
        }

        /// <summary>
        /// Ӧ�ÿ��Ƴߴ�����
        /// </summary>
        private void ApplyCardSize(GameObject cardInstance)
        {
            RectTransform rectTransform = cardInstance.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 finalSize = baseCardSize * scaleFactor;
                rectTransform.sizeDelta = finalSize;

                LogDebug($"Ӧ�ÿ��Ƴߴ�: {finalSize}");
            }
        }

        /// <summary>
        /// �������п���UI�����ݣ����ڶ�̬���£�
        /// </summary>
        public void UpdateCardUI(GameObject cardInstance, CardDisplayData cardData)
        {
            if (cardInstance == null || cardData == null || !cardData.IsValid())
            {
                LogError("��Ч�Ĳ������޷����¿���UI");
                return;
            }

            LogDebug($"���¿���UI: {cardData.cardName}");
            ConfigureCardUI(cardInstance, cardData);
        }

        /// <summary>
        /// �����������ſ���UI
        /// </summary>
        public System.Collections.Generic.List<GameObject> CreateMultipleCardUI(
            System.Collections.Generic.List<CardDisplayData> cardDataList, Transform parent = null)
        {
            var cardUIList = new System.Collections.Generic.List<GameObject>();

            if (cardDataList == null || cardDataList.Count == 0)
            {
                LogWarning("���������б�Ϊ�գ��޷���������UI");
                return cardUIList;
            }

            LogDebug($"��ʼ�������� {cardDataList.Count} �ſ���UI");

            foreach (var cardData in cardDataList)
            {
                GameObject cardUI = CreateCardUI(cardData, parent);
                if (cardUI != null)
                {
                    cardUIList.Add(cardUI);
                }
            }

            LogDebug($"����������ɣ��ɹ����� {cardUIList.Count} �ſ���UI");
            return cardUIList;
        }

        /// <summary>
        /// ���ٿ���UI
        /// </summary>
        public void DestroyCardUI(GameObject cardUI)
        {
            if (cardUI != null)
            {
                LogDebug($"���ٿ���UI: {cardUI.name}");
                Destroy(cardUI);
            }
        }

        /// <summary>
        /// �������ٿ���UI
        /// </summary>
        public void DestroyMultipleCardUI(System.Collections.Generic.List<GameObject> cardUIList)
        {
            if (cardUIList == null) return;

            LogDebug($"��ʼ�������� {cardUIList.Count} �ſ���UI");

            foreach (var cardUI in cardUIList)
            {
                DestroyCardUI(cardUI);
            }

            cardUIList.Clear();
        }

        /// <summary>
        /// ����ȫ���������ӣ��������䲻ͬ��Ļ�ֱ��ʣ�
        /// </summary>
        public void SetScaleFactor(float newScaleFactor)
        {
            scaleFactor = Mathf.Clamp(newScaleFactor, 0.5f, 2f); // �������ŷ�Χ
            LogDebug($"����ȫ����������: {scaleFactor}");
        }

        /// <summary>
        /// ������Ļ�ֱ����Զ�������������
        /// </summary>
        public void AutoCalculateScaleFactor()
        {
            float screenWidth = Screen.width;
            float referenceWidth = 1920f; // �ο��ֱ��ʿ��

            float autoScale = screenWidth / referenceWidth;
            SetScaleFactor(autoScale);

            LogDebug($"�Զ�������������: {scaleFactor} (��Ļ���: {screenWidth})");
        }

        /// <summary>
        /// ��ȡ�����Ŀ��Ƴߴ�
        /// </summary>
        public Vector2 GetFinalCardSize()
        {
            return baseCardSize * scaleFactor;
        }

        /// <summary>
        /// ���Ӷ����в���ָ�����Ƶ����
        /// </summary>
        private T FindComponentInChildren<T>(GameObject parent, string childName) where T : Component
        {
            Transform child = parent.transform.Find(childName);
            if (child != null)
            {
                return child.GetComponent<T>();
            }

            // ���ֱ�Ӳ���ʧ�ܣ����Եݹ����
            T component = parent.GetComponentInChildren<T>();
            return component;
        }

        /// <summary>
        /// ��֤��Ҫ��Դ
        /// </summary>
        public bool ValidateResources()
        {
            if (cardUIPrefab == null)
            {
                LogError("����UIԤ����δ����");
                return false;
            }

            // ��֤Ԥ����ṹ
            return ValidatePrefabStructure();
        }

        /// <summary>
        /// ��֤Ԥ����ṹ
        /// </summary>
        private bool ValidatePrefabStructure()
        {
            if (cardUIPrefab == null) return false;

            bool isValid = true;
            string[] requiredComponents = { "CardFace", "CardTitle", "CardDescription" };

            foreach (string componentName in requiredComponents)
            {
                Transform child = cardUIPrefab.transform.Find(componentName);
                if (child == null)
                {
                    LogWarning($"Ԥ������ȱ�����: {componentName}");
                    isValid = false;
                }
            }

            if (isValid)
            {
                LogDebug("Ԥ����ṹ��֤ͨ��");
            }

            return isValid;
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardUIComponents] {message}");
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardUIComponents] {message}");
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[CardUIComponents] {message}");
        }

    }

    /// <summary>
    /// ����UI��ʶ���
    /// ���ڱ�ʶUI GameObject��Ӧ�Ŀ�������
    public class CardUIIdentifier : MonoBehaviour
    {
        public int cardId;
        public string cardName;

        /// <summary>
        /// ��ȡ������ʾ��Ϣ
        /// </summary>
        public string GetDisplayInfo()
        {
            return $"[{cardId}] {cardName}";
        }
    }
}