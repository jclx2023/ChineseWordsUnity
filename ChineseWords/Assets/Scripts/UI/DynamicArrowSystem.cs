using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通过三阶贝塞尔曲线绘制选择箭头 - 支持外部控制版本
/// </summary>
public class DynamicArrowSystem : MonoBehaviour
{
    [Header("箭头预制体")]
    public RectTransform arrowHeadPrefab;
    public RectTransform arrowNodePrefab;

    [Header("箭头配置")]
    public int arrowNodeDist = 100;
    public float refControlPoint1Height = 1.0f;
    [Header("贝塞尔曲线参数")]
    [Range(0f, 1f)]
    public float controlPoint1LengthRatio = 0.6f; // P1为P0上方的点，P0到P1占P0到P3的y轴高度差的比例
    [Range(0f, 1f)]
    public float controlPoint2LengthRatio = 0.5f; // P2为P3左右侧的点，P2到P3占P0到P3的x轴横向长度差的比例
    public float controlPoint1Height;
    public float controlPoint2Height;
    public float controlPoint3Height;
    [Header("外部控制设置")]
    [SerializeField] private bool useExternalControl = false; // 是否使用外部控制
    [SerializeField] private Vector3 externalStartPosition = Vector3.zero; // 外部设置的起点
    [SerializeField] private Vector3 externalEndPosition = Vector3.zero; // 外部设置的终点

    // 内部变量
    [SerializeField] private RectTransform origin;
    private List<RectTransform> arrowNodes = new List<RectTransform>();
    private List<Vector3> controlPoints = new List<Vector3>();
    [SerializeField] private Canvas parentCanvas;

    // 外部控制状态
    private bool isExternallyControlled = false;
    [SerializeField] private int arrowNodeNum = 16;

    private void Awake()
    {
        // 创建节点
        for (int i = 0; i < arrowNodeNum; ++i)
        {
            var node = Instantiate(arrowNodePrefab, arrowNodePrefab.parent);
            arrowNodes.Add(node);
            node.gameObject.name = "node " + arrowNodes.Count;
        }

        // 创建箭头头部
        var head = Instantiate(arrowHeadPrefab, arrowHeadPrefab.parent);
        arrowNodes.Add(head);
        head.gameObject.SetActive(true);
        head.gameObject.name = "head " + arrowNodes.Count;

        // 初始化控制点
        for (int i = 0; i < 4; ++i)
            controlPoints.Add(Vector3.zero);
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
        Vector3 mousePos;
        bool success = RectTransformUtility.ScreenPointToWorldPointInRectangle(
            origin.parent as RectTransform,
            Input.mousePosition - new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0),
           null,// parentCanvas.worldCamera,
            out mousePos);

        if (!success) return;
        //Debug.Log("  Input.mousePosition " + Input.mousePosition);
        //Debug.Log(origin.anchoredPosition3D);
        // 计算控制点
        CalculateControlPoints(origin.anchoredPosition3D, mousePos);

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
    private void CalculateControlPoints(Vector3 startPosition, Vector3 targetPosition)
    {
        controlPoints[0] = startPosition; // P0: 起点
        controlPoints[3] = targetPosition; // P3: 终点

        Vector3 direction = targetPosition - startPosition;
        float distance = direction.magnitude;
        float noCurveDist = 0.1f;

        if (distance <= noCurveDist)
        {
            // 距离太小时，退化为直线
            controlPoints[1] = Vector3.Lerp(controlPoints[0], controlPoints[3], 0.4f);
            controlPoints[2] = Vector3.Lerp(controlPoints[0], controlPoints[3], 0.6f);
        }
        else
        {
            // 计算垂直向量
            //Vector3 perpendicular = new Vector3(-direction.y, direction.x).normalized;
            var lenY = Mathf.Abs(controlPoints[3].y - controlPoints[0].y);
            var lenXSigned = controlPoints[3].x - controlPoints[0].x;

            // 计算控制点位置
            controlPoints[1] = controlPoints[0] + Vector3.up * lenY * controlPoint1LengthRatio;
            var y1 = (refControlPoint1Height + controlPoints[1].y) * 0.5f;
            controlPoints[1] = new Vector3(controlPoints[1].x, y1, controlPoint1Height);
            controlPoints[2] = controlPoints[3] + Vector3.left * lenXSigned * controlPoint2LengthRatio;
            var y2 = (controlPoints[1].y + controlPoints[2].y) * 0.5f;
            controlPoints[2] = new Vector3(controlPoints[2].x, y2, controlPoint2Height);
            controlPoints[3] = new Vector3(controlPoints[3].x, controlPoints[3].y, controlPoint3Height);
        }
    }

    [SerializeField] private float headRotationOffset;
    [SerializeField] private float floatingSpeed = 0.1f;
    [SerializeField] private float floatingThresholdMax = 0.5f;
    /// <summary>
    /// 更新箭头节点 - 改为公共方法
    /// </summary>
    public void UpdateArrowNodes()
    {
        for (int i = 0; i < arrowNodes.Count; ++i)
        {
            var node = arrowNodes[i];
            node.gameObject.SetActive(true);
            float t = CalculateTValue(i);//   return (float)index / (arrowNodes.Count - 1);
            float div = 1f / (arrowNodes.Count - 1);
            var t_floating = t;
            if (i != arrowNodes.Count - 1)
            {
                t_floating += Time.time * floatingSpeed;
                while (t_floating > t + div * floatingThresholdMax)
                    t_floating -= div;
            }

            // 计算贝塞尔曲线位置
            Vector3 pos = CalculateBezierPoint(t_floating);

            // 验证计算结果
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y))
            {
                pos = Vector3.Lerp(controlPoints[0], controlPoints[3], t);
            }

            // 设置位置
            node.anchoredPosition3D = pos;

            // 设置旋转
            if (i > 0)
            {
                Vector3 dir = node.anchoredPosition3D - arrowNodes[i - 1].anchoredPosition3D;
                if (dir.magnitude > 0.001f)
                {
                    node.rotation = Quaternion.LookRotation(-Vector3.forward, dir);
                }
            }
        }

        if (arrowNodes.Count >= 2)
        {
            //arrowNodes[0].eulerAngles = arrowNodes[1].eulerAngles;
            arrowNodes[arrowNodes.Count - 1].eulerAngles += new Vector3(0, 0, headRotationOffset);
        }

    }

    private float CalculateTValue(int index)
    {
        return (float)index / (arrowNodes.Count - 1);
    }

    private Vector3 CalculateBezierPoint(float t)
    {
        float oneMinusT = 1f - t;
        return Mathf.Pow(oneMinusT, 3) * controlPoints[0] +
               3 * Mathf.Pow(oneMinusT, 2) * t * controlPoints[1] +
               3 * oneMinusT * Mathf.Pow(t, 2) * controlPoints[2] +
               Mathf.Pow(t, 3) * controlPoints[3];
    }

    //estimate the arc length as the average between the chord and the control net. In practice:
    float GetEstimatedBezierLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        var chord = (p3 - p0).magnitude;
        var cont_net = (p0 - p1).magnitude + (p2 - p1).magnitude + (p3 - p2).magnitude;
        return (cont_net + chord) / 2;
    }

    #region 公共接口 - 供外部控制使用

    /// <summary>
    /// 启用外部控制模式
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
    /// 设置起点位置（外部控制模式下）
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
    /// 设置终点位置（外部控制模式下）
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