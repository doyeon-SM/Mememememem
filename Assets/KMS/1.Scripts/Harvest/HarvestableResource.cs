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

        // [HDY 요청] ItemData 직접 참조 대신 Item_ID 문자열로 변경.
        // ItemCatalogManager가 시트 기반으로 바뀌면서 런타임에 매번 새 ItemData 인스턴스를
        // 만들기 때문에, 여기서 특정 ItemData 애셋을 직접 들고 있으면 같은 Item_ID를 가진
        // 두 개의 서로 다른 객체가 메모리에 동시에 존재하게 되어 다른 곳(GridManager 등)의
        // Resources.FindObjectsOfTypeAll<ItemData>() 조회가 꼬일 수 있다. ID 문자열만 들고
        // 있다가 PlayerInventory.AddItem(string, int)로 위임하는 방식으로 통일했다.
        [Header("Optional Drop")]
        [SerializeField] private string dropItemId;
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

            if (inventory == null || string.IsNullOrEmpty(dropItemId) || dropAmount <= 0)
            {
                return false;
            }

            int remaining = inventory.AddItem(dropItemId, dropAmount);
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
