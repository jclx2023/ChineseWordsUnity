using UnityEngine;
using UI;

public class UIEnvironmentMarker : MonoBehaviour
{
    public GameObject gameCanvas;
    public UIManager uiManager;

    private void Awake()
    {
        // �Զ�����UI���
        if (gameCanvas == null)
            gameCanvas = GameObject.Find("GameCanvas");

        if (uiManager == null)
            uiManager = UIManager.Instance;
    }
}