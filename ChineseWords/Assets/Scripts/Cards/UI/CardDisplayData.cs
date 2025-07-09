using UnityEngine;

namespace Cards.UI
{
    /// <summary>
    /// 卡牌展示数据结构
    /// 专门为UI显示设计的轻量级数据结构，避免依赖复杂的业务逻辑
    /// </summary>
    [System.Serializable]
    public class CardDisplayData
    {
        [Header("基础信息")]
        public int cardId;
        public string cardName;
        [TextArea(2, 4)]
        public string description;

        [Header("视觉资源")]
        public Sprite cardFaceSprite;     // 卡牌牌面图
        public Color backgroundColor;      // 背景色（从CardConfig获取）

        [Header("显示设置")]
        public bool showEffectValue;      // 是否显示效果数值
        public float effectValue;         // 效果数值（用于显示）

        /// <summary>
        /// 从CardConfig数据创建显示数据
        /// </summary>
        /// <param name="cardData">原始卡牌数据</param>
        public CardDisplayData(Cards.Core.CardData cardData)
        {
            cardId = cardData.cardId;
            cardName = cardData.cardName;
            description = cardData.description;
            cardFaceSprite = cardData.cardIcon; // 从CardConfig获取牌面图
            backgroundColor = cardData.cardBackgroundColor;
            showEffectValue = cardData.showEffectValue;
            effectValue = cardData.effectValue;
        }

        /// <summary>
        /// 验证数据有效性
        /// </summary>
        public bool IsValid()
        {
            return cardId > 0 &&
                   !string.IsNullOrEmpty(cardName) &&
                   !string.IsNullOrEmpty(description);
        }

        /// <summary>
        /// 获取格式化的效果描述
        /// </summary>
        public string GetFormattedDescription()
        {
            string baseDesc = description;

            if (showEffectValue && effectValue != 0)
            {
                // 根据卡牌ID添加数值显示
                switch (cardId)
                {
                    case 1: // 牛奶盒
                    case 7: // 文艺汇演
                        baseDesc += $" [{effectValue}点生命值]";
                        break;

                    case 4: // 再想想
                    case 10: // 减时卡
                        baseDesc += $" [{effectValue}秒]";
                        break;

                    case 3: // 两根粉笔
                        baseDesc += $" [×{effectValue}]";
                        break;

                    case 8: // 课外补习
                    case 12: // 一盒粉笔
                        baseDesc += $" [{effectValue}张]";
                        break;

                    case 9: // 丢纸团
                        baseDesc += $" [{effectValue * 100}%命中率]";
                        break;
                }
            }

            return baseDesc;
        }

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public override string ToString()
        {
            return $"CardDisplayData[{cardId}] {cardName} - {(IsValid() ? "有效" : "无效")}";
        }
    }
}