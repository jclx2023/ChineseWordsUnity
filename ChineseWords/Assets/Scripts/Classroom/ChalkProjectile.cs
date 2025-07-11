using UnityEngine;

namespace Classroom
{
    /// <summary>
    /// 粉笔弹道组件 - Inspector可调节版本
    /// 处理粉笔的飞行、碰撞检测和特效
    /// </summary>
    public class ChalkProjectile : MonoBehaviour
    {
        [Header("基础配置")]
        [SerializeField] private float lifetime = 5f; // 最大存活时间
        [SerializeField] private bool enableDebugLogs = true;

        [Header("拖尾特效配置")]
        [SerializeField] private bool enableTrail = true;
        [SerializeField] private Material trailMaterial;
        [SerializeField] private float trailTime = 0.5f;
        [SerializeField] private float trailWidth = 0.1f;
        [SerializeField] private Color trailColor = Color.white;
        [SerializeField] private float trailEndWidthMultiplier = 0.1f; // 结束宽度倍数
        [SerializeField] private bool trailAutoDestruct = false;
        [SerializeField] private bool trailEmitting = true;

        [Header("命中特效配置")]
        [SerializeField] private bool enableHitEffect = true;
        [SerializeField] private int dustParticleCount = 20;
        [SerializeField] private float dustLifetime = 0.5f;
        [SerializeField] private float dustSpeed = 2f;
        [SerializeField] private float dustSize = 0.1f;
        [SerializeField] private Color dustColor = Color.white;
        [SerializeField] private float dustSphereRadius = 0.2f;
        [SerializeField] private float dustEffectDuration = 2f;

        [Header("物理调试")]
        [SerializeField] private bool showVelocityDebug = true;
        [SerializeField] private bool showTrajectoryGizmos = true;
        [SerializeField] private int trajectoryPoints = 10; // 轨迹预测点数
        [SerializeField] private float trajectoryTimeStep = 0.1f; // 轨迹时间步长

        [Header("碰撞检测配置")]
        [SerializeField] private bool detectPlayer = true;
        [SerializeField] private bool detectGround = true;
        [SerializeField] private bool detectOtherObjects = true;
        [SerializeField] private string[] additionalPlayerTags = { "Student", "Character" };
        [SerializeField] private string[] additionalGroundTags = { "Floor", "Plane", "Surface" };

        // 私有变量
        private TrailRenderer trailRenderer;
        private Rigidbody rb;
        private Vector3 lastPosition;
        private float totalDistance = 0f;
        private float spawnTime;

        #region Unity生命周期

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            lastPosition = transform.position;
            spawnTime = Time.time;
            LogDebug("粉笔弹道组件已初始化");
        }

        private void Start()
        {
            Destroy(gameObject, lifetime);

            if (enableTrail)
            {
                CreateTrailEffect();
            }

            LogDebug($"粉笔生成 - 位置: {transform.position}");
            if (rb != null)
            {
                LogDebug($"初始速度: {rb.velocity}, 质量: {rb.mass}");
            }
            else
            {
                LogDebug("⚠️ 未找到Rigidbody组件");
            }

            var colliders = GetComponents<Collider>();
            LogDebug($"碰撞体数量: {colliders.Length}");
            foreach (var col in colliders)
            {
                LogDebug($"碰撞体: {col.GetType().Name}, IsTrigger: {col.isTrigger}");
            }
        }

        private void Update()
        {
            if (showVelocityDebug && rb != null)
            {
                float deltaDistance = Vector3.Distance(transform.position, lastPosition);
                totalDistance += deltaDistance;
                lastPosition = transform.position;

                if (Time.time - spawnTime > 0.5f && (int)(Time.time * 2) % 2 == 0)
                {
                    LogDebug($"飞行状态 - 速度: {rb.velocity.magnitude:F2} m/s, " +
                            $"位置: {transform.position}, 总距离: {totalDistance:F2}m");
                }
            }
        }

        #endregion

        #region 拖尾特效

        /// <summary>
        /// 创建拖尾特效 - 使用Inspector配置参数
        /// </summary>
        private void CreateTrailEffect()
        {
            // 获取或添加TrailRenderer组件
            trailRenderer = GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }

            // 设置基础参数（从Inspector配置）
            trailRenderer.time = trailTime;
            trailRenderer.startWidth = trailWidth;
            trailRenderer.endWidth = trailWidth * trailEndWidthMultiplier;
            trailRenderer.autodestruct = trailAutoDestruct;
            trailRenderer.emitting = trailEmitting;

            // 设置颜色
            trailRenderer.startColor = trailColor;
            Color endColor = trailColor;
            endColor.a = 0f; // 结束时透明
            trailRenderer.endColor = endColor;

            // 设置材质
            if (trailMaterial != null)
            {
                trailRenderer.material = trailMaterial;
            }
            else
            {
                // 创建简单的默认材质
                Material defaultMaterial = new Material(Shader.Find("Sprites/Default"));
                defaultMaterial.color = Color.white;
                trailRenderer.material = defaultMaterial;
            }

            // 使用颜色渐变进行精细控制
            Gradient colorGradient = new Gradient();

            // 设置颜色关键点
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(trailColor, 0.0f);
            colorKeys[1] = new GradientColorKey(trailColor, 1.0f);

            // 设置透明度关键点
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(0.0f, 1.0f);

            colorGradient.SetKeys(colorKeys, alphaKeys);
            trailRenderer.colorGradient = colorGradient;

            LogDebug("拖尾特效已创建");
        }

        #endregion

        #region 碰撞检测

        private void OnTriggerEnter(Collider other)
        {
            LogDebug($"触发器检测 - 对象: {other.name}, Tag: {other.tag}");

            if (IsPlayerTarget(other))
            {
                LogDebug($"✓ 粉笔命中学生目标: {other.name}");
                HandleHit(other.gameObject, "学生");
            }
            else
            {
                LogDebug($"触发器碰撞其他对象: {other.name} (Tag: {other.tag})");
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            LogDebug($"物理碰撞检测 - 对象: {collision.gameObject.name}, Tag: {collision.gameObject.tag}");
            LogDebug($"碰撞点: {collision.contacts[0].point}, 法线: {collision.contacts[0].normal}");

            if (IsPlayerTarget(collision.collider))
            {
                LogDebug($"✓ 粉笔碰撞学生目标: {collision.gameObject.name}");
                HandleHit(collision.gameObject, "学生");
            }
            else if (IsGroundTarget(collision.collider))
            {
                LogDebug($"✓ 粉笔落地: {collision.gameObject.name}");
                HandleHit(collision.gameObject, "地面");
            }
            else if (detectOtherObjects)
            {
                LogDebug($"碰撞其他对象: {collision.gameObject.name} (Tag: {collision.gameObject.tag})");
                HandleHit(collision.gameObject, "其他物体");
            }
        }

        /// <summary>
        /// 检查是否为玩家目标
        /// </summary>
        private bool IsPlayerTarget(Collider collider)
        {
            if (!detectPlayer) return false;

            if (collider.CompareTag("Player")) return true;

            foreach (string tag in additionalPlayerTags)
            {
                if (collider.CompareTag(tag)) return true;
            }

            return collider.name.Contains("Character");
        }

        /// <summary>
        /// 检查是否为地面目标
        /// </summary>
        private bool IsGroundTarget(Collider collider)
        {
            if (!detectGround) return false;

            if (collider.CompareTag("Ground")) return true;

            foreach (string tag in additionalGroundTags)
            {
                if (collider.CompareTag(tag)) return true;
            }

            return collider.name.Contains("Floor") || collider.name.Contains("Plane");
        }

        private void HandleHit(GameObject target, string targetType)
        {
            float flightTime = Time.time - spawnTime;
            LogDebug($"🎯 粉笔命中{targetType}: {target.name}, 飞行时间: {flightTime:F2}s, 总距离: {totalDistance:F2}m");

            if (enableHitEffect)
            {
                CreateHitEffect();
            }

            Destroy(gameObject);
        }

        #endregion

        #region 特效系统

        private void CreateHitEffect()
        {
            LogDebug("创建命中特效");
            CreateConfigurableDustEffect();
        }

        /// <summary>
        /// 创建可配置的粉末特效
        /// </summary>
        private void CreateConfigurableDustEffect()
        {
            GameObject dustEffect = new GameObject("ChalkDust");
            dustEffect.transform.position = transform.position;

            ParticleSystem particles = dustEffect.AddComponent<ParticleSystem>();

            // 主要参数（从Inspector配置）
            var main = particles.main;
            main.startLifetime = dustLifetime;
            main.startSpeed = dustSpeed;
            main.startSize = dustSize;
            main.startColor = dustColor;
            main.maxParticles = dustParticleCount;

            // 形状参数
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = dustSphereRadius;

            // 速度参数
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;

            // 自动销毁
            Destroy(dustEffect, dustEffectDuration);
            LogDebug("可配置粉末特效已创建");
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[ChalkProjectile] {message}");
            }
        }

        #endregion

    }
}