using HDY.Item;
using KMS.InventoryDuped;
using UnityEngine;

namespace KMS.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class KMSInventoryAudioFeedback : MonoBehaviour
    {
        private PlayerInventory inventory;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable()
        {
            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }

            if (inventory == null)
            {
                return;
            }

            inventory.OnItemObtained += HandleItemObtained;
            inventory.OnQuickSlotSelectionChanged += HandleQuickSlotSelectionChanged;
        }

        private void OnDisable()
        {
            if (inventory == null)
            {
                return;
            }

            inventory.OnItemObtained -= HandleItemObtained;
            inventory.OnQuickSlotSelectionChanged -= HandleQuickSlotSelectionChanged;
        }

        private static void HandleItemObtained(ItemData _, int amount)
        {
            if (amount > 0)
            {
                KMSAudioService.Play2D(GameSfxId.ItemObtained);
            }
        }

        private static void HandleQuickSlotSelectionChanged(int _)
        {
            KMSAudioService.Play2D(GameSfxId.QuickSlotSelected);
        }
    }
}
