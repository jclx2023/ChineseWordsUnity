using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Button细节覆盖层 - 在九宫格Button上添加细节图片
    /// </summary>
    public class ButtonDetailOverlay : MonoBehaviour
    {
        [Header("细节图片设置")]
        [SerializeField] private Sprite detailSprite;           // 细节图片
        [SerializeField] private bool createOnStart = true;     // 启动时自动创建

        private Button targetButton;
        private Image detailImage;
        private RectTransform buttonRect;
        private RectTransform detailRect;

        private void Start()
        {
            if (createOnStart)
            {
                SetupDetailOverlay();
            }
        }

        /// <summary>
        /// 设置细节覆盖层
        /// </summary>
        public void SetupDetailOverlay()
        {
            // 获取Button组件
            targetButton = GetComponent<Button>();
            if (targetButton == null)
            {
                Debug.LogError("ButtonDetailOverlay必须挂载在Button上！");
                return;
            }

            buttonRect = targetButton.GetComponent<RectTransform>();

            // 创建细节图片对象
            CreateDetailImage();

            // 同步尺寸
            SyncDetailSize();
        }

        /// <summary>
        /// 创建细节图片
        /// </summary>
        private void CreateDetailImage()
        {
            if (detailSprite == null)
            {
                Debug.LogWarning("未设置细节图片！");
                return;
            }

            // 创建子对象
            GameObject detailObj = new GameObject("DetailOverlay");
            detailObj.transform.SetParent(transform);

            // 添加Image组件
            detailImage = detailObj.AddComponent<Image>();
            detailImage.sprite = detailSprite;
            detailImage.raycastTarget = false; // 不接收点击事件

            // 设置RectTransform - 使用中心锚点避免尺寸问题
            detailRect = detailObj.GetComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0.5f, 0.5f);
            detailRect.anchorMax = new Vector2(0.5f, 0.5f);
            detailRect.anchoredPosition = Vector2.zero;
            detailRect.localScale = Vector3.one;
        }

        /// <summary>
        /// 同步细节图片尺寸到Button
        /// </summary>
        private void SyncDetailSize()
        {
            if (detailRect == null || buttonRect == null) return;

            // 直接设置为Button的实际尺寸
            detailRect.sizeDelta = buttonRect.rect.size;
        }


        private void Update()
        {
            // 实时同步尺寸（如果Button大小发生变化）
            if (detailRect != null && buttonRect != null)
            {
                Vector2 currentButtonSize = buttonRect.rect.size;
                if (detailRect.sizeDelta != currentButtonSize)
                {
                    detailRect.sizeDelta = currentButtonSize;
                }
            }
        }
    }
}