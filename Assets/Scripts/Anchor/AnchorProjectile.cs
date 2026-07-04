using UnityEngine;

namespace AnchorGame
{
    /// <summary>
    /// 锚弹丸 — 从飞船发射后沿直线飞行，碰撞天体后附着。
    ///
    /// 行为：
    /// 1. 从飞船位置以初始速度发射
    /// 2. 每帧检测是否超过最大射程
    /// 3. 碰撞到带有 CelestialBody 组件的天体时，通知飞船进入轨道
    /// 4. 碰撞到 Checkpoint 时，通知飞船直线牵引到存档点
    /// 5. 碰撞到其他物体或超射程时自动销毁
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class AnchorProjectile : MonoBehaviour
    {
        [Header("运行时参数（由 PlayerShip 初始化）")]
        [SerializeField] private float _speed = 15f;
        [SerializeField] private float _maxRange = 10f;
        [SerializeField] private Vector2 _direction = Vector2.up;

        [Header("配置")]
        [Tooltip("锚命中天体后是否显示在天体表面附着")]
        public bool attachToSurface = true;

        [Tooltip("锚命中天体后是否销毁自身（false = 留在场景中做绳索视觉反馈，直到玩家释放）")]
        public bool destroyOnAttach = false;

        [Tooltip("锚命中非天体障碍物时是否销毁")]
        public bool destroyOnObstacle = true;

        [Tooltip("钩锁线显示层级。背景图建议使用 -100，行星 0-10，钩锁保持更高避免被遮挡")]
        public int ropeSortingOrder = 25;

        private PlayerShip _owner;
        private Rigidbody2D _rb;
        private float _traveledDistance;
        private bool _hasHit;

        private LineRenderer _lineRenderer;
        private TrailRenderer _trailRenderer;

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
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearVelocity = _direction * _speed;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer != null)
            {
                _lineRenderer.sortingOrder = ropeSortingOrder;
            }
            _trailRenderer = GetComponent<TrailRenderer>();
        }

        private void FixedUpdate()
        {
            if (_hasHit) return;

            if (_rb != null)
            {
                _rb.linearVelocity = _direction * _speed;
            }

            _traveledDistance += _speed * Time.fixedDeltaTime;

            if (_traveledDistance >= _maxRange)
            {
                OnMaxRangeReached();
            }
        }

        private void Update()
        {
            DrawRopeLine();
            UpdateTrail();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return;

            OxygenPickup pickup = other.GetComponent<OxygenPickup>();
            if (pickup != null)
            {
                OnHitOxygenPickup(pickup);
                return;
            }

            Checkpoint checkpoint = other.GetComponent<Checkpoint>();
            if (checkpoint != null)
            {
                OnHitCheckpoint(checkpoint);
                return;
            }

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

        private void OnHitCheckpoint(Checkpoint checkpoint)
        {
            _hasHit = true;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            if (_owner != null)
            {
                _owner.OnAnchorHitCheckpoint(checkpoint, transform.position);
            }

            Debug.Log($"[Anchor] 命中存档点 {checkpoint.name}");

            if (destroyOnAttach)
            {
                Destroy(gameObject, 0.05f);
            }
        }

        private void OnHitOxygenPickup(OxygenPickup pickup)
        {
            _hasHit = true;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            if (_owner != null)
            {
                pickup.Collect(_owner);
                _owner.OnAnchorDestroyed();
            }

            Debug.Log($"[Anchor] 命中氧气道具 {pickup.name}");
            Destroy(gameObject, 0.05f);
        }

        private void OnHitCelestialBody(CelestialBody body)
        {
            _hasHit = true;

            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            if (attachToSurface)
            {
                Vector2 toCenter = (Vector2)transform.position - (Vector2)body.transform.position;
                Vector2 surfacePoint = (Vector2)body.transform.position + toCenter.normalized * body.radius;
                transform.position = surfacePoint;
            }

            body.Flash();

            if (_owner != null)
            {
                _owner.OnAnchorHitCelestial(body, _direction * _speed);
            }

            Debug.Log($"[Anchor] 命中天体 {body.name}");

            if (destroyOnAttach)
            {
                Destroy(gameObject, 0.05f);
            }
        }

        private void OnHitObstacle()
        {
            _hasHit = true;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            Debug.Log("[Anchor] 命中障碍物，销毁");

            if (_owner != null)
            {
                _owner.OnAnchorDestroyed();
            }

            Destroy(gameObject, 0.05f);
        }

        private void OnMaxRangeReached()
        {
            _hasHit = true;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            Debug.Log($"[Anchor] 达到最大射程 {_maxRange:F1}m，未命中");

            if (_owner != null)
            {
                _owner.OnAnchorDestroyed();
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 外部调用：主动销毁锚（玩家取消发射或释放锚）
        /// </summary>
        public void Terminate()
        {
            _hasHit = true;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            if (_owner != null)
            {
                _owner.OnAnchorDestroyed();
            }

            Destroy(gameObject);
        }

        private void DrawRopeLine()
        {
            if (_lineRenderer == null) return;
            if (_owner == null) return;

            _lineRenderer.positionCount = 2;
            _lineRenderer.SetPosition(0, _owner.transform.position);
            _lineRenderer.SetPosition(1, transform.position);
        }

        private void UpdateTrail()
        {
            if (_trailRenderer == null) return;
            if (_hasHit)
            {
                _trailRenderer.emitting = false;
            }
        }

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
}
