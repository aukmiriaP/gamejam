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
    ///
    /// 操作：
    /// - 鼠标瞄准方向，左键单击发射/取消/释放（点击语义随状态切换）
    /// - Orbiting 状态下按住 D 加速摆荡、A 减速摆荡
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

        [Header("摆荡加减速")]
        [Tooltip("持续按住加速键，达到最大角加速度所需的时间 (秒)。加速度本身从 0 缓慢爬升到这个上限，模拟引擎先慢后快的提速感")]
        public float accelRampUpTime = 1.5f;

        [Tooltip("最大角加速度 (弧度/秒²)，加速爬升到头之后的恒定加速度")]
        public float maxOrbitAccel = 12f;

        [Tooltip("刹车（反方向输入）逼近 0 转速的响应速度，越大刹车越干脆")]
        public float brakeResponsiveness = 3f;

        [Tooltip("摆荡角速度上限 (弧度/秒)")]
        public float maxOrbitAngularSpeed = 8f;

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
        [SerializeField] private int _orbitDirection = 1;

        // 加速度爬升计时器：同一个"加速阶段"持续期间累加，用来把加速度本身从 0 缓慢爬升到上限；
        // 阶段一变化（比如从刹车切到加速、或松开按键）就清零重新爬升。
        private float _accelRampTimer;
        private bool _wasAccelPhase;

        private Rigidbody2D _rb;
        private AnchorProjectile _activeAnchor;
        private CelestialBody _orbitingBody;
        private float _anchorCooldownTimer;
        private Vector2 _aimDirection = Vector2.up;
        private Camera _cam;
        private Vector2 _initialSpawnPosition;
        private Checkpoint _stoppedCheckpoint;
        private bool _waitingCanLaunch;
        private bool _waitForMouseReleaseBeforeLaunch;

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

            if (Keyboard.current != null)
            {
                bool accelHeld = Keyboard.current.dKey.isPressed && !Keyboard.current.aKey.isPressed;
                bool decelHeld = Keyboard.current.aKey.isPressed && !Keyboard.current.dKey.isPressed;
                int desiredDir = accelHeld ? 1 : (decelHeld ? -1 : 0);

                if (desiredDir == 0)
                {
                    // 没有输入：保持当前转速不变，同时清空爬升计时器，
                    // 这样下次按键重新开始加速时又是从"慢慢起步"开始。
                    _accelRampTimer = 0f;
                    _wasAccelPhase = false;
                }
                else
                {
                    float signedSpeed = _orbitAngularSpeed * _orbitDirection;

                    // 加速阶段：输入方向跟当前转向一致（或从静止起步）。
                    // 刹车阶段：输入方向跟当前转向相反，即"反打方向"减速。
                    bool isAccelPhase = Mathf.Approximately(signedSpeed, 0f) || (signedSpeed > 0f) == (desiredDir > 0);

                    if (isAccelPhase != _wasAccelPhase)
                    {
                        _accelRampTimer = 0f;
                    }
                    _wasAccelPhase = isAccelPhase;

                    if (isAccelPhase)
                    {
                        // 加速度本身随按住时长做 ease-in 爬升（先慢后快），到 accelRampUpTime 后打满 maxOrbitAccel。
                        _accelRampTimer += Time.fixedDeltaTime;
                        float rampT = Mathf.Clamp01(_accelRampTimer / accelRampUpTime);
                        float accelMagnitude = maxOrbitAccel * rampT * rampT;
                        signedSpeed += desiredDir * accelMagnitude * Time.fixedDeltaTime;
                        signedSpeed = Mathf.Clamp(signedSpeed, -maxOrbitAngularSpeed, maxOrbitAngularSpeed);
                    }
                    else
                    {
                        // 刹车：指数逼近 0，越快(离 0 越远)刹得越猛，越接近 0 越轻，
                        // 到很接近 0 时直接归零，避免无限逼近导致的抖动，紧接着上面的 isAccelPhase 判定会在下一帧转为加速阶段。
                        float t = 1f - Mathf.Exp(-brakeResponsiveness * Time.fixedDeltaTime);
                        signedSpeed = Mathf.Lerp(signedSpeed, 0f, t);
                        if (Mathf.Abs(signedSpeed) < 0.02f) signedSpeed = 0f;
                    }

                    _orbitDirection = signedSpeed >= 0f ? 1 : -1;
                    _orbitAngularSpeed = Mathf.Clamp(Mathf.Abs(signedSpeed), 0f, maxOrbitAngularSpeed);
                }
            }

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

            Vector2 toShip = shipPos - center;
            _orbitAngle = Mathf.Atan2(toShip.y, toShip.x);

            // ω = v / r，速度取完整入射速度大小（不做切向投影），保证换锚时线速度不突变；
            // 静止起步(速度≈0)时用 moveSpeed 兜底，否则挂锚后角速度为 0、完全不转。
            float orbitalSpeed = _velocity.magnitude;
            if (orbitalSpeed < 0.01f) orbitalSpeed = moveSpeed;
            _orbitAngularSpeed = orbitalSpeed / _orbitRadius;

            float cross = toShip.x * _velocity.y - toShip.y * _velocity.x;
            _orbitDirection = cross >= 0 ? 1 : -1;

            _currentState = ShipState.Orbiting;

            Debug.Log($"[Ship] 锚命中 {body.name} → 进入轨道 "
                    + $"r={_orbitRadius:F2} ω={_orbitAngularSpeed:F2} "
                    + $"方向={(_orbitDirection > 0 ? "CCW" : "CW")}");
        }

        private Vector2 GetOrbitTangent()
        {
            if (_orbitingBody == null) return _velocity;

            Vector2 center = _orbitingBody.transform.position;
            Vector2 toShip = (Vector2)transform.position - center;
            Vector2 radial = toShip.normalized;
            Vector2 tangent = new Vector2(-radial.y, radial.x) * _orbitDirection;

            return tangent * (_orbitAngularSpeed * _orbitRadius);
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
            _currentState = ShipState.FreeFlight;
            _anchorCooldownTimer = anchorCooldown;
            _accelRampTimer = 0f;
            _wasAccelPhase = false;

            _velocity += impulse;
            if (_velocity.sqrMagnitude < moveSpeed * moveSpeed * 0.25f)
            {
                _velocity = _velocity.normalized * moveSpeed;
            }

            _rb.linearVelocity = _velocity;
            Debug.Log($"[Ship] 受到碎石击退: {impulse}");
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
            _stoppedCheckpoint = checkpoint;
            _waitingCanLaunch = !checkpoint.IsFinalCheckpoint;
            _waitForMouseReleaseBeforeLaunch = Mouse.current != null && Mouse.current.leftButton.isPressed;
            _currentState = ShipState.WaitingAtCheckpoint;
            _anchorCooldownTimer = 0f;
            _accelRampTimer = 0f;
            _wasAccelPhase = false;
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
            _currentState = ShipState.WaitingAtCheckpoint;
            _anchorCooldownTimer = 0f;
            _accelRampTimer = 0f;
            _wasAccelPhase = false;

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

            Debug.Log(_activeCheckpoint != null
                ? $"[Ship] 从存档点重生并等待出发: {_activeCheckpoint.name}"
                : "[Ship] 回到初始出生点并等待出发");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Debug.Log($"[Ship] 玩家碰撞体触发: {other.name}");
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
