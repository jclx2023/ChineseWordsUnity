// /Scripts/Networking/NetworkMessage.cs
public class NetworkMessage
{
    public string PlayerId;      // 哪个玩家发的
    public string MessageId;     // 消息类型或唯一 ID
    public object Payload;       // 具体的数据结构
    public long SentTimestamp;   // 发出时间（Unix ms）
    public long ReceivedTimestamp; // 收到时间（Unix ms）
}
