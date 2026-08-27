using System.Text;
using HDY.Item;
using TMPro;
using UnityEngine;

namespace KMS
{
    /// <summary>
    /// [멤] 캐릭터 스탯(힘/지능/민첩/행운/의지 + 파생 스탯)을 텍스트로 확인하는 임시 UI 패널.
    /// 스탯 투자/재분배 버튼은 아직 없고(추후 작업 예정), 이번 패스는 확인 전용이다.
    ///
    /// [멤] 영지/탐험 두 씬에서 같은 프리팹(PlayerCanvas_Root)을 그대로 공유해서 쓴다 - 열고/닫는 것은
    /// 각 씬의 SceneUIManager가 ManagedUIButton(id: "Stat")을 통해 처리하고, 이 컴포넌트는 표시 내용
    /// 갱신만 담당한다(SceneUIManager가 이 오브젝트에 IManagedUIPanel 알림을 주면 OnManagedUIOpened로
    /// 최신값을 다시 그린다).
    ///
    /// [멤] "버프를 받으면 변화량을 바로 확인할 수 있음" 요구사항 - 지금은 버프 시스템 자체가 없어
    /// PlayerCombatStats.StatsChanged가 스탯 투자/리스펙/레벨업 시에만 발행되지만, 나중에 버프 시스템이
    /// 스탯을 바꿀 때 그 이벤트만 같이 발행하도록 연결하면 이 패널은 추가 수정 없이 그대로 반영된다.
    ///
    /// [멤] 공격력/마력: 장착 무기의 DamageType과 일치하는 값만 계산하고, 일치하지 않으면(무기 미장착,
    /// 도구류 장착, 반대 타입 무기 장착) 0으로 표시한다(사용자 확정 사양).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCombatStatsPanelUI : MonoBehaviour, IManagedUIPanel
    {
        [Header("References (비워두면 자동 탐색)")]
        [SerializeField] private PlayerCombatStats combatStats;
        [SerializeField] private KMS.InventoryDuped.PlayerInventory inventory;
        [SerializeField] private ItemCatalogManager catalogManager;

        [Header("표시용 UI 요소")]
        [SerializeField] private TMP_Text statText;

        private bool isSubscribed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>[멤] IManagedUIPanel - SceneUIManager가 이 패널을 실제로 열 때(닫힘->열림) 호출된다.</summary>
        public void OnManagedUIOpened()
        {
            Refresh();
        }

        public void OnManagedUIClosed()
        {
        }

        private void ResolveReferences()
        {
            if (combatStats == null) combatStats = FindFirstObjectByType<PlayerCombatStats>();
            if (inventory == null) inventory = FindFirstObjectByType<KMS.InventoryDuped.PlayerInventory>();
            catalogManager = ItemCatalogManager.Resolve(catalogManager);
        }

        private void Subscribe()
        {
            if (isSubscribed) return;

            if (combatStats != null) combatStats.StatsChanged += HandleChanged;
            if (inventory != null)
            {
                inventory.OnQuickSlotSelectionRequested += HandleQuickSlotChanged;
                inventory.OnSelectedQuickSlotChanged += HandleQuickSlotChanged;
                inventory.OnQuickSlotChanged += HandleQuickSlotChanged;
            }

            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed) return;

            if (combatStats != null) combatStats.StatsChanged -= HandleChanged;
            if (inventory != null)
            {
                inventory.OnQuickSlotSelectionRequested -= HandleQuickSlotChanged;
                inventory.OnSelectedQuickSlotChanged -= HandleQuickSlotChanged;
                inventory.OnQuickSlotChanged -= HandleQuickSlotChanged;
            }

            isSubscribed = false;
        }

        private void HandleQuickSlotChanged(int _)
        {
            Refresh();
        }

        private void HandleChanged()
        {
            Refresh();
        }

        /// <summary>지금 보유한 스탯/장착 무기를 기준으로 표시 텍스트를 다시 계산한다.</summary>
        public void Refresh()
        {
            if (statText == null) return;

            ResolveReferences();

            if (combatStats == null)
            {
                statText.text = "캐릭터 스탯 정보를 찾을 수 없습니다.";
                return;
            }

            statText.text = BuildStatText();
        }

        private string BuildStatText()
        {
            var sb = new StringBuilder();
            (float attackPower, float magicPower) = GetAttackAndMagicPower();

            sb.AppendLine("<b>캐릭터 스탯</b>");
            sb.AppendLine($"힘 {combatStats.Strength}   지능 {combatStats.Intelligence}   민첩 {combatStats.Agility}");
            sb.AppendLine($"행운 {combatStats.Luck}   의지 {combatStats.Willpower}");
            sb.AppendLine($"투자 가능 포인트: {combatStats.UnspentPoints}");
            sb.AppendLine();

            sb.AppendLine("<b>전투</b>");
            sb.AppendLine($"공격력: {attackPower:0}");
            sb.AppendLine($"마력: {magicPower:0}");
            sb.AppendLine($"크리티컬 확률: {combatStats.CritChancePercent:0.#}%");
            sb.AppendLine($"크리티컬 데미지: {combatStats.CritDamagePercent:0.#}%");
            sb.AppendLine($"방어력: {combatStats.DefensePercent:0.#}% (받는 데미지 감소)");
            sb.AppendLine($"저항력: {combatStats.ResistancePercent:0.#}% (받는 디버프 지속시간 감소)");
            sb.AppendLine();

            sb.AppendLine("<b>생존 / 이동 / 채집</b>");
            sb.AppendLine($"체력 배율: {combatStats.GetHealthMultiplier() * 100f:0.#}%");
            sb.AppendLine($"이동속도 보너스: +{(CharacterStatFormulas.MoveSpeedMultiplier(combatStats.Agility) - 1f) * 100f:0.#}%");
            sb.AppendLine($"채집량 보너스: +{(combatStats.LuckGatherAmountMultiplier - 1f) * 100f:0.#}%");

            return sb.ToString();
        }

        /// <summary>
        /// 장착 무기의 DamageType과 일치하는 값만 계산한다(물리 무기 -> 공격력만, 마법 무기 -> 마력만).
        /// 무기가 없거나 타입이 어느 쪽에도 해당하지 않으면 (0, 0)을 반환한다.
        /// </summary>
        private (float attackPower, float magicPower) GetAttackAndMagicPower()
        {
            if (!TryGetSelectedWeapon(out Combat.WeaponItemData weapon))
            {
                return (0f, 0f);
            }

            if (weapon.DamageType == WeaponDamageType.Physical)
            {
                return (combatStats.GetAttackOrMagicPower(weapon.ProjectileDamage, WeaponDamageType.Physical), 0f);
            }

            return (0f, combatStats.GetAttackOrMagicPower(weapon.ProjectileDamage, WeaponDamageType.Magic));
        }

        /// <summary>[멤] PlayerWeaponSkillController.TryGetSelectedWeapon과 동일한 조회 로직(그쪽은 private이라 재사용 불가해 그대로 복제).</summary>
        private bool TryGetSelectedWeapon(out Combat.WeaponItemData weapon)
        {
            weapon = null;

            if (inventory == null || catalogManager == null) return false;

            var selected = inventory.GetSelectedQuickSlot();
            if (selected == null || selected.IsEmpty) return false;

            ItemData itemData = catalogManager.FindItemData(selected.itemId);
            if (itemData == null || itemData.Category != ItemCategory.Weapon) return false;

            weapon = itemData as Combat.WeaponItemData;
            return weapon != null;
        }
    }
}
