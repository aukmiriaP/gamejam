using UnityEngine;

namespace AnchorGame
{
    [RequireComponent(typeof(PlayerShip))]
    public class PlayerOxygen : MonoBehaviour
    {
        [Header("氧气")]
        [SerializeField] private float maxOxygen = 100f;
        [SerializeField] private float drainPerSecond = 5f;
        [SerializeField] private bool oxygenActive;
        [SerializeField] private string finishCheckpointName = "Checkpoint_3";

        [Header("UI")]
        [SerializeField] private bool showLegacyOnGui;
        [SerializeField] private Vector2 barPosition = new Vector2(24f, 24f);
        [SerializeField] private Vector2 barSize = new Vector2(220f, 18f);

        [Header("飞船氧气外观")]
        [SerializeField] private SpriteRenderer shipRenderer;
        [SerializeField] private Sprite oxygenHighSprite;
        [SerializeField] private Sprite oxygenMediumSprite;
        [SerializeField] private Sprite oxygenLowSprite;
        [SerializeField, Range(0f, 1f)] private float mediumOxygenThreshold = 0.5f;
        [SerializeField, Range(0f, 1f)] private float lowOxygenThreshold = 0.2f;

        private PlayerShip _ship;
        private float _oxygen;
        private Sprite _lastAppliedSprite;

        public float CurrentOxygen => _oxygen;
        public float MaxOxygen => maxOxygen;
        public float Normalized => maxOxygen <= 0f ? 0f : Mathf.Clamp01(_oxygen / maxOxygen);
        public bool OxygenActive => oxygenActive;

        private void Awake()
        {
            _ship = GetComponent<PlayerShip>();
            if (shipRenderer == null)
            {
                shipRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            _oxygen = maxOxygen;
            UpdateOxygenSprite(force: true);
        }

        private void Update()
        {
            if (!oxygenActive)
            {
                UpdateOxygenSprite();
                return;
            }

            _oxygen = Mathf.Max(0f, _oxygen - drainPerSecond * Time.deltaTime);
            UpdateOxygenSprite();

            if (_oxygen <= 0f)
            {
                oxygenActive = false;
                _ship.RespawnAtCheckpoint();
                ResetOxygen();
                Debug.Log("[Oxygen] 氧气耗尽，回到当前存档点");
            }
        }

        public void BeginChallenge()
        {
            oxygenActive = true;
            ResetOxygen();
            Debug.Log("[Oxygen] 区域3氧气挑战开始");
        }

        public void EndChallenge()
        {
            oxygenActive = false;
            ResetOxygen();
            Debug.Log("[Oxygen] 区域3氧气挑战完成");
        }

        public void AddOxygen(float amount)
        {
            _oxygen = Mathf.Clamp(_oxygen + amount, 0f, maxOxygen);
            UpdateOxygenSprite();
            Debug.Log($"[Oxygen] 补充氧气 +{amount:F0}，当前 {_oxygen:F0}/{maxOxygen:F0}");
        }

        public void Damage(float amount, Vector2 knockback)
        {
            if (!oxygenActive) return;

            _oxygen = Mathf.Max(0f, _oxygen - amount);
            UpdateOxygenSprite();
            _ship.ApplyHazardKnockback(knockback);
            Debug.Log($"[Oxygen] 碎石扣氧 -{amount:F0}，当前 {_oxygen:F0}/{maxOxygen:F0}");

            if (_oxygen <= 0f)
            {
                oxygenActive = false;
                _ship.RespawnAtCheckpoint();
                ResetOxygen();
            }
        }

        public void ResetOxygen()
        {
            _oxygen = maxOxygen;
            UpdateOxygenSprite(force: true);
        }

        public void OnCheckpointReached(Checkpoint checkpoint)
        {
            if (checkpoint != null && checkpoint.name == finishCheckpointName)
            {
                EndChallenge();
            }
        }

        private void UpdateOxygenSprite(bool force = false)
        {
            if (shipRenderer == null) return;

            Sprite targetSprite = GetSpriteForOxygen(Normalized);
            if (targetSprite == null) return;
            if (!force && _lastAppliedSprite == targetSprite) return;

            shipRenderer.sprite = targetSprite;
            _lastAppliedSprite = targetSprite;
        }

        private Sprite GetSpriteForOxygen(float normalizedOxygen)
        {
            if (normalizedOxygen <= lowOxygenThreshold && oxygenLowSprite != null)
            {
                return oxygenLowSprite;
            }

            if (normalizedOxygen <= mediumOxygenThreshold && oxygenMediumSprite != null)
            {
                return oxygenMediumSprite;
            }

            return oxygenHighSprite != null ? oxygenHighSprite : shipRenderer != null ? shipRenderer.sprite : null;
        }

        private void OnGUI()
        {
            if (!showLegacyOnGui) return;
            if (!oxygenActive) return;

            Rect background = new Rect(barPosition.x, barPosition.y, barSize.x, barSize.y);
            Rect fill = new Rect(barPosition.x, barPosition.y, barSize.x * Normalized, barSize.y);

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(background, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(1f, 0.2f, 0.1f, 1f), new Color(0.2f, 0.75f, 1f, 1f), Normalized);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(barPosition.x, barPosition.y + barSize.y + 4f, 180f, 20f), $"O2 {_oxygen:F0}/{maxOxygen:F0}");
        }
    }
}
