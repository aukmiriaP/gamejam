using UnityEngine;

namespace AnchorGame
{
    public class AsteroidStreamSpawner : MonoBehaviour
    {
        [SerializeField] private AsteroidShard shardPrefab;
        [SerializeField] private Vector2 direction = Vector2.down;
        [SerializeField] private float streamWidth = 58f;
        [SerializeField] private float spawnIntervalMin = 0.5f;
        [SerializeField] private float spawnIntervalMax = 1.2f;
        [SerializeField] private float shardSpeedMin = 4f;
        [SerializeField] private float shardSpeedMax = 8f;
        [SerializeField] private float shardDamage = 18f;
        [SerializeField] private float knockbackStrength = 4.5f;
        [SerializeField] private float shardLifetime = 5f;

        private float _timer;

        private void OnEnable()
        {
            ResetTimer();
        }

        private void Update()
        {
            if (shardPrefab == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            SpawnShard();
            ResetTimer();
        }

        private void SpawnShard()
        {
            Vector3 spawnPosition = transform.position + Vector3.right * Random.Range(-streamWidth * 0.5f, streamWidth * 0.5f);
            AsteroidShard shard = Instantiate(shardPrefab, spawnPosition, Quaternion.identity);
            float speed = Random.Range(shardSpeedMin, shardSpeedMax);
            shard.Launch(direction, speed, shardDamage, knockbackStrength, shardLifetime);
        }

        private void ResetTimer()
        {
            _timer = Random.Range(spawnIntervalMin, spawnIntervalMax);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 left = transform.position + Vector3.left * streamWidth * 0.5f;
            Vector3 right = transform.position + Vector3.right * streamWidth * 0.5f;
            Gizmos.DrawLine(left, right);
            Gizmos.DrawRay(transform.position, (Vector3)direction.normalized * 2f);
        }
#endif
    }
}
