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
        private const string CARD_UI_PREFAB_PATH = "Prefabs/UI/CardUIPrefab";

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
            if (Instance == null)
            {
                Instance = this;
                LogDebug("CardUIComponents实例已创建");

                // 动态加载预制体
                LoadCardUIPrefab();
            }
            else
            {
                LogDebug("发现重复的CardUIComponents实例，销毁当前实例");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 验证预制体设置
            if (!ValidateResources())
            {
                LogError("CardUIComponents资源验证失败，请检查预制体设置");
            }
        }

        /// <summary>
        /// 从Resources文件夹加载卡牌UI预制体
        /// </summary>
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

            LogDebug($"开始创建卡牌UI: {cardData.cardName}");

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
            // 配置卡牌标识组件
            ConfigureCardIdentifier(cardInstance, cardData);

            // 配置卡牌牌面
            ConfigureCardFace(cardInstance, cardData);

            // 配置卡牌标题
            ConfigureCardTitle(cardInstance, cardData);

            // 配置卡牌描述
            ConfigureCardDescription(cardInstance, cardData);

            LogDebug($"卡牌UI配置完成: {cardData.cardName}");
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

            if (faceImage != null)
            {
                faceImage.sprite = cardData.cardFaceSprite;
                faceImage.color = cardData.backgroundColor;

                // 如果没有牌面图，显示背景色
                if (cardData.cardFaceSprite == null)
                {
                    LogWarning($"卡牌 {cardData.cardName} 没有牌面图，将显示纯色背景");
                }
            }
            else
            {
                LogWarning($"未找到CardFace组件，卡牌: {cardData.cardName}");
            }
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

                // 应用响应式字体大小
                if (enableResponsiveFonts)
                {
                    titleText.fontSize = baseTitleFontSize * scaleFactor;
                }
            }
            else
            {
                LogWarning($"未找到CardTitle组件，卡牌: {cardData.cardName}");
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
            else
            {
                LogWarning($"未找到CardDescription组件，卡牌: {cardData.cardName}");
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

            // 如果直接查找失败，尝试递归查找
            T component = parent.GetComponentInChildren<T>();
            return component;
        }

        /// <summary>
        /// 验证必要资源
        /// </summary>
        public bool ValidateResources()
        {
            if (cardUIPrefab == null)
            {
                LogError("卡牌UI预制体未设置");
                return false;
            }

            // 验证预制体结构
            return ValidatePrefabStructure();
        }

        /// <summary>
        /// 验证预制体结构
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
                    LogWarning($"预制体中缺少组件: {componentName}");
                    isValid = false;
                }
            }

            if (isValid)
            {
                LogDebug("预制体结构验证通过");
            }

            return isValid;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardUIComponents] {message}");
            }
        }

        /// <summary>
        /// 警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardUIComponents] {message}");
            }
        }

        /// <summary>
        /// 错误日志
        /// </summary>
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