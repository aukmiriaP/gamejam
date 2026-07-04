using UnityEngine;

namespace AnchorGame
{
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryPredictor : MonoBehaviour
    {
        private const int LineTextureWidth = 64;

        [Header("绑定")]
        [SerializeField] private PlayerShip player;

        [Header("预测")]
        [SerializeField] private float predictionDistance = 12f;
        [SerializeField] private int sortingOrder = 25;

        [Header("脉冲能量束")]
        [SerializeField] private TrajectoryEnergyStyle visualStyle;
        [SerializeField] private Color coreColor = new Color(0.55f, 1f, 1f, 0.82f);
        [SerializeField] private Color glowColor = new Color(0f, 0.75f, 1f, 0.26f);
        [SerializeField] private Color pulseColor = new Color(0.95f, 1f, 1f, 0.95f);
        [SerializeField] private float coreWidth = 0.05f;
        [SerializeField] private float glowWidth = 0.22f;
        [SerializeField] private float endAlpha = 0.03f;
        [SerializeField] private float endWidthMultiplier = 0.2f;
        [SerializeField] private float beamPulseSpeed = 2.4f;
        [SerializeField, Range(0f, 1f)] private float beamPulseAmount = 0.12f;
        [SerializeField] private TrajectoryLinePattern linePattern = TrajectoryLinePattern.Solid;
        [SerializeField] private float dashWorldPitch = 1.15f;
        [SerializeField, Range(0.05f, 0.95f)] private float dashFillRatio = 0.52f;

        [Header("流动脉冲")]
        [SerializeField] private int pulseCount = 4;
        [SerializeField] private float pulseLength = 2.4f;
        [SerializeField] private float pulseTravelSpeed = 9f;
        [SerializeField] private float pulseWidthMultiplier = 1.8f;
        [SerializeField] private float pulseHeadFade = 0.12f;
        [SerializeField] private float pulseTailFade = 0.65f;

        private LineRenderer coreLine;
        private LineRenderer glowLine;
        private LineRenderer[] pulseLines;
        private Material coreMaterial;
        private Material glowMaterial;
        private Material pulseMaterial;
        private Texture2D solidTexture;
        private Texture2D dashTexture;
        private TrajectoryLinePattern lastLinePattern;
        private float lastDashFillRatio = -1f;

        private Color CoreColor => visualStyle != null ? visualStyle.coreColor : coreColor;
        private Color GlowColor => visualStyle != null ? visualStyle.glowColor : glowColor;
        private Color PulseColor => visualStyle != null ? visualStyle.pulseColor : pulseColor;
        private float CoreWidth => visualStyle != null ? visualStyle.coreWidth : coreWidth;
        private float GlowWidth => visualStyle != null ? visualStyle.glowWidth : glowWidth;
        private float EndAlpha => visualStyle != null ? visualStyle.endAlpha : endAlpha;
        private float EndWidthMultiplier => visualStyle != null ? visualStyle.endWidthMultiplier : endWidthMultiplier;
        private float BeamPulseSpeed => visualStyle != null ? visualStyle.beamPulseSpeed : beamPulseSpeed;
        private float BeamPulseAmount => visualStyle != null ? visualStyle.beamPulseAmount : beamPulseAmount;
        private TrajectoryLinePattern Pattern => visualStyle != null ? visualStyle.linePattern : linePattern;
        private float DashWorldPitch => visualStyle != null ? visualStyle.dashWorldPitch : dashWorldPitch;
        private float DashFillRatio => visualStyle != null ? visualStyle.dashFillRatio : dashFillRatio;
        private int PulseCount => visualStyle != null ? visualStyle.pulseCount : pulseCount;
        private float PulseLength => visualStyle != null ? visualStyle.pulseLength : pulseLength;
        private float PulseTravelSpeed => visualStyle != null ? visualStyle.pulseTravelSpeed : pulseTravelSpeed;
        private float PulseWidthMultiplier => visualStyle != null ? visualStyle.pulseWidthMultiplier : pulseWidthMultiplier;
        private float PulseHeadFade => visualStyle != null ? visualStyle.pulseHeadFade : pulseHeadFade;
        private float PulseTailFade => visualStyle != null ? visualStyle.pulseTailFade : pulseTailFade;

        private void Awake()
        {
            solidTexture = CreateSolidTexture();
            RebuildDashTextureIfNeeded(true);
            coreLine = GetComponent<LineRenderer>();
            ConfigureCoreLine();
            ConfigureGlowLine();
            ConfigurePulseLines();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && TryGetComponent(out LineRenderer previewLine))
            {
                previewLine.sortingOrder = sortingOrder + 1;
                previewLine.textureMode = Pattern == TrajectoryLinePattern.Dashed ? LineTextureMode.Tile : LineTextureMode.Stretch;
                previewLine.widthMultiplier = Mathf.Max(0.005f, CoreWidth);
                previewLine.widthCurve = CreateWidthCurve();
                previewLine.colorGradient = CreateFadeGradient(CoreColor, EndAlpha);
            }
        }

        private void Update()
        {
            if (player == null || player.CurrentState != PlayerShip.ShipState.Orbiting)
            {
                SetVisible(false);
                return;
            }

            Vector2 velocity = player.Velocity;
            if (velocity.sqrMagnitude < 0.0001f)
            {
                SetVisible(false);
                return;
            }

            Vector3 origin = player.transform.position;
            Vector3 direction = velocity.normalized;
            Vector3 end = origin + direction * predictionDistance;

            SetVisible(true);
            UpdateBeam(origin, end);
            UpdatePulses(origin, direction);
        }

        private void ConfigureCoreLine()
        {
            coreLine.positionCount = 2;
            coreLine.textureMode = LineTextureMode.Stretch;
            coreLine.alignment = LineAlignment.View;
            coreLine.numCapVertices = 6;
            coreLine.numCornerVertices = 2;
            coreLine.sortingOrder = sortingOrder + 1;

            coreMaterial = CreateLineMaterial("TrajectoryEnergyCoreMaterial");
            if (coreMaterial != null)
            {
                coreLine.material = coreMaterial;
            }
        }

        private void ConfigureGlowLine()
        {
            GameObject glowObject = new GameObject("TrajectoryEnergyGlow");
            glowObject.transform.SetParent(transform, false);

            glowLine = glowObject.AddComponent<LineRenderer>();
            glowLine.positionCount = 2;
            glowLine.textureMode = LineTextureMode.Stretch;
            glowLine.alignment = LineAlignment.View;
            glowLine.numCapVertices = 8;
            glowLine.numCornerVertices = 2;
            glowLine.sortingOrder = sortingOrder;

            glowMaterial = CreateLineMaterial("TrajectoryEnergyGlowMaterial");
            if (glowMaterial != null)
            {
                glowLine.material = glowMaterial;
            }
        }

        private void ConfigurePulseLines()
        {
            int count = Mathf.Max(0, PulseCount);
            pulseLines = new LineRenderer[count];
            if (pulseMaterial == null)
            {
                pulseMaterial = CreateLineMaterial("TrajectoryEnergyPulseMaterial");
            }

            for (int i = 0; i < count; i++)
            {
                GameObject pulseObject = new GameObject($"TrajectoryEnergyPulse_{i}");
                pulseObject.transform.SetParent(transform, false);

                LineRenderer pulseLine = pulseObject.AddComponent<LineRenderer>();
                pulseLine.positionCount = 2;
                pulseLine.textureMode = LineTextureMode.Stretch;
                pulseLine.alignment = LineAlignment.View;
                pulseLine.numCapVertices = 8;
                pulseLine.numCornerVertices = 2;
                pulseLine.sortingOrder = sortingOrder + 2;
                if (pulseMaterial != null)
                {
                    pulseLine.material = pulseMaterial;
                }

                pulseLines[i] = pulseLine;
            }
        }

        private void UpdateBeam(Vector3 origin, Vector3 end)
        {
            float pulse = 1f + Mathf.Sin(Time.time * BeamPulseSpeed) * BeamPulseAmount;
            ApplyLinePattern();

            coreLine.SetPosition(0, origin);
            coreLine.SetPosition(1, end);
            coreLine.widthMultiplier = Mathf.Max(0.005f, CoreWidth * pulse);
            coreLine.widthCurve = CreateWidthCurve();
            coreLine.colorGradient = CreateFadeGradient(CoreColor, EndAlpha);

            glowLine.SetPosition(0, origin);
            glowLine.SetPosition(1, end);
            glowLine.widthMultiplier = Mathf.Max(CoreWidth, GlowWidth * pulse);
            glowLine.widthCurve = CreateWidthCurve();
            glowLine.colorGradient = CreateFadeGradient(GlowColor, 0f);
        }

        private void ApplyLinePattern()
        {
            RebuildDashTextureIfNeeded(false);

            bool dashed = Pattern == TrajectoryLinePattern.Dashed;
            Texture2D texture = dashed ? dashTexture : solidTexture;
            LineTextureMode textureMode = dashed ? LineTextureMode.Tile : LineTextureMode.Stretch;
            Vector2 textureScale = dashed
                ? new Vector2(predictionDistance / Mathf.Max(0.05f, DashWorldPitch), 1f)
                : Vector2.one;

            ApplyTexture(coreLine, coreMaterial, texture, textureMode, textureScale);
            ApplyTexture(glowLine, glowMaterial, texture, textureMode, textureScale);

            lastLinePattern = Pattern;
        }

        private static void ApplyTexture(LineRenderer renderer, Material material, Texture2D texture, LineTextureMode textureMode, Vector2 textureScale)
        {
            if (renderer != null)
            {
                renderer.textureMode = textureMode;
            }
            if (material != null)
            {
                material.mainTexture = texture;
                material.mainTextureScale = textureScale;
            }
        }

        private void UpdatePulses(Vector3 origin, Vector3 direction)
        {
            EnsurePulseLineCount();
            if (pulseLines == null || pulseLines.Length == 0) return;

            float distance = Mathf.Max(0.1f, predictionDistance);
            float safePulseLength = Mathf.Clamp(PulseLength, 0.05f, distance);
            float spacing = distance / pulseLines.Length;
            float travel = Mathf.Repeat(Time.time * Mathf.Max(0f, PulseTravelSpeed), spacing);

            for (int i = 0; i < pulseLines.Length; i++)
            {
                LineRenderer pulseLine = pulseLines[i];
                if (pulseLine == null) continue;

                float headDistance = Mathf.Repeat(i * spacing + travel, distance + safePulseLength);
                float tailDistance = headDistance - safePulseLength;
                if (tailDistance >= distance || headDistance <= 0f)
                {
                    pulseLine.enabled = false;
                    continue;
                }

                float visibleTail = Mathf.Clamp(tailDistance, 0f, distance);
                float visibleHead = Mathf.Clamp(headDistance, 0f, distance);
                if (visibleHead <= visibleTail)
                {
                    pulseLine.enabled = false;
                    continue;
                }

                float distanceAlpha = Mathf.Clamp01(1f - visibleHead / distance);
                Color startColor = WithAlpha(PulseColor, PulseColor.a * PulseTailFade * distanceAlpha);
                Color endColor = WithAlpha(PulseColor, PulseColor.a * PulseHeadFade + PulseColor.a * (1f - PulseHeadFade) * distanceAlpha);

                pulseLine.enabled = true;
                pulseLine.SetPosition(0, origin + direction * visibleTail);
                pulseLine.SetPosition(1, origin + direction * visibleHead);
                pulseLine.widthMultiplier = Mathf.Max(0.005f, CoreWidth * PulseWidthMultiplier);
                pulseLine.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0.25f),
                    new Keyframe(0.7f, 1f),
                    new Keyframe(1f, 0f)
                );
                pulseLine.colorGradient = CreatePulseGradient(startColor, endColor);
            }
        }

        private void EnsurePulseLineCount()
        {
            int desiredCount = Mathf.Max(0, PulseCount);
            if (pulseLines != null && pulseLines.Length == desiredCount)
            {
                return;
            }

            DestroyPulseLineObjects();
            ConfigurePulseLines();
        }

        private void DestroyPulseLineObjects()
        {
            if (pulseLines == null) return;

            for (int i = 0; i < pulseLines.Length; i++)
            {
                if (pulseLines[i] != null)
                {
                    DestroyGeneratedObject(pulseLines[i].gameObject);
                }
            }

            pulseLines = null;
        }

        private void SetVisible(bool visible)
        {
            if (coreLine != null) coreLine.enabled = visible;
            if (glowLine != null) glowLine.enabled = visible;

            if (pulseLines == null) return;
            for (int i = 0; i < pulseLines.Length; i++)
            {
                if (pulseLines[i] != null)
                {
                    pulseLines[i].enabled = visible;
                }
            }
        }

        private AnimationCurve CreateWidthCurve()
        {
            float endWidth = Mathf.Clamp01(EndWidthMultiplier);
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, endWidth)
            );
        }

        private Gradient CreateFadeGradient(Color startColor, float farAlpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(startColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(startColor.a, 0f),
                    new GradientAlphaKey(Mathf.Clamp01(startColor.a * farAlpha), 1f)
                }
            );
            return gradient;
        }

        private Gradient CreatePulseGradient(Color startColor, Color endColor)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(startColor.a, 0f),
                    new GradientAlphaKey(endColor.a, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static Material CreateLineMaterial(string materialName)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }
            if (shader == null) return null;

            return new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void RebuildDashTextureIfNeeded(bool force)
        {
            float fillRatio = Mathf.Clamp(DashFillRatio, 0.05f, 0.95f);
            if (!force && Pattern == lastLinePattern && Mathf.Approximately(fillRatio, lastDashFillRatio))
            {
                return;
            }

            DestroyGeneratedObject(dashTexture);
            dashTexture = CreateDashTexture(fillRatio);
            lastDashFillRatio = fillRatio;
        }

        private static Texture2D CreateSolidTexture()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "TrajectorySolidTexture";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private static Texture2D CreateDashTexture(float fillRatio)
        {
            Texture2D texture = new Texture2D(LineTextureWidth, 1, TextureFormat.RGBA32, false);
            texture.name = "TrajectoryDashTexture";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            for (int x = 0; x < LineTextureWidth; x++)
            {
                float t = x / (LineTextureWidth - 1f);
                float edgeFade = 0.08f;
                float alpha = t <= fillRatio ? 1f : 0f;
                if (t <= fillRatio)
                {
                    alpha *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, edgeFade, t));
                    alpha *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fillRatio - edgeFade, fillRatio, t));
                }

                texture.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        private void OnDestroy()
        {
            DestroyPulseLineObjects();
            DestroyGeneratedObject(coreMaterial);
            DestroyGeneratedObject(glowMaterial);
            DestroyGeneratedObject(pulseMaterial);
            DestroyGeneratedObject(solidTexture);
            DestroyGeneratedObject(dashTexture);
        }

        private static void DestroyGeneratedObject(Object obj)
        {
            if (obj == null) return;

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
