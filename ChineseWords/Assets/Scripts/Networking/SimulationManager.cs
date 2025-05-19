using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    public GameObject playerPrefab;
    [Tooltip("Ҫģ��������������������")]
    public int playerCount = 3;

    void Start()
    {
        for (int i = 0; i < playerCount; i++)
        {
            var go = Instantiate(playerPrefab, Vector3.right * i * 2, Quaternion.identity);
            go.name = $"Player_{i + 1}";
            var qmc = go.GetComponent<QuestionManagerController>();
            qmc.OverrideLocalPlayerId($"P{i + 1}");
        }
    }
}
