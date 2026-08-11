using System;
using System.Reflection;
using KMS;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.EditorTools
{
    public static class KMSHungerSegmentGraphicValidation
    {
        private const float PositionTolerance = 0.02f;

        [MenuItem("KMS/Validate/Hunger Segment Geometry")]
        public static void Run()
        {
            Vector2 regularSize = new Vector2(100f, 30f);
            ValidateShape(false, false, true, true, true, true, false, regularSize, "square segment");
            ValidateShape(true, false, false, false, true, true, false, regularSize, "left outer segment");
            ValidateShape(false, true, true, true, false, false, false, regularSize, "right outer segment");
            ValidateShape(true, true, false, false, false, false, false, regularSize, "full rounded segment");
            ValidateShape(
                true,
                true,
                false,
                false,
                false,
                false,
                true,
                new Vector2(20f, 30f),
                "narrow full rounded segment");
            Debug.Log("[KMS Hunger HUD] Segment geometry validation passed.");
        }

        public static void RunFromCommandLine() => Run();

        private static void ValidateShape(
            bool roundLeft,
            bool roundRight,
            bool expectTopLeft,
            bool expectBottomLeft,
            bool expectTopRight,
            bool expectBottomRight,
            bool expectVerticalExtentAtCenter,
            Vector2 size,
            string description)
        {
            var testObject = new GameObject(
                $"KMSHungerSegmentGraphicValidation_{description}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(KMSHungerSegmentGraphic));
            var mesh = new Mesh();
            var vertexHelper = new VertexHelper();

            try
            {
                RectTransform rect = testObject.GetComponent<RectTransform>();
                rect.sizeDelta = size;

                KMSHungerSegmentGraphic graphic = testObject.GetComponent<KMSHungerSegmentGraphic>();
                graphic.SetRoundedEnds(roundLeft, roundRight);

                MethodInfo populateMesh = typeof(KMSHungerSegmentGraphic).GetMethod(
                    "OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(VertexHelper) },
                    null);
                Require(populateMesh != null, "KMS hunger segment mesh method is missing.");
                populateMesh.Invoke(graphic, new object[] { vertexHelper });
                vertexHelper.FillMesh(mesh);

                Vector3[] vertices = mesh.vertices;
                Vector2 halfSize = size * 0.5f;
                Require(vertices.Length >= 5, $"{description} generated too few vertices.");
                RequireCorner(vertices, new Vector2(-halfSize.x, halfSize.y), expectTopLeft, description);
                RequireCorner(vertices, new Vector2(-halfSize.x, -halfSize.y), expectBottomLeft, description);
                RequireCorner(vertices, new Vector2(halfSize.x, halfSize.y), expectTopRight, description);
                RequireCorner(vertices, new Vector2(halfSize.x, -halfSize.y), expectBottomRight, description);

                if (roundLeft)
                {
                    Require(Contains(vertices, new Vector2(-halfSize.x, 0f)),
                        $"{description} has no rounded leftmost vertex.");
                }
                if (roundRight)
                {
                    Require(Contains(vertices, new Vector2(halfSize.x, 0f)),
                        $"{description} has no rounded rightmost vertex.");
                }
                if (expectVerticalExtentAtCenter)
                {
                    Require(Contains(vertices, new Vector2(0f, halfSize.y)),
                        $"{description} does not reach the top of its rect.");
                    Require(Contains(vertices, new Vector2(0f, -halfSize.y)),
                        $"{description} does not reach the bottom of its rect.");
                }
            }
            finally
            {
                vertexHelper.Dispose();
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(testObject);
            }
        }

        private static void RequireCorner(
            Vector3[] vertices,
            Vector2 corner,
            bool expected,
            string description)
        {
            bool found = Contains(vertices, corner);
            Require(found == expected,
                $"{description} corner {corner} expected={expected}, found={found}.");
        }

        private static bool Contains(Vector3[] vertices, Vector2 position)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                if (Vector2.Distance(vertices[i], position) <= PositionTolerance) return true;
            }
            return false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
