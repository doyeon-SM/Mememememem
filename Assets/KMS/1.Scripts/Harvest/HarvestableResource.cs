using HDY.Item;
using UnityEngine;

using KmsPlayerInventory = KMS.InventoryDuped.PlayerInventory;

namespace KMS.Harvesting
{
    [DisallowMultipleComponent]
    public class HarvestableResource : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 3;

        [Header("Optional Drop")]
        [SerializeField] private ItemData dropItem;
        [SerializeField] private int dropAmount = 1;

        [Header("Depletion")]
        [SerializeField] private bool destroyWhenDepleted = true;

        private int currentHealth;
        private bool isDepleted;
        private bool rewardClaimed;

        public bool IsDead => isDepleted || currentHealth <= 0;

        private void Awake()
        {
            currentHealth = Mathf.Max(1, maxHealth);
            isDepleted = false;
            rewardClaimed = false;
        }

        public void TakeDamage(int damage)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));

            if (currentHealth <= 0)
            {
                Deplete();
            }
        }

        public bool TryCollectReward(KmsPlayerInventory inventory)
        {
            if (!IsDead || rewardClaimed) return false;

            rewardClaimed = true;

            if (inventory == null || dropItem == null || dropAmount <= 0)
            {
                return false;
            }

            int remaining = inventory.AddItem(dropItem, dropAmount);
            int collectedAmount = dropAmount - remaining;

            return collectedAmount > 0;
        }

        private void Deplete()
        {
            if (isDepleted) return;

            isDepleted = true;
            currentHealth = 0;

            if (destroyWhenDepleted)
            {
                Destroy(gameObject);
            }
        }
    }
}