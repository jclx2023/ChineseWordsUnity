using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通过三阶贝塞尔曲线绘制选择箭头。
/// </summary>
public class BezierArrowRenderer : MonoBehaviour
{
    [Header("箭头预制体")]
    public GameObject ArrowHeadPrefab;
    public GameObject ArrowNodePrefab;

    [Header("箭头节点数量（不含箭头头部）")]
    public int arrowNodeNum = 10;

    [Header("缩放因子")]
    public float scaleFactor = 1.0f;

    /// 起点（箭头发射点）
    private RectTransform origin;

    /// 所有箭头节点（包括箭头头部）
    private List<RectTransform> arrowNodes = new List<RectTransform>();

    /// 贝塞尔曲线控制点（0为起点，3为终点）
    private List<Vector2> controlPoints = new List<Vector2>();

    /// 控制点1、2的偏移因子（相对终点方向）
    private readonly List<Vector2> controlPointFactors = new List<Vector2> {
        new Vector2(-0.3f, 0.8f),
        new Vector2(0.3f, 0.8f)
    };

    private void Awake()
    {
        this.origin = this.GetComponent<RectTransform>();

        // 实例化箭头节点
        for (int i = 0; i < this.arrowNodeNum; ++i)
        {
            var node = Instantiate(this.ArrowNodePrefab, this.transform).GetComponent<RectTransform>();
            this.arrowNodes.Add(node);
        }

        // 最后一个是箭头头部
        var head = Instantiate(this.ArrowHeadPrefab, this.transform).GetComponent<RectTransform>();
        this.arrowNodes.Add(head);

        // 隐藏节点（移到画面外）
        this.arrowNodes.ForEach(a => a.position = new Vector2(-1000, -1000));

        // 初始化4个控制点
        for (int i = 0; i < 4; ++i)
            this.controlPoints.Add(Vector2.zero);
    }

    private void Update()
    {
        Vector2 start = this.origin.position;
        Vector2 end = Input.mousePosition;

        // 设置 P0、P3
        this.controlPoints[0] = start;
        this.controlPoints[3] = end;

        // 设置 P1、P2（起点向终点方向延伸一定偏移）
        this.controlPoints[1] = start + (end - start) * this.controlPointFactors[0];
        this.controlPoints[2] = start + (end - start) * this.controlPointFactors[1];

        for (int i = 0; i < this.arrowNodes.Count; ++i)
        {
            // t 分布采用对数函数模拟节点密度更高分布
            float t = Mathf.Log(1f * (i + 1) / (this.arrowNodes.Count), 2f);
            t = Mathf.Clamp01(t);

            // 贝塞尔曲线公式计算位置
            Vector2 pos =
                Mathf.Pow(1 - t, 3) * this.controlPoints[0] +
                3 * Mathf.Pow(1 - t, 2) * t * this.controlPoints[1] +
                3 * (1 - t) * Mathf.Pow(t, 2) * this.controlPoints[2] +
                Mathf.Pow(t, 3) * this.controlPoints[3];

            this.arrowNodes[i].position = pos;

            // 设置旋转
            if (i > 0)
            {
                Vector2 dir = this.arrowNodes[i].position - this.arrowNodes[i - 1].position;
                float angle = Vector2.SignedAngle(Vector2.up, dir);
                this.arrowNodes[i].rotation = Quaternion.Euler(0, 0, angle);
            }

            // 设置缩放，尾部较小，越往前越大
            float scale = this.scaleFactor * (1f - 0.03f * (this.arrowNodes.Count - i - 1));
            this.arrowNodes[i].localScale = new Vector3(scale, scale, 1f);
        }

        // 设置第一个节点方向与第二个一致（避免初始角度出错）
        if (this.arrowNodes.Count >= 2)
            this.arrowNodes[0].rotation = this.arrowNodes[1].rotation;
    }
}
