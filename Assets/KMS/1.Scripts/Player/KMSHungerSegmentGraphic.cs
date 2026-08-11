using UnityEngine;
using UnityEngine.UI;

namespace KMS
{
    /// <summary>
    /// Draws a hunger overlay segment with independently rounded outer ends.
    /// Internal food-segment boundaries remain square and meet without gaps.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSHungerSegmentGraphic : MaskableGraphic
    {
        private const int ArcSegments = 12;

        [SerializeField] private bool roundLeft;
        [SerializeField] private bool roundRight;

        public bool RoundLeft => roundLeft;
        public bool RoundRight => roundRight;

        public void SetRoundedEnds(bool left, bool right)
        {
            if (roundLeft == left && roundRight == right) return;

            roundLeft = left;
            roundRight = right;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect drawRect = GetPixelAdjustedRect();
            if (drawRect.width <= 0f || drawRect.height <= 0f) return;

            float radiusY = drawRect.height * 0.5f;
            float radiusX = roundLeft && roundRight
                ? Mathf.Min(radiusY, drawRect.width * 0.5f)
                : Mathf.Min(radiusY, drawRect.width);
            Vector2 arcRadii = new Vector2(radiusX, radiusY);
            Color32 vertexColor = color;

            vertexHelper.AddVert(drawRect.center, vertexColor, Vector2.zero);

            if (roundLeft)
            {
                AddArc(
                    vertexHelper,
                    new Vector2(drawRect.xMin + radiusX, drawRect.center.y),
                    arcRadii,
                    90f,
                    270f,
                    vertexColor);
            }
            else
            {
                AddVertex(vertexHelper, new Vector2(drawRect.xMin, drawRect.yMax), vertexColor);
                AddVertex(vertexHelper, new Vector2(drawRect.xMin, drawRect.yMin), vertexColor);
            }

            if (roundRight)
            {
                AddArc(
                    vertexHelper,
                    new Vector2(drawRect.xMax - radiusX, drawRect.center.y),
                    arcRadii,
                    -90f,
                    90f,
                    vertexColor);
            }
            else
            {
                AddVertex(vertexHelper, new Vector2(drawRect.xMax, drawRect.yMin), vertexColor);
                AddVertex(vertexHelper, new Vector2(drawRect.xMax, drawRect.yMax), vertexColor);
            }

            int perimeterCount = vertexHelper.currentVertCount - 1;
            for (int i = 0; i < perimeterCount; i++)
            {
                int current = i + 1;
                int next = ((i + 1) % perimeterCount) + 1;
                vertexHelper.AddTriangle(0, current, next);
            }
        }

        private static void AddArc(
            VertexHelper vertexHelper,
            Vector2 center,
            Vector2 radii,
            float startDegrees,
            float endDegrees,
            Color32 vertexColor)
        {
            for (int i = 0; i <= ArcSegments; i++)
            {
                float angle = Mathf.Lerp(startDegrees, endDegrees, i / (float)ArcSegments)
                              * Mathf.Deg2Rad;
                AddVertex(
                    vertexHelper,
                    center + new Vector2(
                        Mathf.Cos(angle) * radii.x,
                        Mathf.Sin(angle) * radii.y),
                    vertexColor);
            }
        }

        private static void AddVertex(
            VertexHelper vertexHelper,
            Vector2 position,
            Color32 vertexColor)
        {
            vertexHelper.AddVert(position, vertexColor, Vector2.zero);
        }
    }
}
