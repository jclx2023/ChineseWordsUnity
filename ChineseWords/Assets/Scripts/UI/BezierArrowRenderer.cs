using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通过三阶贝塞尔曲线绘制选择箭头 - 支持外部控制版本
/// 融合了DynamicArrowSystem的3D支持、向上弯曲和动态效果
/// 新增：描边功能支持
/// </summary>
public class BezierArrowRenderer : MonoBehaviour
{
    [Header("箭头预制体")]
    public GameObject ArrowHeadPrefab;
    public GameObject ArrowNodePrefab;

    [Header("箭头配置")]
    public int arrowNodeNum = 10;
    public float scaleFactor = 1.0f;

    [Header("贝塞尔曲线参数")]
    [Range(0f, 1f)]
    public float controlPoint1Position = 0.33f; // P1在直线上的位置比例
    [Range(0f, 1f)]
    public float controlPoint2Position = 0.67f; // P2在直线上的位置比例
    [Range(0f, 1f)]
    public float curveIntensity = 0.2f; // 曲线弯曲强度
    public bool invertCurve = true; // 是否反转曲线方向

    [Header("3D深度控制")]
    public float refControlPoint1Height = 100.0f; // 参考控制点高度，确保箭头向上弯曲
    [Range(0f, 1f)]
    public float controlPoint1LengthRatio = 0.6f; // P1高度控制比例
    [Range(0f, 1f)]
    public float controlPoint2LengthRatio = 0.5f; // P2位置控制比例
    public float controlPoint1Height = 0f; // P1的Z深度
    public float controlPoint2Height = 0f; // P2的Z深度
    public float controlPoint3Height = 0f; // P3的Z深度

    [Header("动态效果")]
    [SerializeField] private bool enableFloatingEffect = true; // 是否启用浮动效果
    [SerializeField] private float floatingSpeed = 0.1f; // 浮动速度
    [SerializeField] private float floatingThresholdMax = 0.5f; // 浮动阈值

    [Header("节点分布")]
    public bool useLogarithmicDistribution = false; // 是否使用对数分布
    [Range(0.1f, 3f)]
    public float distributionPower = 2f; // 分布的幂次（仅对数分布时使用）

    [Header("缩放设置")]
    [Range(0f, 1f)]
    public float minScale = 0.2f; // 尾部最小缩放
    [Range(0f, 2f)]
    public float maxScale = 1.0f; // 头部最大缩放

    [Header("外部控制设置")]
    [SerializeField] private bool useExternalControl = false; // 是否使用外部控制
    [SerializeField] private Vector3 externalStartPosition = Vector3.zero; // 外部设置的起点 (改为Vector3)
    [SerializeField] private Vector3 externalEndPosition = Vector3.zero; // 外部设置的终点 (改为Vector3)

    [Header("描边设置")]
    [SerializeField] private Material outlineMaterial; // 描边材质（共享）
    [SerializeField] private Material originalMaterial; // 原始材质备份

    // 内部变量
    private RectTransform origin;
    private List<RectTransform> arrowNodes = new List<RectTransform>();
    private List<Vector3> controlPoints = new List<Vector3>(); // 改为Vector3支持3D
    private Canvas parentCanvas;

    // 外部控制状态
    private bool isExternallyControlled = false;

    // 描边控制状态
    private bool isOutlineEnabled = false;
    private Color currentOutlineColor = Color.green;
    private float currentOutlineWidth = 2.0f;

    #region 描边控制接口

    /// <summary>
    /// 设置描边启用状态
    /// </summary>
    public void SetOutlineEnabled(bool enabled)
    {
        if (isOutlineEnabled == enabled) return;

        isOutlineEnabled = enabled;
        ApplyOutlineSettings();

        Debug.Log($"[BezierArrowRenderer] 描边已{(enabled ? "启用" : "禁用")}");
    }

    /// <summary>
    /// 设置描边颜色
    /// </summary>
    public void SetOutlineColor(Color color)
    {
        currentOutlineColor = color;

        if (isOutlineEnabled && outlineMaterial != null)
        {
            outlineMaterial.SetColor("_OutlineColor", color);
            Debug.Log($"[BezierArrowRenderer] 描边颜色设置为: {color}");
        }
    }

    /// <summary>
    /// 设置描边宽度
    /// </summary>
    public void SetOutlineWidth(float width)
    {
        currentOutlineWidth = width;

        if (isOutlineEnabled && outlineMaterial != null)
        {
            outlineMaterial.SetFloat("_OutlineWidth", width);
            Debug.Log($"[BezierArrowRenderer] 描边宽度设置为: {width}");
        }
    }

    /// <summary>
    /// 应用描边设置到所有节点
    /// </summary>
    private void ApplyOutlineSettings()
    {
        Material targetMaterial = isOutlineEnabled ? outlineMaterial : originalMaterial;

        if (targetMaterial == null)
        {
            Debug.LogWarning("[BezierArrowRenderer] 目标材质为空，无法应用描边设置");
            return;
        }

        // 如果启用描边，设置材质参数
        if (isOutlineEnabled && outlineMaterial != null)
        {
            outlineMaterial.SetColor("_OutlineColor", currentOutlineColor);
            outlineMaterial.SetFloat("_OutlineWidth", currentOutlineWidth);
            outlineMaterial.SetFloat("_OutlineEnabled", 1.0f);
        }

        // 应用到所有节点
        int appliedCount = 0;
        foreach (var node in arrowNodes)
        {
            if (node != null)
            {
                Image nodeImage = node.GetComponent<Image>();
                if (nodeImage != null)
                {
                    nodeImage.material = targetMaterial;
                    appliedCount++;
                }
            }
        }

        Debug.Log($"[BezierArrowRenderer] 描边设置已应用到 {appliedCount} 个节点");
    }

    /// <summary>
    /// 初始化描边材质
    /// </summary>
    private void InitializeOutlineMaterials()
    {
        // 备份第一个节点的原始材质
        if (arrowNodes.Count > 0 && arrowNodes[0] != null)
        {
            Image firstImage = arrowNodes[0].GetComponent<Image>();
            if (firstImage != null)
            {
                originalMaterial = firstImage.material;
                Debug.Log($"[BezierArrowRenderer] 原始材质已备份: {originalMaterial?.name ?? "null"}");
            }
        }

        // 如果没有设置描边材质，尝试从Resources加载
        if (outlineMaterial == null)
        {
            outlineMaterial = Resources.Load<Material>("Materials/ArrowOutlineMaterial");
        }

        // 初始化描边为禁用状态
        if (outlineMaterial != null)
        {
            outlineMaterial.SetFloat("_OutlineEnabled", 0.0f);
        }
    }

    #endregion

    private void Awake()
    {
        this.origin = this.GetComponent<RectTransform>();

        // 获取Canvas
        parentCanvas = GetComponentInParent<Canvas>();

        // 创建节点
        for (int i = 0; i < this.arrowNodeNum; ++i)
        {
            var nodeObj = Instantiate(this.ArrowNodePrefab, this.transform);
            var node = nodeObj.GetComponent<RectTransform>();
            this.arrowNodes.Add(node);
        }

        // 创建箭头头部
        var headObj = Instantiate(this.ArrowHeadPrefab, this.transform);
        var head = headObj.GetComponent<RectTransform>();
        this.arrowNodes.Add(head);

        // 初始化隐藏
        this.arrowNodes.ForEach(a => a.anchoredPosition3D = new Vector3(-10000, -10000, 0)); // 使用3D位置

        // 初始化控制点
        for (int i = 0; i < 4; ++i)
            this.controlPoints.Add(Vector3.zero); // 改为Vector3

        // 初始化描边材质
        InitializeOutlineMaterials();
    }

    private void Update()
    {
        if (parentCanvas == null || arrowNodes.Count == 0) return;

        // 如果外部控制，使用外部设置的位置
        if (isExternallyControlled)
        {
            UpdateWithExternalControl();
        }
        else
        {
            UpdateWithMouseControl();
        }
    }

    /// <summary>
    /// 使用鼠标控制的更新（原有逻辑）
    /// </summary>
    private void UpdateWithMouseControl()
    {
        // 获取鼠标位置
        Vector2 parentLocal;
        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            origin.parent as RectTransform,
            Input.mousePosition,
            null,
            out parentLocal);

        if (!success) return;

        // 转换为Vector3，Z=0
        Vector3 mousePos3D = new Vector3(parentLocal.x, parentLocal.y, 0);

        // 计算控制点
        CalculateControlPoints(Vector3.zero, mousePos3D);

        // 更新节点
        UpdateArrowNodes();
    }

    /// <summary>
    /// 使用外部控制的更新
    /// </summary>
    private void UpdateWithExternalControl()
    {
        // 计算控制点
        CalculateControlPoints(externalStartPosition, externalEndPosition);

        // 更新节点
        UpdateArrowNodes();
    }

    /// <summary>
    /// 计算控制点 - 融合DynamicArrowSystem的向上弯曲算法
    /// </summary>
    private void CalculateControlPoints(Vector3 startPosition, Vector3 targetPosition)
    {
        this.controlPoints[0] = startPosition; // P0: 起点
        this.controlPoints[3] = targetPosition; // P3: 终点

        Vector3 direction = targetPosition - startPosition;
        float distance = direction.magnitude;
        float noCurveDist = 0.1f;

        if (distance <= noCurveDist)
        {
            // 距离太小时，退化为直线
            this.controlPoints[1] = Vector3.Lerp(this.controlPoints[0], this.controlPoints[3], controlPoint1Position);
            this.controlPoints[2] = Vector3.Lerp(this.controlPoints[0], this.controlPoints[3], controlPoint2Position);
        }
        else
        {
            // 使用DynamicArrowSystem的向上弯曲算法
            var lenY = Mathf.Abs(this.controlPoints[3].y - this.controlPoints[0].y);
            var lenXSigned = this.controlPoints[3].x - this.controlPoints[0].x;

            // 计算P1 - 向上的控制点
            this.controlPoints[1] = this.controlPoints[0] + Vector3.up * lenY * controlPoint1LengthRatio;
            var y1 = (refControlPoint1Height + this.controlPoints[1].y) * 0.5f;
            this.controlPoints[1] = new Vector3(this.controlPoints[1].x, y1, controlPoint1Height);

            // 计算P2 - 侧向的控制点
            this.controlPoints[2] = this.controlPoints[3] + Vector3.left * lenXSigned * controlPoint2LengthRatio;
            var y2 = (this.controlPoints[1].y + this.controlPoints[2].y) * 0.5f;
            this.controlPoints[2] = new Vector3(this.controlPoints[2].x, y2, controlPoint2Height);

            // 设置P3的Z深度
            this.controlPoints[3] = new Vector3(this.controlPoints[3].x, this.controlPoints[3].y, controlPoint3Height);
        }
    }

    /// <summary>
    /// 更新箭头节点 - 改为公共方法，支持3D和动态效果
    /// </summary>
    public void UpdateArrowNodes()
    {
        for (int i = 0; i < this.arrowNodes.Count; ++i)
        {
            var node = this.arrowNodes[i];
            node.gameObject.SetActive(true);

            // 计算基础t值
            float t = CalculateTValue(i);

            // 添加动态浮动效果
            float t_floating = t;
            if (enableFloatingEffect && i != this.arrowNodes.Count - 1) // 头部不浮动
            {
                float div = 1f / (this.arrowNodes.Count - 1);
                t_floating += Time.time * floatingSpeed;
                while (t_floating > t + div * floatingThresholdMax)
                    t_floating -= div;
            }

            // 计算贝塞尔曲线位置 (3D)
            Vector3 pos = CalculateBezierPoint(t_floating);

            // 验证计算结果
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
            {
                pos = Vector3.Lerp(this.controlPoints[0], this.controlPoints[3], t);
            }

            // 设置3D位置
            node.anchoredPosition3D = pos;

            // 设置缩放
            float scale = CalculateScale(i);
            node.localScale = new Vector3(scale, scale, 1f);

            // 设置旋转
            if (i > 0)
            {
                Vector3 dir = node.anchoredPosition3D - this.arrowNodes[i - 1].anchoredPosition3D;
                if (dir.magnitude > 0.001f)
                {
                    // 3D旋转计算
                    node.rotation = Quaternion.LookRotation(-Vector3.forward, dir);
                }
            }
        }

        // 设置第一个节点旋转
        if (this.arrowNodes.Count >= 2)
        {
            this.arrowNodes[0].rotation = this.arrowNodes[1].rotation;
        }
    }

    private float CalculateTValue(int index)
    {
        if (index == 0)
        {
            // 第一个节点始终在起点
            return 0f;
        }

        if (useLogarithmicDistribution)
        {
            float t = Mathf.Log(1f * index / (this.arrowNodes.Count - 1), distributionPower);
            return Mathf.Clamp01(t);
        }
        else
        {
            // 线性分布：从第二个节点开始到最后一个节点
            return (float)index / (this.arrowNodes.Count - 1);
        }
    }

    /// <summary>
    /// 计算贝塞尔曲线点 - 支持3D
    /// </summary>
    private Vector3 CalculateBezierPoint(float t)
    {
        float oneMinusT = 1f - t;
        return Mathf.Pow(oneMinusT, 3) * this.controlPoints[0] +
               3 * Mathf.Pow(oneMinusT, 2) * t * this.controlPoints[1] +
               3 * oneMinusT * Mathf.Pow(t, 2) * this.controlPoints[2] +
               Mathf.Pow(t, 3) * this.controlPoints[3];
    }

    private float CalculateScale(int index)
    {
        float progress = (float)index / Mathf.Max(1, this.arrowNodes.Count - 1);
        return this.scaleFactor * Mathf.Lerp(minScale, maxScale, progress);
    }

    #region 公共接口 - 供外部控制使用 (保持兼容性)

    /// <summary>
    /// 启用外部控制模式 - 保持Vector2接口兼容性
    /// </summary>
    public void EnableExternalControl(Vector2 startPosition, Vector2 endPosition)
    {
        EnableExternalControl(new Vector3(startPosition.x, startPosition.y, 0),
                             new Vector3(endPosition.x, endPosition.y, 0));
    }

    /// <summary>
    /// 启用外部控制模式 - 新的3D版本
    /// </summary>
    public void EnableExternalControl(Vector3 startPosition, Vector3 endPosition)
    {
        isExternallyControlled = true;
        externalStartPosition = startPosition;
        externalEndPosition = endPosition;

        // 立即更新一次
        UpdateWithExternalControl();
    }

    /// <summary>
    /// 禁用外部控制，返回鼠标控制模式
    /// </summary>
    public void DisableExternalControl()
    {
        isExternallyControlled = false;
    }

    /// <summary>
    /// 设置起点位置（外部控制模式下）- 保持Vector2接口兼容性
    /// </summary>
    public void SetStartPosition(Vector2 startPosition)
    {
        SetStartPosition(new Vector3(startPosition.x, startPosition.y, 0));
    }

    /// <summary>
    /// 设置起点位置（外部控制模式下）- 新的3D版本
    /// </summary>
    public void SetStartPosition(Vector3 startPosition)
    {
        externalStartPosition = startPosition;

        // 如果正在外部控制模式，立即更新
        if (isExternallyControlled)
        {
            UpdateWithExternalControl();
        }
    }

    /// <summary>
    /// 设置终点位置（外部控制模式下）- 保持Vector2接口兼容性
    /// </summary>
    public void SetEndPosition(Vector2 endPosition)
    {
        SetEndPosition(new Vector3(endPosition.x, endPosition.y, 0));
    }

    /// <summary>
    /// 设置终点位置（外部控制模式下）- 新的3D版本
    /// </summary>
    public void SetEndPosition(Vector3 endPosition)
    {
        externalEndPosition = endPosition;

        // 如果正在外部控制模式，立即更新
        if (isExternallyControlled)
        {
            UpdateWithExternalControl();
        }
    }

    #endregion
}