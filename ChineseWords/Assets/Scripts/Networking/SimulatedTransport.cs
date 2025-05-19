using System;
using System.Collections.Generic;
using UnityEngine;
namespace Networking
{
    public class SimulatedTransport : MonoBehaviour, INetworkTransport
    {
        // 1. ά������ʵ���б�
        private static readonly List<SimulatedTransport> s_instances = new List<SimulatedTransport>();

        public event Action<NetworkMessage> OnMessageReceived;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<Exception> OnError;

        void Awake()
        {
            s_instances.Add(this);
        }

        void OnDestroy()
        {
            s_instances.Remove(this);
        }

        public void Initialize()
        {
            Debug.Log("[SimulatedTransport] Initialize");
            // �������ӻص�
            OnConnected?.Invoke();
            // ͬ����Զ�ˡ�Ҳ���������¼�
            foreach (var inst in s_instances)
                if (inst != this)
                    inst.OnConnected?.Invoke();
        }

        public void SendMessage(NetworkMessage msg)
        {
            try
            {
                msg.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Debug.Log($"[SimulatedTransport] Broadcast {msg.EventType} from {msg.SenderId}");
                // 2. ������ʵ���㲥
                foreach (var inst in s_instances)
                {
                    if (inst == this) continue;
                    inst.OnMessageReceived?.Invoke(msg);
                }
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
}
