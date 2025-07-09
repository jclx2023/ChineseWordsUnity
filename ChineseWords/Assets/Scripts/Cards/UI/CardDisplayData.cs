using UnityEngine;

namespace Cards.UI
{
    /// <summary>
    /// ����չʾ���ݽṹ
    /// ר��ΪUI��ʾ��Ƶ����������ݽṹ�������������ӵ�ҵ���߼�
    /// </summary>
    [System.Serializable]
    public class CardDisplayData
    {
        [Header("������Ϣ")]
        public int cardId;
        public string cardName;
        [TextArea(2, 4)]
        public string description;

        [Header("�Ӿ���Դ")]
        public Sprite cardFaceSprite;     // ��������ͼ
        public Color backgroundColor;      // ����ɫ����CardConfig��ȡ��

        [Header("��ʾ����")]
        public bool showEffectValue;      // �Ƿ���ʾЧ����ֵ
        public float effectValue;         // Ч����ֵ��������ʾ��

        /// <summary>
        /// ��CardConfig���ݴ�����ʾ����
        /// </summary>
        /// <param name="cardData">ԭʼ��������</param>
        public CardDisplayData(Cards.Core.CardData cardData)
        {
            cardId = cardData.cardId;
            cardName = cardData.cardName;
            description = cardData.description;
            cardFaceSprite = cardData.cardIcon; // ��CardConfig��ȡ����ͼ
            backgroundColor = cardData.cardBackgroundColor;
            showEffectValue = cardData.showEffectValue;
            effectValue = cardData.effectValue;
        }

        /// <summary>
        /// ��֤������Ч��
        /// </summary>
        public bool IsValid()
        {
            return cardId > 0 &&
                   !string.IsNullOrEmpty(cardName) &&
                   !string.IsNullOrEmpty(description);
        }

        /// <summary>
        /// ��ȡ��ʽ����Ч������
        /// </summary>
        public string GetFormattedDescription()
        {
            string baseDesc = description;

            if (showEffectValue && effectValue != 0)
            {
                // ���ݿ���ID�����ֵ��ʾ
                switch (cardId)
                {
                    case 1: // ţ�̺�
                    case 7: // ���ջ���
                        baseDesc += $" [{effectValue}������ֵ]";
                        break;

                    case 4: // ������
                    case 10: // ��ʱ��
                        baseDesc += $" [{effectValue}��]";
                        break;

                    case 3: // �����۱�
                        baseDesc += $" [��{effectValue}]";
                        break;

                    case 8: // ���ⲹϰ
                    case 12: // һ�з۱�
                        baseDesc += $" [{effectValue}��]";
                        break;

                    case 9: // ��ֽ��
                        baseDesc += $" [{effectValue * 100}%������]";
                        break;
                }
            }

            return baseDesc;
        }

        /// <summary>
        /// ��ȡ������Ϣ
        /// </summary>
        public override string ToString()
        {
            return $"CardDisplayData[{cardId}] {cardName} - {(IsValid() ? "��Ч" : "��Ч")}";
        }
    }
}