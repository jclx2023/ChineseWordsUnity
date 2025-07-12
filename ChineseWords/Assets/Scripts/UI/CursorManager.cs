using UnityEngine;

namespace UI
{
    /// <summary>
    /// �򻯰��Զ������ָ�������
    /// </summary>
    public class CustomCursorManager : MonoBehaviour
    {
        [Header("ָ������")]
        [SerializeField] private Texture2D cursorTexture;
        [SerializeField] private Vector2 hotSpot = new Vector2(64, 64); // 128x128�����ĵ�

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
        /// �л����Զ���ָ��
        /// </summary>
        public void EnableCustomCursor()
        {
            if (cursorTexture != null)
            {
                Cursor.SetCursor(cursorTexture, hotSpot, CursorMode.Auto);
            }
        }

        /// <summary>
        /// �ָ�Ĭ��ָ��
        /// </summary>
        public void ResetCursor()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        /// <summary>
        /// ��ʾ/����ָ��
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