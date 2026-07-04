using UnityEngine;

namespace AnchorGame
{
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryPredictor : MonoBehaviour
    {
        [SerializeField] private PlayerShip player;
        [SerializeField] private float predictionDistance = 30f;
        [SerializeField] private float dashWorldPitch = 0.5f;

        private LineRenderer line;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Tile;

            var material = new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = CreateDashTexture();
            line.material = material;
        }

        private void Update()
        {
            if (player == null || player.CurrentState != PlayerShip.ShipState.Orbiting)
            {
                line.enabled = false;
                return;
            }

            Vector2 velocity = player.Velocity;
            if (velocity.sqrMagnitude < 0.0001f)
            {
                line.enabled = false;
                return;
            }

            Vector2 origin = player.transform.position;
            Vector2 end = origin + velocity.normalized * predictionDistance;

            line.enabled = true;
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
            line.material.mainTextureScale = new Vector2(predictionDistance / dashWorldPitch, 1f);
        }

        private static Texture2D CreateDashTexture()
        {
            var texture = new Texture2D(4, 1, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            texture.SetPixels(new[]
            {
                Color.white,
                Color.white,
                new Color(1f, 1f, 1f, 0f),
                new Color(1f, 1f, 1f, 0f)
            });
            texture.Apply();
            return texture;
        }
    }
}
