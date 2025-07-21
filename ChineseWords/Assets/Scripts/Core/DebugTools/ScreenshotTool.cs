using UnityEngine;

public class ScreenshotTool : MonoBehaviour
{
    public int superSize = 2; // ������Ϊ2��4���������ֱ���
    private void Awake()
    {
        DontDestroyOnLoad(this);
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12)) // ��F12��ͼ
        {
            string fileName = $"Screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            ScreenCapture.CaptureScreenshot(fileName, superSize);
            Debug.Log($"��ͼ�ѱ�������{fileName}");
        }
    }
}
