using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// ��Ϣ����������ʱ��ȡ���زֿ�����紫�����Ϣ��
    /// ���ַ��� QuestionManagerController �� PlayerHealthManager��
    /// </summary>
    [DisallowMultipleComponent]
    public class MessageDispatcher : MonoBehaviour
    {
        [Header("���ȼ������)")]
        [Tooltip("ÿ����������ȡ���ַ�һ����Ϣ")]
        public float DispatchInterval = 0.1f;

        [Header("Transport ����������� PhotonTransport/CustomTransport������ʹ�� SimulatedTransport")]
        public MonoBehaviour TransportComponent;

        [Header("QuestionManagerController ����������룬�����Զ����ҳ����е�ʵ��")]
        public QuestionManagerController QuestionController;

        private INetworkTransport _transport;

        private void Awake()
        {
            // ��ȡ��ע�� QuestionManagerController
            if (QuestionController == null)
            {
                QuestionController = FindObjectOfType<QuestionManagerController>();
                if (QuestionController == null)
                    Debug.LogError("[MessageDispatcher] δ�ҵ� QuestionManagerController�����ֶ����������");
            }

            // ��ȡ�򴴽� Transport
            if (TransportComponent is INetworkTransport custom)
            {
                _transport = custom;
                Debug.Log($"[MessageDispatcher] ʹ��ע�� Transport: {TransportComponent.GetType().Name}");
            }
            else
            {
                _transport = new SimulatedTransport();
                Debug.Log("[MessageDispatcher] δָ�� Transport���Ѵ��� SimulatedTransport");
            }
        }

        private void Start()
        {
            Debug.Log($"[MessageDispatcher] ������DispatchInterval={DispatchInterval}s");
            _transport.Initialize();
            _transport.OnConnected += () => Debug.Log("[MessageDispatcher] Transport ������");
            _transport.OnDisconnected += () => Debug.Log("[MessageDispatcher] Transport �ѶϿ�");
            _transport.OnError += ex => Debug.LogError($"[MessageDispatcher] �������: {ex}");
            _transport.OnMessageReceived += msg =>
            {
                Debug.Log($"[MessageDispatcher] �յ���Ϣ���: EventType={msg.EventType}, Sender={msg.SenderId}");
                DataWarehouse.Instance.AddMessage(msg);
            };
            StartCoroutine(DispatchLoop());
        }

        private void OnDestroy()
        {
            try { _transport.Shutdown(); }
            catch (Exception ex) { Debug.LogError($"[MessageDispatcher] �ر� Transport ����: {ex}"); }
        }

        private IEnumerator DispatchLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(DispatchInterval);
                var batch = DataWarehouse.Instance.GetAndClearAll();
                if (batch.Count > 0)
                {
                    Debug.Log($"[MessageDispatcher] �ַ����Σ�����={batch.Count}");
                    foreach (var msg in batch)
                    {
                        try
                        {
                            Debug.Log($"[MessageDispatcher] �ַ� EventType={msg.EventType}");
                            DispatchMessage(msg);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[MessageDispatcher] �ַ�����: {ex}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// ����Ϣ�ַ��� QuestionManagerController
        /// </summary>
        private void DispatchMessage(NetworkMessage msg)
        {
            if (QuestionController == null)
                return;

            switch (msg.EventType)
            {
                case NetworkEventType.RequestDrawQuestion:
                    Debug.Log("[MessageDispatcher] Զ�̴�������");
                    QuestionController.LoadNextQuestion();
                    break;

                case NetworkEventType.BroadcastQuestion:
                    // ֻ�е���Ϣ���ԡ�������ҡ�ʱ���Ŵ��� LoadNextQuestion
                    if (msg.SenderId != QuestionController.LocalPlayerId)
                    {
                        QuestionController.LoadNextQuestion();
                    }
                    break;


                case NetworkEventType.AnswerResult:
                    Debug.Log("[MessageDispatcher] �յ������������� OnRemoteAnswerResult");
                    bool isCorrect = false;
                    try { isCorrect = (bool)msg.Payload; }
                    catch { Debug.LogWarning("[MessageDispatcher] �޷����� Payload Ϊ bool"); }
                    QuestionController.OnRemoteAnswerResult(isCorrect);
                    break;

                default:
                    Debug.LogWarning($"[MessageDispatcher] δ���� EventType={msg.EventType}");
                    break;
            }
        }
    }
}