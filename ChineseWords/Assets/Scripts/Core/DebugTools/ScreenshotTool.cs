using UnityEngine;

public class ScreenshotTool : MonoBehaviour
{
    public int superSize = 2; // ������Ϊ2��4���������ֱ���

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
