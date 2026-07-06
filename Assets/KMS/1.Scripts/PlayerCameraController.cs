using UnityEngine;

namespace KMS
{
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInput input;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform cameraTransform;

        [Header("Orbit")]
        [SerializeField] private Vector3 targetOffset;
        [SerializeField] private float cameraDistance = 4f;
        [SerializeField] private float minDistance = 0.6f;
        [SerializeField] private float horizontalSensitivity = 0.12f;
        [SerializeField] private float verticalSensitivity = 0.12f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float positionSmoothTime = 0.04f;

        [Header("Collision")]
        [SerializeField] private bool avoidClipping = true;
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private float collisionBuffer = 0.1f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnStart = true;
        [SerializeField] private bool toggleCursorWithMenu = true;

        public float Yaw => yaw;
        public float Pitch => pitch;

        private float yaw;
        private float pitch = 20f;
        private Vector3 cameraVelocity;
        private bool cursorLocked;

        private void Reset()
        {
            input = GetComponent<PlayerInput>();
            followTarget = transform;
            cameraTransform = Camera.main != null ? Camera.main.transform : null;
        }

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (followTarget == null) followTarget = transform;
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            yaw = followTarget.eulerAngles.y;
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                SetCursorLocked(true);
            }
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.MenuPressed += ToggleCursor;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.MenuPressed -= ToggleCursor;
            }
        }

        private void LateUpdate()
        {
            if (cameraTransform == null || followTarget == null) return;

            Vector2 look = input != null && cursorLocked ? input.Look : Vector2.zero;

            yaw += look.x * horizontalSensitivity;
            pitch = Mathf.Clamp(pitch - look.y * verticalSensitivity, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = followTarget.position + targetOffset;
            float finalDistance = GetCameraDistance(pivot, rotation);
            Vector3 desiredPosition = pivot + rotation * new Vector3(0f, 0f, -finalDistance);

            cameraTransform.position = Vector3.SmoothDamp(
                cameraTransform.position,
                desiredPosition,
                ref cameraVelocity,
                positionSmoothTime);

            cameraTransform.rotation = rotation;
        }

        public void SetCursorLocked(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void ToggleCursor()
        {
            if (!toggleCursorWithMenu) return;
            SetCursorLocked(!cursorLocked);
        }

        private float GetCameraDistance(Vector3 pivot, Quaternion rotation)
        {
            if (!avoidClipping) return cameraDistance;

            Vector3 direction = rotation * Vector3.back;

            if (Physics.SphereCast(
                    pivot,
                    collisionRadius,
                    direction,
                    out RaycastHit hit,
                    cameraDistance,
                    collisionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return Mathf.Clamp(hit.distance - collisionBuffer, minDistance, cameraDistance);
            }

            return cameraDistance;
        }
    }
}
