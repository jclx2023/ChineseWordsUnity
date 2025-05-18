using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class GameSettingsPanel : MonoBehaviour
{
    public TMP_InputField inputTimeLimit;
    public TMP_InputField inputInitialHP;
    public TMP_InputField inputDamage;

    public Button saveButton;

    private void Start()
    {
        // 初始化输入框的默认值
        var cfg = ConfigManager.Instance.Config;
        inputTimeLimit.text = cfg.timeLimit.ToString("0.0");
        inputInitialHP.text = cfg.initialHealth.ToString();
        inputDamage.text = cfg.damagePerWrong.ToString();

        saveButton.onClick.AddListener(SaveSettings);
    }

    void SaveSettings()
    {
        var cfg = ConfigManager.Instance.Config;

        if (float.TryParse(inputTimeLimit.text, out float newTime))
            cfg.timeLimit = newTime;
        if (int.TryParse(inputInitialHP.text, out int newHP))
            cfg.initialHealth = newHP;
        if (int.TryParse(inputDamage.text, out int newDmg))
            cfg.damagePerWrong = newDmg;

        SaveConfigToFile(cfg);
        Debug.Log("配置保存成功");
    }

    void SaveConfigToFile(GameConfig config)
    {
        string json = JsonUtility.ToJson(config, true);
        string path = Path.Combine(Application.streamingAssetsPath, "config.json");
        File.WriteAllText(path, json);
    }
}
