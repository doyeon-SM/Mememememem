using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInteract_GH : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera rayCamera;
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRay = true;

    private TestInteractable currentTarget;
    private RaycastHit currentHit;
    private bool isInputSubscribed;

    private void Awake()
    {
        if (rayCamera == null)
        {
            rayCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        TrySubscribeInput();
    }

    private void OnDisable()
    {
        if (isInputSubscribed && InputManager.Instance != null)
        {
            InputManager.Instance.OnInteractStarted -= TryInteract;
        }

        isInputSubscribed = false;
    }

    private void Update()
    {
        TrySubscribeInput();
        UpdateCurrentTarget();

        if (InputManager.Instance == null && ReadFallbackInteractPressed())
        {
            TryInteract();
        }
    }

    private void TrySubscribeInput()
    {
        if (isInputSubscribed || InputManager.Instance == null)
        {
            return;
        }

        InputManager.Instance.OnInteractStarted += TryInteract;
        isInputSubscribed = true;
    }

    public void TryInteract()
    {
        UpdateCurrentTarget();
        currentTarget?.Interact();
    }

    private void UpdateCurrentTarget()
    {
        currentTarget = null;

        Ray ray = CreateRay();
        if (drawDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.yellow);
        }

        if (!Physics.Raycast(ray, out currentHit, interactDistance, interactLayerMask, triggerInteraction))
        {
            return;
        }

        currentTarget = currentHit.collider.GetComponentInParent<TestInteractable>();
    }

    private Ray CreateRay()
    {
        if (rayCamera != null)
        {
            return new Ray(rayCamera.transform.position, rayCamera.transform.forward);
        }

        Transform origin = rayOrigin != null ? rayOrigin : transform;
        return new Ray(origin.position, origin.forward);
    }

    private bool ReadFallbackInteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.eKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.E);
    }

    private void OnDrawGizmosSelected()
    {
        Ray ray = CreateRay();
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * interactDistance);
    }
}
