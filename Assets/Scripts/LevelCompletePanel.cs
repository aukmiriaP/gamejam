using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 关卡结算弹窗控制器 — 弹窗出现时暂停游戏，关闭时恢复。
/// 拖入 Panel，通过 Show/ Hide 方法控制。
/// </summary>
public class LevelCompletePanel : MonoBehaviour
{
    [Header("弹窗")]
    [Tooltip("拖入结算弹窗 Panel")]
    public GameObject panel;

    [Header("事件")]
    [Tooltip("弹窗显示时触发")]
    public UnityEvent onPanelShow;

    [Tooltip("弹窗关闭时触发")]
    public UnityEvent onPanelHide;

    private bool _isShowing;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
        _isShowing = false;
    }

    /// <summary>
    /// 显示弹窗并暂停游戏。
    /// </summary>
    public void Show()
    {
        if (_isShowing) return;

        _isShowing = true;

        if (panel != null)
            panel.SetActive(true);

        Time.timeScale = 0f;
        onPanelShow?.Invoke();

        Debug.Log("[LevelComplete] 结算弹窗显示，游戏已暂停");
    }

    /// <summary>
    /// 关闭弹窗并恢复游戏。
    /// </summary>
    public void Hide()
    {
        if (!_isShowing) return;

        _isShowing = false;

        if (panel != null)
            panel.SetActive(false);

        Time.timeScale = 1f;
        onPanelHide?.Invoke();

        Debug.Log("[LevelComplete] 结算弹窗关闭，游戏已恢复");
    }
}
