using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 关卡结算点 — 当 Tag 为 Player 的物体进入触发器时弹窗并暂停游戏。
/// 拖入终点 Transform 和结算 Panel。
/// </summary>
public class LevelFinishPoint : MonoBehaviour
{
    [Header("结算点设置")]
    [Tooltip("拖入终点物体的 Transform（可选，用于视觉反馈位置）")]
    public Transform finishPoint;

    [Header("弹窗")]
    [Tooltip("拖入结算弹窗 Panel")]
    public GameObject completePanel;

    [Header("碰撞检测")]
    [Tooltip("触发结算的 Tag")]
    public string targetTag = "Player";

    [Header("结算事件")]
    [Tooltip("弹窗显示时触发")]
    public UnityEvent onPanelShow;

    [Tooltip("弹窗关闭时触发")]
    public UnityEvent onPanelHide;

    [Tooltip("玩家到达终点时额外触发")]
    public UnityEvent onPlayerReachFinish;

    private bool _finished;
    private bool _isShowing;

    private void Awake()
    {
        _finished = false;
        _isShowing = false;

        if (completePanel != null)
            completePanel.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TriggerFinish(other.CompareTag(targetTag), other.name);
    }

    private void OnTriggerEnter(Collider other)
    {
        TriggerFinish(other.CompareTag(targetTag), other.name);
    }

    private void TriggerFinish(bool isPlayer, string name)
    {
        if (_finished || !isPlayer) return;

        _finished = true;
        Debug.Log($"[LevelFinish] {name} 到达终点");

        onPlayerReachFinish?.Invoke();
        ShowPanel();
    }

    /// <summary>
    /// 显示弹窗并暂停游戏。
    /// </summary>
    public void ShowPanel()
    {
        if (_isShowing) return;

        _isShowing = true;

        if (completePanel != null)
            completePanel.SetActive(true);

        Time.timeScale = 0f;
        onPanelShow?.Invoke();

        Debug.Log("[LevelFinish] 结算弹窗显示，游戏已暂停");
    }

    /// <summary>
    /// 关闭弹窗并恢复游戏。
    /// </summary>
    public void HidePanel()
    {
        if (!_isShowing) return;

        _isShowing = false;

        if (completePanel != null)
            completePanel.SetActive(false);

        Time.timeScale = 1f;
        onPanelHide?.Invoke();

        Debug.Log("[LevelFinish] 结算弹窗关闭，游戏已恢复");
    }

    /// <summary>
    /// 重置结算点（支持关卡重置）
    /// </summary>
    public void ResetFinishPoint()
    {
        _finished = false;
        _isShowing = false;

        if (completePanel != null)
            completePanel.SetActive(false);

        Time.timeScale = 1f;
    }
}
