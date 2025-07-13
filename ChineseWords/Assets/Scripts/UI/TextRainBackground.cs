using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

namespace UI.Effects
{
    /// <summary>
    /// 大范围斜向文字雨背景效果组件
    /// 创建覆盖全屏的斜向移动文字雨，初始化时就铺满屏幕
    /// </summary>
    public class TextRainBackground : MonoBehaviour
    {
        [Header("Canvas配置")]
        [SerializeField] private Canvas textRainCanvas; // 手动指定的TextRainCanvas
        [SerializeField] private RectTransform canvasRect; // Canvas的RectTransform

        [Header("文字雨配置")]
        [SerializeField] private int maxTextCount = 150; // 最大同时存在的文字数量
        [SerializeField] private float spawnInterval = 0.1f; // 生成间隔（秒）
        [SerializeField] private int spawnCountPerInterval = 2; // 每次生成的文字数量

        [Header("移动配置")]
        [SerializeField] private Vector2 moveSpeed = new Vector2(80f, -120f); // 斜向移动速度 (x向右, y向下)
        [SerializeField] private Vector2 speedVariation = new Vector2(20f, 30f); // 速度随机变化范围
        [SerializeField] private bool enableRotation = false; // 是否启用旋转
        [SerializeField] private float rotationSpeed = 10f; // 旋转速度

        [Header("视觉配置")]
        [SerializeField] private Color textColor = new Color(0.8f, 0.8f, 0.8f, 0.15f); // 统一文字颜色（低透明度）
        [SerializeField] private Vector2 scaleRange = new Vector2(0.7f, 1.2f); // 缩放范围
        [SerializeField] private Vector2 fontSizeRange = new Vector2(20f, 40f); // 字体大小范围

        [Header("字体配置")]
        [SerializeField] private TMP_FontAsset textFont; // 字体资源

        [Header("生成范围配置")]
        [SerializeField] private float extraSpawnRange = 400f; // 额外生成范围（确保全屏覆盖）
        [SerializeField] private float textSpacing = 120f; // 文字间距

        [Header("初始化配置")]
        [SerializeField] private bool preloadScreenTexts = true; // 是否预加载满屏文字
        [SerializeField] private int initialTextDensity = 80; // 初始文字密度

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = false;

        // 内部状态
        private List<TextRainItem> activeTexts = new List<TextRainItem>();
        private List<TextRainItem> textPool = new List<TextRainItem>();
        private bool isActive = false;
        private float canvasWidth;
        private float canvasHeight;
        private Coroutine spawnCoroutine;

        private string[] rainTexts = {
    "汉", "字", "词", "语", "成", "诗", "文", "学", "知", "识", "智", "慧", "学", "习",
    "中", "国", "人", "民", "天", "地", "日", "月", "山", "水", "火", "心", "力",
    "教", "师", "课", "堂", "题", "句", "段", "章", "拼", "音", "部", "首", "笔", "画",
    "读", "写", "默", "背", "记", "抄", "改", "听", "说", "答", "问",
    "典", "故", "义", "信", "诚", "仁", "礼", "勇", "忠", "孝", "节",
    "龙", "虎", "风", "云", "花", "鸟", "鱼", "虫", "春","夏", "秋", "冬", "喜", "怒", "哀", "乐",
    "琴", "棋", "书", "卷", "赋", "联", "辞", "笔", "墨", "纸", "砚" ,
    "柳", "烟", "雪", "夜", "梦", "灯", "舟", "江", "湖", "亭", "露",
    "德", "理", "意", "志", "念", "梦", "思", "忍"
};


        /// <summary>
        /// 文字雨项目类
        /// </summary>
        private class TextRainItem
        {
            public GameObject gameObject;
            public RectTransform rectTransform;
            public TextMeshProUGUI textComponent;
            public Vector2 velocity;
            public float rotationVelocity;
            public bool isActive;

            public void Reset()
            {
                isActive = false;
                if (gameObject != null)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        #region Unity生命周期

        private void Awake()
        {
            // 验证Canvas设置
            ValidateCanvasSetup();

            // 获取Canvas尺寸
            UpdateCanvasSize();
        }

        private void Start()
        {
            // 如果启用预加载，先铺满屏幕
            if (preloadScreenTexts)
            {
                StartCoroutine(PreloadScreenTexts());
            }
            else
            {
                StartTextRain();
            }
        }

        private void Update()
        {
            if (!isActive) return;

            // 更新所有活跃的文字
            UpdateActiveTexts();
        }

        private void OnDestroy()
        {
            StopTextRain();
            ClearAllTexts();
        }

        #endregion

        #region Canvas设置验证

        /// <summary>
        /// 验证Canvas设置
        /// </summary>
        private void ValidateCanvasSetup()
        {
            // 自动查找Canvas（如果没有手动指定）
            if (textRainCanvas == null)
            {
                textRainCanvas = GetComponent<Canvas>();
                if (textRainCanvas == null)
                {
                    textRainCanvas = GetComponentInParent<Canvas>();
                }
                if (textRainCanvas == null)
                {
                    textRainCanvas = FindObjectOfType<Canvas>();
                }
            }
            // 自动获取RectTransform
            if (canvasRect == null)
            {
                canvasRect = textRainCanvas.GetComponent<RectTransform>();
            }

            // 确保Canvas的GraphicRaycaster被禁用，避免阻挡点击
            GraphicRaycaster raycaster = textRainCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null && raycaster.enabled)
            {
                raycaster.enabled = false;
            }
        }

        /// <summary>
        /// 更新Canvas尺寸
        /// </summary>
        private void UpdateCanvasSize()
        {
            if (canvasRect != null)
            {
                // 获取Canvas的实际尺寸
                Rect canvasRectData = canvasRect.rect;
                canvasWidth = canvasRectData.width;
                canvasHeight = canvasRectData.height;
            }
            else
            {
                // fallback到屏幕尺寸
                canvasWidth = Screen.width;
                canvasHeight = Screen.height;
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 开始文字雨效果
        /// </summary>
        public void StartTextRain()
        {
            if (isActive) return;

            isActive = true;

            // 激活Canvas
            if (!textRainCanvas.gameObject.activeInHierarchy)
            {
                textRainCanvas.gameObject.SetActive(true);
            }

            // 激活所有现有文字
            foreach (var textItem in activeTexts)
            {
                if (textItem.gameObject != null)
                {
                    textItem.gameObject.SetActive(true);
                    textItem.isActive = true;
                }
            }

            // 开始持续生成文字
            spawnCoroutine = StartCoroutine(ContinuousSpawnCoroutine());
        }

        /// <summary>
        /// 停止文字雨效果
        /// </summary>
        public void StopTextRain()
        {
            isActive = false;

            // 停止生成新文字
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            // 隐藏所有文字
            foreach (var textItem in activeTexts)
            {
                if (textItem.gameObject != null)
                {
                    RecycleTextItem(textItem);
                }
            }
            activeTexts.Clear();
        }

        /// <summary>
        /// 清除所有文字
        /// </summary>
        public void ClearAllTexts()
        {
            // 停止生成
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            // 清理活跃文字
            foreach (var textItem in activeTexts)
            {
                if (textItem.gameObject != null)
                {
                    DestroyImmediate(textItem.gameObject);
                }
            }
            activeTexts.Clear();

            // 清理对象池
            foreach (var textItem in textPool)
            {
                if (textItem.gameObject != null)
                {
                    DestroyImmediate(textItem.gameObject);
                }
            }
            textPool.Clear();
        }

        #endregion

        #region 预加载系统

        /// <summary>
        /// 预加载满屏文字
        /// </summary>
        private IEnumerator PreloadScreenTexts()
        {
            UpdateCanvasSize();

            // 计算需要覆盖的总区域（包括额外范围确保无死角）
            float totalWidth = canvasWidth + extraSpawnRange * 2;
            float totalHeight = canvasHeight + extraSpawnRange * 2;

            // 计算网格数量
            int gridX = Mathf.CeilToInt(totalWidth / textSpacing);
            int gridY = Mathf.CeilToInt(totalHeight / textSpacing);

            // 计算起始位置（确保覆盖整个屏幕包括角落）
            float startX = -canvasWidth * 0.5f - extraSpawnRange;
            float startY = canvasHeight * 0.5f + extraSpawnRange;

            int createdCount = 0;
            int maxInitialTexts = Mathf.Min(gridX * gridY, initialTextDensity);

            // 创建网格状分布的文字
            for (int y = 0; y < gridY && createdCount < maxInitialTexts; y++)
            {
                for (int x = 0; x < gridX && createdCount < maxInitialTexts; x++)
                {
                    // 随机跳过一些位置，避免过于规整
                    if (Random.value < 0.3f) continue;

                    Vector2 gridPos = new Vector2(
                        startX + x * textSpacing + Random.Range(-textSpacing * 0.3f, textSpacing * 0.3f),
                        startY - y * textSpacing + Random.Range(-textSpacing * 0.3f, textSpacing * 0.3f)
                    );

                    TextRainItem textItem = CreateTextItemAtPosition(gridPos);
                    if (textItem != null)
                    {
                        textItem.isActive = false; // 暂时不激活，等StartTextRain时再激活
                        textItem.gameObject.SetActive(false);
                        activeTexts.Add(textItem);
                        createdCount++;
                    }

                    // 每创建几个文字就yield一次，避免卡顿
                    if (createdCount % 10 == 0)
                    {
                        yield return null;
                    }
                }
            }
            // 预加载完成后启动文字雨
            StartTextRain();
        }

        /// <summary>
        /// 在指定位置创建文字项
        /// </summary>
        private TextRainItem CreateTextItemAtPosition(Vector2 position)
        {
            TextRainItem textItem = GetTextItemFromPool();
            if (textItem == null) return null;

            // 随机选择文字内容
            string randomText = rainTexts[Random.Range(0, rainTexts.Length)];
            textItem.textComponent.text = randomText;

            // 设置位置
            textItem.rectTransform.anchoredPosition = position;

            // 设置随机速度
            Vector2 randomizedSpeed = new Vector2(
                moveSpeed.x + Random.Range(-speedVariation.x, speedVariation.x),
                moveSpeed.y + Random.Range(-speedVariation.y, speedVariation.y)
            );
            textItem.velocity = randomizedSpeed;

            // 设置旋转速度
            textItem.rotationVelocity = enableRotation ?
                Random.Range(-rotationSpeed, rotationSpeed) : 0f;

            // 设置外观
            SetupTextAppearance(textItem);

            return textItem;
        }

        #endregion

        #region 持续生成系统

        /// <summary>
        /// 持续生成文字的协程
        /// </summary>
        private IEnumerator ContinuousSpawnCoroutine()
        {
            while (isActive)
            {
                // 检查是否需要生成新文字
                if (activeTexts.Count < maxTextCount)
                {
                    // 一次生成多个文字
                    for (int i = 0; i < spawnCountPerInterval && activeTexts.Count < maxTextCount; i++)
                    {
                        SpawnSingleText();
                    }
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        /// <summary>
        /// 生成单个文字
        /// </summary>
        private void SpawnSingleText()
        {
            // 在屏幕上方和左侧的大范围区域生成
            Vector2 spawnPos = GetRandomSpawnPosition();
            TextRainItem textItem = CreateTextItemAtPosition(spawnPos);

            if (textItem != null)
            {
                // 激活文字
                textItem.isActive = true;
                textItem.gameObject.SetActive(true);
                activeTexts.Add(textItem);
            }
        }

        /// <summary>
        /// 获取随机生成位置（大范围覆盖）
        /// </summary>
        private Vector2 GetRandomSpawnPosition()
        {
            // 在屏幕左上方的大范围区域生成，确保能覆盖所有角落
            float x = Random.Range(-canvasWidth * 0.5f - extraSpawnRange, canvasWidth * 0.5f);
            float y = Random.Range(canvasHeight * 0.5f, canvasHeight * 0.5f + extraSpawnRange);

            return new Vector2(x, y);
        }

        #endregion

        #region 文字更新和回收

        /// <summary>
        /// 更新所有活跃文字
        /// </summary>
        private void UpdateActiveTexts()
        {
            for (int i = activeTexts.Count - 1; i >= 0; i--)
            {
                var textItem = activeTexts[i];

                if (!textItem.isActive) continue;

                // 更新位置
                Vector2 currentPos = textItem.rectTransform.anchoredPosition;
                currentPos += textItem.velocity * Time.deltaTime;
                textItem.rectTransform.anchoredPosition = currentPos;

                // 更新旋转
                if (enableRotation)
                {
                    textItem.rectTransform.Rotate(0, 0, textItem.rotationVelocity * Time.deltaTime);
                }

                // 检查是否移出屏幕，需要回收
                if (ShouldRecycleText(textItem))
                {
                    RecycleTextItem(textItem);
                    activeTexts.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 检查文字是否应该被回收
        /// </summary>
        private bool ShouldRecycleText(TextRainItem textItem)
        {
            Vector2 pos = textItem.rectTransform.anchoredPosition;

            // 移出屏幕右侧或下方时回收（增大回收范围确保不会有文字卡在边缘）
            if (pos.x > canvasWidth * 0.5f + extraSpawnRange || pos.y < -canvasHeight * 0.5f - extraSpawnRange)
            {
                return true;
            }

            return false;
        }

        #endregion

        #region 对象池管理

        /// <summary>
        /// 从对象池获取文字项
        /// </summary>
        private TextRainItem GetTextItemFromPool()
        {
            // 尝试从池中获取
            if (textPool.Count > 0)
            {
                TextRainItem item = textPool[textPool.Count - 1];
                textPool.RemoveAt(textPool.Count - 1);
                return item;
            }

            // 池中没有可用项，创建新的
            return CreateNewTextItem();
        }

        /// <summary>
        /// 创建新的文字项
        /// </summary>
        private TextRainItem CreateNewTextItem()
        {
            if (canvasRect == null) return null;

            GameObject textObj = new GameObject("RainText");

            // 先添加RectTransform组件（UI元素必需）
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();

            // 然后设置父级
            rectTransform.SetParent(canvasRect, false);

            // 添加TextMeshProUGUI组件
            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();

            // 配置文字组件
            if (textFont != null)
            {
                textComponent.font = textFont;
            }

            textComponent.color = textColor;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.text = "文";

            // 设置RectTransform
            rectTransform.sizeDelta = new Vector2(60f, 60f);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // 创建TextRainItem
            TextRainItem textItem = new TextRainItem
            {
                gameObject = textObj,
                rectTransform = rectTransform,
                textComponent = textComponent,
                isActive = false
            };

            textObj.SetActive(false);
            return textItem;
        }

        /// <summary>
        /// 回收文字项到对象池
        /// </summary>
        private void RecycleTextItem(TextRainItem textItem)
        {
            if (textItem == null) return;

            textItem.Reset();
            textPool.Add(textItem);
        }

        /// <summary>
        /// 设置文字外观
        /// </summary>
        private void SetupTextAppearance(TextRainItem textItem)
        {
            // 设置随机缩放
            float randomScale = Random.Range(scaleRange.x, scaleRange.y);
            textItem.rectTransform.localScale = Vector3.one * randomScale;

            // 设置随机字体大小
            float fontSize = Random.Range(fontSizeRange.x, fontSizeRange.y);
            textItem.textComponent.fontSize = fontSize;

            // 统一使用设定的颜色，不做随机变化
            textItem.textComponent.color = textColor;
        }

        #endregion
    }
}