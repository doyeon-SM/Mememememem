using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 스킬 등록 UI 전체를 총괄하는 패널 컨트롤러. 열고 닫는 것은 SceneUIManager가
    /// ManagedUIId="Skill"로 이 오브젝트에 SetActive(true/false)를 호출해서 처리한다(ESC 닫기,
    /// 열기/닫기 버튼은 전부 SceneUIManager + ManagedUIButton이 담당하며 이 스크립트는 관여하지
    /// 않는다). 그래서 Awake 1회 초기화가 아니라 OnEnable에서 매번 갱신한다 - 그래야 패널을
    /// 다시 열 때마다 최신 보유 스킬/장착 상태가 반영된다.
    ///
    /// [멤] 등급↔칸이 1:1로 고정되어 있다(1~4등급 = equipSlots[0..3], 5등급 = specialEquipSlot).
    /// 그래서 "칸을 먼저 고르고 스킬을 선택"하는 단계 없이, 보유 스킬 카드 하나를 클릭하면 그
    /// 스킬의 등급이 곧바로 대상 칸을 결정한다:
    ///   1) 대상 칸이 비어있으면 즉시 그 칸에 장착한다.
    ///   2) 대상 칸에 이미 다른 스킬이 있으면 장착하지 않고 정보만 보여준다(자동 교체 없음).
    /// 반대로 장착 칸(equipSlots/specialEquipSlot) 자체를 클릭하면, 비어있지 않은 한 그 칸의
    /// 스킬을 바로 해제한다.
    ///
    /// [멤] UI 레이아웃/디자인(5xN 스크롤뷰 그리드, 정렬 버튼 3개, 장착 칸 5개, 정보 패널)은
    /// 직접 구성할 예정이라 여기서는 로직/훅만 제공한다 - 정렬 버튼은 SortBySkillId/SortByGrade/
    /// SortByFormType 3개 public 메서드를 그대로 onClick에 연결하면 된다.
    /// </summary>
    public class SkillRegistrationPanelUI : MonoBehaviour
    {
        private enum SortMode { BySkillId, ByGrade, ByFormType }

        [Header("데이터 매니저 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private SkillCatalogManager skillCatalogManager;
        [SerializeField] private SkillUnlockManager skillUnlockManager;
        [SerializeField] private PlayerSkillLoadout skillLoadout;
        [SerializeField] private PlayerWeaponSkillController weaponSkillController;

        [Header("보유 스킬 그리드 (5xN 스크롤뷰의 Content를 gridParent로 지정)")]
        [SerializeField] private Transform gridParent;
        [SerializeField] private SkillGridSlotUI gridSlotPrefab;

        [Header("장착 칸 (배열 순서 = 슬롯 인덱스 0~3 = 1~4등급)")]
        [SerializeField] private SkillEquipSlotUI[] equipSlots = new SkillEquipSlotUI[PlayerSkillLoadout.SlotCount];

        [Header("특수 스킬 칸 (5등급 전용 1칸)")]
        [SerializeField] private SkillEquipSlotUI specialEquipSlot;

        [Header("스킬 정보 표시 패널")]
        [SerializeField] private SkillInfoPanelUI infoPanel;

        private SortMode currentSortMode = SortMode.BySkillId;
        private readonly List<SkillGridSlotUI> spawnedGridSlots = new List<SkillGridSlotUI>();
        private string currentlyShownSkillId;

        private void Awake()
        {
            // [멤] 씬 파일에 저장된 활성 상태가 실수로 켜져 있어도(에디터 작업 중 실수로 켠 채 저장 등)
            // 게임 시작 시엔 항상 닫힌 채로 시작해야 한다. 이 패널을 열고 닫는 주체는 SceneUIManager
            // 하나뿐이므로, 최초 활성화 시점(Awake는 딱 한 번만 실행됨)에 스스로를 강제 비활성화한다.
            // 같은 프레임 안에서 모든 오브젝트의 Awake가 SceneUIManager.Start()보다 먼저 끝나기 때문에,
            // SceneUIManager가 시작 시점의 열림 상태를 판단할 때는 이미 닫힌 상태로 정확히 반영된다.
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);
            skillUnlockManager = SkillUnlockManager.Resolve(skillUnlockManager);
            skillLoadout = ResolveLoadout(skillLoadout);
            weaponSkillController = ResolveWeaponController(weaponSkillController);

            SubscribeLoadoutEvents();

            currentlyShownSkillId = null;
            RefreshGrid();
            RefreshEquipSlots();
            infoPanel?.Hide();
        }

        /// <summary>정보 패널에 스킬을 표시하면서, 그리드에서 지금 표시 중인 카드에도 선택됨 강조를 함께 갱신한다.</summary>
        private void ShowSkillInfo(SkillData data)
        {
            infoPanel?.Show(data);
            currentlyShownSkillId = data != null ? data.Skill_ID : null;
            RefreshGridHighlights();
        }

        private void OnDisable()
        {
            UnsubscribeLoadoutEvents();
        }

        private void SubscribeLoadoutEvents()
        {
            if (skillLoadout == null) return;
            skillLoadout.OnSlotChanged += HandleSlotChanged;
            skillLoadout.OnSpecialSlotChanged += HandleSpecialSlotChanged;
        }

        private void UnsubscribeLoadoutEvents()
        {
            if (skillLoadout == null) return;
            skillLoadout.OnSlotChanged -= HandleSlotChanged;
            skillLoadout.OnSpecialSlotChanged -= HandleSpecialSlotChanged;
        }

        private void HandleSlotChanged(int slotIndex, SkillData data)
        {
            RefreshEquipSlots();
            RefreshGridHighlights();
        }

        private void HandleSpecialSlotChanged(SkillData data)
        {
            RefreshEquipSlots();
            RefreshGridHighlights();
        }

        // ------------------------------------------------------------------
        // 정렬 - 사용자가 만들 정렬 버튼 3개가 아래 메서드를 각각 onClick으로 호출하면 된다.
        // ------------------------------------------------------------------

        /// <summary>보유 스킬 그리드를 Skill_ID 오름차순으로 정렬한다.</summary>
        public void SortBySkillId()
        {
            currentSortMode = SortMode.BySkillId;
            RefreshGrid();
        }

        /// <summary>보유 스킬 그리드를 등급(1~5) 오름차순으로 정렬한다.</summary>
        public void SortByGrade()
        {
            currentSortMode = SortMode.ByGrade;
            RefreshGrid();
        }

        /// <summary>보유 스킬 그리드를 형태(즉발형/스택형/버프) 기준으로 정렬한다.</summary>
        public void SortByFormType()
        {
            currentSortMode = SortMode.ByFormType;
            RefreshGrid();
        }

        // ------------------------------------------------------------------
        // 보유 스킬 그리드
        // ------------------------------------------------------------------

        private void RefreshGrid()
        {
            if (gridParent == null || gridSlotPrefab == null) return;

            foreach (Transform child in gridParent) Destroy(child.gameObject);
            spawnedGridSlots.Clear();

            var ownedSkills = GetSortedOwnedSkills();
            foreach (var data in ownedSkills)
            {
                var slot = Instantiate(gridSlotPrefab, gridParent);
                slot.SetupSlot(data, HandleGridSlotClicked);
                spawnedGridSlots.Add(slot);
            }

            RefreshGridHighlights();
        }

        private List<SkillData> GetSortedOwnedSkills()
        {
            var result = new List<SkillData>();
            if (skillUnlockManager == null || skillCatalogManager == null) return result;

            foreach (var skillId in skillUnlockManager.UnlockedSkillIds)
            {
                var data = skillCatalogManager.FindSkillData(skillId);
                if (data != null) result.Add(data);
            }

            switch (currentSortMode)
            {
                case SortMode.BySkillId:
                    result.Sort((a, b) => string.Compare(a.Skill_ID, b.Skill_ID, StringComparison.Ordinal));
                    break;
                case SortMode.ByGrade:
                    result.Sort((a, b) => a.Grade != b.Grade
                        ? a.Grade.CompareTo(b.Grade)
                        : string.Compare(a.Skill_ID, b.Skill_ID, StringComparison.Ordinal));
                    break;
                case SortMode.ByFormType:
                    result.Sort((a, b) => a.FormType != b.FormType
                        ? a.FormType.CompareTo(b.FormType)
                        : string.Compare(a.Skill_ID, b.Skill_ID, StringComparison.Ordinal));
                    break;
            }

            return result;
        }

        /// <summary>보유 스킬 카드를 클릭했을 때 처리한다 - 스킬 등급이 곧 대상 칸을 결정한다.</summary>
        private void HandleGridSlotClicked(SkillData data)
        {
            if (data == null || skillLoadout == null) return;

            ShowSkillInfo(data);

            if (data.Grade == PlayerSkillLoadout.SpecialSkillGrade)
            {
                if (skillLoadout.GetSpecialSkill() == null)
                {
                    skillLoadout.TryEquipSpecial(data.Skill_ID);
                }
                return; // 이미 차있으면 정보만 표시(위에서 이미 처리됨), 자동 교체하지 않는다.
            }

            int slotIndex = data.Grade - 1;
            if (slotIndex < 0 || slotIndex >= PlayerSkillLoadout.SlotCount)
            {
                return; // 1~4, 5등급 외 값은 이번 로드아웃 체계의 대상이 아니다 - 정보만 표시.
            }

            if (skillLoadout.GetEquippedSkill(slotIndex) == null)
            {
                skillLoadout.TryEquip(slotIndex, data.Skill_ID);
            }
            // 이미 차있으면 정보만 표시하고 자동 교체하지 않는다.
        }

        private void RefreshGridHighlights()
        {
            if (skillLoadout == null) return;

            var equippedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < PlayerSkillLoadout.SlotCount; i++)
            {
                var equipped = skillLoadout.GetEquippedSkill(i);
                if (equipped != null) equippedIds.Add(equipped.Skill_ID);
            }

            var special = skillLoadout.GetSpecialSkill();
            if (special != null) equippedIds.Add(special.Skill_ID);

            foreach (var slot in spawnedGridSlots)
            {
                var bound = slot.BoundSkillData;
                slot.SetEquippedHighlight(bound != null && equippedIds.Contains(bound.Skill_ID));
                slot.SetSelectedHighlight(bound != null && currentlyShownSkillId != null && bound.Skill_ID == currentlyShownSkillId);
            }
        }

        // ------------------------------------------------------------------
        // 장착 칸 (1~4등급 + 5등급 특수)
        // ------------------------------------------------------------------

        private void RefreshEquipSlots()
        {
            if (skillLoadout == null) return;

            for (int i = 0; i < equipSlots.Length && i < PlayerSkillLoadout.SlotCount; i++)
            {
                var slotView = equipSlots[i];
                if (slotView == null) continue;

                int capturedIndex = i;
                var data = skillLoadout.GetEquippedSkill(capturedIndex);
                slotView.SetSkill(data, () => HandleEquipSlotClicked(capturedIndex));
            }

            if (specialEquipSlot != null)
            {
                var specialData = skillLoadout.GetSpecialSkill();
                specialEquipSlot.SetSkill(specialData, HandleSpecialEquipSlotClicked);
            }
        }

        /// <summary>1~4등급 장착 칸을 직접 클릭했을 때 - 비어있지 않으면 해제한다.</summary>
        private void HandleEquipSlotClicked(int slotIndex)
        {
            if (skillLoadout == null) return;

            var data = skillLoadout.GetEquippedSkill(slotIndex);
            if (data == null) return;

            ShowSkillInfo(data);

            if (IsChangeLockedByCooldown(data))
            {
                Debug.Log($"[SkillRegistrationPanelUI] 탐험 중 쿨타임 중인 스킬({data.Skill_ID})은 해제할 수 없습니다.");
                return;
            }

            skillLoadout.Unequip(slotIndex);
        }

        /// <summary>5등급 특수 칸을 직접 클릭했을 때 - 비어있지 않으면 해제한다.</summary>
        private void HandleSpecialEquipSlotClicked()
        {
            if (skillLoadout == null) return;

            var data = skillLoadout.GetSpecialSkill();
            if (data == null) return;

            ShowSkillInfo(data);

            if (IsChangeLockedByCooldown(data))
            {
                Debug.Log($"[SkillRegistrationPanelUI] 탐험 중 쿨타임 중인 스킬({data.Skill_ID})은 해제할 수 없습니다.");
                return;
            }

            skillLoadout.UnequipSpecial();
        }

        /// <summary>
        /// PlayerSkillLoadout은 (플레이어 컴포넌트라) SkillCatalogManager/SkillUnlockManager와
        /// 달리 static Instance/Resolve가 없으므로, 여기서 동일한 폴백 패턴을 직접 구현한다.
        /// </summary>
        private static PlayerSkillLoadout ResolveLoadout(PlayerSkillLoadout existing)
        {
            if (existing != null) return existing;
            return FindFirstObjectByType<PlayerSkillLoadout>();
        }

        /// <summary>PlayerSkillLoadout과 마찬가지로 static Instance/Resolve가 없는 플레이어 컴포넌트라 동일하게 직접 탐색한다.</summary>
        private static PlayerWeaponSkillController ResolveWeaponController(PlayerWeaponSkillController existing)
        {
            if (existing != null) return existing;
            return FindFirstObjectByType<PlayerWeaponSkillController>();
        }

        /// <summary>
        /// 탐험 씬에서는 이미 장착된 스킬이 쿨타임 중이면 그 칸을 변경(해제)할 수 없다 - 쿨타임을 피해 스킬을
        /// 빼는 것을 막는 동작이다. 영지(탐험이 아닌 씬)에서는 이 제한을 적용하지 않는다(영지는 전투 자체가
        /// 불가능해 쿨타임 개념이 의미가 없음).
        /// </summary>
        private bool IsChangeLockedByCooldown(SkillData data)
        {
            if (data == null) return false;
            if (!IsExplorationScene()) return false;
            if (weaponSkillController == null) return false;

            return weaponSkillController.IsSkillOnCooldown(data.Skill_ID);
        }

        /// <summary>RecordManager/PlayerHUD와 동일한 씬 판별 관례(씬 이름에 "main_world" 포함 여부)를 그대로 재사용한다.</summary>
        private static bool IsExplorationScene()
        {
            return SceneManager.GetActiveScene().name.ToLower().Contains("main_world");
        }
    }
}
