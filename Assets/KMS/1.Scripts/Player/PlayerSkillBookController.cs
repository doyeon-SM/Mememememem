using HDY.Item;
using KMS.InventoryDuped;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS.Combat
{
    // [멤] 스킬북 / 궁극의 스킬북 사용(우클릭) 컨트롤러.
    // 두 카테고리는 기능이 완전히 동일하며, 선택된 퀵슬롯 아이템의 ItemData.Skill_ID를 읽어
    // SkillUnlockManager.TryUnlockSkill로 스킬을 획득시키고 아이템 1개를 소모한다.
    // 탐험 씬(Main_World 계열)에서만 동작하고, 이미 배운 스킬북은 조용히 무시(소모 없음, UI 없음)한다.
    // 캐릭터 이동/다른 행동을 잠그지 않는 즉시 사용 방식이라 PlayerActionSlotCoordinator를 거치지 않는다.
    [DisallowMultipleComponent]
    public class PlayerSkillBookController : MonoBehaviour
    {
        [Header("참조 (비어있으면 자동 탐색)")]
        [SerializeField] private PlayerInput input;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private ItemCatalogManager catalogManager;
        [SerializeField] private SkillUnlockManager skillUnlockManager;
        [SerializeField] private SkillCatalogManager skillCatalogManager;
        [SerializeField] private PlayerHUD hud;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (input != null)
            {
                input.SecondaryActionPressed += TryUseSelectedSkillBook;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.SecondaryActionPressed -= TryUseSelectedSkillBook;
            }
        }

        private void ResolveReferences()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (hud == null) hud = GetComponent<PlayerHUD>();
            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            skillUnlockManager = SkillUnlockManager.Resolve(skillUnlockManager);
            skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);
        }

        private void TryUseSelectedSkillBook()
        {
            if (inventory == null || !IsExplorationScene()) return;

            if (catalogManager == null) catalogManager = ItemCatalogManager.Resolve(catalogManager);
            if (skillUnlockManager == null) skillUnlockManager = SkillUnlockManager.Resolve(skillUnlockManager);
            if (catalogManager == null || skillUnlockManager == null) return;

            ItemStack selectedStack = inventory.GetSelectedQuickSlot();
            if (selectedStack == null || selectedStack.IsEmpty) return;

            ItemData selectedItem = catalogManager.FindItemData(selectedStack.itemId);
            if (selectedItem == null || !IsSkillBookCategory(selectedItem.Category)) return;

            string skillId = selectedItem.Skill_ID;
            if (string.IsNullOrEmpty(skillId))
            {
                Debug.LogWarning($"[PlayerSkillBookController] '{selectedItem.ItemName}'({selectedItem.Item_ID})에 Skill_ID가 지정되어 있지 않습니다.", this);
                return;
            }

            // [멤] 이미 배운 스킬북은 조용히 무시한다 (소모/알림 없음).
            if (skillUnlockManager.IsUnlocked(skillId)) return;

            if (!inventory.BeginQuickSlotUse()) return;

            if (!inventory.TryReserveQuickSlotItem(1))
            {
                inventory.EndQuickSlotUse();
                return;
            }

            if (!skillUnlockManager.TryUnlockSkill(skillId))
            {
                inventory.RollbackQuickSlotUse();
                inventory.EndQuickSlotUse();
                return;
            }

            inventory.CommitQuickSlotUse();
            inventory.EndQuickSlotUse();

            if (skillCatalogManager == null) skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);
            SkillData learnedSkill = skillCatalogManager != null ? skillCatalogManager.FindSkillData(skillId) : null;

            if (hud == null) hud = GetComponent<PlayerHUD>();
            hud?.ShowSkillAcquired(learnedSkill);
        }

        private static bool IsSkillBookCategory(ItemCategory category)
            => category == ItemCategory.SkillBook || category == ItemCategory.UltimateSkillBook;

        // [멤] PlayerCapsuleThrowController 등 기존 탐험 전용 컨트롤러와 동일한 씬 이름 규칙(Main_World 포함 여부)을 따른다.
        private static bool IsExplorationScene()
            => SceneManager.GetActiveScene().name.ToLower().Contains("main_world");
    }
}
