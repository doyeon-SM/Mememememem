using System.Collections.Generic;
using UnityEngine;

namespace KMS
{
    [CreateAssetMenu(fileName = "HeldItemPrefabTable", menuName = "KMS/Item/Held Item Prefab Table")]
    public sealed class HeldItemPrefabTable : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string itemId;
            public GameObject prefab;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, GameObject> lookup;

        public GameObject GetPrefab(string itemId)
        {
            BuildLookupIfNeeded();

            if (string.IsNullOrEmpty(itemId)) return null;
            return lookup.TryGetValue(itemId, out GameObject prefab) ? prefab : null;
        }

        private void BuildLookupIfNeeded()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, GameObject>();
            foreach (Entry entry in entries)
            {
                if (string.IsNullOrEmpty(entry.itemId)) continue;

                if (!lookup.ContainsKey(entry.itemId))
                {
                    lookup.Add(entry.itemId, entry.prefab);
                }
                else
                {
                    Debug.LogWarning(
                        $"[HeldItemPrefabTable] Item_ID가 중복되었습니다: {entry.itemId} " +
                        "(먼저 등록된 항목을 유지합니다)",
                        this);
                }
            }
        }

#if UNITY_EDITOR
        public void EditorSetEntries(List<Entry> newEntries)
        {
            entries = newEntries ?? new List<Entry>();
            lookup = null;
        }
#endif
    }
}
