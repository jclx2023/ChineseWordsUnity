using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ͨ�����ױ��������߻���ѡ���ͷ��
/// </summary>
public class BezierArrowRenderer : MonoBehaviour
{
    [Header("��ͷԤ����")]
    public GameObject ArrowHeadPrefab;
    public GameObject ArrowNodePrefab;

    [Header("��ͷ�ڵ�������������ͷͷ����")]
    public int arrowNodeNum = 10;

    [Header("��������")]
    public float scaleFactor = 1.0f;

    /// ��㣨��ͷ����㣩
    private RectTransform origin;

    /// ���м�ͷ�ڵ㣨������ͷͷ����
    private List<RectTransform> arrowNodes = new List<RectTransform>();

    /// ���������߿��Ƶ㣨0Ϊ��㣬3Ϊ�յ㣩
    private List<Vector2> controlPoints = new List<Vector2>();

    /// ���Ƶ�1��2��ƫ�����ӣ�����յ㷽��
    private readonly List<Vector2> controlPointFactors = new List<Vector2> {
        new Vector2(-0.3f, 0.8f),
        new Vector2(0.3f, 0.8f)
    };

    private void Awake()
    {
        this.origin = this.GetComponent<RectTransform>();

        // ʵ������ͷ�ڵ�
        for (int i = 0; i < this.arrowNodeNum; ++i)
        {
            var node = Instantiate(this.ArrowNodePrefab, this.transform).GetComponent<RectTransform>();
            this.arrowNodes.Add(node);
        }

        // ���һ���Ǽ�ͷͷ��
        var head = Instantiate(this.ArrowHeadPrefab, this.transform).GetComponent<RectTransform>();
        this.arrowNodes.Add(head);

        // ���ؽڵ㣨�Ƶ������⣩
        this.arrowNodes.ForEach(a => a.position = new Vector2(-1000, -1000));

        // ��ʼ��4�����Ƶ�
        for (int i = 0; i < 4; ++i)
            this.controlPoints.Add(Vector2.zero);
    }

    private void Update()
    {
        Vector2 start = this.origin.position;
        Vector2 end = Input.mousePosition;

        // ���� P0��P3
        this.controlPoints[0] = start;
        this.controlPoints[3] = end;

        // ���� P1��P2��������յ㷽������һ��ƫ�ƣ�
        this.controlPoints[1] = start + (end - start) * this.controlPointFactors[0];
        this.controlPoints[2] = start + (end - start) * this.controlPointFactors[1];

        for (int i = 0; i < this.arrowNodes.Count; ++i)
        {
            // t �ֲ����ö�������ģ��ڵ��ܶȸ��߷ֲ�
            float t = Mathf.Log(1f * (i + 1) / (this.arrowNodes.Count), 2f);
            t = Mathf.Clamp01(t);

            // ���������߹�ʽ����λ��
            Vector2 pos =
                Mathf.Pow(1 - t, 3) * this.controlPoints[0] +
                3 * Mathf.Pow(1 - t, 2) * t * this.controlPoints[1] +
                3 * (1 - t) * Mathf.Pow(t, 2) * this.controlPoints[2] +
                Mathf.Pow(t, 3) * this.controlPoints[3];

            this.arrowNodes[i].position = pos;

            // ������ת
            if (i > 0)
            {
                Vector2 dir = this.arrowNodes[i].position - this.arrowNodes[i - 1].position;
                float angle = Vector2.SignedAngle(Vector2.up, dir);
                this.arrowNodes[i].rotation = Quaternion.Euler(0, 0, angle);
            }

            // �������ţ�β����С��Խ��ǰԽ��
            float scale = this.scaleFactor * (1f - 0.03f * (this.arrowNodes.Count - i - 1));
            this.arrowNodes[i].localScale = new Vector3(scale, scale, 1f);
        }

        // ���õ�һ���ڵ㷽����ڶ���һ�£������ʼ�Ƕȳ���
        if (this.arrowNodes.Count >= 2)
            this.arrowNodes[0].rotation = this.arrowNodes[1].rotation;
    }
}
