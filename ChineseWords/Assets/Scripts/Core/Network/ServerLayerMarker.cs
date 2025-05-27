using UnityEngine;

namespace Core.Network
{
    /// <summary>
    /// 服务器层标记组件
    /// 标识此NetworkManager为纯服务器，负责游戏逻辑但不参与UI显示
    /// </summary>
    public class ServerLayerMarker : MonoBehaviour
    {
        [Header("服务器层配置")]
        [SerializeField] private bool enableDebugLogs = true;

        public bool IsServerLayer => true;

        private void Awake()
        {
            LogDebug("服务器层标记组件初始化");
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
    /// 玩家Host层标记组件
    /// 标识此NetworkManager为玩家Host，既参与游戏又显示UI
    /// </summary>
    public class PlayerHostLayerMarker : MonoBehaviour
    {
        [Header("玩家Host层配置")]
        [SerializeField] private bool enableDebugLogs = true;

        public bool IsPlayerHostLayer => true;

        private void Awake()
        {
            LogDebug("玩家Host层标记组件初始化");
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