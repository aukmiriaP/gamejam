using UnityEngine;

/// <summary>
/// 天体 — 带有引力场，越接近中心引力越强（平方反比）。
/// 挂载到带有 CircleCollider2D 的 GameObject 上。
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class CelestialBody : MonoBehaviour
{
    [Header("天体属性")]
    [Tooltip("天体半径（应与 CircleCollider2D 的 radius 一致）")]
    public float radius = 2f;

    [Tooltip("天体引力强度（越大引力越强）")]
    public float gravityStrength = 30f;

    [Tooltip("引力衰减曲线：2 = 平方反比（真实引力），值越大衰减越快")]
    public float gravityFalloff = 2f;

    [Tooltip("引力最大作用范围（0 = 无限远）")]
    public float gravityRange = 0f;

    [Header("表面保护")]
    [Tooltip("表面排斥力强度（防止飞船穿入天体内部）")]
    public float surfaceRepulsion = 100f;

    [Tooltip("表面保护外扩距离")]
    public float surfaceMargin = 0.3f;

    [Header("调试可视化")]
    [Tooltip("是否绘制引力场参考线")]
    public bool showGravityField = true;

    [Tooltip("引力场同心参考线数量")]
    [Range(1, 10)]
    public int gravityFieldRings = 5;

    // ── 内部引用 ──────────────────────────────────
    private CircleCollider2D _collider;
    private Rigidbody2D _rb;

    public Vector2 Center => (Vector2)transform.position;

    // ═══════════════════════════════════════════════
    //  Unity 生命周期
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        _collider = GetComponent<CircleCollider2D>();
        _collider.isTrigger = true;
        SyncColliderRadius();

        // 确保有 Rigidbody2D 作为 DistanceJoint2D 的锚点
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void OnValidate()
    {
        if (_collider == null)
            _collider = GetComponent<CircleCollider2D>();
        SyncColliderRadius();
    }

    private void SyncColliderRadius()
    {
        if (_collider != null && Mathf.Abs(_collider.radius - radius) > 0.001f)
        {
            _collider.radius = radius;
        }
    }

    /// <summary>
    /// 获取天体 Rigidbody2D（DistanceJoint2D 连接用）
    /// </summary>
    public Rigidbody2D AttachedRigidbody => _rb;

    // ═══════════════════════════════════════════════
    //  引力 & 表面排斥
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 计算给定位置的引力加速度（方向指向天体中心，大小按距离衰减）。
    /// 使用平方反比或自定义衰减曲线。
    /// </summary>
    public Vector2 GetGravityAt(Vector2 position)
    {
        if (gravityStrength <= 0f) return Vector2.zero;

        Vector2 toTarget = position - Center;
        float dist = toTarget.magnitude;

        // 在天体内部不施加引力（由表面排斥力处理）
        if (dist < radius * 0.1f) return Vector2.zero;

        // 超出引力范围则不施加
        if (gravityRange > 0f && dist > gravityRange) return Vector2.zero;

        // F = G / r^falloff，方向指向中心
        Vector2 direction = -toTarget.normalized;
        float strength = gravityStrength / Mathf.Pow(dist, gravityFalloff);

        return direction * strength;
    }

    /// <summary>
    /// 对给定的 Rigidbody2D 施加此天体的引力。
    /// 由飞船在 FixedUpdate 中调用。
    /// </summary>
    public void ApplyGravityTo(Rigidbody2D targetRb)
    {
        Vector2 acceleration = GetGravityAt(targetRb.position);
        if (acceleration != Vector2.zero)
            targetRb.AddForce(acceleration * targetRb.mass);
    }

    /// <summary>
    /// 对给定的 Rigidbody2D 施加表面排斥力（防止穿入天体）。
    /// 距离越近排斥力越强。
    /// </summary>
    public void ApplySurfaceRepulsionTo(Rigidbody2D targetRb)
    {
        Vector2 toTarget = targetRb.position - Center;
        float dist = toTarget.magnitude;
        float minDist = radius + surfaceMargin;

        if (dist >= minDist || dist < 0.001f) return;

        // 越深入排斥力越强（线性递增）
        Vector2 outward = toTarget.normalized;
        float penetration = minDist - dist;
        float force = surfaceRepulsion * penetration;

        targetRb.AddForce(outward * force);
    }

    // ═══════════════════════════════════════════════
    //  调试
    // ═══════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // ── 天体本体 ──
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);

        // ── 表面保护范围 ──
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, radius + surfaceMargin);

        // ── 引力场参考线 ──
        if (showGravityField && gravityStrength > 0f)
        {
            float maxR = gravityRange > 0f ? gravityRange : radius * 5f;

            for (int i = 1; i <= gravityFieldRings; i++)
            {
                float t = (float)i / gravityFieldRings;
                float ringR = Mathf.Lerp(radius + surfaceMargin, maxR, t);

                // 越远越透明
                float alpha = Mathf.Lerp(0.5f, 0.05f, t);
                Gizmos.color = new Color(0.4f, 0.6f, 1f, alpha);

                DrawWireCircle(transform.position, ringR, 64);
            }

            // 最外圈用虚线提示截断
            if (gravityRange > 0f)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
                DrawWireCircle(transform.position, gravityRange, 64);
            }
        }
    }

    private static void DrawWireCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prev = center + new Vector3(Mathf.Cos(0), Mathf.Sin(0), 0) * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = i * angleStep * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
