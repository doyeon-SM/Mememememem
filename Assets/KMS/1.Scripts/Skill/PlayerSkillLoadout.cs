using System;
using System.Collections.Generic;
using UnityEngine;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 장전 큐 시스템이 사용하는 4칸 스킬 로드아웃. 슬롯 인덱스(0~3)는 곧 우클릭 장전 단계
    /// (1~4단계)이자 요구 등급(1~4등급)과 1:1로 고정된다 - 슬롯 i는 정확히 (i+1)등급 스킬만
    /// 장착할 수 있다. 5등급 이상 스킬은 이 로드아웃의 대상이 아니다(별도 체계 예정).
    ///
    /// [멤] 이 컴포넌트는 "어떤 스킬이 몇 번 칸에 꽂혀있는지"만 관리한다. 실제 장전 큐/쿨타임/발동
    /// 로직은 플레이어 무기 컨트롤러(다음 단계에서 작업 예정)가 이 로드아웃을 참조해서 처리한다.
    ///
    /// [멤] 저장/불러오기 연동은 SkillUnlockManager와 동일하게 이번 단계 범위에서는 데이터 보관용
    /// 훅(GetEquippedSkillIdsForSave/LoadEquippedSkillIds)만 공개하고, 실제 연동은 뒤 단계에서 진행한다.
    /// </summary>
    public class PlayerSkillLoadout : MonoBehaviour
    {
        public const int SlotCount = 4;

        // [미] 5등급 특수 스킬 전용 칸. 기존 4칸(장전 단계 1:1 대응)과 별개로 관리하여 차지하는 charge-stage 매핑을 건드리지 않는다.
        public const int SpecialSkillGrade = 5;

        [Header("스킬 관련 매니저 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private SkillCatalogManager skillCatalogManager;
        [SerializeField] private SkillUnlockManager skillUnlockManager;

        // 슬롯 i = (i+1)등급 칸. 빈 칸은 null/빈 문자열.
        [Header("등록된 스킬 (칸 0~3 = 1~4등급, 저장/불러오기 연동 대상)")]
        [SerializeField] private string[] equippedSkillIds = new string[SlotCount];

        [Header("특수 스킬 (5등급 전용 1칸, 저장/불러오기 연동 대상)")]
        [SerializeField] private string specialSkillId;

        /// <summary>슬롯 하나가 바뀔 때마다 발행된다(슬롯 인덱스, 새로 등록된 SkillData - 해제면 null).</summary>
        public event Action<int, SkillData> OnSlotChanged;

        /// <summary>특수 스킬 칸이 바뀌었을 때마다 발행된다(새로 등록된 SkillData - 해제면 null).</summary>
        public event Action<SkillData> OnSpecialSlotChanged;

        private void Awake()
        {
            skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);
            skillUnlockManager = SkillUnlockManager.Resolve(skillUnlockManager);

            if (equippedSkillIds == null || equippedSkillIds.Length != SlotCount)
            {
                equippedSkillIds = new string[SlotCount];
            }
        }

        /// <summary>슬롯 인덱스(0~3)가 요구하는 스킬 등급(1~4).</summary>
        public static int GetRequiredGrade(int slotIndex) => slotIndex + 1;

        /// <summary>slotIndex 칸에 등록된 SkillData. 비어있으면 null.</summary>
        public SkillData GetEquippedSkill(int slotIndex)
        {
            if (!IsValidSlot(slotIndex)) return null;

            string skillId = equippedSkillIds[slotIndex];
            if (string.IsNullOrEmpty(skillId)) return null;

            skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);
            return skillCatalogManager != null ? skillCatalogManager.FindSkillData(skillId) : null;
        }

        /// <summary>
        /// skillId를 slotIndex 칸에 장착한다. 다음 조건을 모두 만족해야 성공한다:
        /// 1) 보유한 스킬이어야 함(SkillUnlockManager.IsUnlocked)
        /// 2) 스킬 등급이 그 칸의 요구 등급(GetRequiredGrade)과 정확히 일치해야 함
        /// 이미 다른 칸에 같은 스킬이 꽂혀있으면 그 칸에서는 자동으로 해제한다(중복 장착 방지).
        /// </summary>
        public bool TryEquip(int slotIndex, string skillId)
        {
            if (!IsValidSlot(slotIndex) || string.IsNullOrEmpty(skillId)) return false;

            skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);
            skillUnlockManager = SkillUnlockManager.Resolve(skillUnlockManager);

            if (skillUnlockManager == null || !skillUnlockManager.IsUnlocked(skillId)) return false;

            SkillData data = skillCatalogManager != null ? skillCatalogManager.FindSkillData(skillId) : null;
            if (data == null) return false;

            int requiredGrade = GetRequiredGrade(slotIndex);
            if (data.Grade != requiredGrade)
            {
                Debug.LogWarning($"[PlayerSkillLoadout] 등급 불일치 - 칸 {slotIndex}(요구 {requiredGrade}등급)에 {data.Grade}등급 스킬({skillId})을 장착할 수 없습니다.");
                return false;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (i != slotIndex && string.Equals(equippedSkillIds[i], skillId, StringComparison.Ordinal))
                {
                    equippedSkillIds[i] = null;
                    OnSlotChanged?.Invoke(i, null);
                }
            }

            equippedSkillIds[slotIndex] = skillId;
            OnSlotChanged?.Invoke(slotIndex, data);

            return true;
        }

        /// <summary>slotIndex 칸을 비운다.</summary>
        public void Unequip(int slotIndex)
        {
            if (!IsValidSlot(slotIndex)) return;
            if (string.IsNullOrEmpty(equippedSkillIds[slotIndex])) return;

            equippedSkillIds[slotIndex] = null;
            OnSlotChanged?.Invoke(slotIndex, null);
        }

        /// <summary>특수(5등급) 칸에 등록된 SkillData. 비어있으면 null.</summary>
        public SkillData GetSpecialSkill()
        {
            if (string.IsNullOrEmpty(specialSkillId)) return null;

            skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);
            return skillCatalogManager != null ? skillCatalogManager.FindSkillData(specialSkillId) : null;
        }

        /// <summary>
        /// skillId를 특수(5등급) 칸에 장착한다. 보유한 스킬이어야 하고, 등급이 정확히
        /// SpecialSkillGrade(5)여야 성공한다. 이 칸은 4칸 로드아웃과 별개라 중복 장착 방지 로직이 필요 없다.
        /// </summary>
        public bool TryEquipSpecial(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return false;

            skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);
            skillUnlockManager = SkillUnlockManager.Resolve(skillUnlockManager);

            if (skillUnlockManager == null || !skillUnlockManager.IsUnlocked(skillId)) return false;

            SkillData data = skillCatalogManager != null ? skillCatalogManager.FindSkillData(skillId) : null;
            if (data == null) return false;

            if (data.Grade != SpecialSkillGrade)
            {
                Debug.LogWarning($"[PlayerSkillLoadout] 등급 불일치 - 특수 칸(요구 {SpecialSkillGrade}등급)에 {data.Grade}등급 스킬({skillId})을 장착할 수 없습니다.");
                return false;
            }

            specialSkillId = skillId;
            OnSpecialSlotChanged?.Invoke(data);

            return true;
        }

        /// <summary>특수(5등급) 칸을 비운다.</summary>
        public void UnequipSpecial()
        {
            if (string.IsNullOrEmpty(specialSkillId)) return;

            specialSkillId = null;
            OnSpecialSlotChanged?.Invoke(null);
        }

        /// <summary>[저장/불러오기 연동용] 현재 특수 칸 상태(비어있으면 빈 문자열).</summary>
        public string GetSpecialSkillIdForSave()
        {
            return specialSkillId ?? string.Empty;
        }

        /// <summary>[저장/불러오기 연동용] 세이브 파일에서 불러온 특수 칸 상태를 그대로 주입한다.</summary>
        public void LoadSpecialSkillId(string loadedId)
        {
            specialSkillId = string.IsNullOrEmpty(loadedId) ? null : loadedId;
        }

        
        private static bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount;

        /// <summary>[저장/불러오기 연동용] 현재 로드아웃 상태(칸 0~3 순서, 빈 칸은 빈 문자열).</summary>
        public IReadOnlyList<string> GetEquippedSkillIdsForSave()
        {
            var snapshot = new string[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                snapshot[i] = equippedSkillIds[i] ?? string.Empty;
            }
            return snapshot;
        }

        /// <summary>[저장/불러오기 연동용] 세이브 파일에서 불러온 로드아웃을 그대로 주입한다.</summary>
        public void LoadEquippedSkillIds(IReadOnlyList<string> loadedIds)
        {
            equippedSkillIds = new string[SlotCount];

            if (loadedIds != null)
            {
                for (int i = 0; i < SlotCount && i < loadedIds.Count; i++)
                {
                    equippedSkillIds[i] = string.IsNullOrEmpty(loadedIds[i]) ? null : loadedIds[i];
                }
            }
        }
    }
}
