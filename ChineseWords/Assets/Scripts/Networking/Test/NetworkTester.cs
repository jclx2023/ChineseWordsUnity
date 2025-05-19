using UnityEngine;

namespace Networking
{
    public class NetworkTester : MonoBehaviour
    {
        private INetworkTransport _transport;

        void Start()
        {
            // ͨ�� MessageDispatcher ��ȡͬһ�� Transport ʵ��
            var disp = FindObjectOfType<MessageDispatcher>();
            // ������˽���ֶ� _transport
            var field = typeof(MessageDispatcher).GetField("_transport",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            _transport = (INetworkTransport)field.GetValue(disp);
        }

        void Update()
        {
            // 1: ģ��㲥����
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                var msg = new NetworkMessage
                {
                    EventType = NetworkEventType.BroadcastQuestion,
                    SenderId = "Tester",
                    Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Payload = null
                };
                Debug.Log("[Tester] Send BroadcastQuestion");
                _transport.SendMessage(msg);
            }
            // 2: ģ��ش���ȷ
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                var msg = new NetworkMessage
                {
                    EventType = NetworkEventType.AnswerResult,
                    SenderId = "Tester",
                    Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Payload = true
                };
                Debug.Log("[Tester] Send AnswerResult(true)");
                _transport.SendMessage(msg);
            }
            // 3: ģ��ش����
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                var msg = new NetworkMessage
                {
                    EventType = NetworkEventType.AnswerResult,
                    SenderId = "Tester",
                    Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Payload = false
                };
                Debug.Log("[Tester] Send AnswerResult(false)");
                _transport.SendMessage(msg);
            }
        }
    }
}
