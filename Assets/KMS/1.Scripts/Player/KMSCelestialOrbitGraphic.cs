using UnityEngine;
using UnityEngine.UI;

namespace KMS
{
    /// <summary>
    /// A deliberately understated orbit halo. Visibility is controlled by the
    /// viewport so the halo remains symmetrical while the celestial bodies orbit.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KMSCelestialOrbitGraphic : MaskableGraphic
    {
        [SerializeField, Min(1f)] private float radius = 32f;
        [SerializeField, Range(16, 128)] private int segments = 96;
        [SerializeField, Min(0.1f)] private float glowWidth = 5.5f;
        [Header("Day / Night Arc")]
        [SerializeField] private Color dayColor = new Color(0.34f, 0.78f, 1f, 0.42f);
        [SerializeField] private Color nightColor = new Color(0.12f, 0.2f, 0.52f, 0.46f);
        [SerializeField, Range(-180f, 180f)] private float dayCenterAngle = 25f;
        // Previous split-band setting:
        // [SerializeField, Range(0f, 90f)] private float transitionDegrees = 24f;

        private float orbitAngleDegrees;
        private float wholeBandDayWeight = 0.5f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            int safeSegments = Mathf.Max(16, segments);
            float halfWidth = glowWidth * 0.5f;
            float[] radii =
            {
                Mathf.Max(0f, radius - glowWidth),
                Mathf.Max(0f, radius - halfWidth),
                radius + halfWidth,
                radius + glowWidth
            };
            float[] bandAlpha = { 0f, 1f, 1f, 0f };

            for (int i = 0; i <= safeSegments; i++)
            {
                float normalized = i / (float)safeSegments;
                float angle = normalized * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Color arcColor = EvaluateArcColor(angle * Mathf.Rad2Deg);

                for (int band = 0; band < radii.Length; band++)
                {
                    Color vertexColor = arcColor;
                    vertexColor.a *= bandAlpha[band];
                    vertexHelper.AddVert(center + direction * radii[band], vertexColor, Vector2.zero);
                }
            }

            int bandCount = radii.Length;
            for (int segment = 0; segment < safeSegments; segment++)
            {
                int row = segment * bandCount;
                int nextRow = (segment + 1) * bandCount;

                for (int band = 0; band < bandCount - 1; band++)
                {
                    int a = row + band;
                    int b = row + band + 1;
                    int c = nextRow + band + 1;
                    int d = nextRow + band;
                    vertexHelper.AddTriangle(a, b, c);
                    vertexHelper.AddTriangle(a, c, d);
                }
            }
        }

        public void SetDayCenterAngle(float angleDegrees)
        {
            dayCenterAngle = angleDegrees;
            RefreshWholeBandColor();
        }

        public void SetOrbitAngle(float angleDegrees)
        {
            if (Mathf.Approximately(orbitAngleDegrees, angleDegrees)) return;

            orbitAngleDegrees = angleDegrees;
            RefreshWholeBandColor();
        }

        private Color EvaluateArcColor(float angleDegrees)
        {
            // Previous presentation: the ring was split into a sky-blue hemisphere
            // centered on the sun and a navy hemisphere centered on the moon.
            // Kept here for quick comparison if the split-band design is restored.
            /*
            float deltaRadians = Mathf.DeltaAngle(dayCenterAngle, angleDegrees) * Mathf.Deg2Rad;
            float dayHemisphere = Mathf.Cos(deltaRadians);

            if (transitionDegrees <= 0.001f)
                return dayHemisphere >= 0f ? dayColor : nightColor;

            float halfTransitionRadians = transitionDegrees * 0.5f * Mathf.Deg2Rad;
            float threshold = Mathf.Max(0.0001f, Mathf.Sin(halfTransitionRadians));
            float dayWeight = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(-threshold, threshold, dayHemisphere));
            return Color.Lerp(nightColor, dayColor, dayWeight);
            */

            // Current presentation: the entire ring shares one color. It reaches
            // full day color when the sun is at the top and full night color when
            // the moon (the opposite body) is at the top.
            return Color.Lerp(nightColor, dayColor, wholeBandDayWeight);
        }

        private void RefreshWholeBandColor()
        {
            float sunWorldAngle = (dayCenterAngle + orbitAngleDegrees) * Mathf.Deg2Rad;
            float nextDayWeight = (Mathf.Sin(sunWorldAngle) + 1f) * 0.5f;

            if (Mathf.Abs(wholeBandDayWeight - nextDayWeight) < 0.001f) return;

            wholeBandDayWeight = nextDayWeight;
            SetVerticesDirty();
        }
    }
}
