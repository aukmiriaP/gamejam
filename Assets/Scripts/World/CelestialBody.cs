using System.Collections;
using UnityEngine;

namespace AnchorGame
{
    /// <summary>
    /// 天体标记组件 — 挂载到场景中的行星/恒星等天体上。
    ///
    /// 锚碰撞到带有此组件的物体后会触发轨道飞行。
    /// 同时提供可选的引力参数，方便后续扩展（如多天体引力拉扯）。
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class CelestialBody : MonoBehaviour
    {
        [Header("天体属性")]
        [Tooltip("天体世界半径。CircleCollider2D 会根据 Transform Scale 自动换算本地半径")]
        public float radius = 2f;

        [Tooltip("天体引力强度 (0 = 无引力, 仅作为锚的附着点)")]
        public float gravityStrength = 0f;

        [Tooltip("引力影响范围半径 (0 = 仅在天体表面)")]
        public float gravityRange = 0f;

        [Tooltip("天体表面是否允许锚附着")]
        public bool allowAnchor = true;

        private CircleCollider2D _collider;
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;

        public Vector2 Center => transform.position;

        private void Awake()
        {
            _collider = GetComponent<CircleCollider2D>();
            SyncColliderRadius();

            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null) _originalColor = _spriteRenderer.color;
        }

        private void OnValidate()
        {
            if (_collider == null)
                _collider = GetComponent<CircleCollider2D>();
            SyncColliderRadius();
        }

        private void SyncColliderRadius()
        {
            if (_collider == null) return;

            float maxWorldScale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y)
            );
            float localRadius = Mathf.Max(0.01f, radius) / Mathf.Max(0.0001f, maxWorldScale);

            if (Mathf.Abs(_collider.radius - localRadius) > 0.001f)
            {
                _collider.radius = localRadius;
            }
        }

        /// <summary>
        /// 锚命中时的白色闪烁反馈，让挂锚成功这件事始终可见，
        /// 不管挂锚后角速度是否为 0（静止起步时容易看不出任何变化）。
        /// </summary>
        public void Flash()
        {
            if (_spriteRenderer == null) return;
            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.15f);
            _spriteRenderer.color = _originalColor;
        }

        public Vector2 GetGravityAt(Vector2 targetPosition)
        {
            if (gravityStrength <= 0f) return Vector2.zero;

            Vector2 toTarget = targetPosition - Center;
            float dist = toTarget.magnitude;

            if (dist < radius * 0.1f) return Vector2.zero;

            float effectiveRange = gravityRange > 0f ? gravityRange : float.MaxValue;
            if (dist > effectiveRange) return Vector2.zero;

            Vector2 direction = -toTarget.normalized;
            float strength = gravityStrength / (dist * dist);

            return direction * strength;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = allowAnchor ? new Color(1f, 0.8f, 0.2f, 0.8f) : Color.gray;
            Gizmos.DrawWireSphere(transform.position, radius);

            if (gravityStrength > 0f && gravityRange > 0f)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.2f);
                Gizmos.DrawWireSphere(transform.position, gravityRange);
            }
        }
#endif
    }
}
