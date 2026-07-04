using UnityEngine;
using System.Collections.Generic;

namespace AnchorGame
{
    public class Checkpoint : MonoBehaviour
    {
        private enum CheckpointColliderShape
        {
            Circle,
            Box
        }

        [Header("存档点")]
        [SerializeField] private CheckpointColliderShape colliderShape = CheckpointColliderShape.Circle;
        [SerializeField] private float triggerRadius = 1.25f;
        [SerializeField] private Vector2 triggerSize = new Vector2(2.5f, 1.25f);
        [SerializeField] private bool respawnFacingRight = true;
        [SerializeField] private bool finalCheckpoint;

        [Header("视觉反馈")]
        [SerializeField] private Color inactiveColor = new Color(0f, 0.9f, 0.9f, 0.35f);
        [SerializeField] private Color activeColor = new Color(0f, 1f, 0.75f, 0.85f);

        [Header("终点引导波纹")]
        [SerializeField] private bool emitGuidanceRipples;
        [SerializeField] private float rippleInterval = 1.2f;
        [SerializeField] private float rippleSpeed = 6f;
        [SerializeField] private float rippleMaxRadius = 28f;
        [SerializeField] private float rippleWidth = 0.08f;
        [SerializeField] private int rippleSegments = 96;
        [SerializeField] private Color rippleColor = new Color(0f, 0.95f, 1f, 0.55f);

        private CircleCollider2D _circleCollider;
        private BoxCollider2D _boxCollider;
        private SpriteRenderer _spriteRenderer;
        private bool _activated;
        private float _rippleTimer;
        private Material _rippleMaterial;
        private static readonly List<Checkpoint> AllCheckpoints = new List<Checkpoint>();
        private static bool completionWindowVisible;
        private static Checkpoint completionCheckpoint;
        private static float previousTimeScale = 1f;

        public Vector2 SpawnPosition => transform.position;
        public bool IsActivated => _activated;
        public bool IsFinalCheckpoint => finalCheckpoint;

        private void OnEnable()
        {
            if (!AllCheckpoints.Contains(this))
            {
                AllCheckpoints.Add(this);
            }
        }

        private void OnDisable()
        {
            AllCheckpoints.Remove(this);
        }

        private void Awake()
        {
            CacheComponents();
            SyncCollider();
            ApplyVisual();
            EnsureRippleMaterial();
        }

        private void Update()
        {
            if (!emitGuidanceRipples) return;

            _rippleTimer -= Time.deltaTime;
            if (_rippleTimer > 0f) return;

            SpawnRipple();
            _rippleTimer = Mathf.Max(0.05f, rippleInterval);
        }

        private void OnValidate()
        {
            CacheComponents();
            SyncCollider();
            ApplyVisual();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerShip player = other.GetComponentInParent<PlayerShip>();
            if (player == null) return;

            Activate(player);
        }

        public Vector2 GetRespawnVelocity(float speed)
        {
            return (respawnFacingRight ? Vector2.right : (Vector2)transform.right).normalized * speed;
        }

        public void Activate(PlayerShip player)
        {
            if (player == null) return;

            if (!_activated)
            {
                _activated = true;
                ApplyVisual();
                Debug.Log($"[Checkpoint] 激活: {name}");
            }

            player.StopAtCheckpoint(this);

            if (finalCheckpoint)
            {
                ShowCompletionWindow(this);
            }
        }

        private void CacheComponents()
        {
            if (_circleCollider == null)
                _circleCollider = GetComponent<CircleCollider2D>();
            if (_boxCollider == null)
                _boxCollider = GetComponent<BoxCollider2D>();
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void EnsureRippleMaterial()
        {
            if (_rippleMaterial != null) return;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            }

            if (shader != null)
            {
                _rippleMaterial = new Material(shader);
            }
        }

        private void SyncCollider()
        {
            if (colliderShape == CheckpointColliderShape.Circle)
            {
                if (_circleCollider == null)
                    _circleCollider = GetComponent<CircleCollider2D>();
                if (_circleCollider == null)
                    _circleCollider = gameObject.AddComponent<CircleCollider2D>();

                _circleCollider.enabled = true;
                _circleCollider.isTrigger = true;
                _circleCollider.radius = Mathf.Max(0.1f, triggerRadius);

                if (_boxCollider != null)
                    _boxCollider.enabled = false;
            }
            else
            {
                if (_boxCollider == null)
                    _boxCollider = GetComponent<BoxCollider2D>();
                if (_boxCollider == null)
                    _boxCollider = gameObject.AddComponent<BoxCollider2D>();

                _boxCollider.enabled = true;
                _boxCollider.isTrigger = true;
                _boxCollider.size = new Vector2(Mathf.Max(0.1f, triggerSize.x), Mathf.Max(0.1f, triggerSize.y));

                if (_circleCollider != null)
                    _circleCollider.enabled = false;
            }
        }

        private void ApplyVisual()
        {
            if (_spriteRenderer == null) return;

            _spriteRenderer.color = _activated ? activeColor : inactiveColor;
        }

        private void SpawnRipple()
        {
            EnsureRippleMaterial();

            GameObject rippleObject = new GameObject($"{name}_GuidanceRipple");
            rippleObject.transform.position = transform.position;

            LineRenderer line = rippleObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Mathf.Max(12, rippleSegments);
            line.startWidth = rippleWidth;
            line.endWidth = rippleWidth;
            line.sortingOrder = 30;
            line.material = _rippleMaterial;

            CheckpointRipple ripple = rippleObject.AddComponent<CheckpointRipple>();
            ripple.Initialize(line, rippleColor, rippleSpeed, rippleMaxRadius, rippleSegments);
        }

        private static void ShowCompletionWindow(Checkpoint checkpoint)
        {
            if (completionWindowVisible) return;

            completionCheckpoint = checkpoint;
            completionWindowVisible = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        private static void HideCompletionWindow()
        {
            completionWindowVisible = false;
            completionCheckpoint = null;
            Time.timeScale = previousTimeScale;
        }

        private void OnGUI()
        {
            if (!completionWindowVisible || completionCheckpoint != this) return;

            float width = 430f;
            float height = 250f;
            Rect rect = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height
            );

            GUI.ModalWindow(7341, rect, DrawCompletionWindow, "教学关完成");
        }

        private static void DrawCompletionWindow(int windowId)
        {
            List<Checkpoint> checkpoints = GetSortedCheckpoints();
            int activatedCount = 0;
            foreach (Checkpoint checkpoint in checkpoints)
            {
                if (checkpoint != null && checkpoint.IsActivated)
                {
                    activatedCount++;
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label("恭喜已完成教学，是否进入下一关？");
            GUILayout.Space(8f);
            GUILayout.Label($"存档点点亮情况：{activatedCount}/{checkpoints.Count}");
            GUILayout.Space(10f);

            Rect iconRow = GUILayoutUtility.GetRect(1f, 48f, GUILayout.ExpandWidth(true));
            DrawCheckpointIcons(iconRow, checkpoints);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("继续停留", GUILayout.Height(34f)))
            {
                HideCompletionWindow();
            }

            if (GUILayout.Button("进入下一关", GUILayout.Height(34f)))
            {
                HideCompletionWindow();
                LevelManager.LoadNextLevel();
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawCheckpointIcons(Rect row, List<Checkpoint> checkpoints)
        {
            float size = 28f;
            float gap = 12f;
            float totalWidth = checkpoints.Count * size + Mathf.Max(0, checkpoints.Count - 1) * gap;
            float startX = row.x + (row.width - totalWidth) * 0.5f;
            float y = row.y + (row.height - size) * 0.5f;

            for (int i = 0; i < checkpoints.Count; i++)
            {
                Checkpoint checkpoint = checkpoints[i];
                Rect rect = new Rect(startX + i * (size + gap), y, size, size);
                bool active = checkpoint != null && checkpoint.IsActivated;

                GUI.color = active ? new Color(0f, 0.95f, 1f, 1f) : new Color(0.05f, 0.05f, 0.06f, 1f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Box(rect, GUIContent.none);
            }
        }

        private static List<Checkpoint> GetSortedCheckpoints()
        {
            List<Checkpoint> checkpoints = new List<Checkpoint>();
            foreach (Checkpoint checkpoint in AllCheckpoints)
            {
                if (checkpoint != null && checkpoint.gameObject.scene.IsValid())
                {
                    checkpoints.Add(checkpoint);
                }
            }

            checkpoints.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return checkpoints;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _activated ? activeColor : inactiveColor;
            if (colliderShape == CheckpointColliderShape.Circle)
            {
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, triggerRadius));
            }
            else
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(Mathf.Max(0.1f, triggerSize.x), Mathf.Max(0.1f, triggerSize.y), 0f));
                Gizmos.matrix = oldMatrix;
            }

            if (emitGuidanceRipples)
            {
                Gizmos.color = rippleColor;
                Gizmos.DrawWireSphere(transform.position, rippleMaxRadius);
            }
        }
#endif
    }

    public class CheckpointRipple : MonoBehaviour
    {
        private LineRenderer _line;
        private Color _baseColor;
        private float _speed;
        private float _maxRadius;
        private int _segments;
        private float _radius;

        public void Initialize(LineRenderer line, Color color, float speed, float maxRadius, int segments)
        {
            _line = line;
            _baseColor = color;
            _speed = Mathf.Max(0.01f, speed);
            _maxRadius = Mathf.Max(0.1f, maxRadius);
            _segments = Mathf.Max(12, segments);
            Draw();
        }

        private void Update()
        {
            _radius += _speed * Time.deltaTime;
            if (_radius >= _maxRadius)
            {
                Destroy(gameObject);
                return;
            }

            Draw();
        }

        private void Draw()
        {
            if (_line == null) return;

            float t = Mathf.Clamp01(_radius / _maxRadius);
            Color color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * (1f - t));
            _line.startColor = color;
            _line.endColor = color;
            _line.positionCount = _segments;

            for (int i = 0; i < _segments; i++)
            {
                float angle = i / (float)_segments * Mathf.PI * 2f;
                Vector3 point = new Vector3(Mathf.Cos(angle) * _radius, Mathf.Sin(angle) * _radius, 0f);
                _line.SetPosition(i, point);
            }
        }
    }
}
