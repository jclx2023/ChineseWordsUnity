using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI 容器 (Canvas 下的空节点 UIRoot)")]
    [Tooltip("所有 UI 会挂载到这个 Transform 下，每次只保留一个 UI")]
    [SerializeField] private Transform uiRoot;

    private GameObject currentUI;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持到切换场景后
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 加载指定路径的 UI Prefab，并挂载到 UIRoot 下，自动销毁旧 UI
    /// </summary>
    /// <param name="prefabPath">Resources 路径（不含 .prefab 后缀）</param>
    /// <returns>返回 UI 子节点（通常是 Find("UI")）</returns>
    public Transform LoadUI(string prefabPath)
    {
        if (currentUI != null)
        {
            Destroy(currentUI);
            currentUI = null;
        }

        var prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("UIManager 加载失败，路径不存在：" + prefabPath);
            return null;
        }

        currentUI = Instantiate(prefab, uiRoot);
        return currentUI.transform.Find("UI");
    }

    /// <summary>
    /// 主动清空当前 UI
    /// </summary>
    public void ClearUI()
    {
        if (currentUI != null)
        {
            Destroy(currentUI);
            currentUI = null;
        }
    }
}