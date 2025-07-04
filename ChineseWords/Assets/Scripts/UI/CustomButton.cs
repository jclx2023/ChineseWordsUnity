using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 简洁的自定义按钮 - 支持背景和文字同时变色
/// </summary>
public class CustomButton : Selectable, IPointerClickHandler
{
    [Header("组件引用")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI textComponent;

    [Header("背景颜色")]
    [SerializeField] private Color normalBgColor = Color.white;
    [SerializeField] private Color hoverBgColor = Color.gray;
    [SerializeField] private Color pressedBgColor = Color.gray;

    [Header("文字颜色")]
    [SerializeField] private Color normalTextColor = Color.black;
    [SerializeField] private Color hoverTextColor = Color.white;
    [SerializeField] private Color pressedTextColor = Color.white;

    [Header("事件")]
    [SerializeField] private UnityEvent onClick;

    // 缓存初始颜色
    private Color currentBgColor;
    private Color currentTextColor;

    protected override void Awake()
    {
        base.Awake();

        // 自动查找组件（如果没有手动指定）
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (textComponent == null)
            textComponent = GetComponentInChildren<TextMeshProUGUI>();

        // 设置初始颜色
        SetColors(normalBgColor, normalTextColor);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        Color targetBgColor;
        Color targetTextColor;

        // 根据状态选择目标颜色
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
                targetBgColor = normalBgColor * 0.5f; // 禁用时变暗
                targetTextColor = normalTextColor * 0.5f;
                break;
            default:
                targetBgColor = normalBgColor;
                targetTextColor = normalTextColor;
                break;
        }

        // 应用颜色变化
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
    /// 立即设置颜色
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
    /// 平滑颜色过渡
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

        float duration = 0.1f; // 过渡时间
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
    /// 处理点击事件
    /// </summary>
    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable) return;

        onClick?.Invoke();
    }

    /// <summary>
    /// 添加点击监听
    /// </summary>
    public void AddClickListener(UnityAction action)
    {
        onClick.AddListener(action);
    }

    /// <summary>
    /// 移除点击监听
    /// </summary>
    public void RemoveClickListener(UnityAction action)
    {
        onClick.RemoveListener(action);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器中重置组件引用
    /// </summary>
    protected override void Reset()
    {
        base.Reset();

        backgroundImage = GetComponent<Image>();
        textComponent = GetComponentInChildren<TextMeshProUGUI>();

        // 设置默认颜色
        normalBgColor = Color.white;
        hoverBgColor = new Color(0.8f, 0.8f, 0.8f);
        pressedBgColor = new Color(0.6f, 0.6f, 0.6f);

        normalTextColor = Color.black;
        hoverTextColor = Color.black;
        pressedTextColor = Color.black;
    }
#endif
}