using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// �����Զ��尴ť - ֧�ֱ���������ͬʱ��ɫ
/// </summary>
public class CustomButton : Selectable, IPointerClickHandler
{
    [Header("�������")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI textComponent;

    [Header("������ɫ")]
    [SerializeField] private Color normalBgColor = Color.white;
    [SerializeField] private Color hoverBgColor = Color.gray;
    [SerializeField] private Color pressedBgColor = Color.gray;

    [Header("������ɫ")]
    [SerializeField] private Color normalTextColor = Color.black;
    [SerializeField] private Color hoverTextColor = Color.white;
    [SerializeField] private Color pressedTextColor = Color.white;

    [Header("�¼�")]
    [SerializeField] private UnityEvent onClick;

    // �����ʼ��ɫ
    private Color currentBgColor;
    private Color currentTextColor;

    protected override void Awake()
    {
        base.Awake();

        // �Զ�������������û���ֶ�ָ����
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (textComponent == null)
            textComponent = GetComponentInChildren<TextMeshProUGUI>();

        // ���ó�ʼ��ɫ
        SetColors(normalBgColor, normalTextColor);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        Color targetBgColor;
        Color targetTextColor;

        // ����״̬ѡ��Ŀ����ɫ
        switch (state)
        {
            case SelectionState.Normal:
                targetBgColor = normalBgColor;
                targetTextColor = normalTextColor;
                break;
            case SelectionState.Highlighted:
                targetBgColor = hoverBgColor;
                targetTextColor = hoverTextColor;
                break;
            case SelectionState.Pressed:
                targetBgColor = pressedBgColor;
                targetTextColor = pressedTextColor;
                break;
            case SelectionState.Disabled:
                targetBgColor = normalBgColor * 0.5f; // ����ʱ�䰵
                targetTextColor = normalTextColor * 0.5f;
                break;
            default:
                targetBgColor = normalBgColor;
                targetTextColor = normalTextColor;
                break;
        }

        // Ӧ����ɫ�仯
        if (instant)
        {
            SetColors(targetBgColor, targetTextColor);
        }
        else
        {
            StartColorTransition(targetBgColor, targetTextColor);
        }
    }

    /// <summary>
    /// ����������ɫ
    /// </summary>
    private void SetColors(Color bgColor, Color textColor)
    {
        currentBgColor = bgColor;
        currentTextColor = textColor;

        if (backgroundImage != null)
            backgroundImage.color = bgColor;

        if (textComponent != null)
            textComponent.color = textColor;
    }

    /// <summary>
    /// ƽ����ɫ����
    /// </summary>
    private void StartColorTransition(Color targetBgColor, Color targetTextColor)
    {
        StopAllCoroutines();
        StartCoroutine(ColorTransitionCoroutine(targetBgColor, targetTextColor));
    }

    private System.Collections.IEnumerator ColorTransitionCoroutine(Color targetBgColor, Color targetTextColor)
    {
        Color startBgColor = currentBgColor;
        Color startTextColor = currentTextColor;

        float duration = 0.1f; // ����ʱ��
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            Color lerpedBgColor = Color.Lerp(startBgColor, targetBgColor, t);
            Color lerpedTextColor = Color.Lerp(startTextColor, targetTextColor, t);

            SetColors(lerpedBgColor, lerpedTextColor);

            yield return null;
        }

        SetColors(targetBgColor, targetTextColor);
    }

    /// <summary>
    /// �������¼�
    /// </summary>
    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable) return;

        onClick?.Invoke();
    }

    /// <summary>
    /// ��ӵ������
    /// </summary>
    public void AddClickListener(UnityAction action)
    {
        onClick.AddListener(action);
    }

    /// <summary>
    /// �Ƴ��������
    /// </summary>
    public void RemoveClickListener(UnityAction action)
    {
        onClick.RemoveListener(action);
    }

#if UNITY_EDITOR
    /// <summary>
    /// �༭���������������
    /// </summary>
    protected override void Reset()
    {
        base.Reset();

        backgroundImage = GetComponent<Image>();
        textComponent = GetComponentInChildren<TextMeshProUGUI>();

        // ����Ĭ����ɫ
        normalBgColor = Color.white;
        hoverBgColor = new Color(0.8f, 0.8f, 0.8f);
        pressedBgColor = new Color(0.6f, 0.6f, 0.6f);

        normalTextColor = Color.black;
        hoverTextColor = Color.black;
        pressedTextColor = Color.black;
    }
#endif
}