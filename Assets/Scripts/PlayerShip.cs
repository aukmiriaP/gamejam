using UnityEngine;

/// <summary>
/// 玩家飞船控制器 — 全部使用 Unity 原生物理力驱动。
///
/// 移动：飞船头部自动指向运动方向，持续施加前进推力，受所有天体引力场影响。
/// 锚钩：命中天体后使用 DistanceJoint2D 建立绳索约束。
///      绳索保持固定长度，引力继续拉扯飞船，自然形成圆周/椭圆轨道。
///
/// 操作：
/// - 按左键发射锚（朝飞船前方）
/// - 锚命中天体 → DistanceJoint2D 自动维持绳长
/// - 按住左键保持绳索，松开 → 解除关节、切线飞出
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerShip : MonoBehaviour
{
    [Header("飞行参数")]
    [Tooltip("飞船持续推力 (N)")]
    public float thrustForce = 8f;

    [Tooltip("最大飞行速度 (m/s)，防止无限加速")]
    public float maxSpeed = 15f;

    [Tooltip("飞船转向（朝向速度方向）的角速度 (度/秒)")]
    public float turnRate = 360f;

    [Header("锚参数")]
    [Tooltip("锚的预制体")]
    public AnchorProjectile anchorPrefab;

    [Tooltip("锚的最大发射距离")]
    public float anchorMaxRange = 15f;

    [Tooltip("锚的飞行速度")]
    public float anchorSpeed = 20f;

    [Tooltip("锚发射冷却时间 (秒)")]
    public float anchorCooldown = 0.3f;

    [Header("输入设置")]
    [Tooltip("发射/保持锚的按键")]
    public KeyCode fireKey = KeyCode.Mouse0;

    [Header("测试控制")]
    [Tooltip("WASD 测试推力 (N)，0 = 禁用")]
    public float testThrustForce = 0f;

    // ── 运行时状态 ────────────────────────────────
    public enum ShipState
    {
        FreeFlight,   // 自由飞行，受引力影响
        Anchoring,    // 锚飞行中
        Tethered      // DistanceJoint2D 约束中，受引力影响
    }

    [Header("调试（只读）")]
    [SerializeField] private ShipState _currentState = ShipState.FreeFlight;
    public ShipState CurrentState => _currentState;

    // ── 内部引用 ──────────────────────────────────
    private Rigidbody2D _rb;
    private CelestialBody[] _celestialBodies;
    private AnchorProjectile _activeAnchor;
    private DistanceJoint2D _tetherJoint;
    private CelestialBody _tetheredBody;
    private float _anchorCooldownTimer;
    private Vector2 _aimDirection = Vector2.up;

    // ═══════════════════════════════════════════════
    //  Unity 生命周期
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.linearDamping = 0.5f;   // 微弱阻尼防止无限加速
        _rb.angularDamping = 0f;
        _rb.bodyType = RigidbodyType2D.Dynamic;
    }

    private void Start()
    {
        RefreshCelestialBodies();
    }

    private void Update()
    {
        UpdateAimDirection();
        HandleInput();
        UpdateShipHeading();
        TickCooldown();
    }

    private void FixedUpdate()
    {
        // 1. 施加所有天体引力
        ApplyGravityFromAllBodies();

        // 2. 施加所有天体表面排斥力
        ApplySurfaceRepulsionFromAllBodies();

        // 3. 持续前进推力（绳索状态下不施加，让飞船自然减速）
        //if (_currentState != ShipState.Tethered)
            ApplyForwardThrust();

        // 4. WASD 测试控制
        if (testThrustForce > 0f)
            ApplyTestThrust();

        // 5. 限制最大速度
        ClampSpeed();

        // 6. 检查锚丢失
        CheckAnchorLost();
    }

    // ═══════════════════════════════════════════════
    //  原生物理力驱动
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 遍历所有天体，用 AddForce 将引力施加到飞船 Rigidbody2D。
    /// </summary>
    private void ApplyGravityFromAllBodies()
    {
        for (int i = 0; i < _celestialBodies.Length; i++)
        {
            if (_celestialBodies[i] == null) continue;
            _celestialBodies[i].ApplyGravityTo(_rb);
        }
    }

    /// <summary>
    /// 遍历所有天体，施加表面排斥力防止穿入。
    /// </summary>
    private void ApplySurfaceRepulsionFromAllBodies()
    {
        for (int i = 0; i < _celestialBodies.Length; i++)
        {
            if (_celestialBodies[i] == null) continue;
            _celestialBodies[i].ApplySurfaceRepulsionTo(_rb);
        }
    }

    /// <summary>
    /// 沿飞船头部方向施加持续推力。
    /// </summary>
    private void ApplyForwardThrust()
    {
        _rb.AddForce((Vector2)transform.up * thrustForce);
    }

    /// <summary>
    /// WASD 测试控制：在世界坐标系四个方向上施加力。
    /// </summary>
    private void ApplyTestThrust()
    {
        Vector2 input = Vector2.zero;

        if (Input.GetKey(KeyCode.W)) input.y += 1f;
        if (Input.GetKey(KeyCode.S)) input.y -= 1f;
        if (Input.GetKey(KeyCode.A)) input.x -= 1f;
        if (Input.GetKey(KeyCode.D)) input.x += 1f;

        if (input.sqrMagnitude > 0.01f)
            _rb.AddForce(input.normalized * testThrustForce);
    }

    /// <summary>
    /// 限制最大速度，防止引力放大导致失控。
    /// </summary>
    private void ClampSpeed()
    {
        if (_rb.linearVelocity.magnitude > maxSpeed)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
        }
    }

    // ═══════════════════════════════════════════════
    //  瞄准 & 输入
    // ═══════════════════════════════════════════════

    private void HandleInput()
    {
        bool firePressed = Input.GetKeyDown(fireKey);
        bool fireReleased = Input.GetKeyUp(fireKey);

        switch (_currentState)
        {
            case ShipState.FreeFlight:
                if (firePressed && _anchorCooldownTimer <= 0f)
                    FireAnchor();
                break;

            case ShipState.Anchoring:
                if (fireReleased)
                    CancelAnchor();
                break;

            case ShipState.Tethered:
                if (fireReleased)
                    ReleaseAnchor();
                break;
        }
    }

    private void UpdateShipHeading()
    {
        Vector2 vel = _rb.linearVelocity;

        // 速度太小时不旋转（避免朝向闪烁）
        if (vel.sqrMagnitude < 0.01f) return;

        float targetAngle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f;
        float currentAngle = _rb.rotation;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnRate * Time.deltaTime);
        _rb.SetRotation(newAngle);
    }

    /// <summary>
    /// 读取鼠标位置，更新瞄准方向（仅用于锚发射）。
    /// 不影响飞船朝向。
    /// </summary>
    private void UpdateAimDirection()
    {
        if (Camera.main == null) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Vector2 toMouse = (Vector2)mouseWorld - (Vector2)transform.position;
        if (toMouse.sqrMagnitude > 0.01f)
            _aimDirection = toMouse.normalized;
    }

    private void TickCooldown()
    {
        if (_anchorCooldownTimer > 0f)
            _anchorCooldownTimer -= Time.deltaTime;
    }

    // ═══════════════════════════════════════════════
    //  锚 — 发射 / 取消 / 释放
    // ═══════════════════════════════════════════════

    private void FireAnchor()
    {
        if (anchorPrefab == null)
        {
            Debug.LogWarning("PlayerShip: 未设置锚预制体");
            return;
        }

        AnchorProjectile anchor = Instantiate(anchorPrefab, transform.position, Quaternion.identity);
        anchor.Initialize(
            owner: this,
            direction: _aimDirection,       // 朝鼠标方向发射
            speed: anchorSpeed,
            maxRange: anchorMaxRange
        );

        _activeAnchor = anchor;
        _currentState = ShipState.Anchoring;
        _anchorCooldownTimer = anchorCooldown;
    }

    private void CancelAnchor()
    {
        if (_activeAnchor != null)
        {
            _activeAnchor.Terminate();
            _activeAnchor = null;
        }
        _currentState = ShipState.FreeFlight;
    }

    private void ReleaseAnchor()
    {
        DestroyTetherJoint();

        if (_activeAnchor != null)
        {
            _activeAnchor.Terminate();
            _activeAnchor = null;
        }

        _tetheredBody = null;
        _currentState = ShipState.FreeFlight;

        Debug.Log($"[Ship] 绳索释放 — 当前速度 {_rb.linearVelocity.magnitude:F2}");
    }

    // ═══════════════════════════════════════════════
    //  DistanceJoint2D — Unity 原生绳约束
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 锚命中天体时回调。
    /// 创建 DistanceJoint2D 连接飞船和天体，自动维持命中时的距离。
    /// </summary>
    public void OnAnchorHitCelestial(CelestialBody body, AnchorProjectile anchor)
    {
        if (_currentState != ShipState.Anchoring) return;

        _tetheredBody = body;

        // 创建 DistanceJoint2D：Unity 原生物理关节
        // 自动维持两个 Rigidbody2D 之间的固定距离
        _tetherJoint = gameObject.AddComponent<DistanceJoint2D>();
        _tetherJoint.connectedBody = body.AttachedRigidbody;
        _tetherJoint.autoConfigureDistance = false;
        _tetherJoint.distance = Vector2.Distance(transform.position, body.Center);
        _tetherJoint.maxDistanceOnly = true; // 只限制最大距离，不限制最小
        _tetherJoint.enableCollision = false;

        _currentState = ShipState.Tethered;

        Debug.Log($"[Ship] DistanceJoint2D 已建立 "
                + $"— 连接 {body.name}，绳长 {_tetherJoint.distance:F2}");
    }

    /// <summary>
    /// 移除 DistanceJoint2D。
    /// </summary>
    private void DestroyTetherJoint()
    {
        if (_tetherJoint != null)
        {
            Destroy(_tetherJoint);
            _tetherJoint = null;
        }
    }

    // ═══════════════════════════════════════════════
    //  状态检查
    // ═══════════════════════════════════════════════

    private void CheckAnchorLost()
    {
        if (_activeAnchor == null && _currentState == ShipState.Anchoring)
        {
            _currentState = ShipState.FreeFlight;
            Debug.Log("[Ship] 锚未命中，回到自由飞行");
        }

        if (_tetheredBody == null && _currentState == ShipState.Tethered)
        {
            DestroyTetherJoint();
            _currentState = ShipState.FreeFlight;
            Debug.Log("[Ship] 天体丢失，回到自由飞行");
        }
    }

    // ═══════════════════════════════════════════════
    //  回调
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 由 AnchorProjectile 调用：锚被销毁时的通知。
    /// </summary>
    public void OnAnchorDestroyed()
    {
        _activeAnchor = null;
    }

    /// <summary>
    /// 刷新场景中的天体列表。
    /// </summary>
    public void RefreshCelestialBodies()
    {
        _celestialBodies = FindObjectsOfType<CelestialBody>();
    }

    // ═══════════════════════════════════════════════
    //  调试可视化
    // ═══════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 速度向量
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, (Vector3)_rb.linearVelocity * 0.3f);

        // 锚瞄准方向
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, (Vector3)_aimDirection * 2f);

        // 绳索连接
        if (_tetherJoint != null && _tetheredBody != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _tetheredBody.Center);

            // 绳长范围
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Vector3 center = _tetheredBody.Center;
            float r = _tetherJoint.distance;
            int segments = 64;
            Vector3 prev = center + new Vector3(Mathf.Cos(0), Mathf.Sin(0), 0) * r;
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * r;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
#endif
}
