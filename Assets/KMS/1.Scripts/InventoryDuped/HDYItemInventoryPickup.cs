using System.Collections.Generic;
using HDY.Item;
using UnityEngine;

namespace KMS.InventoryDuped
{
    [DisallowMultipleComponent]
    public class HDYItemInventoryPickup : MonoBehaviour, KMS.IInteractable
    {
        private static readonly Dictionary<ItemData, ItemDefinition> RuntimeDefinitions = new Dictionary<ItemData, ItemDefinition>();

        [SerializeField] private ItemData itemData;
        [SerializeField] private int amount = 1;
        [SerializeField] private string promptPrefix = "Pick up";
        [SerializeField] private bool destroyWhenFullyPickedUp = true;

        public string InteractionPrompt
        {
            get
            {
                if (itemData == null || string.IsNullOrEmpty(itemData.ItemName)) return promptPrefix;

                return $"{promptPrefix} {itemData.ItemName}";
            }
        }

        public bool CanInteract(KMS.PlayerInteraction interactor)
        {
            return itemData != null
                   && amount > 0
                   && interactor != null
                   && interactor.GetComponent<PlayerInventory>() != null;
        }

        public void Interact(KMS.PlayerInteraction interactor)
        {
            if (interactor == null || itemData == null || amount <= 0) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            ItemDefinition itemDefinition = GetOrCreateRuntimeDefinition(itemData);
            int remaining = inventory.AddItem(itemDefinition, amount);
            int pickedUp = amount - remaining;

            if (pickedUp <= 0) return;

            Debug.Log($"[InventoryPickup] {itemData.ItemName} pickup requested: amount={amount}, pickedUp={pickedUp}, remaining={remaining}");

            amount = remaining;

            if (amount <= 0 && destroyWhenFullyPickedUp)
            {
                Destroy(gameObject);
            }
        }

        private static ItemDefinition GetOrCreateRuntimeDefinition(ItemData source)
        {
            if (RuntimeDefinitions.TryGetValue(source, out ItemDefinition cached) && cached != null)
            {
                return cached;
            }

            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = source.name;
            definition.itemId = source.Item_ID;
            definition.displayName = source.ItemName;
            definition.icon = source.ItemIcon;
            definition.value = source.Value;
            definition.maxStack = Mathf.Max(1, source.MaxStack);
            definition.category = ConvertCategory(source.Category);
            definition.useAction = ConvertUseAction(source.UseAction);

            RuntimeDefinitions[source] = definition;
            return definition;
        }

        private static ItemCategory ConvertCategory(HDY.Item.ItemCategory category)
        {
            switch (category)
            {
                case HDY.Item.ItemCategory.Food:
                    return ItemCategory.Food;
                case HDY.Item.ItemCategory.Material:
                    return ItemCategory.Material;
                case HDY.Item.ItemCategory.Tool:
                    return ItemCategory.Tool;
                case HDY.Item.ItemCategory.Goods:
                case HDY.Item.ItemCategory.Capsule:
                default:
                    return ItemCategory.Misc;
            }
        }

        private static ItemUseAction ConvertUseAction(UseAction useAction)
        {
            switch (useAction)
            {
                case UseAction.Eat:
                    return ItemUseAction.Eat;
                case UseAction.Default:
                case UseAction.Use:
                default:
                    return ItemUseAction.None;
            }
        }
    }
}
