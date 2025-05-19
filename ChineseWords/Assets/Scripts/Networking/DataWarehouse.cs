using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ������Ϣ�ֿ⣺��������ͨ�� Transport �㷢�ͻ���յ���Ϣ��
/// ���������ṩ�� MessageDispatcher �ַ���
/// </summary>
[DisallowMultipleComponent]
public class DataWarehouse : MonoBehaviour
{
    public static DataWarehouse Instance { get; private set; }

    // �ڴ滺�������洢����δ�ַ�����Ϣ
    private readonly List<NetworkMessage> _buffer = new List<NetworkMessage>();

    /// <summary>
    /// ��ǰ��������Ϣ�������������Բ鿴
    /// </summary>
    public int BufferCount => _buffer.Count;

    private void Awake()
    {
        // ����ģʽ
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[DataWarehouse] ��ʼ������");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ���һ����Ϣ��������
    /// </summary>
    public void AddMessage(NetworkMessage msg)
    {
        _buffer.Add(msg);
        Debug.Log($"[DataWarehouse] AddMessage: EventType={msg.EventType}, BufferCount={_buffer.Count}");
    }

    /// <summary>
    /// ��ȡ���л������Ϣ����ջ�����
    /// </summary>
    public List<NetworkMessage> GetAndClearAll()
    {
        var all = new List<NetworkMessage>(_buffer);
        //Debug.Log($"[DataWarehouse] GetAndClearAll: retrieving {all.Count} messages, clearing buffer");
        _buffer.Clear();
        return all;
    }
}
