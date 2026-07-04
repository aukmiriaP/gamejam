using UnityEngine;
using UnityEngine.UI;

namespace AnchorGame
{
    [RequireComponent(typeof(PlayerOxygen))]
    public class PlayerOxygenBarUI : MonoBehaviour
    {
        [Header("绑定")]
        [SerializeField] private PlayerOxygen oxygen;
        [SerializeField] private bool showOnlyWhenOxygenActive = true;

        [Header("布局")]
        [SerializeField] private Vector2 anchoredPosition = new Vector2(46f, -70f);
        [SerializeField] private Vector2 size = new Vector2(54f, 168f);
        [SerializeField] private int segmentCount = 8;
        [SerializeField] private float segmentGap = 5f;
        [SerializeField] private float frameThickness = 3f;
        [SerializeField] private float cornerRadius = 7f;
        [SerializeField] private float segmentCornerRadius = 2.5f;

        [Header("颜色")]
        [SerializeField] private Color frameColor = new Color(0.18f, 1f, 1f, 0.95f);
        [SerializeField] private Color fillColor = new Color(0.05f, 0.95f, 1f, 0.9f);
        [SerializeField] private Color lowFillColor = new Color(1f, 0.2f, 0.1f, 0.95f);
        [SerializeField] private Color emptyColor = new Color(0.02f, 0.25f, 0.28f, 0.45f);
        [SerializeField] private Color lowEmptyColor = new Color(0.28f, 0.015f, 0.015f, 1f);
        [SerializeField] private Color backPlateColor = new Color(0f, 0.07f, 0.09f, 0.82f);
        [SerializeField] private Color lowBackPlateColor = new Color(0.09f, 0.004f, 0.006f, 1f);
        [SerializeField] private Color glowColor = new Color(0f, 0.85f, 1f, 0.28f);

        [Header("动效")]
        [SerializeField] private float glowPulseSpeed = 2.4f;
        [SerializeField] private float lowOxygenBlinkSpeed = 8f;

        private RectTransform _root;
        private Image _outerGlow;
        private Image _innerGlow;
        private Image _backPlate;
        private Image _frame;
        private Image[] _segmentEmpties;
        private Image[] _segmentFills;
        private Image[] _segmentCores;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (oxygen == null)
            {
                oxygen = GetComponent<PlayerOxygen>();
            }

            BuildUI();
        }

        private void LateUpdate()
        {
            if (oxygen == null || _root == null) return;

            bool visible = !showOnlyWhenOxygenActive || oxygen.OxygenActive;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            if (!visible) return;

            float normalized = oxygen.Normalized;
            UpdateSegments(normalized);
            UpdateGlow(normalized);
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("OxygenBarCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGroup = canvasObject.GetComponent<CanvasGroup>();

            GameObject rootObject = new GameObject("OxygenBar", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            _root = rootObject.GetComponent<RectTransform>();
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = anchoredPosition;
            _root.sizeDelta = size;

            _outerGlow = AddRoundedImage("OuterGlow", _root, new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * 0.6f), new Vector2(-14f, 14f), size + new Vector2(28f, 28f), cornerRadius + 14f, 0f);
            _innerGlow = AddRoundedImage("InnerGlow", _root, glowColor, new Vector2(-6f, 6f), size + new Vector2(12f, 12f), cornerRadius + 6f, 0f);
            _backPlate = AddRoundedImage("BackPlate", _root, backPlateColor, Vector2.zero, size, cornerRadius, 0f);

            BuildFrame();
            BuildSegments();
        }

        private void BuildFrame()
        {
            _frame = AddRoundedImage("Frame", _root, frameColor, Vector2.zero, size, cornerRadius, frameThickness);
        }

        private void BuildSegments()
        {
            int count = Mathf.Max(1, segmentCount);
            _segmentEmpties = new Image[count];
            _segmentFills = new Image[count];
            _segmentCores = new Image[count];

            float innerX = frameThickness + 6f;
            float innerTop = frameThickness + 8f;
            float innerWidth = size.x - innerX * 2f;
            float usableHeight = size.y - innerTop * 2f;
            float segmentHeight = (usableHeight - segmentGap * (count - 1)) / count;

            for (int i = 0; i < count; i++)
            {
                float y = -innerTop - i * (segmentHeight + segmentGap);
                RectTransform slot = CreateRect($"Segment_{i}", _root, new Vector2(innerX, y), new Vector2(innerWidth, segmentHeight), new Vector2(0f, 1f), new Vector2(0f, 1f));

                _segmentEmpties[i] = AddRoundedImage("Empty", slot, emptyColor, Vector2.zero, new Vector2(innerWidth, segmentHeight), segmentCornerRadius, 0f);
                _segmentFills[i] = AddRoundedImage("Fill", slot, fillColor, Vector2.zero, new Vector2(innerWidth, segmentHeight), segmentCornerRadius, 0f);
                _segmentCores[i] = AddRoundedImage("Core", slot, new Color(1f, 1f, 1f, 0.14f), new Vector2(innerWidth * 0.12f, -segmentHeight * 0.18f), new Vector2(innerWidth * 0.76f, segmentHeight * 0.18f), segmentCornerRadius * 0.5f, 0f);
            }
        }

        private void UpdateSegments(float normalized)
        {
            float scaled = Mathf.Clamp01(normalized) * _segmentFills.Length;
            bool lowOxygen = normalized <= 0.2f;
            Color activeColor = normalized <= 0.2f
                ? Color.Lerp(lowFillColor, Color.clear, Mathf.PingPong(Time.unscaledTime * lowOxygenBlinkSpeed, 0.45f))
                : Color.Lerp(fillColor, Color.white, 0.08f);
            Color currentEmptyColor = lowOxygen ? ForceOpaqueLowColor(lowEmptyColor) : emptyColor;
            if (_backPlate != null)
            {
                _backPlate.color = lowOxygen ? ForceOpaqueLowColor(lowBackPlateColor) : backPlateColor;
            }
            if (_frame != null)
            {
                _frame.color = lowOxygen
                    ? new Color(lowFillColor.r, lowFillColor.g, lowFillColor.b, frameColor.a)
                    : frameColor;
            }

            for (int i = 0; i < _segmentFills.Length; i++)
            {
                int bottomIndex = _segmentFills.Length - 1 - i;
                float amount = Mathf.Clamp01(scaled - bottomIndex);
                if (_segmentEmpties != null && _segmentEmpties[i] != null)
                {
                    _segmentEmpties[i].color = currentEmptyColor;
                }
                _segmentFills[i].color = new Color(activeColor.r, activeColor.g, activeColor.b, activeColor.a * amount);
                _segmentCores[i].color = lowOxygen
                    ? new Color(lowFillColor.r, lowFillColor.g, lowFillColor.b, 0.18f * amount)
                    : new Color(1f, 1f, 1f, 0.14f * amount);
            }
        }

        private void UpdateGlow(float normalized)
        {
            float pulse = 0.75f + Mathf.Sin(Time.unscaledTime * glowPulseSpeed) * 0.25f;
            float lowBoost = normalized <= 0.2f ? 1.6f : 1f;
            Color color = normalized <= 0.2f ? lowFillColor : glowColor;

            _outerGlow.color = new Color(color.r, color.g, color.b, glowColor.a * 0.45f * pulse * lowBoost);
            _innerGlow.color = new Color(color.r, color.g, color.b, glowColor.a * pulse * lowBoost);
        }

        private static Color ForceOpaqueLowColor(Color color)
        {
            float red = Mathf.Max(color.r, 0.09f);
            float green = Mathf.Min(color.g, 0.025f);
            float blue = Mathf.Min(color.b, 0.025f);
            return new Color(red, green, blue, 1f);
        }

        private Image AddRoundedImage(string name, Transform parent, Color color, Vector2 anchoredPosition, Vector2 imageSize, float radius, float borderThickness)
        {
            RectTransform rect = CreateRect(name, parent, anchoredPosition, imageSize, new Vector2(0f, 1f), new Vector2(0f, 1f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = CreateRoundedSprite(imageSize, radius, borderThickness);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchoredPosition, Vector2 rectSize, Vector2 anchor, Vector2 pivot)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = rectSize;
            return rect;
        }

        private static Sprite CreateRoundedSprite(Vector2 imageSize, float radius, float borderThickness)
        {
            int width = Mathf.Max(2, Mathf.CeilToInt(imageSize.x));
            int height = Mathf.Max(2, Mathf.CeilToInt(imageSize.y));
            float clampedRadius = Mathf.Clamp(radius, 0f, Mathf.Min(width, height) * 0.5f);
            float clampedBorder = Mathf.Max(0f, borderThickness);

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = borderThickness > 0f ? "GeneratedRoundedOutline" : "GeneratedRoundedFill";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = IsInsideRoundedRect(x + 0.5f, y + 0.5f, width, height, clampedRadius) ? 1f : 0f;

                    if (alpha > 0f && clampedBorder > 0f)
                    {
                        bool insideInner = IsInsideRoundedRect(
                            x + 0.5f - clampedBorder,
                            y + 0.5f - clampedBorder,
                            width - clampedBorder * 2f,
                            height - clampedBorder * 2f,
                            Mathf.Max(0f, clampedRadius - clampedBorder)
                        );
                        alpha = insideInner ? 0f : 1f;
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static bool IsInsideRoundedRect(float x, float y, float width, float height, float radius)
        {
            if (width <= 0f || height <= 0f) return false;
            if (radius <= 0f) return x >= 0f && x <= width && y >= 0f && y <= height;

            float left = radius;
            float right = width - radius;
            float bottom = radius;
            float top = height - radius;

            float nearestX = Mathf.Clamp(x, left, right);
            float nearestY = Mathf.Clamp(y, bottom, top);
            float dx = x - nearestX;
            float dy = y - nearestY;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
