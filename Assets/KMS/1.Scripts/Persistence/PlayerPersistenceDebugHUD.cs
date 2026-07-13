using System.Text;
using KMS.InventoryDuped;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS.Persistence
{
    /// <summary>씬 이동 저장 테스트에서 복원 결과를 화면에 표시하는 개발용 HUD.</summary>
    public class PlayerPersistenceDebugHUD : MonoBehaviour
    {
        private PlayerInventory inventory;
        private PlayerStats stats;
        private GUIStyle style;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            stats = GetComponent<PlayerStats>();
        }

        private void OnGUI()
        {
            if (inventory == null || stats == null) return;

            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            var text = new StringBuilder();
            text.AppendLine($"Persistence Test - {SceneManager.GetActiveScene().name}");
            text.AppendLine($"Health: {stats.CurrentHealth:0.##} / {stats.MaxHealth:0.##}");
            text.AppendLine($"Hunger: {stats.CurrentHunger:0.##} / {stats.MaxHunger:0.##}");
            text.AppendLine($"Selected Quick Slot: {inventory.selectedQuickSlotIndex}");
            AppendSlots(text, "Inventory", inventory.inventory);
            AppendSlots(text, "Quick Slots", inventory.quickSlots);

            GUI.Box(new Rect(16f, 16f, 440f, 260f), text.ToString(), style);
        }

        private static void AppendSlots(StringBuilder text, string title, InventoryContainer container)
        {
            text.Append(title).Append(": ");
            bool found = false;

            if (container?.slots != null)
            {
                for (int i = 0; i < container.slots.Length; i++)
                {
                    ItemStack slot = container.slots[i];
                    if (slot == null || slot.IsEmpty) continue;

                    if (found) text.Append(", ");
                    text.Append('[').Append(i).Append("] ").Append(slot.itemId).Append(" x").Append(slot.amount);
                    found = true;
                }
            }

            if (!found) text.Append("Empty");
            text.AppendLine();
        }
    }
}
