using UnityEngine;

namespace AnchorGame
{
    [RequireComponent(typeof(CircleCollider2D))]
    public class Wormhole : MonoBehaviour
    {
        public enum EndpointKind
        {
            Entrance,
            Exit
        }

        [Header("虫洞")]
        [SerializeField] private EndpointKind endpointKind = EndpointKind.Entrance;
        [SerializeField] private Wormhole linkedWormhole;
        [SerializeField] private float triggerRadius = 1.1f;
        [SerializeField] private float arrivalOffsetDistance = 0.55f;
        [SerializeField] private float teleportCooldown = 0.25f;

        [Header("视觉")]
        [SerializeField] private Color entranceColor = Color.white;
        [SerializeField] private Color exitColor = Color.white;
        [SerializeField] private int sortingOrder = 8;

        private CircleCollider2D _circleCollider;
        private SpriteRenderer _spriteRenderer;

        public EndpointKind Kind => endpointKind;
        public Wormhole LinkedWormhole => linkedWormhole;
        public Vector2 AnchorPoint => transform.position;
        public float ArrivalDistance => Mathf.Max(0.05f, triggerRadius * 0.25f);

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

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerShip player = other.GetComponentInParent<PlayerShip>();
            if (player == null) return;

            Teleport(player);
        }

        public void Teleport(PlayerShip player)
        {
            if (player == null || linkedWormhole == null) return;
            if (!player.CanUseWormhole) return;

            Vector2 velocity = player.Velocity;
            Vector2 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.right;
            Vector2 destination = linkedWormhole.AnchorPoint + direction * Mathf.Max(0f, linkedWormhole.arrivalOffsetDistance);

            player.TeleportThroughWormhole(destination, Mathf.Max(0.01f, teleportCooldown));
            Debug.Log($"[Wormhole] {name} ({endpointKind}) → {linkedWormhole.name}");
        }

        private void CacheComponents()
        {
            if (_circleCollider == null)
                _circleCollider = GetComponent<CircleCollider2D>();
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void SyncComponents()
        {
            if (_circleCollider == null)
                _circleCollider = GetComponent<CircleCollider2D>();
            if (_circleCollider == null)
                _circleCollider = gameObject.AddComponent<CircleCollider2D>();

            _circleCollider.isTrigger = true;
            _circleCollider.radius = Mathf.Max(0.05f, triggerRadius);

            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = sortingOrder;
                _spriteRenderer.color = endpointKind == EndpointKind.Entrance ? entranceColor : exitColor;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = endpointKind == EndpointKind.Entrance
                ? new Color(0.15f, 0.9f, 1f, 0.55f)
                : new Color(1f, 0.35f, 0.95f, 0.55f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.05f, triggerRadius));

            if (linkedWormhole != null)
            {
                Gizmos.DrawLine(transform.position, linkedWormhole.transform.position);
            }
        }
#endif
    }
}
