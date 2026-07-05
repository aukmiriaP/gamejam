using UnityEngine;

namespace AnchorGame
{
    public class AsteroidStreamSpawner : MonoBehaviour
    {
        [SerializeField] private AsteroidShard shardPrefab;
        [SerializeField] private AsteroidShard[] shardPrefabs;
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
            if (!HasShardPrefab()) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            SpawnShard();
            ResetTimer();
        }

        private void SpawnShard()
        {
            Vector3 spawnPosition = transform.position + Vector3.right * Random.Range(-streamWidth * 0.5f, streamWidth * 0.5f);
            AsteroidShard prefab = GetRandomShardPrefab();
            if (prefab == null) return;

            AsteroidShard shard = Instantiate(prefab, spawnPosition, Quaternion.identity);
            float speed = Random.Range(shardSpeedMin, shardSpeedMax);
            shard.Launch(direction, speed, shardDamage, knockbackStrength, shardLifetime);
        }

        private bool HasShardPrefab()
        {
            if (shardPrefabs != null)
            {
                for (int i = 0; i < shardPrefabs.Length; i++)
                {
                    if (shardPrefabs[i] != null) return true;
                }
            }

            return shardPrefab != null;
        }

        private AsteroidShard GetRandomShardPrefab()
        {
            if (shardPrefabs != null && shardPrefabs.Length > 0)
            {
                int startIndex = Random.Range(0, shardPrefabs.Length);
                for (int i = 0; i < shardPrefabs.Length; i++)
                {
                    AsteroidShard candidate = shardPrefabs[(startIndex + i) % shardPrefabs.Length];
                    if (candidate != null) return candidate;
                }
            }

            return shardPrefab;
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
