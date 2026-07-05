using UnityEngine;

namespace AnchorGame
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class AsteroidShard : MonoBehaviour
    {
        [SerializeField] private float oxygenDamage = 18f;
        [SerializeField] private float knockbackStrength = 4f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private bool destroyAfterLaunch = true;

        private Rigidbody2D _rb;
        private Vector2 _direction = Vector2.down;
        private bool _launched;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.linearDamping = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            circle.isTrigger = true;
        }

        public void Launch(Vector2 direction, float speed, float damage, float knockback, float life)
        {
            _direction = direction.normalized;
            oxygenDamage = damage;
            knockbackStrength = knockback;
            lifetime = life;
            _launched = true;
            _rb.linearVelocity = _direction * speed;
        }

        private void Update()
        {
            if (!_launched || !destroyAfterLaunch) return;

            lifetime -= Time.deltaTime;
            if (lifetime <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerOxygen oxygen = other.GetComponentInParent<PlayerOxygen>();
            if (oxygen == null) return;

            Vector2 knockbackDirection = _launched
                ? _direction
                : ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
            if (knockbackDirection.sqrMagnitude < 0.0001f)
            {
                knockbackDirection = _direction;
            }

            oxygen.Damage(oxygenDamage, knockbackDirection * knockbackStrength);
            if (_launched)
            {
                Destroy(gameObject);
            }
        }
    }
}
