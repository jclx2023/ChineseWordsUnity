// /Scripts/Networking/NetworkMessage.cs
public class NetworkMessage
{
    public string PlayerId;      // �ĸ���ҷ���
    public string MessageId;     // ��Ϣ���ͻ�Ψһ ID
    public object Payload;       // ��������ݽṹ
    public long SentTimestamp;   // ����ʱ�䣨Unix ms��
    public long ReceivedTimestamp; // �յ�ʱ�䣨Unix ms��
}
