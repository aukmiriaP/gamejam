using UnityEngine;

namespace AnchorGame
{
    public class SimpleFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector2 offset = Vector2.zero;
        [SerializeField] private bool clampBetweenVerticalBounds;
        [SerializeField] private Collider2D bottomBound;
        [SerializeField] private Collider2D topBound;

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
            desired.y = ClampVerticalPosition(desired.y);

            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        }

        private float ClampVerticalPosition(float desiredY)
        {
            if (!clampBetweenVerticalBounds || bottomBound == null || topBound == null || _camera == null)
            {
                return desiredY;
            }

            Bounds bottom = bottomBound.bounds;
            Bounds top = topBound.bounds;

            float safeBottom = bottom.max.y;
            float safeTop = top.min.y;
            if (safeTop < safeBottom)
            {
                (safeBottom, safeTop) = (safeTop, safeBottom);
            }

            float halfHeight = _camera.orthographic ? _camera.orthographicSize : 0f;
            float minY = safeBottom + halfHeight;
            float maxY = safeTop - halfHeight;

            if (minY > maxY)
            {
                return (safeBottom + safeTop) * 0.5f;
            }

            return Mathf.Clamp(desiredY, minY, maxY);
        }
    }
}
