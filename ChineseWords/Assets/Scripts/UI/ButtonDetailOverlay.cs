using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Buttonϸ�ڸ��ǲ� - �ھŹ���Button�����ϸ��ͼƬ
    /// </summary>
    public class ButtonDetailOverlay : MonoBehaviour
    {
        [Header("ϸ��ͼƬ����")]
        [SerializeField] private Sprite detailSprite;           // ϸ��ͼƬ
        [SerializeField] private bool createOnStart = true;     // ����ʱ�Զ�����

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
        /// ����ϸ�ڸ��ǲ�
        /// </summary>
        public void SetupDetailOverlay()
        {
            // ��ȡButton���
            targetButton = GetComponent<Button>();
            if (targetButton == null)
            {
                Debug.LogError("ButtonDetailOverlay���������Button�ϣ�");
                return;
            }

            buttonRect = targetButton.GetComponent<RectTransform>();

            // ����ϸ��ͼƬ����
            CreateDetailImage();

            // ͬ���ߴ�
            SyncDetailSize();
        }

        /// <summary>
        /// ����ϸ��ͼƬ
        /// </summary>
        private void CreateDetailImage()
        {
            if (detailSprite == null)
            {
                Debug.LogWarning("δ����ϸ��ͼƬ��");
                return;
            }

            // �����Ӷ���
            GameObject detailObj = new GameObject("DetailOverlay");
            detailObj.transform.SetParent(transform);

            // ���Image���
            detailImage = detailObj.AddComponent<Image>();
            detailImage.sprite = detailSprite;
            detailImage.raycastTarget = false; // �����յ���¼�

            // ����RectTransform - ʹ������ê�����ߴ�����
            detailRect = detailObj.GetComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0.5f, 0.5f);
            detailRect.anchorMax = new Vector2(0.5f, 0.5f);
            detailRect.anchoredPosition = Vector2.zero;
            detailRect.localScale = Vector3.one;
        }

        /// <summary>
        /// ͬ��ϸ��ͼƬ�ߴ絽Button
        /// </summary>
        private void SyncDetailSize()
        {
            if (detailRect == null || buttonRect == null) return;

            // ֱ������ΪButton��ʵ�ʳߴ�
            detailRect.sizeDelta = buttonRect.rect.size;
        }


        private void Update()
        {
            // ʵʱͬ���ߴ磨���Button��С�����仯��
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