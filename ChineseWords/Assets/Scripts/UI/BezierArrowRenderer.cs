using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ͨ�����ױ��������߻���ѡ���ͷ
/// </summary>
public class BezierArrowRenderer : MonoBehaviour
{
    [Header("��ͷԤ����")]
    public GameObject ArrowHeadPrefab;
    public GameObject ArrowNodePrefab;

    [Header("��ͷ����")]
    public int arrowNodeNum = 10;
    public float scaleFactor = 1.0f;

    [Header("���������߲���")]
    [Range(0f, 1f)]
    public float controlPoint1Position = 0.33f; // P1��ֱ���ϵ�λ�ñ���
    [Range(0f, 1f)]
    public float controlPoint2Position = 0.67f; // P2��ֱ���ϵ�λ�ñ���
    [Range(0f, 1f)]
    public float curveIntensity = 0.2f; // ��������ǿ��
    public bool invertCurve = false; // �Ƿ�ת���߷���

    [Header("�ڵ�ֲ�")]
    public bool useLogarithmicDistribution = false; // �Ƿ�ʹ�ö����ֲ�
    [Range(0.1f, 3f)]
    public float distributionPower = 2f; // �ֲ����ݴΣ��������ֲ�ʱʹ�ã�

    [Header("��������")]
    [Range(0f, 1f)]
    public float minScale = 0.3f; // β����С����
    [Range(0f, 2f)]
    public float maxScale = 1.0f; // ͷ���������

    // �ڲ�����
    private RectTransform origin;
    private List<RectTransform> arrowNodes = new List<RectTransform>();
    private List<Vector2> controlPoints = new List<Vector2>();
    private Canvas parentCanvas;

    private void Awake()
    {
        this.origin = this.GetComponent<RectTransform>();

        // ��ȡCanvas
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogError("BezierArrowRenderer������Canvas��ʹ�ã�");
            return;
        }

        // �����ڵ�
        for (int i = 0; i < this.arrowNodeNum; ++i)
        {
            var nodeObj = Instantiate(this.ArrowNodePrefab, this.transform);
            var node = nodeObj.GetComponent<RectTransform>();
            this.arrowNodes.Add(node);
        }

        // ������ͷͷ��
        var headObj = Instantiate(this.ArrowHeadPrefab, this.transform);
        var head = headObj.GetComponent<RectTransform>();
        this.arrowNodes.Add(head);

        // ��ʼ������
        this.arrowNodes.ForEach(a => a.anchoredPosition = new Vector2(-10000, -10000));

        // ��ʼ�����Ƶ�
        for (int i = 0; i < 4; ++i)
            this.controlPoints.Add(Vector2.zero);
    }

    private void Update()
    {
        if (parentCanvas == null || arrowNodes.Count == 0) return;

        // ��ȡ���λ��
        Vector2 parentLocal;
        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            origin.parent as RectTransform,
            Input.mousePosition,
            null,
            out parentLocal);

        if (!success) return;

        // ������Ƶ�
        CalculateControlPoints(parentLocal);

        // ���½ڵ�
        UpdateArrowNodes();
    }

    private void CalculateControlPoints(Vector2 targetPosition)
    {
        this.controlPoints[0] = Vector2.zero; // P0: ���
        this.controlPoints[3] = targetPosition; // P3: �յ�

        Vector2 direction = targetPosition - Vector2.zero;
        float distance = direction.magnitude;

        if (distance < 0.1f)
        {
            // ����̫Сʱ���˻�Ϊֱ��
            this.controlPoints[1] = Vector2.Lerp(this.controlPoints[0], this.controlPoints[3], controlPoint1Position);
            this.controlPoints[2] = Vector2.Lerp(this.controlPoints[0], this.controlPoints[3], controlPoint2Position);
        }
        else
        {
            // ���㴹ֱ����
            Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;
            if (invertCurve) perpendicular = -perpendicular;

            // ������Ƶ����λ��
            Vector2 p1Base = this.controlPoints[0] + direction * controlPoint1Position;
            Vector2 p2Base = this.controlPoints[0] + direction * controlPoint2Position;

            // ��Ӵ�ֱƫ��
            float offset1 = distance * curveIntensity;
            float offset2 = distance * curveIntensity;

            this.controlPoints[1] = p1Base + perpendicular * offset1;
            this.controlPoints[2] = p2Base - perpendicular * offset2;
        }
    }

    private void UpdateArrowNodes()
    {
        for (int i = 0; i < this.arrowNodes.Count; ++i)
        {
            // ����tֵ
            float t = CalculateTValue(i);

            // ���㱴��������λ��
            Vector2 pos = CalculateBezierPoint(t);

            // ��֤������
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y))
            {
                pos = Vector2.Lerp(this.controlPoints[0], this.controlPoints[3], t);
            }

            // ����λ��
            this.arrowNodes[i].anchoredPosition = pos;

            // ��������
            float scale = CalculateScale(i);
            this.arrowNodes[i].localScale = new Vector3(scale, scale, 1f);

            // ������ת
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

        // ���õ�һ���ڵ���ת
        if (this.arrowNodes.Count >= 2)
        {
            this.arrowNodes[0].rotation = this.arrowNodes[1].rotation;
        }
    }

    private float CalculateTValue(int index)
    {
        if (index == 0)
        {
            // ��һ���ڵ�ʼ�������
            return 0f;
        }

        if (useLogarithmicDistribution)
        {
            float t = Mathf.Log(1f * index / (this.arrowNodes.Count - 1), distributionPower);
            return Mathf.Clamp01(t);
        }
        else
        {
            // ���Էֲ����ӵڶ����ڵ㿪ʼ�����һ���ڵ�
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
}