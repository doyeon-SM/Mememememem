using System.Collections.Generic;
using UnityEngine;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] ProjectileId(문자열) -> 투사체 Prefab 매핑 전용 SO.
    /// 무기 기본 공격(WeaponCatalog.csv의 ProjectileId)과 스킬(SkillCatalog.csv의 ProjectileId)이
    /// 이 하나의 테이블을 공유해서 조회한다 - "스킬은 어떤 무기를 쓰든 항상 같은 효과"라는 요구사항
    /// 때문에 스킬의 투사체는 무기가 아니라 이 테이블에서 스킬 자신의 ProjectileId로 직접 조회한다.
    /// Prefab 참조는 csv에 담을 수 없어서 ItemIconTable/SkillIconTable과 동일하게 별도 SO로 분리했다.
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectilePrefabTable", menuName = "KMS/Combat/Projectile Prefab Table")]
    public class ProjectilePrefabTable : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string ProjectileId;
            public GameObject Prefab;
        }

        [Header("ProjectileId -> 투사체 Prefab 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, GameObject> lookup;

        private void BuildLookupIfNeeded()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, GameObject>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.ProjectileId)) continue;

                if (!lookup.ContainsKey(entry.ProjectileId))
                {
                    lookup.Add(entry.ProjectileId, entry.Prefab);
                }
                else
                {
                    Debug.LogWarning($"[ProjectilePrefabTable] ProjectileId가 중복되었습니다: {entry.ProjectileId} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>ProjectileId로 투사체 Prefab을 찾는다. 목록에 없거나 비어있으면 null.</summary>
        public GameObject GetPrefab(string projectileId)
        {
            BuildLookupIfNeeded();

            if (!string.IsNullOrEmpty(projectileId) && lookup.TryGetValue(projectileId, out var prefab))
            {
                return prefab;
            }

            return null;
        }
    }
}
