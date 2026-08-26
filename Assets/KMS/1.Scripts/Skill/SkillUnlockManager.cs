using System;
using System.Collections.Generic;
using UnityEngine;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 플레이어가 보유한 스킬(Skill_ID) 목록을 관리하는 매니저. HDY.Cook.CookRecipeUnlockManager와
    /// 동일한 원칙 - "존재하는 모든 스킬 정의"는 SkillCatalogManager가, "그중 무엇을 실제로 갖고
    /// 있는지"는 이 매니저가 담당한다.
    ///
    /// [멤] 저장/불러오기 연동(RecordManager/IRecord/SaveData)은 이번 단계 범위에 포함하지 않았다 -
    /// CookRecipeUnlockManager와 동일하게, 연동에 필요한 UnlockedSkillIds / LoadUnlockedSkillIds /
    /// OnSkillUnlocked 3가지만 공개해둔다. 실제 세이브 연동은 뒤 단계에서 별도로 진행한다.
    /// </summary>
    public class SkillUnlockManager : MonoBehaviour
    {
        public static SkillUnlockManager Instance { get; private set; }

        [Header("스킬 카탈로그 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private SkillCatalogManager skillCatalogManager;

        [Header("보유한 스킬 목록 (Skill_ID, 저장/불러오기 연동 대상)")]
        [SerializeField] private List<string> unlockedSkillIds = new List<string>();

        private readonly HashSet<string> unlockedLookup = new HashSet<string>();

        /// <summary>[저장/불러오기 연동용] 지금까지 보유하게 된 스킬(Skill_ID) 전체.</summary>
        public IReadOnlyList<string> UnlockedSkillIds => unlockedSkillIds;

        /// <summary>새 스킬이 보유 목록에 추가될 때마다 발행된다(추가된 SkillData 전달).</summary>
        public event Action<SkillData> OnSkillUnlocked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SkillUnlockManager] 씬에 SkillUnlockManager가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);
            RebuildLookup();
        }

        /// <summary>인스펙터(또는 LoadUnlockedSkillIds)에 채워진 unlockedSkillIds 리스트로부터 조회용 HashSet을 다시 만든다.</summary>
        private void RebuildLookup()
        {
            unlockedLookup.Clear();

            foreach (var id in unlockedSkillIds)
            {
                if (!string.IsNullOrEmpty(id)) unlockedLookup.Add(id);
            }
        }

        /// <summary>이 스킬(Skill_ID)을 보유하고 있는지 여부.</summary>
        public bool IsUnlocked(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return false;
            return unlockedLookup.Contains(skillId);
        }

        /// <summary>
        /// skillId를 보유 목록에 추가한다. 이미 갖고 있거나 카탈로그에 없는 ID면 false를 반환한다
        /// (호출부가 필요하면 그때 지급 실패로 처리).
        /// </summary>
        public bool TryUnlockSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || unlockedLookup.Contains(skillId)) return false;

            skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);
            SkillData data = skillCatalogManager != null ? skillCatalogManager.FindSkillData(skillId) : null;
            if (data == null)
            {
                Debug.LogWarning($"[SkillUnlockManager] 카탈로그에 없는 Skill_ID입니다: {skillId}");
                return false;
            }

            unlockedSkillIds.Add(skillId);
            unlockedLookup.Add(skillId);

            Debug.Log($"[SkillUnlockManager] 스킬 보유: Skill_ID={skillId}");
            OnSkillUnlocked?.Invoke(data);

            return true;
        }

        /// <summary>
        /// [저장/불러오기 연동용] 세이브 파일에서 불러온 보유 목록을 이 매니저에 그대로 주입한다.
        /// unlockedSkillIds와 조회용 HashSet(unlockedLookup)을 한 번에 정합성 있게 교체한다.
        /// </summary>
        public void LoadUnlockedSkillIds(IEnumerable<string> loadedIds)
        {
            unlockedSkillIds.Clear();
            unlockedLookup.Clear();

            if (loadedIds != null)
            {
                foreach (var id in loadedIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (unlockedLookup.Contains(id)) continue;

                    unlockedSkillIds.Add(id);
                    unlockedLookup.Add(id);
                }
            }

            Debug.Log($"[SkillUnlockManager] 저장된 보유 스킬 목록 불러오기 완료: {unlockedSkillIds.Count}개");
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 SkillUnlockManager 참조가 비어있을 때 쓰는 공용 폴백 탐색.
        /// (CookRecipeUnlockManager.Resolve와 동일한 패턴)
        /// </summary>
        public static SkillUnlockManager Resolve(SkillUnlockManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<SkillUnlockManager>();
            if (found == null)
            {
                Debug.LogWarning("[SkillUnlockManager] 씬에서 SkillUnlockManager를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
