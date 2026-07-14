using System;
using System.Reflection;
using HDY.Mem;
using HDY.UI;
using KMS.InventoryDuped;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using GameCursor = UnityEngine.Cursor;
using InputSystemKeyboard = UnityEngine.InputSystem.Keyboard;
using UIToolkitButton = UnityEngine.UIElements.Button;

namespace KMS
{
    /// <summary>
    /// KMS UI Toolkit HUD의 도감 버튼과 HDY uGUI 멤 도감 프리팹을 연결한다.
    /// 현재는 자체 Canvas에 도감을 생성하며, HDY UIManager 원본은 수정하지 않는다.
    /// </summary>
    public class KMSMemDexLauncher : MonoBehaviour
    {
        [Header("HUD Button")]
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

        [Header("Future HDY UIManager Bridge (Disabled By Default)")]
        [Tooltip("HDY UIManager가 public OpenPrefab(GameObject)를 제공하게 된 뒤에만 켠다. 지금은 자체 Canvas 경로를 사용한다.")]
        [SerializeField] private bool preferHdyUiManagerWhenAvailable;
        [SerializeField] private string hdyOpenMethodName = "OpenPrefab";

        private UIToolkitButton collectionButton;
        private GameObject modalCanvasObject;
        private RectTransform modalRoot;
        private GameObject memDexInstance;
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

        private void Start()
        {
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

            openedThroughHdyUiManager = TryOpenThroughFutureHdyUiManager();
            if (!openedThroughHdyUiManager && !OpenStandalone())
            {
                RestorePlayerState();
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
                Destroy(memDexInstance);
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
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
            if (cameraController == null) cameraController = GetComponent<PlayerCameraController>();
            if (inventoryUi == null) inventoryUi = FindFirstObjectByType<InventoryUI>();
        }

        private void BindCollectionButton()
        {
            UnbindCollectionButton();

            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            collectionButton = uiDocument.rootVisualElement.Q<UIToolkitButton>(collectionButtonName);
            if (collectionButton == null)
            {
                Debug.LogWarning($"[KMSMemDexLauncher] '{collectionButtonName}' 버튼을 찾을 수 없습니다.", this);
                return;
            }

            collectionButton.clicked += Toggle;
        }

        private void UnbindCollectionButton()
        {
            if (collectionButton != null)
            {
                collectionButton.clicked -= Toggle;
                collectionButton = null;
            }
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
            if (isOpen)
            {
                Close();
                return;
            }

            // 인벤토리 등 다른 모달 UI가 플레이어 입력을 막고 있으면 새 도감을 열지 않는다.
            if (playerInput != null && playerInput.IsGameplayInputBlocked) return;

            Open();
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
            EnsureModalCanvas();
            if (modalRoot == null) return false;

            modalCanvasObject.SetActive(true);
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

        private void EnsureModalCanvas()
        {
            if (modalRoot != null) return;

            modalCanvasObject = new GameObject(
                "KMS Mem Dex Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            modalCanvasObject.transform.SetParent(transform, false);

            var canvas = modalCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = modalSortingOrder;

            var scaler = modalCanvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var rootObject = new GameObject("MemDexModalRoot", typeof(RectTransform));
            modalRoot = rootObject.GetComponent<RectTransform>();
            modalRoot.SetParent(modalCanvasObject.transform, false);
            modalRoot.anchorMin = Vector2.zero;
            modalRoot.anchorMax = Vector2.one;
            modalRoot.offsetMin = Vector2.zero;
            modalRoot.offsetMax = Vector2.zero;

            modalCanvasObject.SetActive(false);
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

        private void RestorePlayerState()
        {
            if (playerInput != null)
            {
                playerInput.SetCursorReleased(previousCursorReleased);
                playerInput.SetGameplayInputBlocked(previousGameplayInputBlocked);
            }

            if (playerMovement != null) playerMovement.IsMovementEnabled = previousMovementEnabled;

            if (cameraController != null)
            {
                cameraController.SetCursorLocked(previousCursorLockMode == CursorLockMode.Locked);
            }

            GameCursor.lockState = previousCursorLockMode;
            GameCursor.visible = previousCursorVisible;
        }

        private void FinishClose()
        {
            isOpen = false;
            openedThroughHdyUiManager = false;
            memDexInstance = null;

            if (modalCanvasObject != null) modalCanvasObject.SetActive(false);

            RestorePlayerState();
        }

        /// <summary>
        /// 미래 HDY 연동 지점.
        /// 현재 UIManager의 실제 열기 함수는 private이므로 이 옵션은 기본적으로 꺼져 있다.
        /// 나중에 HDY가 public OpenPrefab(GameObject)를 제공하면 HDY 코드를 다시 참조해
        /// 컴파일할 필요 없이 preferHdyUiManagerWhenAvailable만 켜서 전환할 수 있다.
        /// 공개 메서드 이름이 다르면 hdyOpenMethodName을 인스펙터에서 맞춘다.
        /// </summary>
        private bool TryOpenThroughFutureHdyUiManager()
        {
            if (!preferHdyUiManagerWhenAvailable || UIManager.Instance == null) return false;

            MethodInfo openMethod = typeof(UIManager).GetMethod(
                hdyOpenMethodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(GameObject) },
                null);

            if (openMethod == null)
            {
                Debug.LogWarning(
                    $"[KMSMemDexLauncher] UIManager에 public {hdyOpenMethodName}(GameObject)가 없어 자체 Canvas로 엽니다.",
                    this);
                return false;
            }

            try
            {
                openMethod.Invoke(UIManager.Instance, new object[] { memDexPrefab });
                return UIManager.Instance.HasActivePanel();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[KMSMemDexLauncher] HDY UIManager 도감 열기에 실패해 자체 Canvas로 대체합니다. {exception.Message}",
                    this);
                return false;
            }
        }
    }
}
