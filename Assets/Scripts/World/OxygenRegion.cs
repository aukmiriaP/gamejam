using UnityEngine;

namespace AnchorGame
{
    [RequireComponent(typeof(Collider2D))]
    public class OxygenRegion : MonoBehaviour
    {
        [SerializeField] private bool startChallenge = true;

        private void Reset()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerOxygen oxygen = other.GetComponentInParent<PlayerOxygen>();
            if (oxygen == null) return;

            if (startChallenge)
            {
                PlayerShip player = oxygen.GetComponent<PlayerShip>();
                Checkpoint checkpoint2 = GameObject.Find("Checkpoint_2")?.GetComponent<Checkpoint>();
                if (player != null && checkpoint2 != null)
                {
                    player.SetCheckpoint(checkpoint2);
                }

                oxygen.BeginChallenge();
            }
            else
            {
                oxygen.EndChallenge();
            }
        }
    }
}
