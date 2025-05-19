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
    // ���Ժ�ɼ�����չ
}

public class NetworkMessage
{
    public NetworkEventType EventType;  // ȡ���ַ��� MessageId�������Ͱ�ȫ
    public string SenderId;             // ˭����
    public long Timestamp;              // ͳһһ��ʱ���
    public int TurnIndex;               // ��ǰ�غ���ţ���ѡ��
    public object Payload;              // �����¼�����
}
