using UnityEngine;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{

    /// <summary>
    /// Draws one continuous capsule mesh so translucent round ends never overlap
    /// the center and create darker seams.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSCapsuleGraphic : MaskableGraphic
    {

        private const int ArcSegments = 16;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {

            vertexHelper.Clear();

            Rect drawRect = GetPixelAdjustedRect();
            float radius = Mathf.Min(drawRect.height * 0.5f, drawRect.width * 0.5f);
            Vector2 center = drawRect.center;
            Color32 vertexColor = color;

            vertexHelper.AddVert(center, vertexColor, Vector2.zero);

            AddArc(vertexHelper, new Vector2(drawRect.xMax - radius, center.y), radius, -90f, 90f, vertexColor);
            AddArc(vertexHelper, new Vector2(drawRect.xMin + radius, center.y), radius, 90f, 270f, vertexColor);

            int perimeterCount = (ArcSegments + 1) * 2;
            for (int i = 0; i < perimeterCount; i++)
            {

                int current = i + 1;
                int next = ((i + 1) % perimeterCount) + 1;
                vertexHelper.AddTriangle(0, current, next);

            }

        }

        private static void AddArc(
            VertexHelper vertexHelper,
            Vector2 arcCenter,
            float radius,
            float startDegrees,
            float endDegrees,
            Color32 vertexColor)
        {

            for (int i = 0; i <= ArcSegments; i++)
            {

                float angle = Mathf.Lerp(startDegrees, endDegrees, i / (float)ArcSegments) * Mathf.Deg2Rad;
                Vector2 position = arcCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vertexHelper.AddVert(position, vertexColor, Vector2.zero);

            }

        }

    }

}
