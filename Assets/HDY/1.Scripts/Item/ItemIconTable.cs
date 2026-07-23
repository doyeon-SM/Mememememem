using System.Collections.Generic;
using UnityEngine;

namespace HDY.Item
{
    /// <summary>
    /// Item_ID -> 아이콘(Sprite) 매핑 전용 SO.
    /// ItemCatalogManager가 시트(csv/tsv)로 아이템 데이터를 관리하게 되면서,
    /// Sprite 참조만은 시트에 담을 수 없어 이 테이블에 따로 분리해 관리한다.
    /// 아이콘은 자주 바뀌지 않으므로 지금처럼 Inspector에서 드래그 등록하는 방식을 유지한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemIconTable", menuName = "HDY/Item/Item Icon Table", order = 1)]
    public class ItemIconTable : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string Item_ID;
            public Sprite Icon;
        }

        [Header("임시 아이콘 (Item_ID는 있는데 Icon이 비어있을 때 대신 채워짐)")]
        [SerializeField] private Sprite fallbackIcon;

        [Header("Item_ID -> 아이콘 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Sprite> lookup;

        private void BuildLookupIfNeeded()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, Sprite>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Item_ID)) continue;

                if (!lookup.ContainsKey(entry.Item_ID))
                {
                    lookup.Add(entry.Item_ID, entry.Icon);
                }
                else
                {
                    Debug.LogWarning($"[ItemIconTable] Item_ID가 중복되었습니다: {entry.Item_ID} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>
        /// Item_ID에 해당하는 아이콘을 찾는다.
        /// 목록에 아예 없거나, 목록엔 있지만 Icon 슬롯이 비어있으면 fallbackIcon을 대신 반환한다.
        /// fallbackIcon도 비어있으면 null.
        /// </summary>
        public Sprite GetIcon(string itemId)
        {
            BuildLookupIfNeeded();

            if (!string.IsNullOrEmpty(itemId) &&
                lookup.TryGetValue(itemId, out var sprite) &&
                sprite != null)
            {
                return sprite;
            }

            return fallbackIcon;
        }

        /// <summary>fallbackIcon으로 대체된 것인지(= 원래 아이콘이 비어있었는지) 확인하고 싶을 때 사용.</summary>
        public bool HasDedicatedIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            BuildLookupIfNeeded();

            return lookup.TryGetValue(itemId, out var sprite) && sprite != null;
        }

#if UNITY_EDITOR
        /// <summary>에디터 마이그레이션 툴 등에서 항목을 채울 때 사용. 런타임에는 사용하지 않는다.</summary>
        public void EditorSetEntries(List<Entry> newEntries)
        {
            entries = newEntries;
            lookup = null;
        }
#endif
    }
}
