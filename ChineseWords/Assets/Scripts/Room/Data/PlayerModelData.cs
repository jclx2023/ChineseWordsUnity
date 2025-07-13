using UnityEngine;

namespace RoomScene.PlayerModel
{
    /// <summary>
    /// 角色模型配置数据 - 支持3D预览和动画
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerModel", menuName = "Game/Player Model Data")]
    public class PlayerModelData : ScriptableObject
    {
        [Header("基础信息")]
        public int modelId;
        public string modelName;
        public string modelDescription;

        [Header("模型资源")]
        public GameObject modelPrefab;           // 游戏中使用的完整预制体
        public GameObject previewPrefab;         // UI预览用的简化预制体（可选）

        [Header("预览配置")]
        public Vector3 previewScale = Vector3.one;
        public Vector3 previewRotation = Vector3.zero;
        public Vector3 previewPosition = Vector3.zero;

        [Header("动画配置")]
        public AnimationClip idleAnimation;      // 预览时的待机动画
        public AnimationClip selectAnimation;    // 选择时的动画
        public float animationSpeed = 1f;

        [Header("可用性")]
        public bool isUnlocked = true;
        public bool isDefault = false;           // 是否为默认模型

        /// <summary>
        /// 获取预览用的预制体（优先使用previewPrefab，否则使用modelPrefab）
        /// </summary>
        public GameObject GetPreviewPrefab()
        {
            return previewPrefab != null ? previewPrefab : modelPrefab;
        }

        /// <summary>
        /// 获取游戏中使用的预制体
        /// </summary>
        public GameObject GetGamePrefab()
        {
            return modelPrefab;
        }

        /// <summary>
        /// 验证模型数据是否有效
        /// </summary>
        public bool IsValid()
        {
            return modelPrefab != null && !string.IsNullOrEmpty(modelName);
        }
    }
}