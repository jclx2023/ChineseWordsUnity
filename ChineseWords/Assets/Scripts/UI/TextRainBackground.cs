using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

namespace UI.Effects
{
    /// <summary>
    /// ��Χб�������걳��Ч�����
    /// ��������ȫ����б���ƶ������꣬��ʼ��ʱ��������Ļ
    /// </summary>
    public class TextRainBackground : MonoBehaviour
    {
        [Header("Canvas����")]
        [SerializeField] private Canvas textRainCanvas; // �ֶ�ָ����TextRainCanvas
        [SerializeField] private RectTransform canvasRect; // Canvas��RectTransform

        [Header("����������")]
        [SerializeField] private int maxTextCount = 150; // ���ͬʱ���ڵ���������
        [SerializeField] private float spawnInterval = 0.1f; // ���ɼ�����룩
        [SerializeField] private int spawnCountPerInterval = 2; // ÿ�����ɵ���������

        [Header("�ƶ�����")]
        [SerializeField] private Vector2 moveSpeed = new Vector2(80f, -120f); // б���ƶ��ٶ� (x����, y����)
        [SerializeField] private Vector2 speedVariation = new Vector2(20f, 30f); // �ٶ�����仯��Χ
        [SerializeField] private bool enableRotation = false; // �Ƿ�������ת
        [SerializeField] private float rotationSpeed = 10f; // ��ת�ٶ�

        [Header("�Ӿ�����")]
        [SerializeField] private Color textColor = new Color(0.8f, 0.8f, 0.8f, 0.15f); // ͳһ������ɫ����͸���ȣ�
        [SerializeField] private Vector2 scaleRange = new Vector2(0.7f, 1.2f); // ���ŷ�Χ
        [SerializeField] private Vector2 fontSizeRange = new Vector2(20f, 40f); // �����С��Χ

        [Header("��������")]
        [SerializeField] private TMP_FontAsset textFont; // ������Դ

        [Header("���ɷ�Χ����")]
        [SerializeField] private float extraSpawnRange = 400f; // �������ɷ�Χ��ȷ��ȫ�����ǣ�
        [SerializeField] private float textSpacing = 120f; // ���ּ��

        [Header("��ʼ������")]
        [SerializeField] private bool preloadScreenTexts = true; // �Ƿ�Ԥ������������
        [SerializeField] private int initialTextDensity = 80; // ��ʼ�����ܶ�

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = false;

        // �ڲ�״̬
        private List<TextRainItem> activeTexts = new List<TextRainItem>();
        private List<TextRainItem> textPool = new List<TextRainItem>();
        private bool isActive = false;
        private float canvasWidth;
        private float canvasHeight;
        private Coroutine spawnCoroutine;

        private string[] rainTexts = {
    "��", "��", "��", "��", "��", "ʫ", "��", "ѧ", "֪", "ʶ", "��", "��", "ѧ", "ϰ",
    "��", "��", "��", "��", "��", "��", "��", "��", "ɽ", "ˮ", "��", "��", "��",
    "��", "ʦ", "��", "��", "��", "��", "��", "��", "ƴ", "��", "��", "��", "��", "��",
    "��", "д", "Ĭ", "��", "��", "��", "��", "��", "˵", "��", "��",
    "��", "��", "��", "��", "��", "��", "��", "��", "��", "Т", "��",
    "��", "��", "��", "��", "��", "��", "��", "��", "��","��", "��", "��", "ϲ", "ŭ", "��", "��",
    "��", "��", "��", "��", "��", "��", "��", "��", "ī", "ֽ", "��" ,
    "��", "��", "ѩ", "ҹ", "��", "��", "��", "��", "��", "ͤ", "¶",
    "��", "��", "��", "־", "��", "��", "˼", "��"
};


        /// <summary>
        /// ��������Ŀ��
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

        #region Unity��������

        private void Awake()
        {
            // ��֤Canvas����
            ValidateCanvasSetup();

            // ��ȡCanvas�ߴ�
            UpdateCanvasSize();
        }

        private void Start()
        {
            // �������Ԥ���أ���������Ļ
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

            // �������л�Ծ������
            UpdateActiveTexts();
        }

        private void OnDestroy()
        {
            StopTextRain();
            ClearAllTexts();
        }

        #endregion

        #region Canvas������֤

        /// <summary>
        /// ��֤Canvas����
        /// </summary>
        private void ValidateCanvasSetup()
        {
            // �Զ�����Canvas�����û���ֶ�ָ����
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
            // �Զ���ȡRectTransform
            if (canvasRect == null)
            {
                canvasRect = textRainCanvas.GetComponent<RectTransform>();
            }

            // ȷ��Canvas��GraphicRaycaster�����ã������赲���
            GraphicRaycaster raycaster = textRainCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null && raycaster.enabled)
            {
                raycaster.enabled = false;
            }
        }

        /// <summary>
        /// ����Canvas�ߴ�
        /// </summary>
        private void UpdateCanvasSize()
        {
            if (canvasRect != null)
            {
                // ��ȡCanvas��ʵ�ʳߴ�
                Rect canvasRectData = canvasRect.rect;
                canvasWidth = canvasRectData.width;
                canvasHeight = canvasRectData.height;
            }
            else
            {
                // fallback����Ļ�ߴ�
                canvasWidth = Screen.width;
                canvasHeight = Screen.height;
            }
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ��ʼ������Ч��
        /// </summary>
        public void StartTextRain()
        {
            if (isActive) return;

            isActive = true;

            // ����Canvas
            if (!textRainCanvas.gameObject.activeInHierarchy)
            {
                textRainCanvas.gameObject.SetActive(true);
            }

            // ����������������
            foreach (var textItem in activeTexts)
            {
                if (textItem.gameObject != null)
                {
                    textItem.gameObject.SetActive(true);
                    textItem.isActive = true;
                }
            }

            // ��ʼ������������
            spawnCoroutine = StartCoroutine(ContinuousSpawnCoroutine());
        }

        /// <summary>
        /// ֹͣ������Ч��
        /// </summary>
        public void StopTextRain()
        {
            isActive = false;

            // ֹͣ����������
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            // ������������
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
        /// �����������
        /// </summary>
        public void ClearAllTexts()
        {
            // ֹͣ����
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            // �����Ծ����
            foreach (var textItem in activeTexts)
            {
                if (textItem.gameObject != null)
                {
                    DestroyImmediate(textItem.gameObject);
                }
            }
            activeTexts.Clear();

            // ��������
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

        #region Ԥ����ϵͳ

        /// <summary>
        /// Ԥ������������
        /// </summary>
        private IEnumerator PreloadScreenTexts()
        {
            UpdateCanvasSize();

            // ������Ҫ���ǵ������򣨰������ⷶΧȷ�������ǣ�
            float totalWidth = canvasWidth + extraSpawnRange * 2;
            float totalHeight = canvasHeight + extraSpawnRange * 2;

            // ������������
            int gridX = Mathf.CeilToInt(totalWidth / textSpacing);
            int gridY = Mathf.CeilToInt(totalHeight / textSpacing);

            // ������ʼλ�ã�ȷ������������Ļ�������䣩
            float startX = -canvasWidth * 0.5f - extraSpawnRange;
            float startY = canvasHeight * 0.5f + extraSpawnRange;

            int createdCount = 0;
            int maxInitialTexts = Mathf.Min(gridX * gridY, initialTextDensity);

            // ��������״�ֲ�������
            for (int y = 0; y < gridY && createdCount < maxInitialTexts; y++)
            {
                for (int x = 0; x < gridX && createdCount < maxInitialTexts; x++)
                {
                    // �������һЩλ�ã�������ڹ���
                    if (Random.value < 0.3f) continue;

                    Vector2 gridPos = new Vector2(
                        startX + x * textSpacing + Random.Range(-textSpacing * 0.3f, textSpacing * 0.3f),
                        startY - y * textSpacing + Random.Range(-textSpacing * 0.3f, textSpacing * 0.3f)
                    );

                    TextRainItem textItem = CreateTextItemAtPosition(gridPos);
                    if (textItem != null)
                    {
                        textItem.isActive = false; // ��ʱ�������StartTextRainʱ�ټ���
                        textItem.gameObject.SetActive(false);
                        activeTexts.Add(textItem);
                        createdCount++;
                    }

                    // ÿ�����������־�yieldһ�Σ����⿨��
                    if (createdCount % 10 == 0)
                    {
                        yield return null;
                    }
                }
            }
            // Ԥ������ɺ�����������
            StartTextRain();
        }

        /// <summary>
        /// ��ָ��λ�ô���������
        /// </summary>
        private TextRainItem CreateTextItemAtPosition(Vector2 position)
        {
            TextRainItem textItem = GetTextItemFromPool();
            if (textItem == null) return null;

            // ���ѡ����������
            string randomText = rainTexts[Random.Range(0, rainTexts.Length)];
            textItem.textComponent.text = randomText;

            // ����λ��
            textItem.rectTransform.anchoredPosition = position;

            // ��������ٶ�
            Vector2 randomizedSpeed = new Vector2(
                moveSpeed.x + Random.Range(-speedVariation.x, speedVariation.x),
                moveSpeed.y + Random.Range(-speedVariation.y, speedVariation.y)
            );
            textItem.velocity = randomizedSpeed;

            // ������ת�ٶ�
            textItem.rotationVelocity = enableRotation ?
                Random.Range(-rotationSpeed, rotationSpeed) : 0f;

            // �������
            SetupTextAppearance(textItem);

            return textItem;
        }

        #endregion

        #region ��������ϵͳ

        /// <summary>
        /// �����������ֵ�Э��
        /// </summary>
        private IEnumerator ContinuousSpawnCoroutine()
        {
            while (isActive)
            {
                // ����Ƿ���Ҫ����������
                if (activeTexts.Count < maxTextCount)
                {
                    // һ�����ɶ������
                    for (int i = 0; i < spawnCountPerInterval && activeTexts.Count < maxTextCount; i++)
                    {
                        SpawnSingleText();
                    }
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        /// <summary>
        /// ���ɵ�������
        /// </summary>
        private void SpawnSingleText()
        {
            // ����Ļ�Ϸ������Ĵ�Χ��������
            Vector2 spawnPos = GetRandomSpawnPosition();
            TextRainItem textItem = CreateTextItemAtPosition(spawnPos);

            if (textItem != null)
            {
                // ��������
                textItem.isActive = true;
                textItem.gameObject.SetActive(true);
                activeTexts.Add(textItem);
            }
        }

        /// <summary>
        /// ��ȡ�������λ�ã���Χ���ǣ�
        /// </summary>
        private Vector2 GetRandomSpawnPosition()
        {
            // ����Ļ���Ϸ��Ĵ�Χ�������ɣ�ȷ���ܸ������н���
            float x = Random.Range(-canvasWidth * 0.5f - extraSpawnRange, canvasWidth * 0.5f);
            float y = Random.Range(canvasHeight * 0.5f, canvasHeight * 0.5f + extraSpawnRange);

            return new Vector2(x, y);
        }

        #endregion

        #region ���ָ��ºͻ���

        /// <summary>
        /// �������л�Ծ����
        /// </summary>
        private void UpdateActiveTexts()
        {
            for (int i = activeTexts.Count - 1; i >= 0; i--)
            {
                var textItem = activeTexts[i];

                if (!textItem.isActive) continue;

                // ����λ��
                Vector2 currentPos = textItem.rectTransform.anchoredPosition;
                currentPos += textItem.velocity * Time.deltaTime;
                textItem.rectTransform.anchoredPosition = currentPos;

                // ������ת
                if (enableRotation)
                {
                    textItem.rectTransform.Rotate(0, 0, textItem.rotationVelocity * Time.deltaTime);
                }

                // ����Ƿ��Ƴ���Ļ����Ҫ����
                if (ShouldRecycleText(textItem))
                {
                    RecycleTextItem(textItem);
                    activeTexts.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// ��������Ƿ�Ӧ�ñ�����
        /// </summary>
        private bool ShouldRecycleText(TextRainItem textItem)
        {
            Vector2 pos = textItem.rectTransform.anchoredPosition;

            // �Ƴ���Ļ�Ҳ���·�ʱ���գ�������շ�Χȷ�����������ֿ��ڱ�Ե��
            if (pos.x > canvasWidth * 0.5f + extraSpawnRange || pos.y < -canvasHeight * 0.5f - extraSpawnRange)
            {
                return true;
            }

            return false;
        }

        #endregion

        #region ����ع���

        /// <summary>
        /// �Ӷ���ػ�ȡ������
        /// </summary>
        private TextRainItem GetTextItemFromPool()
        {
            // ���Դӳ��л�ȡ
            if (textPool.Count > 0)
            {
                TextRainItem item = textPool[textPool.Count - 1];
                textPool.RemoveAt(textPool.Count - 1);
                return item;
            }

            // ����û�п���������µ�
            return CreateNewTextItem();
        }

        /// <summary>
        /// �����µ�������
        /// </summary>
        private TextRainItem CreateNewTextItem()
        {
            if (canvasRect == null) return null;

            GameObject textObj = new GameObject("RainText");

            // �����RectTransform�����UIԪ�ر��裩
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();

            // Ȼ�����ø���
            rectTransform.SetParent(canvasRect, false);

            // ���TextMeshProUGUI���
            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();

            // �����������
            if (textFont != null)
            {
                textComponent.font = textFont;
            }

            textComponent.color = textColor;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.text = "��";

            // ����RectTransform
            rectTransform.sizeDelta = new Vector2(60f, 60f);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // ����TextRainItem
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
        /// ��������������
        /// </summary>
        private void RecycleTextItem(TextRainItem textItem)
        {
            if (textItem == null) return;

            textItem.Reset();
            textPool.Add(textItem);
        }

        /// <summary>
        /// �����������
        /// </summary>
        private void SetupTextAppearance(TextRainItem textItem)
        {
            // �����������
            float randomScale = Random.Range(scaleRange.x, scaleRange.y);
            textItem.rectTransform.localScale = Vector3.one * randomScale;

            // ������������С
            float fontSize = Random.Range(fontSizeRange.x, fontSizeRange.y);
            textItem.textComponent.fontSize = fontSize;

            // ͳһʹ���趨����ɫ����������仯
            textItem.textComponent.color = textColor;
        }

        #endregion
    }
}