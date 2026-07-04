using UnityEngine;

namespace AnchorGame
{
    [RequireComponent(typeof(Collider2D))]
    public class DeathZone : MonoBehaviour
    {
        private void Reset()
        {
            Collider2D zone = GetComponent<Collider2D>();
            zone.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerShip player = other.GetComponentInParent<PlayerShip>();
            if (player == null) return;

            Debug.Log($"[DeathZone] 玩家进入死亡区域: {name}");
            player.RespawnAtCheckpoint();
        }
    }
}
