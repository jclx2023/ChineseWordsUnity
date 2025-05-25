using UnityEngine;
using Riptide;
using Core.Network;
using System.Collections.Generic;
using System.Linq;

namespace Core.Network
{
    /// <summary>
    /// 主机端游戏逻辑管理器
    /// 负责：题目生成、答案验证、回合管理、玩家状态管理
    /// 只在Host模式下激活
    /// </summary>
    public class HostGameManager : MonoBehaviour
    {
        [Header("游戏配置")]
        [SerializeField] private float questionTimeLimit = 30f;
        [SerializeField] private int initialPlayerHealth = 100;
        [SerializeField] private int damagePerWrongAnswer = 20;

        public static HostGameManager Instance { get; private set; }

        // 游戏状态
        private Dictionary<ushort, PlayerGameState> playerStates;
        private ushort currentTurnPlayerId;
        private bool gameInProgress;
        private NetworkQuestionData currentQuestion;

        // 题目生成（复用现有逻辑）
        private NetworkQuestionManagerController questionController;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 只在Host模式下初始化
            if (NetworkManager.Instance?.IsHost == true)
            {
                InitializeHostGame();
            }
        }

        /// <summary>
        /// 初始化主机游戏
        /// </summary>
        private void InitializeHostGame()
        {
            Debug.Log("初始化主机游戏逻辑");

            playerStates = new Dictionary<ushort, PlayerGameState>();
            questionController = FindObjectOfType<NetworkQuestionManagerController>();

            // 注册网络事件
            NetworkManager.OnPlayerJoined += OnPlayerJoined;
            NetworkManager.OnPlayerLeft += OnPlayerLeft;

            // 注册消息处理器
            RegisterMessageHandlers();

            // 添加主机自己为玩家
            if (NetworkManager.Instance.IsClient)
            {
                AddPlayer(NetworkManager.Instance.ClientId);
            }
        }

        /// <summary>
        /// 注册消息处理器
        /// </summary>
        private void RegisterMessageHandlers()
        {
            // 这些方法需要添加到EnhancedNetworkManager或单独的消息处理器中
        }

        /// <summary>
        /// 添加玩家
        /// </summary>
        private void AddPlayer(ushort playerId)
        {
            if (!playerStates.ContainsKey(playerId))
            {
                playerStates[playerId] = new PlayerGameState
                {
                    playerId = playerId,
                    health = initialPlayerHealth,
                    isAlive = true,
                    score = 0
                };

                Debug.Log($"添加玩家到游戏: {playerId}, 当前玩家数: {playerStates.Count}");

                // 如果是第一个玩家且游戏未开始，开始游戏
                if (playerStates.Count == 1 && !gameInProgress)
                {
                    StartHostGame();
                }
            }
        }

        /// <summary>
        /// 移除玩家
        /// </summary>
        private void RemovePlayer(ushort playerId)
        {
            if (playerStates.ContainsKey(playerId))
            {
                playerStates.Remove(playerId);
                Debug.Log($"移除玩家: {playerId}, 剩余玩家数: {playerStates.Count}");

                // 如果当前回合的玩家离开，切换到下一个玩家
                if (currentTurnPlayerId == playerId)
                {
                    NextPlayerTurn();
                }

                // 如果没有玩家了，停止游戏
                if (playerStates.Count == 0)
                {
                    StopHostGame();
                }
            }
        }

        /// <summary>
        /// 开始主机游戏
        /// </summary>
        private void StartHostGame()
        {
            Debug.Log("主机开始游戏");
            gameInProgress = true;

            // 选择第一个玩家开始
            currentTurnPlayerId = playerStates.Keys.FirstOrDefault();
            BroadcastPlayerTurnChanged(currentTurnPlayerId);

            // 生成第一题
            GenerateAndSendQuestion();
        }

        /// <summary>
        /// 停止主机游戏
        /// </summary>
        private void StopHostGame()
        {
            Debug.Log("主机停止游戏");
            gameInProgress = false;
            currentQuestion = null;
        }

        /// <summary>
        /// 生成并发送题目
        /// </summary>
        private void GenerateAndSendQuestion()
        {
            if (!gameInProgress)
                return;

            Debug.Log("生成新题目");

            // 使用现有的题目生成逻辑
            var questionType = SelectRandomQuestionType();
            currentQuestion = GenerateQuestionData(questionType);

            if (currentQuestion != null)
            {
                // 广播题目给所有客户端
                BroadcastQuestion(currentQuestion);
                Debug.Log($"题目已发送: {questionType}");
            }
            else
            {
                Debug.LogError("题目生成失败");
            }
        }

        /// <summary>
        /// 选择随机题目类型
        /// </summary>
        private QuestionType SelectRandomQuestionType()
        {
            // 复用NetworkQuestionManagerController的权重逻辑
            var weights = questionController?.TypeWeights ?? new Dictionary<QuestionType, float>();

            if (weights.Count == 0)
            {
                // 默认权重
                return QuestionType.ExplanationChoice;
            }

            float total = weights.Values.Sum();
            float random = Random.value * total;
            float accumulator = 0f;

            foreach (var pair in weights)
            {
                accumulator += pair.Value;
                if (random <= accumulator)
                    return pair.Key;
            }

            return weights.Keys.First();
        }

        /// <summary>
        /// 生成题目数据（简化版，实际需要连接数据库）
        /// </summary>
        private NetworkQuestionData GenerateQuestionData(QuestionType questionType)
        {
            // 这里应该根据题目类型从数据库生成真实的题目
            // 为了演示，创建一个示例题目
            var questionData = new NetworkQuestionData
            {
                questionType = questionType,
                questionText = $"示例{questionType}题目",
                correctAnswer = "示例答案",
                options = new string[] { "选项A", "选项B", "选项C", "选项D" },
                timeLimit = questionTimeLimit
            };

            return questionData;
        }

        /// <summary>
        /// 广播题目
        /// </summary>
        private void BroadcastQuestion(NetworkQuestionData question)
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SendQuestion);
            message.AddBytes(question.Serialize());
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// <summary>
        /// 广播玩家回合变更
        /// </summary>
        private void BroadcastPlayerTurnChanged(ushort playerId)
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.PlayerTurnChanged);
            message.AddUShort(playerId);
            NetworkManager.Instance.BroadcastMessage(message);

            Debug.Log($"广播回合变更: 轮到玩家 {playerId}");
        }

        /// <summary>
        /// 广播血量更新
        /// </summary>
        private void BroadcastHealthUpdate(ushort playerId, int newHealth)
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.HealthUpdate);
            message.AddUShort(playerId);
            message.AddInt(newHealth);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// <summary>
        /// 处理玩家答案
        /// </summary>
        public void HandlePlayerAnswer(ushort playerId, string answer)
        {
            if (!gameInProgress || playerId != currentTurnPlayerId || currentQuestion == null)
            {
                Debug.LogWarning($"无效的答案提交: playerId={playerId}, currentTurn={currentTurnPlayerId}");
                return;
            }

            Debug.Log($"处理玩家 {playerId} 的答案: {answer}");

            // 验证答案
            bool isCorrect = ValidateAnswer(answer, currentQuestion);

            // 更新玩家状态
            if (playerStates.ContainsKey(playerId))
            {
                var playerState = playerStates[playerId];

                if (!isCorrect)
                {
                    playerState.health -= damagePerWrongAnswer;
                    if (playerState.health <= 0)
                    {
                        playerState.health = 0;
                        playerState.isAlive = false;
                    }

                    // 广播血量更新
                    BroadcastHealthUpdate(playerId, playerState.health);
                }
                else
                {
                    playerState.score += 10; // 答对得分
                }
            }

            // 广播答题结果
            BroadcastAnswerResult(isCorrect, currentQuestion.correctAnswer);

            // 切换到下一个玩家
            NextPlayerTurn();
        }

        /// <summary>
        /// 验证答案
        /// </summary>
        private bool ValidateAnswer(string answer, NetworkQuestionData question)
        {
            if (question == null || string.IsNullOrEmpty(answer))
                return false;

            // 简单的字符串比较，实际可能需要更复杂的逻辑
            return answer.Trim().Equals(question.correctAnswer.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 广播答题结果
        /// </summary>
        private void BroadcastAnswerResult(bool isCorrect, string correctAnswer)
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.AnswerResult);
            message.AddBool(isCorrect);
            message.AddString(correctAnswer);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// <summary>
        /// 切换到下一个玩家
        /// </summary>
        private void NextPlayerTurn()
        {
            var alivePlayers = playerStates.Where(p => p.Value.isAlive).Select(p => p.Key).ToList();

            if (alivePlayers.Count == 0)
            {
                Debug.Log("游戏结束：没有存活的玩家");
                StopHostGame();
                return;
            }

            if (alivePlayers.Count == 1)
            {
                Debug.Log($"游戏结束：玩家 {alivePlayers[0]} 获胜");
                // 这里可以发送游戏结束消息
                return;
            }

            // 找到下一个存活的玩家
            int currentIndex = alivePlayers.IndexOf(currentTurnPlayerId);
            int nextIndex = (currentIndex + 1) % alivePlayers.Count;
            currentTurnPlayerId = alivePlayers[nextIndex];

            // 广播回合变更并生成新题目
            BroadcastPlayerTurnChanged(currentTurnPlayerId);

            // 短暂延迟后生成新题目
            Invoke(nameof(GenerateAndSendQuestion), 2f);
        }

        #region 网络事件处理

        private void OnPlayerJoined(ushort playerId)
        {
            AddPlayer(playerId);
        }

        private void OnPlayerLeft(ushort playerId)
        {
            RemovePlayer(playerId);
        }

        #endregion

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnPlayerLeft;
            }
        }
    }

    /// <summary>
    /// 玩家游戏状态
    /// </summary>
    [System.Serializable]
    public class PlayerGameState
    {
        public ushort playerId;
        public int health;
        public bool isAlive;
        public int score;
        public string playerName;
    }
}