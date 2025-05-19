using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本地消息仓库：缓存所有通过 Transport 层发送或接收的消息，
/// 并按批次提供给 MessageDispatcher 分发。
/// </summary>
[DisallowMultipleComponent]
public class DataWarehouse : MonoBehaviour
{
    public static DataWarehouse Instance { get; private set; }

    // 内存缓冲区，存储所有未分发的消息
    private readonly List<NetworkMessage> _buffer = new List<NetworkMessage>();

    /// <summary>
    /// 当前缓冲区消息数量，仅供调试查看
    /// </summary>
    public int BufferCount => _buffer.Count;

    private void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[DataWarehouse] 初始化单例");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 添加一条消息到缓冲区
    /// </summary>
    public void AddMessage(NetworkMessage msg)
    {
        _buffer.Add(msg);
        Debug.Log($"[DataWarehouse] AddMessage: EventType={msg.EventType}, BufferCount={_buffer.Count}");
    }

    /// <summary>
    /// 拉取所有缓存的消息并清空缓冲区
    /// </summary>
    public List<NetworkMessage> GetAndClearAll()
    {
        var all = new List<NetworkMessage>(_buffer);
        //Debug.Log($"[DataWarehouse] GetAndClearAll: retrieving {all.Count} messages, clearing buffer");
        _buffer.Clear();
        return all;
    }
}
