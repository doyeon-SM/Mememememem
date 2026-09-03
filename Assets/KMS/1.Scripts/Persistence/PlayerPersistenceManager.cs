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

            PlayerCombatStats combatStats = stats.GetComponent<PlayerCombatStats>();
            // [멤] 장비 시스템 - 장착창 12칸도 씬 이동 간 함께 유지한다.
            KMS.Equipment.PlayerEquipment equipment = stats.GetComponent<KMS.Equipment.PlayerEquipment>();

            currentData = new PlayerSaveData
            {
                inventory = inventory.CaptureSaveData(),
                stats = stats.CaptureSaveData(),
                combatStats = combatStats != null ? combatStats.CaptureSaveData() : null,
                equipment = equipment != null ? equipment.CaptureSaveData() : null
            };

            Debug.Log($"[PlayerPersistence] 캡처 완료: 체력={currentData.stats.currentHealth:0.##}, 허기={currentData.stats.currentHunger:0.##}, 일반 슬롯={currentData.inventory.inventory.slots.Length}, 퀵슬롯={currentData.inventory.quickSlots.slots.Length}");
        }

        public void RegisterPlayer(PlayerInventory inventory, PlayerStats stats)
        {
            if (!HasData) return;
            if (inventory == null || stats == null) return;

            inventory.RestoreSaveData(currentData.inventory);
            stats.RestoreSaveData(currentData.stats);

            // [멤] 캐릭터 스탯 시스템 복원(씨 이동 간 유지). 이전 버전 데이터(combatStats 없음)이면 null이므로 그대로 무시된다.
            if (currentData.combatStats != null)
            {
                PlayerCombatStats combatStats = stats.GetComponent<PlayerCombatStats>();
                if (combatStats != null)
                {
                    combatStats.RestoreSaveData(currentData.combatStats);
                }
            }

            // [멤] 장비 시스템 - 장착창 복원. 구버전 데이터면 null이라 그대로 무시된다.
            if (currentData.equipment != null)
            {
                KMS.Equipment.PlayerEquipment equipment = stats.GetComponent<KMS.Equipment.PlayerEquipment>();
                if (equipment != null)
                {
                    equipment.RestoreSaveData(currentData.equipment);
                }
            }

            Debug.Log($"[PlayerPersistence] '{inventory.gameObject.name}' 복원 완료: 체력={stats.CurrentHealth:0.##}, 허기={stats.CurrentHunger:0.##}");
        }
    }
}
