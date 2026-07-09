using UnityEngine;

namespace KMS
{
    [DisallowMultipleComponent]
    public class LadderVolume : MonoBehaviour
    {
        [Header("Path")]
        [SerializeField] private float bottomLocalY = -0.5f;
        [SerializeField] private float topLocalY = 0.5f;
        [SerializeField] private float climbLocalX;
        [SerializeField] private float climbLocalZ = -0.35f;

        [Header("Exit")]
        [SerializeField] private Vector3 topExitLocalOffset = new Vector3(0f, 0.25f, -0.8f);
        [SerializeField] private Vector3 bottomExitLocalOffset = new Vector3(0f, 0f, -0.8f);

        public Vector3 Up => transform.up;
        public Vector3 Forward => transform.forward;

        public Vector3 GetClosestPointOnPath(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            local.x = climbLocalX;
            local.y = Mathf.Clamp(local.y, bottomLocalY, topLocalY);
            local.z = climbLocalZ;
            return transform.TransformPoint(local);
        }

        public float GetNormalizedHeight(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            return Mathf.InverseLerp(bottomLocalY, topLocalY, local.y);
        }

        public Vector3 GetTopExitPoint()
        {
            return transform.TransformPoint(new Vector3(climbLocalX, topLocalY, climbLocalZ) + topExitLocalOffset);
        }

        public Vector3 GetBottomExitPoint()
        {
            return transform.TransformPoint(new Vector3(climbLocalX, bottomLocalY, climbLocalZ) + bottomExitLocalOffset);
        }

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 bottom = transform.TransformPoint(new Vector3(climbLocalX, bottomLocalY, climbLocalZ));
            Vector3 top = transform.TransformPoint(new Vector3(climbLocalX, topLocalY, climbLocalZ));

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(bottom, top);
            Gizmos.DrawWireSphere(bottom, 0.15f);
            Gizmos.DrawWireSphere(top, 0.15f);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(GetTopExitPoint(), 0.2f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(GetBottomExitPoint(), 0.2f);
        }
    }
}
