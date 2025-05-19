using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// 消息调度器：定时拉取本地仓库或网络传入的消息，
    /// 并分发到 QuestionManagerController 与 PlayerHealthManager。
    /// </summary>
    [DisallowMultipleComponent]
    public class MessageDispatcher : MonoBehaviour
    {
        [Header("调度间隔（秒)")]
        [Tooltip("每隔多少秒拉取并分发一次消息")]
        public float DispatchInterval = 0.1f;

        [Header("Transport 组件，可拖入 PhotonTransport/CustomTransport，否则使用 SimulatedTransport")]
        public MonoBehaviour TransportComponent;

        [Header("QuestionManagerController 组件，可拖入，否则自动查找场景中的实例")]
        public QuestionManagerController QuestionController;

        private INetworkTransport _transport;

        private void Awake()
        {
            // 获取或注入 QuestionManagerController
            if (QuestionController == null)
            {
                QuestionController = FindObjectOfType<QuestionManagerController>();
                if (QuestionController == null)
                    Debug.LogError("[MessageDispatcher] 未找到 QuestionManagerController，请手动拖入组件。");
            }

            // 获取或创建 Transport
            if (TransportComponent is INetworkTransport custom)
            {
                _transport = custom;
                Debug.Log($"[MessageDispatcher] 使用注入 Transport: {TransportComponent.GetType().Name}");
            }
            else
            {
                _transport = new SimulatedTransport();
                Debug.Log("[MessageDispatcher] 未指定 Transport，已创建 SimulatedTransport");
            }
        }

        private void Start()
        {
            Debug.Log($"[MessageDispatcher] 启动，DispatchInterval={DispatchInterval}s");
            _transport.Initialize();
            _transport.OnConnected += () => Debug.Log("[MessageDispatcher] Transport 已连接");
            _transport.OnDisconnected += () => Debug.Log("[MessageDispatcher] Transport 已断开");
            _transport.OnError += ex => Debug.LogError($"[MessageDispatcher] 传输错误: {ex}");
            _transport.OnMessageReceived += msg =>
            {
                Debug.Log($"[MessageDispatcher] 收到消息入队: EventType={msg.EventType}, Sender={msg.SenderId}");
                DataWarehouse.Instance.AddMessage(msg);
            };
            StartCoroutine(DispatchLoop());
        }

        private void OnDestroy()
        {
            try { _transport.Shutdown(); }
            catch (Exception ex) { Debug.LogError($"[MessageDispatcher] 关闭 Transport 错误: {ex}"); }
        }

        private IEnumerator DispatchLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(DispatchInterval);
                var batch = DataWarehouse.Instance.GetAndClearAll();
                if (batch.Count > 0)
                {
                    Debug.Log($"[MessageDispatcher] 分发批次，数量={batch.Count}");
                    foreach (var msg in batch)
                    {
                        try
                        {
                            Debug.Log($"[MessageDispatcher] 分发 EventType={msg.EventType}");
                            DispatchMessage(msg);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[MessageDispatcher] 分发错误: {ex}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 将消息分发给 QuestionManagerController
        /// </summary>
        private void DispatchMessage(NetworkMessage msg)
        {
            if (QuestionController == null)
                return;

            switch (msg.EventType)
            {
                case NetworkEventType.RequestDrawQuestion:
                    Debug.Log("[MessageDispatcher] 远程触发出题");
                    QuestionController.LoadNextQuestion();
                    break;

                case NetworkEventType.BroadcastQuestion:
                    // 只有当消息来自“其他玩家”时，才触发 LoadNextQuestion
                    if (msg.SenderId != QuestionController.LocalPlayerId)
                    {
                        QuestionController.LoadNextQuestion();
                    }
                    break;


                case NetworkEventType.AnswerResult:
                    Debug.Log("[MessageDispatcher] 收到判题结果，调用 OnRemoteAnswerResult");
                    bool isCorrect = false;
                    try { isCorrect = (bool)msg.Payload; }
                    catch { Debug.LogWarning("[MessageDispatcher] 无法解析 Payload 为 bool"); }
                    QuestionController.OnRemoteAnswerResult(isCorrect);
                    break;

                default:
                    Debug.LogWarning($"[MessageDispatcher] 未处理 EventType={msg.EventType}");
                    break;
            }
        }
    }
}