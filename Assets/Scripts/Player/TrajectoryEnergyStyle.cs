using UnityEngine;

namespace AnchorGame
{
    public enum TrajectoryLinePattern
    {
        Solid,
        Dashed
    }

    [CreateAssetMenu(fileName = "TrajectoryEnergyStyle", menuName = "Anchor Game/Visual Styles/Trajectory Energy Style")]
    public class TrajectoryEnergyStyle : ScriptableObject
    {
        [Header("能量束")]
        public Color coreColor = new Color(0.55f, 1f, 1f, 0.82f);
        public Color glowColor = new Color(0f, 0.75f, 1f, 0.26f);
        public Color pulseColor = new Color(0.95f, 1f, 1f, 0.95f);
        public float coreWidth = 0.05f;
        public float glowWidth = 0.22f;
        public float endAlpha = 0.03f;
        public float endWidthMultiplier = 0.2f;
        public float beamPulseSpeed = 2.4f;
        [Range(0f, 1f)] public float beamPulseAmount = 0.12f;

        [Header("线型")]
        public TrajectoryLinePattern linePattern = TrajectoryLinePattern.Solid;
        public float dashWorldPitch = 1.15f;
        [Range(0.05f, 0.95f)] public float dashFillRatio = 0.52f;

        [Header("流动脉冲")]
        public int pulseCount = 4;
        public float pulseLength = 2.4f;
        public float pulseTravelSpeed = 9f;
        public float pulseWidthMultiplier = 1.8f;
        public float pulseHeadFade = 0.12f;
        public float pulseTailFade = 0.65f;
    }
}
