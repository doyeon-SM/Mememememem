using System.Collections.Generic;
using UnityEngine;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] Skill_ID -> 아이콘(Sprite) 매핑 전용 SO. HDY.Item.ItemIconTable과 동일한 이유로 분리한다 -
    /// 스킬 데이터는 csv 시트로 관리하는데 Sprite 참조는 시트에 담을 수 없기 때문이다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillIconTable", menuName = "KMS/Combat/Skill Icon Table", order = 1)]
    public class SkillIconTable : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string Skill_ID;
            public Sprite Icon;
        }

        [Header("임시 아이콘 (Skill_ID는 있는데 Icon이 비어있을 때 대신 채워짐)")]
        [SerializeField] private Sprite fallbackIcon;

        [Header("Skill_ID -> 아이콘 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Sprite> lookup;

        private void BuildLookupIfNeeded()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, Sprite>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Skill_ID)) continue;

                if (!lookup.ContainsKey(entry.Skill_ID))
                {
                    lookup.Add(entry.Skill_ID, entry.Icon);
                }
                else
                {
                    Debug.LogWarning($"[SkillIconTable] Skill_ID가 중복되었습니다: {entry.Skill_ID} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>
        /// Skill_ID에 해당하는 아이콘을 찾는다.
        /// 목록에 아예 없거나, 목록엔 있지만 Icon 슬롯이 비어있으면 fallbackIcon을 대신 반환한다.
        /// fallbackIcon도 비어있으면 null.
        /// </summary>
        public Sprite GetIcon(string skillId)
        {
            BuildLookupIfNeeded();

            if (!string.IsNullOrEmpty(skillId) &&
                lookup.TryGetValue(skillId, out var sprite) &&
                sprite != null)
            {
                return sprite;
            }

            return fallbackIcon;
        }
    }
}
