public enum NetworkEventType
{
    AssignTurnOrder,
    RequestDrawQuestion,
    BroadcastQuestion,
    SubmitAnswer,
    AnswerResult,
    UpdateHealth,
    PlayerDied,
    NextTurn,
    GameOver,
    // …以后可继续扩展
}

public class NetworkMessage
{
    public NetworkEventType EventType;  // 取代字符串 MessageId，更类型安全
    public string SenderId;             // 谁发的
    public long Timestamp;              // 统一一个时间戳
    public int TurnIndex;               // 当前回合序号（可选）
    public object Payload;              // 具体事件数据
}
