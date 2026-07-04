using UnityEngine;
using System.Collections;

public class LinearPatrol : MonoBehaviour
{
    [Header("巡逻配置")]
    [Tooltip("巡逻方向的角度（度数，0表示正右方，90表示正上方）")]
    [Range(0f, 360f)]
    public float patrolAngle = 0f;

    [Tooltip("单向巡逻的最大距离")]
    public float patrolDistance = 5f;

    [Tooltip("最大移动速度")]
    public float maxSpeed = 5f;

    [Tooltip("加速度")]
    public float acceleration = 2f;

    [Header("时间控制（用于错开敌人）")]
    [Tooltip("到达端点后的静止等待时间（秒）")]
    public float freezeTime = 0.5f;

    [Tooltip("游戏开局时的初始延迟出发时间（秒），用于彻底错开多只怪物")]
    public float initialDelay = 0f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 direction;

    private float currentSpeed = 0f;
    private bool movingToTarget = true;
    private bool isWaiting = false; // 是否处于静止等待状态

    void Start()
    {
        // 记录初始位置
        startPosition = transform.position;
        // 初始化计算目标点
        UpdatePatrolPoints();

        // 如果设置了开局延迟，先进入等待状态
        if (initialDelay > 0f)
        {
            StartCoroutine(WaitRoutine(initialDelay));
        }
    }

    void Update()
    {
        // 如果正处于静止等待状态，则不执行移动逻辑
        if (isWaiting) return;

        // 动态计算当前的目标点
        Vector3 currentTarget = movingToTarget ? targetPosition : startPosition;

        // 计算到目标点的向量和距离
        Vector3 toTarget = currentTarget - transform.position;
        float distanceToTarget = toTarget.magnitude;

        // 判断是否到达目标点（容差 0.05 避免浮点数抖动）
        if (distanceToTarget < 0.05f)
        {
            // 归零速度
            currentSpeed = 0f;
            // 切换方向标志
            movingToTarget = !movingToTarget;

            // 如果开启了静止时间，触发协程等待
            if (freezeTime > 0f)
            {
                StartCoroutine(WaitRoutine(freezeTime));
            }
            return;
        }

        // --- 加速度控制逻辑 ---
        // 随着时间平滑增加速度，但不超过最大速度
        currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);

        // 如果快要到了，开始平滑减速
        float brakingDistance = (currentSpeed * currentSpeed) / (2f * acceleration);
        if (distanceToTarget < brakingDistance)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0.1f, acceleration * Time.deltaTime);
        }

        // --- 移动物体 ---
        Vector3 moveDir = toTarget.normalized;
        transform.position += moveDir * currentSpeed * Time.deltaTime;
    }

    /// <summary>
    /// 专用于处理静止等待的协程
    /// </summary>
    private IEnumerator WaitRoutine(float time)
    {
        isWaiting = true;
        yield return new WaitForSeconds(time);
        isWaiting = false;
    }

    /// <summary>
    /// 根据设定的角度和距离，计算出目标终点
    /// </summary>
    [ContextMenu("Update Patrol Points")]
    public void UpdatePatrolPoints()
    {
        float radians = patrolAngle * Mathf.Deg2Rad;
        direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f).normalized;
        targetPosition = startPosition + direction * patrolDistance;
    }

    // 在编辑器场景中绘制辅助线
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            startPosition = transform.position;
            UpdatePatrolPoints();
        }

        Gizmos.color = Color.green;
        Gizmos.DrawLine(startPosition, targetPosition);
        Gizmos.DrawWireSphere(startPosition, 0.2f);
        Gizmos.DrawWireSphere(targetPosition, 0.2f);
    }
}