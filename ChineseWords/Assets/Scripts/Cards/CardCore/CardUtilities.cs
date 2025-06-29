using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cards.Core;

namespace Cards.Core
{
    /// <summary>
    /// ����ϵͳ�����ࣨ���°棩
    /// ����ͨ�ù��߷�������֤������־ϵͳ
    /// ֧���µ�12�ſ���Ч��ϵͳ
    /// </summary>
    public static class CardUtilities
    {
        #region ����ͳ鿨����

        /// <summary>
        /// ���ݿ���Ȩ�������ȡ����
        /// </summary>
        /// <param name="allCards">���п��ÿ���</param>
        /// <param name="excludeIds">�ų��Ŀ���ID</param>
        /// <returns>���еĿ�������</returns>
        public static CardData DrawRandomCard(List<CardData> allCards, HashSet<int> excludeIds = null)
        {
            if (allCards == null || allCards.Count == 0)
            {
                LogError("�鿨ʧ�ܣ������б�Ϊ��");
                return null;
            }

            // �����ų��Ŀ���
            var availableCards = excludeIds == null
                ? allCards
                : allCards.Where(card => !excludeIds.Contains(card.cardId)).ToList();

            if (availableCards.Count == 0)
            {
                LogWarning("�鿨ʧ�ܣ�û�п��õĿ���");
                return null;
            }

            // ������Ȩ��
            float totalWeight = 0f;
            foreach (var card in availableCards)
            {
                totalWeight += card.drawWeight;
            }

            if (totalWeight <= 0)
            {
                LogWarning("�鿨ʧ�ܣ���Ȩ��Ϊ0�����ѡ��һ�ſ���");
                return availableCards[UnityEngine.Random.Range(0, availableCards.Count)];
            }

            // �����ȡ
            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var card in availableCards)
            {
                currentWeight += card.drawWeight;
                if (randomValue <= currentWeight)
                {
                    LogDebug($"���п���: {card.cardName} (Ȩ��: {card.drawWeight})");
                    return card;
                }
            }

            // ���׷������һ��
            return availableCards.Last();
        }

        /// <summary>
        /// �����鿨
        /// </summary>
        /// <param name="allCards">���п��ÿ���</param>
        /// <param name="count">�鿨����</param>
        /// <param name="allowDuplicates">�Ƿ������ظ�</param>
        /// <returns>���еĿ���ID�б�</returns>
        public static List<int> DrawMultipleCards(List<CardData> allCards, int count, bool allowDuplicates = true)
        {
            var result = new List<int>();
            var drawnIds = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                var excludeIds = allowDuplicates ? null : drawnIds;
                var drawnCard = DrawRandomCard(allCards, excludeIds);

                if (drawnCard != null)
                {
                    result.Add(drawnCard.cardId);
                    drawnIds.Add(drawnCard.cardId);
                }
                else
                {
                    LogWarning($"��{i + 1}�γ鿨ʧ��");
                    break;
                }
            }

            LogDebug($"�����鿨��ɣ����鵽{result.Count}�ſ���");
            return result;
        }

        /// <summary>
        /// �����б�˳��
        /// </summary>
        public static void Shuffle<T>(List<T> list)
        {
            if (list == null) return;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }

        /// <summary>
        /// ���ʼ��㹤��
        /// </summary>
        /// <param name="chance">����ֵ (0.0 - 1.0)</param>
        /// <returns>�Ƿ�����</returns>
        public static bool RollProbability(float chance)
        {
            return UnityEngine.Random.Range(0f, 1f) <= chance;
        }

        #endregion

        #region ��֤��

        /// <summary>
        /// ��֤��������
        /// </summary>
        public static class Validator
        {
            /// <summary>
            /// ��֤����ʹ������Ļ�����Ч��
            /// </summary>
            public static bool ValidateCardUseRequest(CardUseRequest request, CardData cardData)
            {
                if (request == null)
                {
                    LogError("����ʹ������Ϊ��");
                    return false;
                }

                if (cardData == null)
                {
                    LogError("��������Ϊ��");
                    return false;
                }

                if (request.userId <= 0)
                {
                    LogError("��Ч���û�ID");
                    return false;
                }

                if (request.cardId != cardData.cardId)
                {
                    LogError("�����еĿ���ID�����ݲ�ƥ��");
                    return false;
                }

                return true;
            }

            /// <summary>
            /// ��֤Ŀ��ѡ�񣨸��°棬֧���¿��ƣ�
            /// </summary>
            public static bool ValidateTargetSelection(CardUseRequest request, CardData cardData, List<int> availablePlayers)
            {
                if (!ValidateCardUseRequest(request, cardData))
                {
                    return false;
                }

                // ����Ƿ���Ҫѡ��Ŀ��
                if (cardData.NeedsTargetSelection)
                {
                    if (request.targetPlayerId <= 0)
                    {
                        LogError("ָ���Ϳ�����Ҫѡ����ЧĿ��");
                        return false;
                    }

                    if (availablePlayers != null && !availablePlayers.Contains(request.targetPlayerId))
                    {
                        LogError("ѡ���Ŀ����Ҳ��ڿ����б���");
                        return false;
                    }

                    // ָ���Ϳ��Ʋ���ѡ���Լ���ΪĿ�꣨����������ƣ�
                    if (request.targetPlayerId == request.userId)
                    {
                        LogError("ָ���Ϳ��Ʋ���ѡ���Լ���ΪĿ��");
                        return false;
                    }

                    // ������֤������¿��Ƶ�Ŀ��ѡ��
                    switch (cardData.cardId)
                    {
                        case 9: // ��ֽ�ţ������˺�
                        case 10: // ��ʱ��
                        case 11: // ������Ƥ
                            // ��Щ������Ҫѡ���������ŵ����
                            if (request.targetPlayerId == request.userId)
                            {
                                LogError($"{cardData.cardName}���ܶ��Լ�ʹ��");
                                return false;
                            }
                            break;
                    }
                }

                return true;
            }

            /// <summary>
            /// ��֤����Ƿ����ʹ�øÿ��ƣ����°棩
            /// </summary>
            public static bool ValidatePlayerCanUseCard(int playerId, CardData cardData, PlayerCardState playerState, bool isMyTurn)
            {
                if (cardData == null || playerState == null)
                {
                    LogError("�������ݻ����״̬Ϊ��");
                    return false;
                }

                // �������Ƿ�ӵ�иÿ���
                if (!playerState.handCards.Contains(cardData.cardId))
                {
                    LogError("��Ҳ�ӵ�иÿ���");
                    return false;
                }

                // ��鱾���Ƿ���ʹ�ù�����
                if (!playerState.canUseCardThisRound)
                {
                    LogError("������ʹ�ù����ƣ���ȴ���һ��");
                    return false;
                }

                // ���غ����ƣ��󲿷ֿ��ƶ������ڷ��Լ��غ�ʹ�ã�
                if (!cardData.canUseWhenNotMyTurn && !isMyTurn)
                {
                    LogError("�ÿ���ֻ�����Լ��غ�ʱʹ��");
                    return false;
                }

                return true;
            }

            /// <summary>
            /// ��֤��Ϸ״̬�Ƿ�����ʹ�ÿ���
            /// </summary>
            public static bool ValidateGameState(bool isGameActive)
            {
                if (!isGameActive)
                {
                    LogError("��Ϸδ����״̬���޷�ʹ�ÿ���");
                    return false;
                }

                return true;
            }

            /// <summary>
            /// ��֤����Ч���Ƿ����ִ�У�������
            /// </summary>
            public static bool ValidateCardEffect(CardData cardData, int userId, int targetPlayerId = -1)
            {
                if (cardData == null)
                {
                    LogError("��������Ϊ�գ��޷���֤Ч��");
                    return false;
                }

                // ����Ч�����ͽ���������֤
                switch (cardData.effectType)
                {
                    case EffectType.ProbabilityDamage: // ��ֽ��
                        if (targetPlayerId <= 0 || targetPlayerId == userId)
                        {
                            LogError("�����˺�����Ҫѡ����Ч�����������ΪĿ��");
                            return false;
                        }
                        break;

                    case EffectType.ReduceTime: // ��ʱ��
                        if (targetPlayerId <= 0 || targetPlayerId == userId)
                        {
                            LogError("��ʱ����Ҫѡ����Ч�����������ΪĿ��");
                            return false;
                        }
                        break;

                    case EffectType.GetCard:
                        // ���ݿ���ID�������⴦��
                        if (cardData.cardId == 11) // ������Ƥ
                        {
                            if (targetPlayerId <= 0 || targetPlayerId == userId)
                            {
                                LogError("������Ƥ��Ҫѡ����Ч�����������ΪĿ��");
                                return false;
                            }
                        }
                        break;
                }

                return true;
            }
        }

        #endregion

        #region �غϹ�����

        /// <summary>
        /// �غϹ�������
        /// </summary>
        public static class RoundManager
        {
            /// <summary>
            /// �����ض���ҵĿ���ʹ�û��ᣨ�ڸ���Ҵ�����ɺ���ã�
            /// </summary>
            public static void ResetPlayerCardUsage(PlayerCardState playerState)
            {
                if (playerState != null)
                {
                    playerState.ResetForNewRound();
                    LogDebug($"���{playerState.playerId}������ɣ�����ʹ�û���������");
                    CardEvents.OnPlayerCardUsageReset?.Invoke(playerState.playerId);
                }
            }

            /// <summary>
            /// ����ָ��ID��ҵĿ���ʹ�û���
            /// </summary>
            public static void ResetPlayerCardUsage(List<PlayerCardState> allPlayers, int playerId)
            {
                var player = allPlayers?.Find(p => p.playerId == playerId);
                if (player != null)
                {
                    ResetPlayerCardUsage(player);
                }
                else
                {
                    LogWarning($"δ�ҵ����{playerId}���޷������俨��ʹ�û���");
                }
            }

            /// <summary>
            /// �����ұ�����ʹ�ÿ���
            /// </summary>
            public static void MarkPlayerUsedCard(PlayerCardState playerState)
            {
                if (playerState != null)
                {
                    playerState.MarkCardUsedThisRound();
                    LogDebug($"���{playerState.playerId}���ֿ���ʹ�û���������");
                }
            }

            /// <summary>
            /// �������Ƿ���ʹ�ÿ���
            /// </summary>
            public static bool CanPlayerUseCard(PlayerCardState playerState)
            {
                return playerState?.canUseCardThisRound ?? false;
            }

            /// <summary>
            /// ��ȡ��ʹ�ÿ��Ƶ�����б�
            /// </summary>
            public static List<int> GetPlayersWhoCanUseCards(List<PlayerCardState> allPlayers)
            {
                var result = new List<int>();
                if (allPlayers != null)
                {
                    foreach (var player in allPlayers)
                    {
                        if (CanPlayerUseCard(player))
                        {
                            result.Add(player.playerId);
                        }
                    }
                }
                return result;
            }
        }

        #endregion

        #region ���߷��������°棩

        /// <summary>
        /// ����Ч�����ͻ�ȡĬ��Ч�����������°棬֧����Ч����
        /// </summary>
        public static string GetEffectDescription(EffectType effectType, float effectValue)
        {
            return effectType switch
            {
                EffectType.AddTime => $"����{effectValue}�����ʱ��",
                EffectType.ReduceTime => $"����{effectValue}�����ʱ��",
                EffectType.Heal => $"�ظ�{effectValue}������ֵ",
                EffectType.GroupHeal => $"������һظ�{effectValue}������ֵ", // ����
                EffectType.Damage => effectValue > 1 ? $"���{effectValue}���˺�" : "����˺�",
                EffectType.ProbabilityDamage => $"{effectValue * 100}%�������1���˺�", // ����
                EffectType.SkipQuestion => "������һ��",
                EffectType.ChengYuChain => "��һ��Ϊ�������",
                EffectType.JudgeQuestion => "��һ��Ϊ�ж���",
                EffectType.GetCard => effectValue > 0 ? $"���{effectValue}�ſ���" : "͵ȡһ�ſ���",
                _ => "δ֪Ч��"
            };
        }

        /// <summary>
        /// ��ȡ����Ч������ϸ����������ֵ��ʾ��
        /// </summary>
        public static string GetDetailedCardDescription(CardData cardData)
        {
            if (cardData == null) return "δ֪����";

            string baseDescription = cardData.description;

            // �����������ʾЧ��ֵ���������ֵ��Ϣ
            if (cardData.showEffectValue && cardData.effectValue != 0)
            {
                switch (cardData.effectType)
                {
                    case EffectType.Heal:
                    case EffectType.GroupHeal:
                        baseDescription += $" [{cardData.effectValue}������ֵ]";
                        break;

                    case EffectType.AddTime:
                    case EffectType.ReduceTime:
                        baseDescription += $" [{cardData.effectValue}��]";
                        break;

                    case EffectType.Damage:
                        baseDescription += $" [��{cardData.effectValue}]";
                        break;

                    case EffectType.ProbabilityDamage:
                        baseDescription += $" [{cardData.effectValue * 100}%������]";
                        break;

                    case EffectType.GetCard:
                        if (cardData.effectValue > 0)
                            baseDescription += $" [{cardData.effectValue}��]";
                        break;
                }
            }

            return baseDescription;
        }

        /// <summary>
        /// ����ϡ�жȻ�ȡ��ɫ
        /// </summary>
        public static Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => Color.white,
                CardRarity.Uncommon => Color.green,
                CardRarity.Rare => Color.blue,
                CardRarity.Epic => new Color(0.6f, 0.3f, 0.9f), // ��ɫ
                CardRarity.Legendary => new Color(1f, 0.6f, 0f), // ��ɫ
                _ => Color.gray
            };
        }

        /// <summary>
        /// ��ȡϡ�жȵ���������
        /// </summary>
        public static string GetRarityName(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => "��ͨ",
                CardRarity.Uncommon => "������",
                CardRarity.Rare => "ϡ��",
                CardRarity.Epic => "ʷʫ",
                CardRarity.Legendary => "��˵",
                _ => "δ֪"
            };
        }

        /// <summary>
        /// ��ȡ�������͵���������
        /// </summary>
        public static string GetCardTypeName(CardType cardType)
        {
            return cardType switch
            {
                CardType.SelfTarget => "�Է���",
                CardType.PlayerTarget => "ָ����",
                CardType.Special => "������",
                _ => "δ֪"
            };
        }

        /// <summary>
        /// ��ȡЧ�����͵���������
        /// </summary>
        public static string GetEffectTypeName(EffectType effectType)
        {
            return effectType switch
            {
                EffectType.AddTime => "��ʱ",
                EffectType.ReduceTime => "��ʱ",
                EffectType.Heal => "��Ѫ",
                EffectType.GroupHeal => "Ⱥ���Ѫ",
                EffectType.Damage => "�ӱ��˺�",
                EffectType.ProbabilityDamage => "�����˺�",
                EffectType.SkipQuestion => "������Ŀ",
                EffectType.ChengYuChain => "�������",
                EffectType.JudgeQuestion => "�ж���",
                EffectType.GetCard => "���Ʋ���",
                _ => "δ֪Ч��"
            };
        }

        /// <summary>
        /// ������������֮��ľ���
        /// </summary>
        public static float CalculateDistance(Vector3 from, Vector3 to)
        {
            return Vector3.Distance(from, to);
        }

        /// <summary>
        /// ����ΨһID
        /// </summary>
        public static string GenerateUniqueId()
        {
            return Guid.NewGuid().ToString("N")[..8]; // ȡǰ8λ
        }

        /// <summary>
        /// ��ȫת���ַ���Ϊ����
        /// </summary>
        public static int SafeParseInt(string str, int defaultValue = 0)
        {
            return int.TryParse(str, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// ��ȫת���ַ���Ϊ������
        /// </summary>
        public static float SafeParseFloat(string str, float defaultValue = 0f)
        {
            return float.TryParse(str, out float result) ? result : defaultValue;
        }

        /// <summary>
        /// ������ֵ��ָ����Χ��
        /// </summary>
        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;
            return value;
        }

        /// <summary>
        /// ��ʽ��ʱ����ʾ
        /// </summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds:F1}��";
            }
            else
            {
                int minutes = Mathf.FloorToInt(seconds / 60);
                int remainingSeconds = Mathf.FloorToInt(seconds % 60);
                return $"{minutes}��{remainingSeconds}��";
            }
        }

        /// <summary>
        /// ��ʽ��������ʾ
        /// </summary>
        public static string FormatProbability(float probability)
        {
            return $"{(probability * 100):F0}%";
        }

        #endregion

        #region ���ϲ�������

        /// <summary>
        /// ��ȫ�ش��б����Ƴ�Ԫ��
        /// </summary>
        public static bool SafeRemove<T>(List<T> list, T item)
        {
            if (list == null || item == null) return false;
            return list.Remove(item);
        }

        /// <summary>
        /// ��ȫ�����б����Ԫ�أ������ظ���
        /// </summary>
        public static bool SafeAdd<T>(List<T> list, T item, bool allowDuplicates = true)
        {
            if (list == null || item == null) return false;

            if (!allowDuplicates && list.Contains(item))
            {
                return false;
            }

            list.Add(item);
            return true;
        }

        /// <summary>
        /// ��ȡ�б��е����Ԫ��
        /// </summary>
        public static T GetRandomElement<T>(List<T> list)
        {
            if (list == null || list.Count == 0) return default(T);
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        /// <summary>
        /// ��ȫ�ػ�ȡ�ֵ�ֵ
        /// </summary>
        public static TValue SafeGetValue<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default(TValue))
        {
            if (dict == null) return defaultValue;
            return dict.ContainsKey(key) ? dict[key] : defaultValue;
        }

        /// <summary>
        /// ����ָ���Ϳ���
        /// </summary>
        public static List<CardData> FilterPlayerTargetCards(List<CardData> cards)
        {
            return cards?.Where(card => card.cardType == CardType.PlayerTarget).ToList() ?? new List<CardData>();
        }

        /// <summary>
        /// �����Է��Ϳ���
        /// </summary>
        public static List<CardData> FilterSelfTargetCards(List<CardData> cards)
        {
            return cards?.Where(card => card.cardType == CardType.SelfTarget).ToList() ?? new List<CardData>();
        }

        #endregion

        #region ����Ч���������ߣ�������

        /// <summary>
        /// ���������Ƿ��Ӱ���������
        /// </summary>
        public static bool WillAffectOtherPlayers(CardData cardData)
        {
            if (cardData == null) return false;

            switch (cardData.effectType)
            {
                case EffectType.GroupHeal: // ���ջ��ݣ�Ӱ���������
                case EffectType.Damage: // �����۱ʣ�Ӱ���´δ������
                    return true;

                case EffectType.ProbabilityDamage: // ��ֽ�ţ�ֱ�ӹ���Ŀ��
                case EffectType.ReduceTime: // ��ʱ����Ӱ��Ŀ�����
                    return true;

                case EffectType.GetCard:
                    return cardData.cardId == 11; // ������Ƥ��Ӱ��Ŀ�����

                default:
                    return false;
            }
        }

        /// <summary>
        /// ��ȡ���Ƶ�Ӱ�췶Χ
        /// </summary>
        public static string GetCardImpactScope(CardData cardData)
        {
            if (cardData == null) return "��Ӱ��";

            switch (cardData.targetType)
            {
                case TargetType.Self:
                    return "�Լ�";
                case TargetType.SinglePlayer:
                    return "����Ŀ��";
                case TargetType.AllPlayers:
                    return "�������";
                case TargetType.AllOthers:
                    return "�������";
                case TargetType.Random:
                    return "������";
                default:
                    return "δ֪��Χ";
            }
        }

        /// <summary>
        /// �жϿ����Ƿ�Ϊ�����Կ���
        /// </summary>
        public static bool IsAggressiveCard(CardData cardData)
        {
            if (cardData == null) return false;

            switch (cardData.effectType)
            {
                case EffectType.ProbabilityDamage: // ��ֽ��
                case EffectType.ReduceTime: // ��ʱ��
                case EffectType.Damage: // �����۱�
                    return true;

                case EffectType.GetCard:
                    return cardData.cardId == 11; // ������Ƥ

                default:
                    return false;
            }
        }

        /// <summary>
        /// �жϿ����Ƿ�Ϊ������/�����Կ���
        /// </summary>
        public static bool IsSupportiveCard(CardData cardData)
        {
            if (cardData == null) return false;

            switch (cardData.effectType)
            {
                case EffectType.Heal: // ţ�̺�
                case EffectType.GroupHeal: // ���ջ���
                case EffectType.AddTime: // ������
                case EffectType.SkipQuestion: // �����
                case EffectType.ChengYuChain: // �������
                case EffectType.JudgeQuestion: // �ж���
                    return true;

                case EffectType.GetCard:
                    return cardData.cardId == 8 || cardData.cardId == 12; // ���ⲹϰ��һ�з۱�

                default:
                    return false;
            }
        }

        #endregion

        #region ��־ϵͳ

        private static bool enableLogs = true;

        /// <summary>
        /// ������־����
        /// </summary>
        public static void SetLogEnabled(bool enabled)
        {
            enableLogs = enabled;
        }

        /// <summary>
        /// ������־
        /// </summary>
        public static void LogDebug(string message)
        {
            if (enableLogs)
            {
                Debug.Log($"{CardConstants.LOG_TAG} {message}");
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        public static void LogWarning(string message)
        {
            if (enableLogs)
            {
                Debug.LogWarning($"{CardConstants.LOG_TAG} {message}");
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        public static void LogError(string message)
        {
            if (enableLogs)
            {
                Debug.LogError($"{CardConstants.LOG_TAG} {message}");
            }
        }

        /// <summary>
        /// ����ǩ����־
        /// </summary>
        public static void LogWithTag(string tag, string message, LogType logType = LogType.Log)
        {
            if (!enableLogs) return;

            string fullMessage = $"{tag} {message}";
            switch (logType)
            {
                case LogType.Log:
                    Debug.Log(fullMessage);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(fullMessage);
                    break;
                case LogType.Error:
                    Debug.LogError(fullMessage);
                    break;
            }
        }

        /// <summary>
        /// ��¼����ʹ����־����ǿ�棩
        /// </summary>
        public static void LogCardUse(int playerId, int cardId, string cardName, string result, int targetPlayerId = -1)
        {
            string targetInfo = targetPlayerId > 0 ? $" -> Ŀ�����{targetPlayerId}" : "";
            LogWithTag(CardConstants.LOG_TAG_EFFECT,
                $"���{playerId}ʹ�ÿ���[{cardId}]{cardName}{targetInfo} -> {result}");
        }

        /// <summary>
        /// ��¼�����¼���־
        /// </summary>
        public static void LogNetworkEvent(string eventName, string details)
        {
            LogWithTag(CardConstants.LOG_TAG_NETWORK,
                $"{eventName}: {details}");
        }

        /// <summary>
        /// ��¼UI�¼���־
        /// </summary>
        public static void LogUIEvent(string eventName, string details)
        {
            LogWithTag(CardConstants.LOG_TAG_UI,
                $"{eventName}: {details}");
        }

        #endregion

        #region �����Ż�����

        /// <summary>
        /// ����ؼ�ʵ��
        /// </summary>
        public static class ObjectPool<T> where T : new()
        {
            private static readonly Stack<T> pool = new Stack<T>();

            public static T Get()
            {
                return pool.Count > 0 ? pool.Pop() : new T();
            }

            public static void Return(T obj)
            {
                if (obj != null && pool.Count < 100) // ���Ƴش�С
                {
                    pool.Push(obj);
                }
            }

            public static void Clear()
            {
                pool.Clear();
            }
        }

        /// <summary>
        /// �ӳ�ִ�й���
        /// </summary>
        public static void DelayedCall(float delay, System.Action action)
        {
            if (action == null) return;

            var coroutineObject = new GameObject("DelayedCall");
            var coroutineRunner = coroutineObject.AddComponent<CoroutineRunner>();
            coroutineRunner.StartDelayedCall(delay, action);
        }

        #endregion
    }

    #region ������

    /// <summary>
    /// Э�������������ھ�̬�ӳٵ��ã�
    /// </summary>
    public class CoroutineRunner : MonoBehaviour
    {
        public void StartDelayedCall(float delay, System.Action action)
        {
            StartCoroutine(DelayedCallCoroutine(delay, action));
        }

        private IEnumerator DelayedCallCoroutine(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// �򵥵��¼�������
    /// </summary>
    public static class CardEventDispatcher
    {
        private static readonly Dictionary<string, List<System.Action<object>>> eventListeners =
            new Dictionary<string, List<System.Action<object>>>();

        /// <summary>
        /// �����¼�
        /// </summary>
        public static void Subscribe(string eventName, System.Action<object> listener)
        {
            if (!eventListeners.ContainsKey(eventName))
            {
                eventListeners[eventName] = new List<System.Action<object>>();
            }
            eventListeners[eventName].Add(listener);
        }

        /// <summary>
        /// ȡ�������¼�
        /// </summary>
        public static void Unsubscribe(string eventName, System.Action<object> listener)
        {
            if (eventListeners.ContainsKey(eventName))
            {
                eventListeners[eventName].Remove(listener);
            }
        }

        /// <summary>
        /// �����¼�
        /// </summary>
        public static void Dispatch(string eventName, object eventData = null)
        {
            if (eventListeners.ContainsKey(eventName))
            {
                foreach (var listener in eventListeners[eventName])
                {
                    try
                    {
                        listener?.Invoke(eventData);
                    }
                    catch (System.Exception e)
                    {
                        CardUtilities.LogError($"�¼������쳣 {eventName}: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// ���������¼�������
        /// </summary>
        public static void Clear()
        {
            eventListeners.Clear();
        }
    }

    #endregion
}