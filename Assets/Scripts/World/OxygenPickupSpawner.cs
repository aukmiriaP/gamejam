using UnityEngine;

namespace AnchorGame
{
    public class OxygenPickupSpawner : MonoBehaviour
    {
        [SerializeField] private OxygenPickup pickupPrefab;
        [SerializeField] private Vector2 areaSize = new Vector2(28f, 5f);
        [SerializeField] private float spawnIntervalMin = 5f;
        [SerializeField] private float spawnIntervalMax = 9f;
        [SerializeField] private int maxAlive = 2;

        private float _timer;

        private void OnEnable()
        {
            ResetTimer();
        }

        private void Update()
        {
            if (pickupPrefab == null) return;
            if (CountAlivePickups() >= maxAlive) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            SpawnPickup();
            ResetTimer();
        }

        private void SpawnPickup()
        {
            Vector3 offset = new Vector3(
                Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f),
                0f
            );

            OxygenPickup pickup = Instantiate(pickupPrefab, transform.position + offset, Quaternion.identity);
            pickup.name = "Region3_OxygenPickup";
        }

        private int CountAlivePickups()
        {
            int count = 0;
            OxygenPickup[] pickups = FindObjectsByType<OxygenPickup>(FindObjectsSortMode.None);
            foreach (OxygenPickup pickup in pickups)
            {
                if (pickup.gameObject.scene == gameObject.scene)
                {
                    count++;
                }
            }

            return count;
        }

        private void ResetTimer()
        {
            _timer = Random.Range(spawnIntervalMin, spawnIntervalMax);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.4f);
            Gizmos.DrawWireCube(transform.position, areaSize);
        }
#endif
    }
}
