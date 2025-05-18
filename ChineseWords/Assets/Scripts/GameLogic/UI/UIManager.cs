using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI ���� (Canvas �µĿսڵ� UIRoot)")]
    [Tooltip("���� UI ����ص���� Transform �£�ÿ��ֻ����һ�� UI")]
    [SerializeField] private Transform uiRoot;

    private GameObject currentUI;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // ���ֵ��л�������
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ����ָ��·���� UI Prefab�������ص� UIRoot �£��Զ����پ� UI
    /// </summary>
    /// <param name="prefabPath">Resources ·�������� .prefab ��׺��</param>
    /// <returns>���� UI �ӽڵ㣨ͨ���� Find("UI")��</returns>
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
            Debug.LogError("UIManager ����ʧ�ܣ�·�������ڣ�" + prefabPath);
            return null;
        }

        currentUI = Instantiate(prefab, uiRoot);
        return currentUI.transform.Find("UI");
    }

    /// <summary>
    /// ������յ�ǰ UI
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