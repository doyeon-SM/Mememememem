using UnityEngine;
using UnityEngine.UI;

namespace KMS
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KMSGameClockGraphic : MaskableGraphic
    {
        [Header("Clock Colors")]
        [SerializeField] private Color dayColor = new Color32(242, 187, 69, 255);
        [SerializeField] private Color nightColor = new Color32(45, 58, 105, 255);
        [SerializeField] private Color innerFaceColor = new Color32(16, 20, 30, 240);
        [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.88f);
        [SerializeField] private Color dayMarkerColor = new Color32(255, 221, 99, 255);
        [SerializeField] private Color nightMarkerColor = new Color32(139, 177, 255, 255);

        [Header("Clock Shape")]
        [SerializeField, Range(16, 96)] private int circleSegments = 48;
        [SerializeField, Min(0f)] private float borderWidth = 3f;
        [SerializeField, Range(0.2f, 0.9f)] private float innerFaceRatio = 0.7f;
        [SerializeField, Min(1f)] private float handWidth = 4f;
        [SerializeField, Range(0.5f, 1f)] private float markerRadiusRatio = 0.86f;
        [SerializeField, Min(1f)] private float markerRadius = 6f;
        [SerializeField, Min(0f)] private float markerBorderWidth = 1.5f;

        private float normalizedProgress;

        public bool IsDay => normalizedProgress < 0.5f;

        public void SetProgress(float progress)
        {
            float nextProgress = Mathf.Repeat(progress, 1f);
            if (Mathf.Approximately(normalizedProgress, nextProgress)) return;

            normalizedProgress = nextProgress;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            float radius = Mathf.Max(1f, Mathf.Min(rect.width, rect.height) * 0.5f);
            float faceRadius = Mathf.Max(1f, radius - borderWidth);

            AddDisc(vertexHelper, center, radius, borderColor, 0f, Mathf.PI * 2f, circleSegments);
            AddDisc(vertexHelper, center, faceRadius, dayColor, 0f, Mathf.PI, circleSegments / 2);
            AddDisc(vertexHelper, center, faceRadius, nightColor, Mathf.PI, Mathf.PI * 2f, circleSegments / 2);
            AddDisc(
                vertexHelper,
                center,
                faceRadius * innerFaceRatio,
                innerFaceColor,
                0f,
                Mathf.PI * 2f,
                circleSegments);

            float angle = normalizedProgress * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            Vector2 markerCenter = center + direction * (faceRadius * markerRadiusRatio);
            Color markerColor = IsDay ? dayMarkerColor : nightMarkerColor;

            AddLine(vertexHelper, center, markerCenter, handWidth, markerColor);
            AddDisc(
                vertexHelper,
                markerCenter,
                markerRadius + markerBorderWidth,
                borderColor,
                0f,
                Mathf.PI * 2f,
                Mathf.Max(16, circleSegments / 2));
            AddDisc(
                vertexHelper,
                markerCenter,
                markerRadius,
                markerColor,
                0f,
                Mathf.PI * 2f,
                Mathf.Max(16, circleSegments / 2));
        }

        private static void AddDisc(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            Color color,
            float startAngle,
            float endAngle,
            int segments)
        {
            segments = Mathf.Max(2, segments);
            int centerIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(center, color, Vector2.zero);

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(startAngle, endAngle, t);
                Vector2 point = center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
                vertexHelper.AddVert(point, color, Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
            {
                vertexHelper.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
            }
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float width,
            Color color)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 offset = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            int index = vertexHelper.currentVertCount;

            vertexHelper.AddVert(start - offset, color, Vector2.zero);
            vertexHelper.AddVert(start + offset, color, Vector2.zero);
            vertexHelper.AddVert(end + offset, color, Vector2.zero);
            vertexHelper.AddVert(end - offset, color, Vector2.zero);
            vertexHelper.AddTriangle(index, index + 1, index + 2);
            vertexHelper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
