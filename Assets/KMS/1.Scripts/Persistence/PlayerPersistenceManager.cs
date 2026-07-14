using KMS.InventoryDuped;
using UnityEngine;

namespace KMS.Persistence
{
    /// <summary>씬 사이에서 플레이어 데이터만 유지한다. 파일 저장은 담당하지 않는다.</summary>
    public class PlayerPersistenceManager : MonoBehaviour
    {
        public static PlayerPersistenceManager Instance { get; private set; }

        private PlayerSaveData currentData;

        public bool HasData => currentData != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBeforeSceneLoad()
        {
            EnsureInstance();
        }

        public static PlayerPersistenceManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            var existing = FindFirstObjectByType<PlayerPersistenceManager>();
            if (existing != null) return existing;

            var managerObject = new GameObject(nameof(PlayerPersistenceManager));
            return managerObject.AddComponent<PlayerPersistenceManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Capture(PlayerInventory inventory, PlayerStats stats)
        {
            if (inventory == null || stats == null)
            {
                Debug.LogWarning("[PlayerPersistence] PlayerInventory 또는 PlayerStats가 없어 캡처하지 못했습니다.");
                return;
            }

            currentData = new PlayerSaveData
            {
                inventory = inventory.CaptureSaveData(),
                stats = stats.CaptureSaveData()
            };

            Debug.Log($"[PlayerPersistence] 캡처 완료: 체력={currentData.stats.currentHealth:0.##}, 허기={currentData.stats.currentHunger:0.##}, 일반 슬롯={currentData.inventory.inventory.slots.Length}, 퀵슬롯={currentData.inventory.quickSlots.slots.Length}");
        }

        public void RegisterPlayer(PlayerInventory inventory, PlayerStats stats)
        {
            if (!HasData) return;
            if (inventory == null || stats == null) return;

            inventory.RestoreSaveData(currentData.inventory);
            stats.RestoreSaveData(currentData.stats);

            Debug.Log($"[PlayerPersistence] '{inventory.gameObject.name}' 복원 완료: 체력={stats.CurrentHealth:0.##}, 허기={stats.CurrentHunger:0.##}");
        }
    }
}
