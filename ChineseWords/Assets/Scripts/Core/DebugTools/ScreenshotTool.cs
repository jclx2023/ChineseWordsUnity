using UnityEngine;

public class ScreenshotTool : MonoBehaviour
{
    public int superSize = 2; // 可设置为2、4等以提升分辨率
    private void Awake()
    {
        DontDestroyOnLoad(this);
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12)) // 按F12截图
        {
            string fileName = $"Screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            ScreenCapture.CaptureScreenshot(fileName, superSize);
            Debug.Log($"截图已保存至：{fileName}");
        }
    }
}
