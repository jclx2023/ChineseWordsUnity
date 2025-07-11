using UnityEngine;

namespace UI
{
    /// <summary>
    /// 简化版自定义鼠标指针管理器
    /// </summary>
    public class CustomCursorManager : MonoBehaviour
    {
        [Header("指针设置")]
        [SerializeField] private Texture2D cursorTexture;
        [SerializeField] private Vector2 hotSpot = new Vector2(64, 64); // 128x128的中心点

        public static CustomCursorManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                SetCustomCursor();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetCustomCursor()
        {
            if (cursorTexture != null)
            {
                Cursor.SetCursor(cursorTexture, hotSpot, CursorMode.Auto);
            }
        }

        /// <summary>
        /// 切换到自定义指针
        /// </summary>
        public void EnableCustomCursor()
        {
            if (cursorTexture != null)
            {
                Cursor.SetCursor(cursorTexture, hotSpot, CursorMode.Auto);
            }
        }

        /// <summary>
        /// 恢复默认指针
        /// </summary>
        public void ResetCursor()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        /// <summary>
        /// 显示/隐藏指针
        /// </summary>
        public void SetCursorVisible(bool visible)
        {
            Cursor.visible = visible;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }
    }
}