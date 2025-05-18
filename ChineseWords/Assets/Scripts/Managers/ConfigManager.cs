using UnityEngine;
using System.IO;

public class ConfigManager : MonoBehaviour
{
    public static ConfigManager Instance { get; private set; }

    public GameConfig Config { get; private set; } = new GameConfig();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfigFromStreamingAssets();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadConfigFromStreamingAssets()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "config.json");

        if (!File.Exists(path))
        {
            Debug.LogWarning($"�����ļ�δ�ҵ���ʹ��Ĭ������: {path}");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            Config = JsonUtility.FromJson<GameConfig>(json);
            Debug.Log($"�ɹ��������ã�timeLimit={Config.timeLimit}, HP={Config.initialHealth}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("��ȡ����ʧ�ܣ�" + ex.Message);
        }
    }
}
