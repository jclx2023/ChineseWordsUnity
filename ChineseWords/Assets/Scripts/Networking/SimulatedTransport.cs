// SimulatedTransport.cs
using System;
using UnityEngine;

/// <summary>
/// 请注意现在它继承自 MonoBehaviour，
/// 你才能像普通脚本那样挂到 GameObject 上。
/// </summary>
public class SimulatedTransport : MonoBehaviour, INetworkTransport
{
    public event Action<NetworkMessage> OnMessageReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<Exception> OnError;

    public void Initialize()
    {
        Debug.Log("[SimulatedTransport] Initialize");
        OnConnected?.Invoke();
    }

    public void SendMessage(NetworkMessage msg)
    {
        try
        {
            msg.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Debug.Log($"[SimulatedTransport] SendMessage → {msg.EventType}");
            //DataWarehouse.Instance.AddMessage(msg);
            OnMessageReceived?.Invoke(msg);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SimulatedTransport] Error: {ex}");
            OnError?.Invoke(ex);
        }
    }

    public void Shutdown()
    {
        Debug.Log("[SimulatedTransport] Shutdown");
        OnDisconnected?.Invoke();
    }
}
