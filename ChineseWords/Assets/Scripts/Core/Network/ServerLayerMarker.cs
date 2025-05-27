using UnityEngine;

namespace Core.Network
{
    /// <summary>
    /// �������������
    /// ��ʶ��NetworkManagerΪ����������������Ϸ�߼���������UI��ʾ
    /// </summary>
    public class ServerLayerMarker : MonoBehaviour
    {
        [Header("������������")]
        [SerializeField] private bool enableDebugLogs = true;

        public bool IsServerLayer => true;

        private void Awake()
        {
            LogDebug("���������������ʼ��");
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[ServerLayer] {message}");
            }
        }
    }

    /// <summary>
    /// ���Host�������
    /// ��ʶ��NetworkManagerΪ���Host���Ȳ�����Ϸ����ʾUI
    /// </summary>
    public class PlayerHostLayerMarker : MonoBehaviour
    {
        [Header("���Host������")]
        [SerializeField] private bool enableDebugLogs = true;

        public bool IsPlayerHostLayer => true;

        private void Awake()
        {
            LogDebug("���Host���������ʼ��");
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHostLayer] {message}");
            }
        }
    }
}