using UnityEngine;
using UI;

public class UIEnvironmentMarker : MonoBehaviour
{
    public GameObject gameCanvas;
    public UIManager uiManager;

    private void Awake()
    {
        // 自动查找UI组件
        if (gameCanvas == null)
            gameCanvas = GameObject.Find("GameCanvas");

        if (uiManager == null)
            uiManager = UIManager.Instance;
    }
}