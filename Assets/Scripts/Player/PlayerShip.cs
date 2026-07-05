using UnityEngine;
using UnityEngine.InputSystem;

namespace AnchorGame
{
    /// <summary>
    /// 玩家飞船控制器 — 通过发射"锚"到天体上来实现移动。
    ///
    /// 核心机制：
    /// - FreeFlight:  飞船沿当前速度方向做匀速直线运动（无外力惯性飞行）
    /// - Anchoring:   锚已发射但尚未命中天体，飞船继续惯性飞行
    /// - Orbiting:    锚已命中天体，飞船以天体为圆心做匀速圆周运动
    /// - PullingToCheckpoint: 锚命中存档点，飞船沿钩锁方向直线靠近
    /// - PullingToWormhole: 锚命中虫洞，飞船沿钩锁方向直线靠近并传送
    ///
    /// 操作：
    /// - 鼠标瞄准方向，左键单击发射/取消/释放（点击语义随状态切换）
    /// - Orbiting 状态下锚链会自动缩短，玩家需要在撞上天体前释放
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerShip : MonoBehaviour
    {
        [Header("移动参数")]
        [Tooltip("飞船在自由飞行时的恒定速率 (m/s)")]
        public float moveSpeed = 5f;

        [Tooltip("飞船转向（瞄准）的角速度 (度/秒)")]
        public float turnRate = 360f;

        [Header("锚参数")]
        [Tooltip("锚的预制体（需要 AnchorProjectile 组件）")]
        public AnchorProjectile anchorPrefab;

        [Tooltip("锚的最大发射距离（超过此距离锚自动销毁）")]
        public float anchorMaxRange = 10f;

        [Tooltip("锚的飞行速度 (m/s)")]
        public float anchorSpeed = 15f;

        [Tooltip("锚发射后的冷却时间 (秒)")]
        public float anchorCooldown = 0.3f;

        [Header("锚定坠落")]
        [Tooltip("锚命中天体后，飞船被拉向天体的半径缩短速度 (m/s)")]
        public float anchorChainShortenSpeed = 1.4f;

        [Tooltip("最大角加速度 (弧度/秒²)，加速爬升到头之后的恒定加速度")]
        public float maxOrbitAccel = 12f;
        [Tooltip("发射音效")]
        public AudioClip shootSFX;
        [Tooltip("坠毁音效")]
        public AudioClip crashSFX;
        [Tooltip("插锚音效")]
        public AudioClip hitSFX;

        [Tooltip("靠近天体时释放速度的增长曲线。0=线性，0.5=前段增长更明显，1=标准曲线，2=后段增长更明显")]
        public float orbitSpeedGainExponent = 0.5f;

        [Tooltip("锚定期间可积累到的最大释放速度 (m/s)")]
        public float maxOrbitReleaseSpeed = 18f;

        [Tooltip("判定撞上天体时额外保留的距离 (m)，越大越早死亡")]
        public float planetImpactPadding = 0.08f;

        [Tooltip("找不到玩家碰撞体时，用于撞星判定的飞船半径兜底值 (m)")]
        public float fallbackShipCollisionRadius = 0.35f;

        [Header("星球碰撞反弹")]
        [Tooltip("撞到非当前锚定星球时，反弹速度相对碰撞前速度的倍率")]
        public float planetBounceSpeedMultiplier = 0.85f;

        [Tooltip("撞到非当前锚定星球后的最小反弹速度 (m/s)")]
        public float planetBounceMinSpeed = 5f;

        [Tooltip("撞到非当前锚定星球后的最大反弹速度 (m/s)")]
        public float planetBounceMaxSpeed = 14f;

        [Tooltip("反弹后额外推出星球表面的距离，避免连续触发或卡在星球内")]
        public float planetBounceSeparationPadding = 0.08f;

        [Tooltip("同一个星球反弹后的短暂保护时间，避免 OnTriggerStay 每帧重复反弹")]
        public float planetBounceCooldown = 0.12f;

        [Header("存档点牵引")]
        [Tooltip("锚命中存档点后，飞船沿钩锁方向直线靠近的速度 (m/s)")]
        public float checkpointPullSpeed = 8f;

        [Tooltip("距离钩锁命中点多近时视为抵达存档点")]
        public float checkpointArrivalDistance = 0.2f;

        [Header("虫洞牵引")]
        [Tooltip("锚命中虫洞后，飞船沿钩锁方向直线靠近的速度 (m/s)")]
        public float wormholePullSpeed = 8f;

        [Tooltip("距离虫洞多近时视为接触虫洞并触发传送")]
        public float wormholeArrivalDistance = 0.2f;

        [Header("输入设置")]
        [Tooltip("true=按住鼠标左键发射松开释放, false=单击切换发射/释放")]
        public bool useHoldToAim = false;

        [Header("存档点")]
        [SerializeField] private Checkpoint _activeCheckpoint;
        public Checkpoint ActiveCheckpoint => _activeCheckpoint;

        public enum ShipState
        {
            FreeFlight,
            Anchoring,
            Orbiting,
            PullingToCheckpoint,
            PullingToWormhole,
            WaitingAtCheckpoint
        }

        [Header("调试（只读）")]
        [SerializeField] private ShipState _currentState = ShipState.FreeFlight;
        public ShipState CurrentState => _currentState;

        [SerializeField] private Vector2 _velocity;
        public Vector2 Velocity => _velocity;

        [SerializeField] private float _orbitAngle;
        [SerializeField] private float _orbitRadius;
        [SerializeField] private float _orbitAngularSpeed;
        [SerializeField] private float _orbitTangentSpeed;
        [SerializeField] private int _orbitDirection = 1;

        private Rigidbody2D _rb;
        private AnchorProjectile _activeAnchor;
        private CelestialBody _orbitingBody;
        private Checkpoint _pullingCheckpoint;
        private Wormhole _pullingWormhole;
        private Vector2 _checkpointPullTarget;
        private Vector2 _wormholePullTarget;
        private float _orbitStartRadius;
        private float _shipCollisionRadius;
        private float _anchorCooldownTimer;
        private float _wormholeCooldownTimer;
        private Vector2 _aimDirection = Vector2.up;
        private Camera _cam;
        private Vector2 _initialSpawnPosition;
        private Checkpoint _stoppedCheckpoint;
        private bool _waitingCanLaunch;
        private bool _waitForMouseReleaseBeforeLaunch;
        private CelestialBody _lastBouncedPlanet;
        private float _planetBounceCooldownTimer;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            _rb.bodyType = RigidbodyType2D.Dynamic;

            _cam = Camera.main;
            _velocity = Vector2.right * moveSpeed;
            _initialSpawnPosition = transform.position;
            _shipCollisionRadius = EstimateShipCollisionRadius();
        }

        private void Update()
        {
            HandleInput();
            UpdateAimDirection();
            TickCooldown();
        }

        private void FixedUpdate()
        {
            switch (_currentState)
            {
                case ShipState.FreeFlight:
                    FreeFlightMove();
                    break;
                case ShipState.Anchoring:
                    FreeFlightMove();
                    CheckAnchorLost();
                    break;
                case ShipState.Orbiting:
                    OrbitMove();
                    CheckAnchorLost();
                    break;
                case ShipState.PullingToCheckpoint:
                    PullToCheckpointMove();
                    CheckAnchorLost();
                    break;
                case ShipState.PullingToWormhole:
                    PullToWormholeMove();
                    CheckAnchorLost();
                    break;
                case ShipState.WaitingAtCheckpoint:
                    HoldAtCheckpoint();
                    break;
            }
        }

        private void HandleInput()
        {
            if (Mouse.current == null) return;

            bool firePressed = Mouse.current.leftButton.wasPressedThisFrame;
            bool fireReleased = Mouse.current.leftButton.wasReleasedThisFrame;

            switch (_currentState)
            {
                case ShipState.WaitingAtCheckpoint:
                    UpdateCheckpointLaunchInput(firePressed);
                    break;

                case ShipState.FreeFlight:
                    if (firePressed && _anchorCooldownTimer <= 0f)
                    {
                        FireAnchor();
                    }
                    break;

                case ShipState.Anchoring:
                    if (fireReleased && useHoldToAim)
                    {
                        CancelAnchor();
                    }
                    break;

                case ShipState.Orbiting:
                    if (fireReleased || (firePressed && !useHoldToAim))
                    {
                        ReleaseAnchor();
                    }
                    break;

                case ShipState.PullingToCheckpoint:
                    if (fireReleased || (firePressed && !useHoldToAim))
                    {
                        ReleaseCheckpointPull();
                    }
                    break;

                case ShipState.PullingToWormhole:
                    if (fireReleased || (firePressed && !useHoldToAim))
                    {
                        ReleaseWormholePull();
                    }
                    break;
            }
        }

        private void UpdateCheckpointLaunchInput(bool firePressed)
        {
            if (Mouse.current != null && !Mouse.current.leftButton.isPressed)
            {
                _waitForMouseReleaseBeforeLaunch = false;
            }

            if (!_waitingCanLaunch || _waitForMouseReleaseBeforeLaunch || !firePressed)
            {
                return;
            }

            LaunchFromCheckpoint();
        }

        private void UpdateAimDirection()
        {
            if (Mouse.current == null || _cam == null) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld = _cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -_cam.transform.position.z));
            Vector2 toMouse = (Vector2)mouseWorld - (Vector2)transform.position;
            if (toMouse.sqrMagnitude > 0.01f)
            {
                _aimDirection = toMouse.normalized;
            }

            float targetAngle = Mathf.Atan2(_aimDirection.y, _aimDirection.x) * Mathf.Rad2Deg - 90f;
            float currentAngle = _rb.rotation;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnRate * Time.deltaTime);
            _rb.SetRotation(newAngle);
        }

        private void TickCooldown()
        {
            if (_anchorCooldownTimer > 0f)
                _anchorCooldownTimer -= Time.deltaTime;
            if (_wormholeCooldownTimer > 0f)
                _wormholeCooldownTimer -= Time.deltaTime;
            if (_planetBounceCooldownTimer > 0f)
                _planetBounceCooldownTimer -= Time.deltaTime;
        }

        private void FireAnchor()
        {
            if (anchorPrefab == null)
            {
                Debug.LogWarning("PlayerShip: 未设置锚预制体 (anchorPrefab)");
                return;
            }

            AnchorProjectile anchor = Instantiate(anchorPrefab, transform.position, Quaternion.identity);
            anchor.Initialize(
                owner: this,
                direction: _aimDirection,
                speed: anchorSpeed,
                maxRange: anchorMaxRange
            );

            if (AudioManager.Instance != null && shootSFX != null)
            {
                AudioManager.Instance.PlaySFX(shootSFX);
            }

            _activeAnchor = anchor;
            _currentState = ShipState.Anchoring;
            _anchorCooldownTimer = anchorCooldown;

            Debug.Log($"[Ship] 锚已发射 → 方向:{_aimDirection} 速度:{anchorSpeed}");
        }

        private void CancelAnchor()
        {
            if (_activeAnchor != null)
            {
                _activeAnchor.Terminate();
                _activeAnchor = null;
            }
            _currentState = ShipState.FreeFlight;
            Debug.Log("[Ship] 锚已取消");
        }

        private void ReleaseAnchor()
        {
            Vector2 tangent = GetOrbitTangent();
            _velocity = tangent;

            if (_activeAnchor != null)
            {
                _activeAnchor.Terminate();
                _activeAnchor = null;
            }

            _orbitingBody = null;
            _currentState = ShipState.FreeFlight;

            Debug.Log($"[Ship] 锚已释放 → 切线飞出 速度:{_velocity}");
        }

        private void FreeFlightMove()
        {
            _rb.linearVelocity = _velocity;
        }

        private void HoldAtCheckpoint()
        {
            _velocity = Vector2.zero;
            _rb.linearVelocity = Vector2.zero;

            if (_stoppedCheckpoint != null)
            {
                _rb.position = _stoppedCheckpoint.SpawnPosition;
                transform.position = _stoppedCheckpoint.SpawnPosition;
            }
        }

        private void OrbitMove()
        {
            if (_orbitingBody == null) return;

            float impactRadius = GetOrbitImpactRadius();
            _orbitRadius = Mathf.MoveTowards(
                _orbitRadius,
                impactRadius,
                Mathf.Max(0f, anchorChainShortenSpeed) * Time.fixedDeltaTime
            );

            if (_orbitRadius <= impactRadius + 0.001f)
            {
                CrashIntoOrbitingBody();
                return;
            }

            float fallProgress = Mathf.Clamp01(Mathf.InverseLerp(_orbitStartRadius, impactRadius, _orbitRadius));
            float speedProgress = orbitSpeedGainExponent <= 0f
                ? fallProgress
                : Mathf.Pow(fallProgress, orbitSpeedGainExponent);
            float speedCap = Mathf.Max(moveSpeed, maxOrbitReleaseSpeed);
            _orbitTangentSpeed = Mathf.Lerp(moveSpeed, speedCap, speedProgress);
            _orbitAngularSpeed = _orbitTangentSpeed / Mathf.Max(_orbitRadius, 0.01f);

            Vector2 center = _orbitingBody.transform.position;

            _orbitAngle += _orbitAngularSpeed * _orbitDirection * Time.fixedDeltaTime;

            Vector2 orbitOffset = new Vector2(
                Mathf.Cos(_orbitAngle) * _orbitRadius,
                Mathf.Sin(_orbitAngle) * _orbitRadius
            );
            Vector2 targetPosition = center + orbitOffset;

            _rb.MovePosition(targetPosition);

            _velocity = GetOrbitTangent();
        }

        private void PullToCheckpointMove()
        {
            if (_pullingCheckpoint == null)
            {
                _currentState = ShipState.FreeFlight;
                return;
            }

            Vector2 currentPosition = _rb.position;
            Vector2 toTarget = _checkpointPullTarget - currentPosition;
            float distance = toTarget.magnitude;

            if (distance <= Mathf.Max(0.01f, checkpointArrivalDistance))
            {
                _pullingCheckpoint.Activate(this);
                return;
            }

            Vector2 direction = toTarget / distance;
            float step = Mathf.Min(distance, Mathf.Max(0.01f, checkpointPullSpeed) * Time.fixedDeltaTime);
            Vector2 nextPosition = currentPosition + direction * step;

            _velocity = direction * checkpointPullSpeed;
            _rb.MovePosition(nextPosition);
        }

        private void PullToWormholeMove()
        {
            if (_pullingWormhole == null)
            {
                _currentState = ShipState.FreeFlight;
                return;
            }

            Vector2 currentPosition = _rb.position;
            Vector2 toTarget = _wormholePullTarget - currentPosition;
            float distance = toTarget.magnitude;

            float arrivalDistance = Mathf.Max(
                Mathf.Max(0.01f, wormholeArrivalDistance),
                _pullingWormhole.ArrivalDistance
            );
            if (distance <= arrivalDistance)
            {
                _pullingWormhole.Teleport(this);
                return;
            }

            Vector2 direction = toTarget / distance;
            float pullSpeed = Mathf.Max(0.01f, wormholePullSpeed);
            float step = Mathf.Min(distance, pullSpeed * Time.fixedDeltaTime);
            Vector2 nextPosition = currentPosition + direction * step;

            _velocity = direction * pullSpeed;
            _rb.MovePosition(nextPosition);
        }

        private void CheckAnchorLost()
        {
            if (_activeAnchor == null && _currentState != ShipState.FreeFlight)
            {
                if (_currentState == ShipState.Anchoring)
                {
                    _currentState = ShipState.FreeFlight;
                    Debug.Log("[Ship] 锚未命中，回到自由飞行");
                }
                else if (_currentState == ShipState.Orbiting)
                {
                    _orbitingBody = null;
                    _currentState = ShipState.FreeFlight;
                    Debug.Log("[Ship] 轨道天体丢失，回到自由飞行");
                }
                else if (_currentState == ShipState.PullingToCheckpoint)
                {
                    _pullingCheckpoint = null;
                    _currentState = ShipState.FreeFlight;
                    Debug.Log("[Ship] 存档点钩锁丢失，回到自由飞行");
                }
                else if (_currentState == ShipState.PullingToWormhole)
                {
                    _pullingWormhole = null;
                    _currentState = ShipState.FreeFlight;
                    Debug.Log("[Ship] 虫洞钩锁丢失，回到自由飞行");
                }
            }
        }

        /// <summary>
        /// 锚命中天体时由 AnchorProjectile 调用。
        /// </summary>
        public void OnAnchorHitCelestial(CelestialBody body, Vector2 anchorVelocity)
        {
            if (_currentState != ShipState.Anchoring) return;

            _orbitingBody = body;
            Vector2 center = body.transform.position;
            Vector2 shipPos = transform.position;

            // 注意：不能像 "Mathf.Max(dist, body.radius + 0.5f)" 那样强行把半径撑大到最小值——
            // 那样一来近距离命中时，飞船会在挂锚瞬间被 MovePosition 硬生生"传送"到更大的半径处，
            // 而那个方向通常跟飞船当时的前进方向相反，看起来就像被反向弹开。
            // 这里只挡一个真正的 0 半径除零边界情况，不做任何视觉上的位置矫正。
            _orbitRadius = Mathf.Max(Vector2.Distance(shipPos, center), 0.05f);
            _orbitStartRadius = _orbitRadius;

            Vector2 toShip = shipPos - center;
            _orbitAngle = Mathf.Atan2(toShip.y, toShip.x);

            // 每次锚定都是独立的释放速度窗口，避免连续锚定时上一颗行星的高速被下一颗继续放大。
            _orbitTangentSpeed = moveSpeed;
            _orbitAngularSpeed = _orbitTangentSpeed / _orbitRadius;

            float cross = toShip.x * _velocity.y - toShip.y * _velocity.x;
            _orbitDirection = cross >= 0 ? 1 : -1;

            _currentState = ShipState.Orbiting;

            Debug.Log($"[Ship] 锚命中 {body.name} → 进入轨道 "
                    + $"r={_orbitRadius:F2} ω={_orbitAngularSpeed:F2} "
                    + $"方向={(_orbitDirection > 0 ? "CCW" : "CW")}");
        }

        public void OnAnchorHitCheckpoint(Checkpoint checkpoint, Vector2 anchorPoint)
        {
            if (_currentState != ShipState.Anchoring || checkpoint == null) return;

            _orbitingBody = null;
            _pullingCheckpoint = checkpoint;
            _checkpointPullTarget = anchorPoint;

            Vector2 toTarget = _checkpointPullTarget - (Vector2)transform.position;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                _checkpointPullTarget = checkpoint.SpawnPosition;
                toTarget = _checkpointPullTarget - (Vector2)transform.position;
            }

            _velocity = toTarget.sqrMagnitude > 0.0001f
                ? toTarget.normalized * checkpointPullSpeed
                : Vector2.zero;
            _currentState = ShipState.PullingToCheckpoint;

            Debug.Log($"[Ship] 锚命中存档点 {checkpoint.name} → 沿钩锁直线靠近");
        }

        public void OnAnchorHitWormhole(Wormhole wormhole, Vector2 anchorPoint)
        {
            if (_currentState != ShipState.Anchoring || wormhole == null) return;

            _orbitingBody = null;
            _pullingCheckpoint = null;
            _pullingWormhole = wormhole;
            _wormholePullTarget = wormhole.AnchorPoint;

            Vector2 toTarget = _wormholePullTarget - (Vector2)transform.position;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                toTarget = anchorPoint - (Vector2)transform.position;
            }

            float pullSpeed = Mathf.Max(0.01f, wormholePullSpeed);
            _velocity = toTarget.sqrMagnitude > 0.0001f
                ? toTarget.normalized * pullSpeed
                : Vector2.zero;
            _currentState = ShipState.PullingToWormhole;

            Debug.Log($"[Ship] 锚命中虫洞 {wormhole.name} → 沿钩锁直线靠近");
        }

        private Vector2 GetOrbitTangent()
        {
            if (_orbitingBody == null) return _velocity;

            Vector2 center = _orbitingBody.transform.position;
            Vector2 toShip = (Vector2)transform.position - center;
            Vector2 radial = toShip.normalized;
            Vector2 tangent = new Vector2(-radial.y, radial.x) * _orbitDirection;

            return tangent * _orbitTangentSpeed;
        }

        private void ReleaseCheckpointPull()
        {
            if (_activeAnchor != null)
            {
                _activeAnchor.Terminate();
                _activeAnchor = null;
            }

            _pullingCheckpoint = null;
            _orbitingBody = null;
            _currentState = ShipState.FreeFlight;
            _anchorCooldownTimer = anchorCooldown;

            if (_velocity.sqrMagnitude < moveSpeed * moveSpeed * 0.25f)
            {
                _velocity = _velocity.sqrMagnitude > 0.0001f
                    ? _velocity.normalized * moveSpeed
                    : Vector2.right * moveSpeed;
            }

            _rb.linearVelocity = _velocity;
            Debug.Log($"[Ship] 释放存档点钩锁，继续直线飞行: {_velocity}");
        }

        private void ReleaseWormholePull()
        {
            if (_activeAnchor != null)
            {
                _activeAnchor.Terminate();
                _activeAnchor = null;
            }

            _pullingWormhole = null;
            _pullingCheckpoint = null;
            _orbitingBody = null;
            _currentState = ShipState.FreeFlight;
            _anchorCooldownTimer = anchorCooldown;

            if (_velocity.sqrMagnitude < moveSpeed * moveSpeed * 0.25f)
            {
                _velocity = _velocity.sqrMagnitude > 0.0001f
                    ? _velocity.normalized * moveSpeed
                    : Vector2.right * moveSpeed;
            }

            _rb.linearVelocity = _velocity;
            Debug.Log($"[Ship] 释放虫洞钩锁，继续直线飞行: {_velocity}");
        }

        public bool CanUseWormhole => _wormholeCooldownTimer <= 0f;

        public void TeleportThroughWormhole(Vector2 destination, float cooldown)
        {
            Vector2 preservedVelocity = _velocity;

            if (_activeAnchor != null)
            {
                _activeAnchor.Terminate();
                _activeAnchor = null;
            }

            _orbitingBody = null;
            _pullingCheckpoint = null;
            _pullingWormhole = null;
            _currentState = ShipState.FreeFlight;
            _anchorCooldownTimer = anchorCooldown;
            _wormholeCooldownTimer = Mathf.Max(0.01f, cooldown);
            _velocity = preservedVelocity;

            _rb.position = destination;
            transform.position = destination;
            _rb.linearVelocity = _velocity;

            Debug.Log($"[Ship] 虫洞传送完成，速度保持: {_velocity}");
        }

        private void CrashIntoOrbitingBody()
        {
            string bodyName = _orbitingBody != null ? _orbitingBody.name : "天体";
            Debug.Log($"[Ship] 撞上 {bodyName}，从当前存档点重生");
            RespawnAtCheckpoint();
        }

        private float GetOrbitImpactRadius()
        {
            if (_orbitingBody == null) return 0f;

            return Mathf.Max(0f, _orbitingBody.radius)
                + Mathf.Max(0f, _shipCollisionRadius)
                + Mathf.Max(0f, planetImpactPadding);
        }

        private float EstimateShipCollisionRadius()
        {
            Collider2D shipCollider = GetComponentInChildren<Collider2D>();
            if (shipCollider == null)
            {
                return Mathf.Max(0.01f, fallbackShipCollisionRadius);
            }

            Vector3 extents = shipCollider.bounds.extents;
            float radius = Mathf.Max(extents.x, extents.y);
            return Mathf.Max(0.01f, radius);

        }

        public void OnAnchorDestroyed()
        {
            _activeAnchor = null;
        }

        public void ApplyHazardKnockback(Vector2 impulse)
        {
            if (_activeAnchor != null)
            {
                _activeAnchor.Terminate();
                _activeAnchor = null;
            }

            _orbitingBody = null;
            _pullingCheckpoint = null;
            _pullingWormhole = null;
            _currentState = ShipState.FreeFlight;
            _anchorCooldownTimer = anchorCooldown;

            _velocity += impulse;
            if (_velocity.sqrMagnitude < moveSpeed * moveSpeed * 0.25f)
            {
                _velocity = _velocity.normalized * moveSpeed;
            }

            _rb.linearVelocity = _velocity;
            Debug.Log($"[Ship] 受到碎石击退: {impulse}");
        }

        public void ApplyBlackHoleAttraction(
            Vector2 blackHoleCenter,
            float acceleration,
            float maxSpeed,
            float radialBrakeStrength,
            float steeringStrength,
            float deltaTime
        )
        {
            if (_currentState == ShipState.WaitingAtCheckpoint) return;

            if (_activeAnchor != null)
            {
                _activeAnchor.Terminate();
                _activeAnchor = null;
            }

            _orbitingBody = null;
            _pullingCheckpoint = null;
            _pullingWormhole = null;
            _currentState = ShipState.FreeFlight;

            Vector2 toCenter = blackHoleCenter - (Vector2)transform.position;
            if (toCenter.sqrMagnitude < 0.0001f) return;

            Vector2 pullDirection = toCenter.normalized;
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            _velocity += pullDirection * Mathf.Max(0f, acceleration) * safeDeltaTime;

            // Game-feel helper: cancel part of the velocity that is escaping away from the black hole.
            float inwardSpeed = Vector2.Dot(_velocity, pullDirection);
            if (inwardSpeed < 0f)
            {
                float brakeAmount = Mathf.Clamp01(Mathf.Max(0f, radialBrakeStrength) * safeDeltaTime);
                _velocity -= pullDirection * inwardSpeed * brakeAmount;
            }

            float currentSpeed = _velocity.magnitude;
            if (currentSpeed > 0.0001f)
            {
                Vector2 targetVelocity = pullDirection * Mathf.Max(moveSpeed, currentSpeed);
                float steering = Mathf.Clamp01(Mathf.Max(0f, steeringStrength) * safeDeltaTime);
                _velocity = Vector2.Lerp(_velocity, targetVelocity, steering);
            }

            float speedCap = Mathf.Max(moveSpeed, maxSpeed);
            if (_velocity.sqrMagnitude > speedCap * speedCap)
            {
                _velocity = _velocity.normalized * speedCap;
            }

            _rb.linearVelocity = _velocity;
        }

        public void StopAtCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null) return;

            if (_activeAnchor != null)
            {
                _activeAnchor.Terminate();
                _activeAnchor = null;
            }

            _orbitingBody = null;
            _pullingCheckpoint = null;
            _pullingWormhole = null;
            _stoppedCheckpoint = checkpoint;
            _waitingCanLaunch = !checkpoint.IsFinalCheckpoint;
            _waitForMouseReleaseBeforeLaunch = Mouse.current != null && Mouse.current.leftButton.isPressed;
            _currentState = ShipState.WaitingAtCheckpoint;
            _anchorCooldownTimer = 0f;
            _velocity = Vector2.zero;

            _activeCheckpoint = checkpoint;
            PlayerOxygen oxygen = GetComponent<PlayerOxygen>();
            if (oxygen != null)
            {
                oxygen.OnCheckpointReached(checkpoint);
            }

            _rb.position = checkpoint.SpawnPosition;
            transform.position = checkpoint.SpawnPosition;
            _rb.linearVelocity = Vector2.zero;

            Debug.Log(checkpoint.IsFinalCheckpoint
                ? $"[Ship] 到达终点存档点并停止: {checkpoint.name}"
                : $"[Ship] 到达存档点并等待再次出发: {checkpoint.name}");
        }

        public void SetCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null || _activeCheckpoint == checkpoint) return;

            _activeCheckpoint = checkpoint;
            PlayerOxygen oxygen = GetComponent<PlayerOxygen>();
            if (oxygen != null)
            {
                oxygen.OnCheckpointReached(checkpoint);
            }

            Debug.Log($"[Ship] 存档点已更新: {checkpoint.name}");
        }

        private void LaunchFromCheckpoint()
        {
            Checkpoint checkpoint = _stoppedCheckpoint != null ? _stoppedCheckpoint : _activeCheckpoint;
            _velocity = checkpoint != null
                ? checkpoint.GetRespawnVelocity(moveSpeed)
                : Vector2.right * moveSpeed;

            _stoppedCheckpoint = null;
            _currentState = ShipState.FreeFlight;
            _anchorCooldownTimer = anchorCooldown;
            _rb.linearVelocity = _velocity;

            Debug.Log($"[Ship] 从存档点重新出发，速度: {_velocity}");
        }

        public void RespawnAtCheckpoint()
        {
            if (_activeAnchor != null)
            {
                _activeAnchor.Terminate();
                _activeAnchor = null;
            }

            _orbitingBody = null;
            _pullingCheckpoint = null;
            _pullingWormhole = null;
            _currentState = ShipState.WaitingAtCheckpoint;
            _anchorCooldownTimer = 0f;

            Vector2 spawnPosition = _activeCheckpoint != null
                ? _activeCheckpoint.SpawnPosition
                : _initialSpawnPosition;

            _stoppedCheckpoint = _activeCheckpoint;
            _waitingCanLaunch = true;
            _waitForMouseReleaseBeforeLaunch = Mouse.current != null && Mouse.current.leftButton.isPressed;
            _velocity = Vector2.zero;

            _rb.position = spawnPosition;
            transform.position = spawnPosition;
            _rb.linearVelocity = Vector2.zero;

            if (AudioManager.Instance != null && shootSFX != null)
            {
                AudioManager.Instance.PlaySFX(crashSFX);
            }

            Debug.Log(_activeCheckpoint != null
                ? $"[Ship] 从存档点重生并等待出发: {_activeCheckpoint.name}"
                : "[Ship] 回到初始出生点并等待出发");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandlePlanetContact(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            HandlePlanetContact(other);
        }

        private void HandlePlanetContact(Collider2D other)
        {
            CelestialBody body = other.GetComponentInParent<CelestialBody>();
            if (body == null) return;
            if (body == _orbitingBody) return;
            if (_currentState == ShipState.WaitingAtCheckpoint) return;
            if (_planetBounceCooldownTimer > 0f && body == _lastBouncedPlanet) return;

            BounceOffPlanet(body);
        }

        private void BounceOffPlanet(CelestialBody body)
        {
            Vector2 center = body.Center;
            Vector2 currentPosition = _rb.position;
            Vector2 normal = currentPosition - center;
            if (normal.sqrMagnitude < 0.0001f)
            {
                Vector2 fallbackVelocity = _velocity.sqrMagnitude > 0.0001f ? _velocity : _rb.linearVelocity;
                normal = fallbackVelocity.sqrMagnitude > 0.0001f ? -fallbackVelocity.normalized : Vector2.up;
            }
            else
            {
                normal.Normalize();
            }

            Vector2 incomingVelocity = _velocity.sqrMagnitude > 0.0001f ? _velocity : _rb.linearVelocity;
            if (_currentState == ShipState.Orbiting && _orbitingBody != null)
            {
                incomingVelocity = GetOrbitTangent();
            }
            if (incomingVelocity.sqrMagnitude < 0.0001f)
            {
                incomingVelocity = normal * moveSpeed;
            }

            float speed = Mathf.Clamp(
                incomingVelocity.magnitude * Mathf.Max(0f, planetBounceSpeedMultiplier),
                Mathf.Max(0.01f, planetBounceMinSpeed),
                Mathf.Max(planetBounceMinSpeed, planetBounceMaxSpeed)
            );
            Vector2 reflectedVelocity = Vector2.Reflect(incomingVelocity.normalized, normal).normalized * speed;
            if (Vector2.Dot(reflectedVelocity, normal) < speed * 0.25f)
            {
                reflectedVelocity = Vector2.Lerp(reflectedVelocity.normalized, normal, 0.5f).normalized * speed;
            }

            if (_activeAnchor != null)
            {
                _activeAnchor.Terminate();
                _activeAnchor = null;
            }

            _orbitingBody = null;
            _pullingCheckpoint = null;
            _pullingWormhole = null;
            _currentState = ShipState.FreeFlight;
            _anchorCooldownTimer = anchorCooldown;

            float separationRadius = Mathf.Max(0f, body.radius)
                + Mathf.Max(0f, _shipCollisionRadius)
                + Mathf.Max(0f, planetBounceSeparationPadding);
            Vector2 separatedPosition = center + normal * separationRadius;

            _velocity = reflectedVelocity;
            _rb.position = separatedPosition;
            transform.position = separatedPosition;
            _rb.linearVelocity = _velocity;

            _lastBouncedPlanet = body;
            _planetBounceCooldownTimer = Mathf.Max(0f, planetBounceCooldown);

            Debug.Log($"[Ship] 撞到非锚定星球 {body.name}，反弹速度: {_velocity}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, _velocity * 0.5f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, (Vector3)_aimDirection * 2f);

            if (_currentState == ShipState.Orbiting && _orbitingBody != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
                Vector3 center = _orbitingBody.transform.position;
                int segments = 64;
                Vector3 prev = center + new Vector3(Mathf.Cos(0), Mathf.Sin(0), 0) * _orbitRadius;
                for (int i = 1; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    Vector3 next = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * _orbitRadius;
                    Gizmos.DrawLine(prev, next);
                    prev = next;
                }
            }

            if (_orbitingBody != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _orbitingBody.transform.position);
            }
        }
#endif
    }
}
