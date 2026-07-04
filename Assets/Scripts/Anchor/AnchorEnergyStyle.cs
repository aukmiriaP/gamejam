using UnityEngine;

namespace AnchorGame
{
    [CreateAssetMenu(fileName = "AnchorEnergyStyle", menuName = "Anchor Game/Visual Styles/Anchor Energy Style")]
    public class AnchorEnergyStyle : ScriptableObject
    {
        [Header("锚链")]
        public bool useEnergyRopeStyle = true;
        public Color ropeCoreColor = new Color(0.35f, 1f, 1f, 0.95f);
        public Color ropeGlowColor = new Color(0f, 0.65f, 1f, 0.34f);
        public float ropeCoreWidth = 0.045f;
        public float ropeGlowWidth = 0.18f;
        public float ropePulseSpeed = 7f;
        [Range(0f, 1f)] public float ropePulseAmount = 0.18f;

        [Header("锚点")]
        public Color anchorHeadColor = new Color(0.55f, 1f, 1f, 1f);
        public Color anchorHeadGlowColor = new Color(0f, 0.75f, 1f, 0.38f);
        public float anchorHeadGlowScale = 1.75f;

        [Header("拖尾")]
        public Color anchorTrailColor = new Color(0f, 0.8f, 1f, 0.58f);
        public float anchorTrailTime = 0.22f;
        public float anchorTrailWidth = 0.14f;
    }
}
