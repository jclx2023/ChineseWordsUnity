using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Cards.UI
{
    /// <summary>
    /// 卡牌UI组件工厂 - 预制体模式
    /// 负责实例化卡牌预制体并设置数据
    /// </summary>
    public class CardUIComponents : MonoBehaviour
    {
        [Header("预制体设置")]
        [SerializeField] private GameObject cardUIPrefab; // 卡牌UI预制体

        // 预制体资源路径
        private const string CARD_UI_PREFAB_PATH = "Prefabs/UI/Card/CardUIPrefab";

        [Header("尺寸设置")]
        [SerializeField] private Vector2 baseCardSize = new Vector2(200f, 279f);
        [SerializeField] private float scaleFactor = 1f; // 全局缩放因子

        [Header("字体设置")]
        [SerializeField] private float baseTitleFontSize = 18f; // 基础标题字体大小
        [SerializeField] private float baseDescriptionFontSize = 12f; // 基础描述字体大小
        [SerializeField] private bool enableResponsiveFonts = true; // 启用响应式字体

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 单例实例
        public static CardUIComponents Instance { get; private set; }

        private void Awake()
        {
            LogDebug($"{GetType().Name} 组件已创建，等待单例设置");
            LoadCardUIPrefab();
        }

        private void Start()
        {
        }

        private void LoadCardUIPrefab()
        {
            cardUIPrefab = Resources.Load<GameObject>(CARD_UI_PREFAB_PATH);
        }



        /// <summary>
        /// 创建完整的卡牌UI GameObject
        /// </summary>
        public GameObject CreateCardUI(CardDisplayData cardData, Transform parent = null)
        {
            if (cardData == null || !cardData.IsValid())
            {
                LogError("无效的卡牌数据，无法创建UI");
                return null;
            }

            // 确保预制体已加载
            if (cardUIPrefab == null)
            {
                LogWarning("预制体为空，尝试重新加载");
                LoadCardUIPrefab();

                if (cardUIPrefab == null)
                {
                    LogError("卡牌UI预制体加载失败，无法创建UI");
                    return null;
                }
            }

            // 实例化预制体
            GameObject cardInstance = Instantiate(cardUIPrefab, parent);

            // 设置名称
            cardInstance.name = $"Card_{cardData.cardId}_{cardData.cardName}";

            // 应用尺寸设置
            ApplyCardSize(cardInstance);

            // 配置卡牌数据
            ConfigureCardUI(cardInstance, cardData);

            LogDebug($"卡牌UI创建完成: {cardData.cardName}");
            return cardInstance;
        }

        /// <summary>
        /// 配置卡牌UI的数据和外观
        /// </summary>
        private void ConfigureCardUI(GameObject cardInstance, CardDisplayData cardData)
        {
            ConfigureCardIdentifier(cardInstance, cardData);

            ConfigureCardFace(cardInstance, cardData);

            ConfigureCardTitle(cardInstance, cardData);

            ConfigureCardDescription(cardInstance, cardData);

        }

        /// <summary>
        /// 配置卡牌标识组件
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
        /// 配置卡牌牌面
        /// </summary>
        private void ConfigureCardFace(GameObject cardInstance, CardDisplayData cardData)
        {
            // 查找卡牌牌面Image组件
            Image faceImage = FindComponentInChildren<Image>(cardInstance, "CardFace");
            faceImage.sprite = cardData.cardFaceSprite;
            faceImage.color = cardData.backgroundColor;
        }

        /// <summary>
        /// 配置卡牌标题
        /// </summary>
        private void ConfigureCardTitle(GameObject cardInstance, CardDisplayData cardData)
        {
            TextMeshProUGUI titleText = FindComponentInChildren<TextMeshProUGUI>(cardInstance, "CardTitle");

            if (titleText != null)
            {
                titleText.text = cardData.cardName;
                if (enableResponsiveFonts)
                {
                    titleText.fontSize = baseTitleFontSize * scaleFactor;
                }
            }
        }

        /// <summary>
        /// 配置卡牌描述
        /// </summary>
        private void ConfigureCardDescription(GameObject cardInstance, CardDisplayData cardData)
        {
            TextMeshProUGUI descriptionText = FindComponentInChildren<TextMeshProUGUI>(cardInstance, "CardDescription");

            if (descriptionText != null)
            {
                descriptionText.text = cardData.GetFormattedDescription();

                // 应用响应式字体大小
                if (enableResponsiveFonts)
                {
                    descriptionText.fontSize = baseDescriptionFontSize * scaleFactor;
                }
            }
        }

        /// <summary>
        /// 应用卡牌尺寸设置
        /// </summary>
        private void ApplyCardSize(GameObject cardInstance)
        {
            RectTransform rectTransform = cardInstance.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 finalSize = baseCardSize * scaleFactor;
                rectTransform.sizeDelta = finalSize;

                LogDebug($"应用卡牌尺寸: {finalSize}");
            }
        }

        /// <summary>
        /// 更新现有卡牌UI的数据（用于动态更新）
        /// </summary>
        public void UpdateCardUI(GameObject cardInstance, CardDisplayData cardData)
        {
            if (cardInstance == null || cardData == null || !cardData.IsValid())
            {
                LogError("无效的参数，无法更新卡牌UI");
                return;
            }

            LogDebug($"更新卡牌UI: {cardData.cardName}");
            ConfigureCardUI(cardInstance, cardData);
        }

        /// <summary>
        /// 批量创建多张卡牌UI
        /// </summary>
        public System.Collections.Generic.List<GameObject> CreateMultipleCardUI(
            System.Collections.Generic.List<CardDisplayData> cardDataList, Transform parent = null)
        {
            var cardUIList = new System.Collections.Generic.List<GameObject>();

            if (cardDataList == null || cardDataList.Count == 0)
            {
                LogWarning("卡牌数据列表为空，无法批量创建UI");
                return cardUIList;
            }

            LogDebug($"开始批量创建 {cardDataList.Count} 张卡牌UI");

            foreach (var cardData in cardDataList)
            {
                GameObject cardUI = CreateCardUI(cardData, parent);
                if (cardUI != null)
                {
                    cardUIList.Add(cardUI);
                }
            }

            LogDebug($"批量创建完成，成功创建 {cardUIList.Count} 张卡牌UI");
            return cardUIList;
        }

        /// <summary>
        /// 销毁卡牌UI
        /// </summary>
        public void DestroyCardUI(GameObject cardUI)
        {
            if (cardUI != null)
            {
                LogDebug($"销毁卡牌UI: {cardUI.name}");
                Destroy(cardUI);
            }
        }

        /// <summary>
        /// 批量销毁卡牌UI
        /// </summary>
        public void DestroyMultipleCardUI(System.Collections.Generic.List<GameObject> cardUIList)
        {
            if (cardUIList == null) return;

            LogDebug($"开始批量销毁 {cardUIList.Count} 张卡牌UI");

            foreach (var cardUI in cardUIList)
            {
                DestroyCardUI(cardUI);
            }

            cardUIList.Clear();
        }

        /// <summary>
        /// 设置全局缩放因子（用于适配不同屏幕分辨率）
        /// </summary>
        public void SetScaleFactor(float newScaleFactor)
        {
            scaleFactor = Mathf.Clamp(newScaleFactor, 0.5f, 2f); // 限制缩放范围
            LogDebug($"设置全局缩放因子: {scaleFactor}");
        }

        /// <summary>
        /// 根据屏幕分辨率自动计算缩放因子
        /// </summary>
        public void AutoCalculateScaleFactor()
        {
            float screenWidth = Screen.width;
            float referenceWidth = 1920f; // 参考分辨率宽度

            float autoScale = screenWidth / referenceWidth;
            SetScaleFactor(autoScale);

            LogDebug($"自动计算缩放因子: {scaleFactor} (屏幕宽度: {screenWidth})");
        }

        /// <summary>
        /// 获取计算后的卡牌尺寸
        /// </summary>
        public Vector2 GetFinalCardSize()
        {
            return baseCardSize * scaleFactor;
        }

        /// <summary>
        /// 在子对象中查找指定名称的组件
        /// </summary>
        private T FindComponentInChildren<T>(GameObject parent, string childName) where T : Component
        {
            Transform child = parent.transform.Find(childName);
            if (child != null)
            {
                return child.GetComponent<T>();
            }

            T component = parent.GetComponentInChildren<T>();
            return component;
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardUIComponents] {message}");
            }
        }
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardUIComponents] {message}");
            }
        }
        private void LogError(string message)
        {
            Debug.LogError($"[CardUIComponents] {message}");
        }

    }

    /// <summary>
    /// 卡牌UI标识组件
    /// 用于标识UI GameObject对应的卡牌数据
    public class CardUIIdentifier : MonoBehaviour
    {
        public int cardId;
        public string cardName;

        /// <summary>
        /// 获取卡牌显示信息
        /// </summary>
        public string GetDisplayInfo()
        {
            return $"[{cardId}] {cardName}";
        }
    }
}