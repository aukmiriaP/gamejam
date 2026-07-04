using System.Collections.Generic;
using UnityEngine;

namespace AnchorGame
{
    [RequireComponent(typeof(CircleCollider2D))]
    public class BlackHole : MonoBehaviour
    {
        [Header("黑洞")]
        [SerializeField] private float attractionRadius = 7.5f;
        [SerializeField] private float consumeRadius = 0.65f;
        [SerializeField] private float attractionAcceleration = 28f;
        [SerializeField] private float maxAttractedSpeed = 12f;
        [SerializeField] private float falloffExponent = 1.35f;
        [SerializeField] private float radialBrakeStrength = 7f;
        [SerializeField] private float steeringStrength = 2.8f;

        [Header("视觉")]
        [SerializeField] private int sortingOrder = 9;
        [SerializeField] private bool rotateVisual = true;
        [SerializeField] private float rotationSpeed = -35f;

        private readonly Collider2D[] _hits = new Collider2D[16];
        private readonly HashSet<PlayerShip> _affectedPlayers = new HashSet<PlayerShip>();
        private CircleCollider2D _attractionCollider;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            CacheComponents();
            SyncComponents();
        }

        private void OnValidate()
        {
            CacheComponents();
            SyncComponents();
        }

        private void Update()
        {
            if (!rotateVisual || _spriteRenderer == null) return;

            _spriteRenderer.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }

        private void FixedUpdate()
        {
            Vector2 center = transform.position;
            float radius = Mathf.Max(0.05f, attractionRadius);
            int count = Physics2D.OverlapCircleNonAlloc(center, radius, _hits);
            _affectedPlayers.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                if (hit == null) continue;

                PlayerShip player = hit.GetComponentInParent<PlayerShip>();
                if (player == null) continue;
                if (!_affectedPlayers.Add(player)) continue;

                Vector2 toCenter = center - (Vector2)player.transform.position;
                float distance = toCenter.magnitude;
                if (distance <= Mathf.Max(0.01f, consumeRadius))
                {
                    Debug.Log($"[BlackHole] 玩家被黑洞吞噬: {name}");
                    player.RespawnAtCheckpoint();
                    continue;
                }

                float pullRange = Mathf.Max(0.05f, radius - Mathf.Max(0f, consumeRadius));
                float normalized = Mathf.Clamp01(1f - (distance - consumeRadius) / pullRange);
                float falloff = Mathf.Pow(normalized, Mathf.Max(0.01f, falloffExponent));
                float acceleration = attractionAcceleration * Mathf.Lerp(0.35f, 1f, falloff);
                player.ApplyBlackHoleAttraction(
                    center,
                    acceleration,
                    maxAttractedSpeed,
                    radialBrakeStrength,
                    steeringStrength,
                    Time.fixedDeltaTime
                );
            }
        }

        private void CacheComponents()
        {
            if (_attractionCollider == null)
                _attractionCollider = GetComponent<CircleCollider2D>();
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void SyncComponents()
        {
            if (_attractionCollider == null)
                _attractionCollider = GetComponent<CircleCollider2D>();
            if (_attractionCollider == null)
                _attractionCollider = gameObject.AddComponent<CircleCollider2D>();

            _attractionCollider.isTrigger = true;
            _attractionCollider.radius = Mathf.Max(0.05f, attractionRadius);

            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = sortingOrder;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.65f, 0.25f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.05f, attractionRadius));

            Gizmos.color = new Color(1f, 0.1f, 0.35f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, consumeRadius));
        }
#endif
    }
}
