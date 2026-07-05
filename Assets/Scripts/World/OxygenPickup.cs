using UnityEngine;

namespace AnchorGame
{
    [RequireComponent(typeof(CircleCollider2D))]
    public class OxygenPickup : MonoBehaviour
    {
        [SerializeField] private float restoreAmount = 35f;
        [SerializeField] private float respawnDelay = 8f;

        private CircleCollider2D _collider;
        private SpriteRenderer[] _spriteRenderers;
        private float _respawnTimer;
        private bool _available = true;

        private void Awake()
        {
            _collider = GetComponent<CircleCollider2D>();
            _collider.isTrigger = true;
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void Update()
        {
            if (_available) return;

            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
            {
                SetAvailable(true);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerShip player = other.GetComponentInParent<PlayerShip>();
            if (player == null) return;

            Collect(player);
        }

        public void Collect(PlayerShip player)
        {
            if (!_available || player == null) return;

            PlayerOxygen oxygen = player.GetComponent<PlayerOxygen>();
            if (oxygen != null)
            {
                oxygen.AddOxygen(restoreAmount);
            }

            _respawnTimer = respawnDelay;
            SetAvailable(false);
        }

        private void SetAvailable(bool available)
        {
            _available = available;
            if (_collider != null) _collider.enabled = available;
            if (_spriteRenderers == null) return;

            foreach (SpriteRenderer spriteRenderer in _spriteRenderers)
            {
                if (spriteRenderer != null) spriteRenderer.enabled = available;
            }
        }
    }
}
