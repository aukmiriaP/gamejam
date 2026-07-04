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

        [Header("锚链视觉")]
        [Tooltip("可持久化样式资产。运行时调这个资产的颜色/宽度会保留，不会因为退出 Play Mode 丢失")]
        public AnchorEnergyStyle visualStyle;

        [Tooltip("开启后使用核心线 + 外发光线表现能量束")]
        public bool useEnergyRopeStyle = true;

        [Tooltip("能量束中心颜色")]
        public Color ropeCoreColor = new Color(0.35f, 1f, 1f, 0.95f);

        [Tooltip("能量束外发光颜色")]
        public Color ropeGlowColor = new Color(0f, 0.65f, 1f, 0.34f);

        [Tooltip("能量束中心线宽")]
        public float ropeCoreWidth = 0.045f;

        [Tooltip("能量束外发光线宽")]
        public float ropeGlowWidth = 0.18f;

        [Tooltip("能量束闪烁速度")]
        public float ropePulseSpeed = 7f;

        [Tooltip("能量束闪烁强度")]
        [Range(0f, 1f)] public float ropePulseAmount = 0.18f;

        [Tooltip("锚点核心颜色")]
        public Color anchorHeadColor = new Color(0.55f, 1f, 1f, 1f);

        [Tooltip("锚点外发光颜色")]
        public Color anchorHeadGlowColor = new Color(0f, 0.75f, 1f, 0.38f);

        [Tooltip("锚点外发光相对缩放")]
        public float anchorHeadGlowScale = 1.75f;

        [Tooltip("锚点飞行拖尾颜色")]
        public Color anchorTrailColor = new Color(0f, 0.8f, 1f, 0.58f);

        [Tooltip("锚点飞行拖尾持续时间")]
        public float anchorTrailTime = 0.22f;

        [Tooltip("锚点飞行拖尾宽度")]
        public float anchorTrailWidth = 0.14f;

        private PlayerShip _owner;
        private Rigidbody2D _rb;
        private float _traveledDistance;
        private bool _hasHit;

        private SpriteRenderer _spriteRenderer;
        private SpriteRenderer _headGlowRenderer;
        private LineRenderer _lineRenderer;
        private LineRenderer _glowLineRenderer;
        private TrailRenderer _trailRenderer;
        private Material _headMaterial;
        private Material _headGlowMaterial;
        private Material _ropeMaterial;
        private Material _glowRopeMaterial;
        private Material _trailMaterial;

        private bool UseEnergyRopeStyle => visualStyle != null ? visualStyle.useEnergyRopeStyle : useEnergyRopeStyle;
        private Color RopeCoreColor => visualStyle != null ? visualStyle.ropeCoreColor : ropeCoreColor;
        private Color RopeGlowColor => visualStyle != null ? visualStyle.ropeGlowColor : ropeGlowColor;
        private float RopeCoreWidth => visualStyle != null ? visualStyle.ropeCoreWidth : ropeCoreWidth;
        private float RopeGlowWidth => visualStyle != null ? visualStyle.ropeGlowWidth : ropeGlowWidth;
        private float RopePulseSpeed => visualStyle != null ? visualStyle.ropePulseSpeed : ropePulseSpeed;
        private float RopePulseAmount => visualStyle != null ? visualStyle.ropePulseAmount : ropePulseAmount;
        private Color AnchorHeadColor => visualStyle != null ? visualStyle.anchorHeadColor : anchorHeadColor;
        private Color AnchorHeadGlowColor => visualStyle != null ? visualStyle.anchorHeadGlowColor : anchorHeadGlowColor;
        private float AnchorHeadGlowScale => visualStyle != null ? visualStyle.anchorHeadGlowScale : anchorHeadGlowScale;
        private Color AnchorTrailColor => visualStyle != null ? visualStyle.anchorTrailColor : anchorTrailColor;
        private float AnchorTrailTime => visualStyle != null ? visualStyle.anchorTrailTime : anchorTrailTime;
        private float AnchorTrailWidth => visualStyle != null ? visualStyle.anchorTrailWidth : anchorTrailWidth;

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

            _spriteRenderer = GetComponent<SpriteRenderer>();
            ConfigureAnchorHead();

            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer != null)
            {
                ConfigureCoreRope();
                ConfigureGlowRope();
            }

            _trailRenderer = GetComponent<TrailRenderer>();
            ConfigureTrail();
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
            UpdateRopeStyle();
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

            if (_glowLineRenderer != null)
            {
                _glowLineRenderer.positionCount = 2;
                _glowLineRenderer.SetPosition(0, _owner.transform.position);
                _glowLineRenderer.SetPosition(1, transform.position);
            }
        }

        private void ConfigureCoreRope()
        {
            _lineRenderer.sortingOrder = ropeSortingOrder + 1;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.alignment = LineAlignment.View;
            _lineRenderer.numCapVertices = 6;
            _lineRenderer.numCornerVertices = 2;

            _ropeMaterial = CreateRopeMaterial("AnchorRopeCoreMaterial");
            if (_ropeMaterial != null)
            {
                _lineRenderer.material = _ropeMaterial;
            }
        }

        private void ConfigureGlowRope()
        {
            if (!UseEnergyRopeStyle) return;
            if (_glowLineRenderer != null) return;

            GameObject glowObject = new GameObject("AnchorEnergyGlow");
            glowObject.transform.SetParent(transform, false);

            _glowLineRenderer = glowObject.AddComponent<LineRenderer>();
            _glowLineRenderer.useWorldSpace = true;
            _glowLineRenderer.alignment = LineAlignment.View;
            _glowLineRenderer.numCapVertices = 8;
            _glowLineRenderer.numCornerVertices = 2;
            _glowLineRenderer.sortingOrder = ropeSortingOrder;
            _glowLineRenderer.positionCount = 2;

            _glowRopeMaterial = CreateRopeMaterial("AnchorRopeGlowMaterial");
            if (_glowRopeMaterial != null)
            {
                _glowLineRenderer.material = _glowRopeMaterial;
            }
        }

        private void UpdateRopeStyle()
        {
            if (_lineRenderer == null) return;

            if (!UseEnergyRopeStyle)
            {
                _lineRenderer.startWidth = RopeCoreWidth;
                _lineRenderer.endWidth = RopeCoreWidth;
                _lineRenderer.startColor = RopeCoreColor;
                _lineRenderer.endColor = RopeCoreColor;
                if (_glowLineRenderer != null)
                {
                    _glowLineRenderer.enabled = false;
                }
                UpdateAnchorHeadStyle(1f);
                UpdateTrailStyle();
                return;
            }

            if (_glowLineRenderer == null)
            {
                ConfigureGlowRope();
            }

            float pulse = 1f + Mathf.Sin(Time.time * RopePulseSpeed) * RopePulseAmount;
            float coreWidth = Mathf.Max(0.005f, RopeCoreWidth * pulse);
            float glowWidth = Mathf.Max(coreWidth, RopeGlowWidth * pulse);
            Color core = WithAlphaMultiplier(RopeCoreColor, 0.82f + (pulse - 1f) * 0.7f);
            Color glow = WithAlphaMultiplier(RopeGlowColor, 0.78f + (pulse - 1f) * 0.9f);

            _lineRenderer.startWidth = coreWidth;
            _lineRenderer.endWidth = coreWidth;
            _lineRenderer.startColor = core;
            _lineRenderer.endColor = core;

            if (_glowLineRenderer != null)
            {
                _glowLineRenderer.enabled = true;
                _glowLineRenderer.startWidth = glowWidth;
                _glowLineRenderer.endWidth = glowWidth;
                _glowLineRenderer.startColor = glow;
                _glowLineRenderer.endColor = new Color(glow.r, glow.g, glow.b, glow.a * 0.75f);
            }

            UpdateAnchorHeadStyle(pulse);
            UpdateTrailStyle();
        }

        private void ConfigureAnchorHead()
        {
            if (_spriteRenderer == null) return;

            _spriteRenderer.sortingOrder = ropeSortingOrder + 3;
            _spriteRenderer.color = AnchorHeadColor;

            _headMaterial = CreateRopeMaterial("AnchorHeadMaterial");
            if (_headMaterial != null)
            {
                _spriteRenderer.material = _headMaterial;
            }

            if (!UseEnergyRopeStyle || _spriteRenderer.sprite == null) return;

            CreateAnchorHeadGlow();
        }

        private void CreateAnchorHeadGlow()
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;
            if (_headGlowRenderer != null) return;

            GameObject glowObject = new GameObject("AnchorHeadGlow");
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.localPosition = Vector3.zero;
            glowObject.transform.localRotation = Quaternion.identity;
            glowObject.transform.localScale = Vector3.one * Mathf.Max(1f, AnchorHeadGlowScale);

            _headGlowRenderer = glowObject.AddComponent<SpriteRenderer>();
            _headGlowRenderer.sprite = _spriteRenderer.sprite;
            _headGlowRenderer.sortingOrder = ropeSortingOrder + 2;
            _headGlowRenderer.color = AnchorHeadGlowColor;

            _headGlowMaterial = CreateRopeMaterial("AnchorHeadGlowMaterial");
            if (_headGlowMaterial != null)
            {
                _headGlowRenderer.material = _headGlowMaterial;
            }
        }

        private void UpdateAnchorHeadStyle(float pulse)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = WithAlphaMultiplier(AnchorHeadColor, 0.9f + (pulse - 1f) * 0.65f);
            }

            if (UseEnergyRopeStyle && _headGlowRenderer == null)
            {
                CreateAnchorHeadGlow();
            }

            if (_headGlowRenderer != null)
            {
                _headGlowRenderer.enabled = UseEnergyRopeStyle;
                _headGlowRenderer.color = WithAlphaMultiplier(AnchorHeadGlowColor, 0.82f + (pulse - 1f) * 0.9f);
                _headGlowRenderer.transform.localScale = Vector3.one * Mathf.Max(1f, AnchorHeadGlowScale * pulse);
            }
        }

        private void ConfigureTrail()
        {
            if (_trailRenderer == null) return;

            _trailRenderer.sortingOrder = ropeSortingOrder - 1;
            _trailRenderer.time = Mathf.Max(0.01f, AnchorTrailTime);
            _trailRenderer.widthMultiplier = Mathf.Max(0.005f, AnchorTrailWidth);
            _trailRenderer.numCapVertices = 6;
            _trailRenderer.numCornerVertices = 2;
            _trailRenderer.emitting = true;

            _trailMaterial = CreateRopeMaterial("AnchorTrailMaterial");
            if (_trailMaterial != null)
            {
                _trailRenderer.material = _trailMaterial;
            }

            UpdateTrailStyle();
        }

        private void UpdateTrailStyle()
        {
            if (_trailRenderer == null) return;

            _trailRenderer.time = Mathf.Max(0.01f, AnchorTrailTime);
            _trailRenderer.widthMultiplier = Mathf.Max(0.005f, AnchorTrailWidth);

            Color trailStart = UseEnergyRopeStyle ? AnchorTrailColor : RopeCoreColor;
            Color trailEnd = UseEnergyRopeStyle ? RopeGlowColor : RopeCoreColor;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(trailStart, 0f),
                    new GradientColorKey(trailEnd, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(trailStart.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            _trailRenderer.colorGradient = gradient;
        }

        private static Material CreateRopeMaterial(string materialName)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }
            if (shader == null) return null;

            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave
            };
            return material;
        }

        private static Color WithAlphaMultiplier(Color color, float multiplier)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * multiplier));
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

        private void OnDestroy()
        {
            if (_headMaterial != null)
            {
                DestroyGeneratedMaterial(_headMaterial);
            }
            if (_headGlowMaterial != null)
            {
                DestroyGeneratedMaterial(_headGlowMaterial);
            }
            if (_ropeMaterial != null)
            {
                DestroyGeneratedMaterial(_ropeMaterial);
            }
            if (_glowRopeMaterial != null)
            {
                DestroyGeneratedMaterial(_glowRopeMaterial);
            }
            if (_trailMaterial != null)
            {
                DestroyGeneratedMaterial(_trailMaterial);
            }
        }

        private static void DestroyGeneratedMaterial(Material material)
        {
            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
