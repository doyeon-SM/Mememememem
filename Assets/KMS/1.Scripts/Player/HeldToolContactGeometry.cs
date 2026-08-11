using UnityEngine;

namespace KMS
{
    [DisallowMultipleComponent]
    public sealed class HeldToolContactGeometry : MonoBehaviour
    {
        [SerializeField] private Vector3 shaftDirectionLocal = Vector3.up;
        [SerializeField] private Vector3 bladeContactPointLocal = Vector3.up;
        [SerializeField] private Vector3 bladeNormalLocal = Vector3.forward;

        public Vector3 ShaftDirectionLocal => shaftDirectionLocal.sqrMagnitude > 0.0001f
            ? shaftDirectionLocal.normalized
            : Vector3.up;

        public Vector3 BladeContactDirectionLocal => bladeContactPointLocal.sqrMagnitude > 0.0001f
            ? bladeContactPointLocal.normalized
            : ShaftDirectionLocal;

        public Vector3 BladeNormalLocal => bladeNormalLocal.sqrMagnitude > 0.0001f
            ? bladeNormalLocal.normalized
            : Vector3.forward;

        public Vector3 BladeContactPointLocal => bladeContactPointLocal;

        public void SetGeometry(
            Vector3 shaftDirection,
            Vector3 bladeContactPoint,
            Vector3 bladeNormal)
        {
            shaftDirectionLocal = shaftDirection.normalized;
            bladeContactPointLocal = bladeContactPoint;
            bladeNormalLocal = bladeNormal.normalized;
        }
    }
}
