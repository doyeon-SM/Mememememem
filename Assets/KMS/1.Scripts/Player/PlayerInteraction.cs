using System;
using UnityEngine;

namespace KMS
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(PlayerInteraction interactor);
        void Interact(PlayerInteraction interactor);
    }

    public class PlayerInteraction : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInput input;
        [SerializeField] private Transform cameraTransform;

        [Header("Detection")]
        [SerializeField] private LayerMask interactionLayer = ~0;
        [SerializeField] private float raycastDistance = 4f;
        [SerializeField] private float proximityRadius = 1.5f;
        [SerializeField] private bool useRaycast = true;
        [SerializeField] private bool useProximity = true;

        [Header("Interaction")]
        [SerializeField] private float interactionCooldown = 0.2f;

        public IInteractable CurrentInteractable { get; private set; }
        public event Action<IInteractable> FocusChanged;

        private readonly Collider[] proximityHits = new Collider[20];
        private float cooldownTimer;

        private void Reset()
        {
            input = GetComponent<PlayerInput>();
            cameraTransform = Camera.main != null ? Camera.main.transform : null;
        }

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.InteractPressed += TryInteract;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.InteractPressed -= TryInteract;
            }

            SetFocus(null);
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            FindBestInteractable();
        }

        public void TryInteract()
        {
            if (cooldownTimer > 0f) return;
            if (CurrentInteractable == null) return;
            if (!CurrentInteractable.CanInteract(this)) return;

            CurrentInteractable.Interact(this);
            cooldownTimer = interactionCooldown;
        }

        private void FindBestInteractable()
        {
            IInteractable best = null;

            if (useRaycast)
            {
                best = FindRaycastInteractable();
            }

            if (best == null && useProximity)
            {
                best = FindProximityInteractable();
            }

            SetFocus(best);
        }

        private IInteractable FindRaycastInteractable()
        {
            Transform cam = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
            if (cam == null) return null;

            Ray ray = new Ray(cam.position, cam.forward);

            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    raycastDistance,
                    interactionLayer,
                    QueryTriggerInteraction.Collide))
            {
                return hit.collider.GetComponentInParent<IInteractable>();
            }

            return null;
        }

        private IInteractable FindProximityInteractable()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                proximityRadius,
                proximityHits,
                interactionLayer,
                QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider hit = proximityHits[i];
                if (hit == null) continue;

                IInteractable interactable = hit.GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract(this)) continue;

                float distance = Vector3.Distance(transform.position, hit.transform.position);
                if (distance < bestDistance)
                {
                    best = interactable;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void SetFocus(IInteractable interactable)
        {
            if (ReferenceEquals(CurrentInteractable, interactable)) return;

            CurrentInteractable = interactable;
            FocusChanged?.Invoke(CurrentInteractable);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, proximityRadius);

            Transform cam = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
            if (cam != null)
            {
                Gizmos.DrawLine(cam.position, cam.position + cam.forward * raycastDistance);
            }
        }
    }
}
