using UnityEngine;

namespace KMS
{
    [DisallowMultipleComponent]
    public class CapsuleTrajectoryPreview : MonoBehaviour
    {
        [Header("Prediction")]
        [SerializeField] private LayerMask collisionLayers = ~0;
        [SerializeField, Min(0.5f)] private float predictionDuration = 2f;
        [SerializeField, Range(0.02f, 0.2f)] private float timeStep = 0.05f;
        [SerializeField, Min(0.01f)] private float castRadius = 0.12f;

        [Header("Visual")]
        [SerializeField, Min(0.005f)] private float lineWidth = 0.035f;
        [SerializeField, Min(0.05f)] private float markerRadius = 0.2f;
        [SerializeField] private Color lineColor = new Color(0.2f, 0.9f, 1f, 0.9f);
        [SerializeField] private Color markerColor = new Color(0.3f, 1f, 0.55f, 1f);

        private const int MaxPoints = 128;
        private const int MarkerSegments = 32;
        private readonly Vector3[] trajectoryPoints = new Vector3[MaxPoints];
        private readonly Vector3[] markerPoints = new Vector3[MarkerSegments];
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        private LineRenderer trajectoryLine;
        private LineRenderer landingMarker;
        private Material runtimeMaterial;

        private void Awake()
        {
            EnsureVisuals();
            Hide();
        }

        public void Show(Vector3 origin, Vector3 initialVelocity)
        {
            EnsureVisuals();

            int pointCount = 1;
            trajectoryPoints[0] = origin;
            Vector3 previous = origin;
            bool foundLanding = false;
            RaycastHit landingHit = default;
            int steps = Mathf.Min(MaxPoints - 1, Mathf.CeilToInt(predictionDuration / timeStep));

            for (int i = 1; i <= steps; i++)
            {
                float time = i * timeStep;
                Vector3 next = origin + initialVelocity * time + 0.5f * Physics.gravity * time * time;
                Vector3 segment = next - previous;
                float distance = segment.magnitude;

                if (distance > 0.0001f && TryFindFirstExternalHit(previous, segment / distance, distance, out landingHit))
                {
                    next = landingHit.point;
                    foundLanding = true;
                }

                trajectoryPoints[pointCount++] = next;
                previous = next;
                if (foundLanding) break;
            }

            trajectoryLine.positionCount = pointCount;
            for (int i = 0; i < pointCount; i++)
            {
                trajectoryLine.SetPosition(i, trajectoryPoints[i]);
            }
            trajectoryLine.enabled = true;

            if (foundLanding) ShowLandingMarker(landingHit.point, landingHit.normal);
            else landingMarker.enabled = false;
        }

        public void Hide()
        {
            if (trajectoryLine != null) trajectoryLine.enabled = false;
            if (landingMarker != null) landingMarker.enabled = false;
        }

        private bool TryFindFirstExternalHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit closestHit)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                castRadius,
                direction,
                hitBuffer,
                distance,
                collisionLayers,
                QueryTriggerInteraction.Ignore);

            closestHit = default;
            float closestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = hitBuffer[i].collider;
                if (hitCollider == null || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform)) continue;
                if (hitBuffer[i].distance >= closestDistance) continue;

                closestDistance = hitBuffer[i].distance;
                closestHit = hitBuffer[i];
            }

            return closestDistance < float.PositiveInfinity;
        }

        private void ShowLandingMarker(Vector3 point, Vector3 normal)
        {
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            Vector3 center = point + normal * 0.025f;

            for (int i = 0; i < MarkerSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / MarkerSegments;
                markerPoints[i] = center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * markerRadius;
            }

            landingMarker.positionCount = MarkerSegments;
            landingMarker.SetPositions(markerPoints);
            landingMarker.enabled = true;
        }

        private void EnsureVisuals()
        {
            if (trajectoryLine != null && landingMarker != null) return;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            runtimeMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };

            trajectoryLine = CreateLineRenderer("CapsuleTrajectoryLine", false, lineWidth, lineColor);
            landingMarker = CreateLineRenderer("CapsuleLandingMarker", true, lineWidth * 1.5f, markerColor);
        }

        private LineRenderer CreateLineRenderer(string objectName, bool loop, float width, Color color)
        {
            GameObject visualObject = new GameObject(objectName);
            visualObject.transform.SetParent(transform, false);
            LineRenderer line = visualObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.material = runtimeMaterial;
            line.startColor = color;
            line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
