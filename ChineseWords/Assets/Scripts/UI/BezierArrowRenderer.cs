using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ͨ�����ױ��������߻���ѡ���ͷ - ֧���ⲿ���ư汾
/// �ں���DynamicArrowSystem��3D֧�֡����������Ͷ�̬Ч��
/// ��������߹���֧��
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
    public bool invertCurve = true; // �Ƿ�ת���߷���

    [Header("3D��ȿ���")]
    public float refControlPoint1Height = 100.0f; // �ο����Ƶ�߶ȣ�ȷ����ͷ��������
    [Range(0f, 1f)]
    public float controlPoint1LengthRatio = 0.6f; // P1�߶ȿ��Ʊ���
    [Range(0f, 1f)]
    public float controlPoint2LengthRatio = 0.5f; // P2λ�ÿ��Ʊ���
    public float controlPoint1Height = 0f; // P1��Z���
    public float controlPoint2Height = 0f; // P2��Z���
    public float controlPoint3Height = 0f; // P3��Z���

    [Header("��̬Ч��")]
    [SerializeField] private bool enableFloatingEffect = true; // �Ƿ����ø���Ч��
    [SerializeField] private float floatingSpeed = 0.1f; // �����ٶ�
    [SerializeField] private float floatingThresholdMax = 0.5f; // ������ֵ

    [Header("�ڵ�ֲ�")]
    public bool useLogarithmicDistribution = false; // �Ƿ�ʹ�ö����ֲ�
    [Range(0.1f, 3f)]
    public float distributionPower = 2f; // �ֲ����ݴΣ��������ֲ�ʱʹ�ã�

    [Header("��������")]
    [Range(0f, 1f)]
    public float minScale = 0.2f; // β����С����
    [Range(0f, 2f)]
    public float maxScale = 1.0f; // ͷ���������

    [Header("�ⲿ��������")]
    [SerializeField] private bool useExternalControl = false; // �Ƿ�ʹ���ⲿ����
    [SerializeField] private Vector3 externalStartPosition = Vector3.zero; // �ⲿ���õ���� (��ΪVector3)
    [SerializeField] private Vector3 externalEndPosition = Vector3.zero; // �ⲿ���õ��յ� (��ΪVector3)

    [Header("�������")]
    [SerializeField] private Material outlineMaterial; // ��߲��ʣ�����
    [SerializeField] private Material originalMaterial; // ԭʼ���ʱ���

    // �ڲ�����
    private RectTransform origin;
    private List<RectTransform> arrowNodes = new List<RectTransform>();
    private List<Vector3> controlPoints = new List<Vector3>(); // ��ΪVector3֧��3D
    private Canvas parentCanvas;

    // �ⲿ����״̬
    private bool isExternallyControlled = false;

    // ��߿���״̬
    private bool isOutlineEnabled = false;
    private Color currentOutlineColor = Color.green;
    private float currentOutlineWidth = 2.0f;

    #region ��߿��ƽӿ�

    /// <summary>
    /// �����������״̬
    /// </summary>
    public void SetOutlineEnabled(bool enabled)
    {
        if (isOutlineEnabled == enabled) return;

        isOutlineEnabled = enabled;
        ApplyOutlineSettings();

        Debug.Log($"[BezierArrowRenderer] �����{(enabled ? "����" : "����")}");
    }

    /// <summary>
    /// ���������ɫ
    /// </summary>
    public void SetOutlineColor(Color color)
    {
        currentOutlineColor = color;

        if (isOutlineEnabled && outlineMaterial != null)
        {
            outlineMaterial.SetColor("_OutlineColor", color);
            Debug.Log($"[BezierArrowRenderer] �����ɫ����Ϊ: {color}");
        }
    }

    /// <summary>
    /// ������߿��
    /// </summary>
    public void SetOutlineWidth(float width)
    {
        currentOutlineWidth = width;

        if (isOutlineEnabled && outlineMaterial != null)
        {
            outlineMaterial.SetFloat("_OutlineWidth", width);
            Debug.Log($"[BezierArrowRenderer] ��߿������Ϊ: {width}");
        }
    }

    /// <summary>
    /// Ӧ��������õ����нڵ�
    /// </summary>
    private void ApplyOutlineSettings()
    {
        Material targetMaterial = isOutlineEnabled ? outlineMaterial : originalMaterial;

        if (targetMaterial == null)
        {
            Debug.LogWarning("[BezierArrowRenderer] Ŀ�����Ϊ�գ��޷�Ӧ���������");
            return;
        }

        // ���������ߣ����ò��ʲ���
        if (isOutlineEnabled && outlineMaterial != null)
        {
            outlineMaterial.SetColor("_OutlineColor", currentOutlineColor);
            outlineMaterial.SetFloat("_OutlineWidth", currentOutlineWidth);
            outlineMaterial.SetFloat("_OutlineEnabled", 1.0f);
        }

        // Ӧ�õ����нڵ�
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

        Debug.Log($"[BezierArrowRenderer] ���������Ӧ�õ� {appliedCount} ���ڵ�");
    }

    /// <summary>
    /// ��ʼ����߲���
    /// </summary>
    private void InitializeOutlineMaterials()
    {
        // ���ݵ�һ���ڵ��ԭʼ����
        if (arrowNodes.Count > 0 && arrowNodes[0] != null)
        {
            Image firstImage = arrowNodes[0].GetComponent<Image>();
            if (firstImage != null)
            {
                originalMaterial = firstImage.material;
                Debug.Log($"[BezierArrowRenderer] ԭʼ�����ѱ���: {originalMaterial?.name ?? "null"}");
            }
        }

        // ���û��������߲��ʣ����Դ�Resources����
        if (outlineMaterial == null)
        {
            outlineMaterial = Resources.Load<Material>("Materials/ArrowOutlineMaterial");
        }

        // ��ʼ�����Ϊ����״̬
        if (outlineMaterial != null)
        {
            outlineMaterial.SetFloat("_OutlineEnabled", 0.0f);
        }
    }

    #endregion

    private void Awake()
    {
        this.origin = this.GetComponent<RectTransform>();

        // ��ȡCanvas
        parentCanvas = GetComponentInParent<Canvas>();

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
        this.arrowNodes.ForEach(a => a.anchoredPosition3D = new Vector3(-10000, -10000, 0)); // ʹ��3Dλ��

        // ��ʼ�����Ƶ�
        for (int i = 0; i < 4; ++i)
            this.controlPoints.Add(Vector3.zero); // ��ΪVector3

        // ��ʼ����߲���
        InitializeOutlineMaterials();
    }

    private void Update()
    {
        if (parentCanvas == null || arrowNodes.Count == 0) return;

        // ����ⲿ���ƣ�ʹ���ⲿ���õ�λ��
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
    /// ʹ�������Ƶĸ��£�ԭ���߼���
    /// </summary>
    private void UpdateWithMouseControl()
    {
        // ��ȡ���λ��
        Vector2 parentLocal;
        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            origin.parent as RectTransform,
            Input.mousePosition,
            null,
            out parentLocal);

        if (!success) return;

        // ת��ΪVector3��Z=0
        Vector3 mousePos3D = new Vector3(parentLocal.x, parentLocal.y, 0);

        // ������Ƶ�
        CalculateControlPoints(Vector3.zero, mousePos3D);

        // ���½ڵ�
        UpdateArrowNodes();
    }

    /// <summary>
    /// ʹ���ⲿ���Ƶĸ���
    /// </summary>
    private void UpdateWithExternalControl()
    {
        // ������Ƶ�
        CalculateControlPoints(externalStartPosition, externalEndPosition);

        // ���½ڵ�
        UpdateArrowNodes();
    }

    /// <summary>
    /// ������Ƶ� - �ں�DynamicArrowSystem�����������㷨
    /// </summary>
    private void CalculateControlPoints(Vector3 startPosition, Vector3 targetPosition)
    {
        this.controlPoints[0] = startPosition; // P0: ���
        this.controlPoints[3] = targetPosition; // P3: �յ�

        Vector3 direction = targetPosition - startPosition;
        float distance = direction.magnitude;
        float noCurveDist = 0.1f;

        if (distance <= noCurveDist)
        {
            // ����̫Сʱ���˻�Ϊֱ��
            this.controlPoints[1] = Vector3.Lerp(this.controlPoints[0], this.controlPoints[3], controlPoint1Position);
            this.controlPoints[2] = Vector3.Lerp(this.controlPoints[0], this.controlPoints[3], controlPoint2Position);
        }
        else
        {
            // ʹ��DynamicArrowSystem�����������㷨
            var lenY = Mathf.Abs(this.controlPoints[3].y - this.controlPoints[0].y);
            var lenXSigned = this.controlPoints[3].x - this.controlPoints[0].x;

            // ����P1 - ���ϵĿ��Ƶ�
            this.controlPoints[1] = this.controlPoints[0] + Vector3.up * lenY * controlPoint1LengthRatio;
            var y1 = (refControlPoint1Height + this.controlPoints[1].y) * 0.5f;
            this.controlPoints[1] = new Vector3(this.controlPoints[1].x, y1, controlPoint1Height);

            // ����P2 - ����Ŀ��Ƶ�
            this.controlPoints[2] = this.controlPoints[3] + Vector3.left * lenXSigned * controlPoint2LengthRatio;
            var y2 = (this.controlPoints[1].y + this.controlPoints[2].y) * 0.5f;
            this.controlPoints[2] = new Vector3(this.controlPoints[2].x, y2, controlPoint2Height);

            // ����P3��Z���
            this.controlPoints[3] = new Vector3(this.controlPoints[3].x, this.controlPoints[3].y, controlPoint3Height);
        }
    }

    /// <summary>
    /// ���¼�ͷ�ڵ� - ��Ϊ����������֧��3D�Ͷ�̬Ч��
    /// </summary>
    public void UpdateArrowNodes()
    {
        for (int i = 0; i < this.arrowNodes.Count; ++i)
        {
            var node = this.arrowNodes[i];
            node.gameObject.SetActive(true);

            // �������tֵ
            float t = CalculateTValue(i);

            // ��Ӷ�̬����Ч��
            float t_floating = t;
            if (enableFloatingEffect && i != this.arrowNodes.Count - 1) // ͷ��������
            {
                float div = 1f / (this.arrowNodes.Count - 1);
                t_floating += Time.time * floatingSpeed;
                while (t_floating > t + div * floatingThresholdMax)
                    t_floating -= div;
            }

            // ���㱴��������λ�� (3D)
            Vector3 pos = CalculateBezierPoint(t_floating);

            // ��֤������
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
            {
                pos = Vector3.Lerp(this.controlPoints[0], this.controlPoints[3], t);
            }

            // ����3Dλ��
            node.anchoredPosition3D = pos;

            // ��������
            float scale = CalculateScale(i);
            node.localScale = new Vector3(scale, scale, 1f);

            // ������ת
            if (i > 0)
            {
                Vector3 dir = node.anchoredPosition3D - this.arrowNodes[i - 1].anchoredPosition3D;
                if (dir.magnitude > 0.001f)
                {
                    // 3D��ת����
                    node.rotation = Quaternion.LookRotation(-Vector3.forward, dir);
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

    /// <summary>
    /// ���㱴�������ߵ� - ֧��3D
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

    #region �����ӿ� - ���ⲿ����ʹ�� (���ּ�����)

    /// <summary>
    /// �����ⲿ����ģʽ - ����Vector2�ӿڼ�����
    /// </summary>
    public void EnableExternalControl(Vector2 startPosition, Vector2 endPosition)
    {
        EnableExternalControl(new Vector3(startPosition.x, startPosition.y, 0),
                             new Vector3(endPosition.x, endPosition.y, 0));
    }

    /// <summary>
    /// �����ⲿ����ģʽ - �µ�3D�汾
    /// </summary>
    public void EnableExternalControl(Vector3 startPosition, Vector3 endPosition)
    {
        isExternallyControlled = true;
        externalStartPosition = startPosition;
        externalEndPosition = endPosition;

        // ��������һ��
        UpdateWithExternalControl();
    }

    /// <summary>
    /// �����ⲿ���ƣ�����������ģʽ
    /// </summary>
    public void DisableExternalControl()
    {
        isExternallyControlled = false;
    }

    /// <summary>
    /// �������λ�ã��ⲿ����ģʽ�£�- ����Vector2�ӿڼ�����
    /// </summary>
    public void SetStartPosition(Vector2 startPosition)
    {
        SetStartPosition(new Vector3(startPosition.x, startPosition.y, 0));
    }

    /// <summary>
    /// �������λ�ã��ⲿ����ģʽ�£�- �µ�3D�汾
    /// </summary>
    public void SetStartPosition(Vector3 startPosition)
    {
        externalStartPosition = startPosition;

        // ��������ⲿ����ģʽ����������
        if (isExternallyControlled)
        {
            UpdateWithExternalControl();
        }
    }

    /// <summary>
    /// �����յ�λ�ã��ⲿ����ģʽ�£�- ����Vector2�ӿڼ�����
    /// </summary>
    public void SetEndPosition(Vector2 endPosition)
    {
        SetEndPosition(new Vector3(endPosition.x, endPosition.y, 0));
    }

    /// <summary>
    /// �����յ�λ�ã��ⲿ����ģʽ�£�- �µ�3D�汾
    /// </summary>
    public void SetEndPosition(Vector3 endPosition)
    {
        externalEndPosition = endPosition;

        // ��������ⲿ����ģʽ����������
        if (isExternallyControlled)
        {
            UpdateWithExternalControl();
        }
    }

    #endregion
}