using UnityEngine;

/// <summary>
/// 锚弹丸 — 从飞船发射后沿直线飞行，碰撞天体后通知飞船建立绳索连接。
/// 本身不销毁，留在场景中作为视觉反馈（锚插在天体表面）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class AnchorProjectile : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("锚可以和哪些层的物体碰撞")]
    public LayerMask collisionMask = ~0;

    [Tooltip("命中后是否吸附到天体表面")]
    public bool attachToSurface = true;

    [Tooltip("锚命中非天体障碍物时是否销毁")]
    public bool destroyOnObstacle = true;

    // ── 内部状态 ──────────────────────────────────
    private PlayerShip _owner;
    private Rigidbody2D _rb;
    private Vector2 _direction;
    private float _speed;
    private float _maxRange;
    private float _traveledDistance;
    private bool _hasHit;

    // ── 可选视觉组件 ──────────────────────────────
    private LineRenderer _lineRenderer;

    // ═══════════════════════════════════════════════
    //  初始化
    // ═══════════════════════════════════════════════

    public void Initialize(PlayerShip owner, Vector2 direction, float speed, float maxRange)
    {
        _owner = owner;
        _direction = direction.normalized;
        _speed = speed;
        _maxRange = maxRange;
        _traveledDistance = 0f;
        _hasHit = false;

        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.linearDamping = 0f;
        _rb.bodyType = RigidbodyType2D.Kinematic; // 锚不受引力影响
        _rb.linearVelocity = _direction * _speed;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        _lineRenderer = GetComponent<LineRenderer>();
    }

    // ═══════════════════════════════════════════════
    //  Unity 生命周期
    // ═══════════════════════════════════════════════

    private void FixedUpdate()
    {
        if (_hasHit) return;

        _rb.linearVelocity = _direction * _speed;
        _traveledDistance += _speed * Time.fixedDeltaTime;

        if (_traveledDistance >= _maxRange)
        {
            OnMaxRangeReached();
        }
    }

    private void Update()
    {
        // 从飞船画线到锚
        if (_lineRenderer != null && _owner != null)
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.SetPosition(0, _owner.transform.position);
            _lineRenderer.SetPosition(1, transform.position);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit) return;

        if (!IsInLayerMask(other.gameObject.layer, collisionMask)) return;

        CelestialBody body = other.GetComponent<CelestialBody>();
        if (body != null)
        {
            OnHitCelestialBody(body);
            return;
        }

        if (destroyOnObstacle && !other.isTrigger)
        {
            OnHitObstacle();
        }
    }

    // ═══════════════════════════════════════════════
    //  命中处理
    // ═══════════════════════════════════════════════

    private void OnHitCelestialBody(CelestialBody body)
    {
        _hasHit = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Static;

        // 吸附到天体表面
        if (attachToSurface)
        {
            Vector2 toCenter = (Vector2)transform.position - body.Center;
            transform.position = body.Center + toCenter.normalized * body.radius;
        }

        // 通知飞船
        if (_owner != null)
        {
            _owner.OnAnchorHitCelestial(body, this);
        }

        Debug.Log($"[Anchor] 命中天体 {body.name}");
    }

    private void OnHitObstacle()
    {
        _hasHit = true;
        _rb.linearVelocity = Vector2.zero;

        if (_owner != null)
            _owner.OnAnchorDestroyed();

        Debug.Log("[Anchor] 命中障碍物，销毁");
        Destroy(gameObject);
    }

    private void OnMaxRangeReached()
    {
        _hasHit = true;
        _rb.linearVelocity = Vector2.zero;

        if (_owner != null)
            _owner.OnAnchorDestroyed();

        Debug.Log($"[Anchor] 达到最大射程 {_maxRange:F1}m，未命中");
        Destroy(gameObject);
    }

    /// <summary>
    /// 外部调用：主动销毁锚（玩家取消发射或释放锚）。
    /// </summary>
    public void Terminate()
    {
        _hasHit = true;
        _rb.linearVelocity = Vector2.zero;

        if (_owner != null)
            _owner.OnAnchorDestroyed();

        Destroy(gameObject);
    }

    // ═══════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    // ═══════════════════════════════════════════════
    //  调试
    // ═══════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || _hasHit) return;

        if (_owner != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, _owner.transform.position);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, (Vector3)_direction * 0.5f);
    }
#endif
}
