using UnityEngine;

namespace KMS
{
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInput input;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Camera targetCamera;

        [Header("Orbit")]
        [SerializeField] private Vector3 targetOffset;
        [SerializeField] private float cameraDistance = 4f;
        [SerializeField] private float minDistance = 0.6f;
        [SerializeField] private float horizontalSensitivity = 0.12f;
        [SerializeField] private float verticalSensitivity = 0.12f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float positionSmoothTime = 0.04f;

        [Header("Aim Camera")]
        [SerializeField, Min(0f)] private float aimFovReduction = 5f;
        [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.2f;
        [Tooltip("조준 중 카메라 피벗 오프셋입니다. X=카메라 기준 오른쪽, Y=월드 위, Z=카메라 수평 전방입니다.")]
        [SerializeField] private Vector3 aimShoulderOffset = new Vector3(0.7f, 0.15f, 0.25f);
        [SerializeField, Min(0.01f)] private float aimShoulderSmoothTime = 0.2f;

        [Header("Collision")]
        [SerializeField] private bool avoidClipping = true;
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private float collisionBuffer = 0.1f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnStart = true;

        public float Yaw => yaw;
        public float Pitch => pitch;

        private float yaw;
        private float pitch = 20f;
        private Vector3 cameraVelocity;
        private bool cursorLocked;
        private float defaultFieldOfView;
        private float targetFieldOfView;
        private float fieldOfViewVelocity;
        private Vector3 currentAimShoulderOffset;
        private Vector3 targetAimShoulderOffset;
        private Vector3 aimShoulderOffsetVelocity;

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
            if (targetCamera == null && cameraTransform != null) targetCamera = cameraTransform.GetComponent<Camera>();

            if (targetCamera != null)
            {
                defaultFieldOfView = targetCamera.fieldOfView;
                targetFieldOfView = defaultFieldOfView;
            }

            yaw = followTarget.eulerAngles.y;
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                SetCursorLocked(true);
            }
        }

        private void LateUpdate()
        {
            UpdateAimCameraTransition();

            if (input != null)
            {
                bool shouldLockCursor = !input.IsCursorReleased;
                CursorLockMode expectedLockMode = shouldLockCursor
                    ? CursorLockMode.Locked
                    : CursorLockMode.None;
                bool expectedVisible = !shouldLockCursor;

                if (cursorLocked != shouldLockCursor ||
                    Cursor.lockState != expectedLockMode ||
                    Cursor.visible != expectedVisible)
                {
                    SetCursorLocked(shouldLockCursor);
                }
            }

            if (cameraTransform == null || followTarget == null) return;

            Vector2 look = input != null && cursorLocked ? input.Look : Vector2.zero;

            yaw += look.x * horizontalSensitivity;
            pitch = Mathf.Clamp(pitch - look.y * verticalSensitivity, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Quaternion horizontalRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 horizontalShoulderOffset = horizontalRotation * new Vector3(
                currentAimShoulderOffset.x,
                0f,
                currentAimShoulderOffset.z);
            Vector3 shoulderOffset = horizontalShoulderOffset + Vector3.up * currentAimShoulderOffset.y;
            Vector3 pivot = followTarget.position + targetOffset + shoulderOffset;
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

        public void SetAimZoom(bool isAiming)
        {
            targetAimShoulderOffset = isAiming ? aimShoulderOffset : Vector3.zero;

            if (targetCamera != null)
            {
                targetFieldOfView = isAiming
                    ? Mathf.Max(1f, defaultFieldOfView - aimFovReduction)
                    : defaultFieldOfView;
            }
        }

        private void UpdateAimCameraTransition()
        {
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = Mathf.SmoothDamp(
                    targetCamera.fieldOfView,
                    targetFieldOfView,
                    ref fieldOfViewVelocity,
                    zoomSmoothTime);
            }

            currentAimShoulderOffset = Vector3.SmoothDamp(
                currentAimShoulderOffset,
                targetAimShoulderOffset,
                ref aimShoulderOffsetVelocity,
                aimShoulderSmoothTime);
        }

        private void OnDisable()
        {
            if (targetCamera != null) targetCamera.fieldOfView = defaultFieldOfView;
            fieldOfViewVelocity = 0f;
            currentAimShoulderOffset = Vector3.zero;
            targetAimShoulderOffset = Vector3.zero;
            aimShoulderOffsetVelocity = Vector3.zero;
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
