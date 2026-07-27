using HDY.Mem;
using HDY.UI;
using KMS.InventoryDuped;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GameCursor = UnityEngine.Cursor;
using InputSystemKeyboard = UnityEngine.InputSystem.Keyboard;
using ToolkitButton = UnityEngine.UIElements.Button;
using UIDocument = UnityEngine.UIElements.UIDocument;

namespace KMS
{
    /// <summary>
    /// KMS uGUI HUD의 도감 버튼과 HDY uGUI 멤 도감 프리팹을 연결한다.
    /// HDY UIManager가 있는 씬에서는 공용 패널 스택을 사용하고,
    /// 없는 KMS 테스트 씬에서는 자체 Canvas로 대체한다.
    /// </summary>
    public class KMSMemDexLauncher : MonoBehaviour
    {
        [Header("HUD Button")]
        [SerializeField] private KMSPlayerHudView hudView;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private string collectionButtonName = "collection-button";

        [Header("Mem Dex")]
        [SerializeField] private GameObject memDexPrefab;
        [SerializeField] private GameObject runtimeServicesPrefab;
        [SerializeField] private int modalSortingOrder = 200;

        [Header("Player Modal State")]
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private InventoryUI inventoryUi;

        private Button collectionButton;
        private ToolkitButton toolkitCollectionButton;
        private GameObject fallbackModalCanvasObject;
        private RectTransform modalRoot;
        private GameObject memDexInstance;
        private CanvasGroup preplacedMemDexCanvasGroup;
        private bool usesPreplacedMemDex;
        private bool isOpen;
        private bool openedThroughHdyUiManager;

        private bool previousMovementEnabled;
        private bool previousGameplayInputBlocked;
        private bool previousCursorReleased;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;

        public bool IsOpen => isOpen;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureRuntimeServices();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            BindCollectionButton();
            BindPlayerInput();
        }

        private void Update()
        {
            if (!isOpen) return;

            if (InputSystemKeyboard.current != null && InputSystemKeyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            // HDY UIManager가 ESC나 다른 HUD 버튼으로 자신의 패널을 닫은 경우에도
            // KMS 플레이어의 이동/커서 상태가 남지 않도록 감시한다.
            if (openedThroughHdyUiManager &&
                (UIManager.Instance == null || !UIManager.Instance.HasActivePanel()))
            {
                FinishClose();
            }
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            UnbindCollectionButton();
            UnbindPlayerInput();
            Close();
        }

        public void Open()
        {
            if (isOpen || memDexPrefab == null) return;

            ResolveReferences();
            EnsureRuntimeServices();

            // 인벤토리가 먼저 커서/이동 상태를 원래대로 돌려놓은 다음 도감 상태를 저장한다.
            inventoryUi?.Close();
            CapturePlayerState();

            openedThroughHdyUiManager = TryOpenThroughHdyUiManager();
            if (!openedThroughHdyUiManager && !OpenStandalone())
            {
                RestorePlayerState(true);
                return;
            }

            isOpen = true;
            ApplyModalPlayerState();
        }

        public void Close()
        {
            if (!isOpen) return;

            if (openedThroughHdyUiManager)
            {
                UIManager.Instance?.CloseCurrent();
            }
            else if (memDexInstance != null)
            {
                if (usesPreplacedMemDex) SetPreplacedMemDexVisible(false);
                else Destroy(memDexInstance);
            }

            FinishClose();
        }

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        private void ResolveReferences()
        {
            if (hudView == null) hudView = FindFirstObjectByType<KMSPlayerHudView>(FindObjectsInactive.Include);
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
            if (cameraController == null) cameraController = GetComponent<PlayerCameraController>();
            if (inventoryUi == null)
            {
                inventoryUi = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            }
        }

        private void BindCollectionButton()
        {
            UnbindCollectionButton();
            ResolveReferences();

            if (uiDocument != null && uiDocument.enabled && uiDocument.rootVisualElement != null)
            {
                toolkitCollectionButton = UnityEngine.UIElements.UQueryExtensions.Q<ToolkitButton>(
                    uiDocument.rootVisualElement,
                    collectionButtonName);
                if (toolkitCollectionButton != null)
                {
                    // Temporarily disabled while testing a non-runtime-bound Collection button.
                    // toolkitCollectionButton.clicked += Toggle;
                    return;
                }

                Debug.LogWarning($"[KMSMemDexLauncher] UI Toolkit의 '{collectionButtonName}' 버튼을 찾을 수 없습니다.", this);
                return;
            }

            collectionButton = hudView != null ? hudView.CollectionButton : null;
            if (collectionButton == null)
            {
                Debug.LogWarning("[KMSMemDexLauncher] uGUI 도감 버튼을 찾을 수 없습니다.", this);
                return;
            }

            // Temporarily disabled so the Collection button can be tested with an Inspector-assigned OnClick event.
            // collectionButton.onClick.AddListener(Toggle);
        }

        private void UnbindCollectionButton()
        {
            if (collectionButton != null)
            {
                collectionButton.onClick.RemoveListener(Toggle);
                collectionButton = null;
            }

            if (toolkitCollectionButton != null)
            {
                toolkitCollectionButton.clicked -= Toggle;
                toolkitCollectionButton = null;
            }
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            UnbindCollectionButton();
            hudView = null;
            inventoryUi = null;
            BindCollectionButton();
        }

        private void BindPlayerInput()
        {
            UnbindPlayerInput();
            if (playerInput != null) playerInput.CollectionPressed += HandleCollectionPressed;
        }

        private void UnbindPlayerInput()
        {
            if (playerInput != null) playerInput.CollectionPressed -= HandleCollectionPressed;
        }

        private void HandleCollectionPressed()
        {
            SceneUIManager.TryToggleManagedUI("MemDex");
        }

        private void EnsureRuntimeServices()
        {
            if (MemCatalogManager.Instance == null && runtimeServicesPrefab != null)
            {
                Instantiate(runtimeServicesPrefab);
            }

            if (MemCatalogManager.Instance == null)
            {
                Debug.LogWarning("[KMSMemDexLauncher] MemCatalogManager가 없어 도감 목록을 채울 수 없습니다.", this);
                return;
            }

            if (MemIconRenderer.Instance == null)
            {
                var rendererObject = new GameObject("KMS Mem Icon Renderer");
                rendererObject.transform.SetParent(MemCatalogManager.Instance.transform, false);
                rendererObject.AddComponent<MemIconRenderer>();
            }
        }

        private bool OpenStandalone()
        {
            if (TryOpenPreplacedMemDex()) return true;

            EnsureModalRoot();
            if (modalRoot == null) return false;

            if (fallbackModalCanvasObject != null)
            {
                fallbackModalCanvasObject.SetActive(true);
            }

            modalRoot.SetAsLastSibling();
            memDexInstance = Instantiate(memDexPrefab, modalRoot);

            var instanceTransform = memDexInstance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;

            if (instanceTransform is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition = Vector2.zero;
            }

            return true;
        }

        private void EnsureModalRoot()
        {
            if (modalRoot != null) return;

            if (TryCreateInventoryCanvasRoot()) return;

            CreateFallbackModalCanvas();
        }

        private bool TryCreateInventoryCanvasRoot()
        {
            ResolveReferences();
            if (inventoryUi == null) return false;

            Canvas inventoryCanvas = inventoryUi.GetComponentInParent<Canvas>(true);
            if (inventoryCanvas == null || inventoryCanvas.transform is not RectTransform canvasRoot)
            {
                return false;
            }

            modalRoot = CreateModalRoot(canvasRoot);
            return true;
        }

        private bool TryOpenPreplacedMemDex()
        {
            ResolveReferences();
            if (inventoryUi == null) return false;

            Canvas inventoryCanvas = inventoryUi.GetComponentInParent<Canvas>(true);
            if (inventoryCanvas == null) return false;

            MemDexUI preplacedMemDex = inventoryCanvas.GetComponentInChildren<MemDexUI>(true);
            if (preplacedMemDex == null) return false;

            memDexInstance = preplacedMemDex.gameObject;
            modalRoot = preplacedMemDex.transform.parent as RectTransform;
            if (modalRoot != null) modalRoot.SetAsLastSibling();

            preplacedMemDexCanvasGroup = modalRoot != null
                ? modalRoot.GetComponent<CanvasGroup>()
                : null;
            if (preplacedMemDexCanvasGroup == null && modalRoot != null)
            {
                preplacedMemDexCanvasGroup = modalRoot.gameObject.AddComponent<CanvasGroup>();
            }

            usesPreplacedMemDex = true;
            memDexInstance.SetActive(true);
            SetPreplacedMemDexVisible(true);
            return true;
        }

        private void SetPreplacedMemDexVisible(bool visible)
        {
            if (preplacedMemDexCanvasGroup == null) return;

            preplacedMemDexCanvasGroup.alpha = visible ? 1f : 0f;
            preplacedMemDexCanvasGroup.interactable = visible;
            preplacedMemDexCanvasGroup.blocksRaycasts = visible;
        }

        private void CreateFallbackModalCanvas()
        {
            fallbackModalCanvasObject = new GameObject(
                "KMS Mem Dex Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            fallbackModalCanvasObject.transform.SetParent(transform, false);

            var canvas = fallbackModalCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = modalSortingOrder;

            var scaler = fallbackModalCanvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            modalRoot = CreateModalRoot(fallbackModalCanvasObject.transform);
            fallbackModalCanvasObject.SetActive(false);
        }

        private static RectTransform CreateModalRoot(Transform parent)
        {
            var rootObject = new GameObject("MemDexModalRoot", typeof(RectTransform));
            rootObject.layer = parent.gameObject.layer;

            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            return root;
        }

        private void CapturePlayerState()
        {
            previousMovementEnabled = playerMovement == null || playerMovement.IsMovementEnabled;
            previousGameplayInputBlocked = playerInput != null && playerInput.IsGameplayInputBlocked;
            previousCursorReleased = playerInput != null && playerInput.IsCursorReleased;
            previousCursorLockMode = GameCursor.lockState;
            previousCursorVisible = GameCursor.visible;
        }

        private void ApplyModalPlayerState()
        {
            if (playerInput != null)
            {
                playerInput.SetCursorReleased(true);
                playerInput.SetGameplayInputBlocked(true);
            }

            if (playerMovement != null) playerMovement.IsMovementEnabled = false;

            if (cameraController != null) cameraController.SetCursorLocked(false);
            else
            {
                GameCursor.lockState = CursorLockMode.None;
                GameCursor.visible = true;
            }
        }

        private void RestorePlayerState(bool restorePreviousCursor)
        {
            bool releaseCursor = restorePreviousCursor && previousCursorReleased;

            if (playerInput != null)
            {
                playerInput.SetCursorReleased(releaseCursor);
                playerInput.SetGameplayInputBlocked(previousGameplayInputBlocked);
            }

            if (playerMovement != null) playerMovement.IsMovementEnabled = previousMovementEnabled;

            if (cameraController != null)
            {
                cameraController.SetCursorLocked(
                    restorePreviousCursor
                        ? previousCursorLockMode == CursorLockMode.Locked
                        : true);
            }

            GameCursor.lockState = restorePreviousCursor
                ? previousCursorLockMode
                : CursorLockMode.Locked;
            GameCursor.visible = restorePreviousCursor && previousCursorVisible;
        }

        private void FinishClose()
        {
            isOpen = false;
            openedThroughHdyUiManager = false;
            memDexInstance = null;
            usesPreplacedMemDex = false;

            if (fallbackModalCanvasObject != null) fallbackModalCanvasObject.SetActive(false);

            RestorePlayerState(false);
        }

        private bool TryOpenThroughHdyUiManager()
        {
            var uiManager = UIManager.Instance;
            if (uiManager == null) return false;

            uiManager.HandleHudButtonClicked(memDexPrefab);
            return uiManager.HasActivePanel();
        }
    }
}
