using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通过三阶贝塞尔曲线绘制选择箭头 - 支持外部控制版本
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
    [SerializeField] private Vector2 externalStartPosition = Vector2.zero; // 外部设置的起点
    [SerializeField] private Vector2 externalEndPosition = Vector2.zero; // 外部设置的终点

    // 内部变量
    private RectTransform origin;
    private List<RectTransform> arrowNodes = new List<RectTransform>();
    private List<Vector2> controlPoints = new List<Vector2>();
    private Canvas parentCanvas;

    // 外部控制状态
    private bool isExternallyControlled = false;

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
        this.arrowNodes.ForEach(a => a.anchoredPosition = new Vector2(-10000, -10000));

        // 初始化控制点
        for (int i = 0; i < 4; ++i)
            this.controlPoints.Add(Vector2.zero);
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

        // 计算控制点
        CalculateControlPoints(Vector2.zero, parentLocal);

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
    /// 计算控制点
    /// </summary>
    private void CalculateControlPoints(Vector2 startPosition, Vector2 targetPosition)
    {
        this.controlPoints[0] = startPosition; // P0: 起点
        this.controlPoints[3] = targetPosition; // P3: 终点

        Vector2 direction = targetPosition - startPosition;
        float distance = direction.magnitude;

        if (distance < 0.1f)
        {
            // 距离太小时，退化为直线
            this.controlPoints[1] = Vector2.Lerp(this.controlPoints[0], this.controlPoints[3], controlPoint1Position);
            this.controlPoints[2] = Vector2.Lerp(this.controlPoints[0], this.controlPoints[3], controlPoint2Position);
        }
        else
        {
            // 计算垂直向量
            Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;
            if (invertCurve) perpendicular = -perpendicular;

            // 计算控制点基础位置
            Vector2 p1Base = this.controlPoints[0] + direction * controlPoint1Position;
            Vector2 p2Base = this.controlPoints[0] + direction * controlPoint2Position;

            // 添加垂直偏移
            float offset1 = distance * curveIntensity;
            float offset2 = distance * curveIntensity;

            this.controlPoints[1] = p1Base + perpendicular * offset1;
            this.controlPoints[2] = p2Base - perpendicular * offset2;
        }
    }

    /// <summary>
    /// 更新箭头节点 - 改为公共方法
    /// </summary>
    public void UpdateArrowNodes()
    {
        for (int i = 0; i < this.arrowNodes.Count; ++i)
        {
            // 计算t值
            float t = CalculateTValue(i);

            // 计算贝塞尔曲线位置
            Vector2 pos = CalculateBezierPoint(t);

            // 验证计算结果
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y))
            {
                pos = Vector2.Lerp(this.controlPoints[0], this.controlPoints[3], t);
            }

            // 设置位置
            this.arrowNodes[i].anchoredPosition = pos;

            // 设置缩放
            float scale = CalculateScale(i);
            this.arrowNodes[i].localScale = new Vector3(scale, scale, 1f);

            // 设置旋转
            if (i > 0)
            {
                Vector2 dir = this.arrowNodes[i].anchoredPosition - this.arrowNodes[i - 1].anchoredPosition;
                if (dir.magnitude > 0.001f)
                {
                    float angle = Vector2.SignedAngle(Vector2.up, dir);
                    this.arrowNodes[i].rotation = Quaternion.Euler(0, 0, angle);
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

    private Vector2 CalculateBezierPoint(float t)
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

    #region 公共接口 - 供外部控制使用

    /// <summary>
    /// 启用外部控制模式
    /// </summary>
    public void EnableExternalControl(Vector2 startPosition, Vector2 endPosition)
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
    /// 设置起点位置（外部控制模式下）
    /// </summary>
    public void SetStartPosition(Vector2 startPosition)
    {
        externalStartPosition = startPosition;

        // 如果正在外部控制模式，立即更新
        if (isExternallyControlled)
        {
            UpdateWithExternalControl();
        }
    }

    /// <summary>
    /// 设置终点位置（外部控制模式下）
    /// </summary>
    public void SetEndPosition(Vector2 endPosition)
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