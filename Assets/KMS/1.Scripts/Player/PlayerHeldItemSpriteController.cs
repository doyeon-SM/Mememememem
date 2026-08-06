using HDY.Item;
using KMS.InventoryDuped;
using UnityEngine;

namespace KMS
{
    [DisallowMultipleComponent]
    public class PlayerHeldItemSpriteController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private ItemCatalogManager catalogManager;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform handAnchor;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform cameraTransform;

        [Header("Held Sprite Layout")]
        [SerializeField] private Vector3 localPosition = new Vector3(0.08f, 0.02f, 0.02f);
        [SerializeField] private Vector3 localEulerAngles;
        [Min(0.01f)] [SerializeField] private float spriteScale = 0.3f;
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private bool flipX;
        [SerializeField] private int sortingOrder = 20;

        private Transform visualTransform;
        private bool ownsRuntimeAnchor;
        private bool isThrowVisualSuppressed;

        private void Awake()
        {
            ResolveReferences();
            EnsureHeldSpriteVisual();
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.OnSelectedQuickSlotChanged += HandleSelectedQuickSlotChanged;
                inventory.OnQuickSlotChanged += HandleQuickSlotChanged;
            }

            RefreshHeldSprite();
        }

        private void Start()
        {
            // 다른 컴포넌트의 Awake 및 씬 복원 이후 상태도 한 번 더 반영한다.
            ResolveReferences();
            EnsureHeldSpriteVisual();
            RefreshHeldSprite();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.OnSelectedQuickSlotChanged -= HandleSelectedQuickSlotChanged;
                inventory.OnQuickSlotChanged -= HandleQuickSlotChanged;
            }

            SetSprite(null);
        }

        private void LateUpdate()
        {
            if (visualTransform == null) return;

            if (ownsRuntimeAnchor)
            {
                visualTransform.localPosition = localPosition;
                visualTransform.localScale = Vector3.one * spriteScale;
            }

            if (faceCamera && cameraTransform != null)
            {
                visualTransform.rotation = cameraTransform.rotation;
            }
            else if (ownsRuntimeAnchor)
            {
                visualTransform.localRotation = Quaternion.Euler(localEulerAngles);
            }
        }

        private void HandleSelectedQuickSlotChanged(int _)
        {
            RefreshHeldSprite();
        }

        private void HandleQuickSlotChanged(int changedIndex)
        {
            // 음식 마지막 1개 소비처럼 선택 번호는 그대로이고 슬롯 내용만 바뀌는 경우를 반영한다.
            if (inventory != null && changedIndex == inventory.selectedQuickSlotIndex)
            {
                RefreshHeldSprite();
            }
        }

        private void RefreshHeldSprite()
        {
            if (isThrowVisualSuppressed || inventory == null || spriteRenderer == null)
            {
                SetSprite(null);
                return;
            }

            ItemStack selectedStack = inventory.GetSelectedQuickSlot();
            if (selectedStack == null || selectedStack.IsEmpty)
            {
                SetSprite(null);
                return;
            }

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            ItemData selectedItem = catalogManager != null
                ? catalogManager.FindItemData(selectedStack.itemId)
                : null;

            SetSprite(selectedItem != null ? selectedItem.ItemIcon : null);
        }

        public void SetThrowVisualSuppressed(bool suppressed)
        {
            if (isThrowVisualSuppressed == suppressed) return;

            isThrowVisualSuppressed = suppressed;
            RefreshHeldSprite();
        }

        private void SetSprite(Sprite sprite)
        {
            if (spriteRenderer == null) return;

            spriteRenderer.sprite = sprite;
            spriteRenderer.flipX = flipX;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.enabled = sprite != null;
        }

        private void ResolveReferences()
        {
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (animator == null && movement != null) animator = movement.Animator;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
        }

        private void EnsureHeldSpriteVisual()
        {
            if (spriteRenderer != null)
            {
                visualTransform = spriteRenderer.transform;
                ownsRuntimeAnchor = false;
                SetSprite(null);
                return;
            }

            if (handAnchor == null && animator != null && animator.isHuman)
            {
                handAnchor = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }

            if (handAnchor == null)
            {
                Debug.LogWarning("[HeldItemSprite] 활성 Humanoid 오른손 본을 찾을 수 없습니다.", this);
                return;
            }

            GameObject visualObject = new GameObject("HeldItemSpriteAnchor");
            visualTransform = visualObject.transform;
            visualTransform.SetParent(handAnchor, false);
            visualTransform.localPosition = localPosition;
            visualTransform.localRotation = Quaternion.Euler(localEulerAngles);
            visualTransform.localScale = Vector3.one * spriteScale;

            spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
            spriteRenderer.flipX = flipX;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.enabled = false;
            ownsRuntimeAnchor = true;
        }

        private void OnValidate()
        {
            spriteScale = Mathf.Max(0.01f, spriteScale);

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = flipX;
                spriteRenderer.sortingOrder = sortingOrder;
            }
        }
    }
}
