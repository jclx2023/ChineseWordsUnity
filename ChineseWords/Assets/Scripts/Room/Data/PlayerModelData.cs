using UnityEngine;

namespace RoomScene.PlayerModel
{
    /// <summary>
    /// ��ɫģ���������� - ֧��3DԤ���Ͷ���
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerModel", menuName = "Game/Player Model Data")]
    public class PlayerModelData : ScriptableObject
    {
        [Header("������Ϣ")]
        public int modelId;
        public string modelName;
        public string modelDescription;

        [Header("ģ����Դ")]
        public GameObject modelPrefab;           // ��Ϸ��ʹ�õ�����Ԥ����
        public GameObject previewPrefab;         // UIԤ���õļ�Ԥ���壨��ѡ��

        [Header("Ԥ������")]
        public Vector3 previewScale = Vector3.one;
        public Vector3 previewRotation = Vector3.zero;
        public Vector3 previewPosition = Vector3.zero;

        [Header("��������")]
        public AnimationClip idleAnimation;      // Ԥ��ʱ�Ĵ�������
        public AnimationClip selectAnimation;    // ѡ��ʱ�Ķ���
        public float animationSpeed = 1f;

        [Header("������")]
        public bool isUnlocked = true;
        public bool isDefault = false;           // �Ƿ�ΪĬ��ģ��

        /// <summary>
        /// ��ȡԤ���õ�Ԥ���壨����ʹ��previewPrefab������ʹ��modelPrefab��
        /// </summary>
        public GameObject GetPreviewPrefab()
        {
            return previewPrefab != null ? previewPrefab : modelPrefab;
        }

        /// <summary>
        /// ��ȡ��Ϸ��ʹ�õ�Ԥ����
        /// </summary>
        public GameObject GetGamePrefab()
        {
            return modelPrefab;
        }

        /// <summary>
        /// ��֤ģ�������Ƿ���Ч
        /// </summary>
        public bool IsValid()
        {
            return modelPrefab != null && !string.IsNullOrEmpty(modelName);
        }
    }
}