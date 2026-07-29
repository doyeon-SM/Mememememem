using System.Collections.Generic;
using UnityEngine;

namespace HDY.Mem
{
    /// <summary>
    /// Mem_ID -> 외형(모델 프리팹) 매핑 전용 SO.
    /// MemCatalogManager가 시트(csv)로 멤 데이터를 관리하게 되면서,
    /// GameObject(모델 프리팹) 참조만은 시트에 담을 수 없어 이 테이블에 따로 분리해 관리한다.
    /// (ItemIconTable과 동일한 패턴)
    /// 외형은 자주 바뀌지 않으므로 지금처럼 Inspector에서 드래그 등록하는 방식을 유지한다.
    /// </summary>
    [CreateAssetMenu(fileName = "MemAppearanceTable", menuName = "HDY/Mem/Mem Appearance Table", order = 1)]
    public class MemAppearanceTable : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string Mem_ID;
            public GameObject Prefab;
        }

        [Header("임시 외형 (Mem_ID는 있는데 Prefab이 비어있을 때 대신 채워짐)")]
        [SerializeField] private GameObject fallbackPrefab;

        [Header("Mem_ID -> 외형 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, GameObject> lookup;

        private void BuildLookupIfNeeded()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, GameObject>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Mem_ID)) continue;

                if (!lookup.ContainsKey(entry.Mem_ID))
                {
                    lookup.Add(entry.Mem_ID, entry.Prefab);
                }
                else
                {
                    Debug.LogWarning($"[MemAppearanceTable] Mem_ID가 중복되었습니다: {entry.Mem_ID} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>
        /// Mem_ID에 해당하는 외형(모델 프리팹)을 찾는다.
        /// 목록에 아예 없거나, 목록엔 있지만 Prefab 슬롯이 비어있으면 fallbackPrefab을 대신 반환한다.
        /// fallbackPrefab도 비어있으면 null.
        /// </summary>
        public GameObject GetAppearance(string memId)
        {
            BuildLookupIfNeeded();

            if (!string.IsNullOrEmpty(memId) &&
                lookup.TryGetValue(memId, out var prefab) &&
                prefab != null)
            {
                return prefab;
            }

            return fallbackPrefab;
        }

        /// <summary>fallbackPrefab으로 대체된 것인지(= 원래 외형이 비어있었는지) 확인하고 싶을 때 사용.</summary>
        public bool HasDedicatedAppearance(string memId)
        {
            if (string.IsNullOrEmpty(memId)) return false;

            BuildLookupIfNeeded();

            return lookup.TryGetValue(memId, out var prefab) && prefab != null;
        }

#if UNITY_EDITOR
        /// <summary>에디터 마이그레이션 툴 등에서 항목을 채울 때 사용. 런타임에는 사용하지 않는다.</summary>
        public void EditorSetEntries(List<Entry> newEntries)
        {
            entries = newEntries;
            lookup = null;
        }

        /// <summary>
        /// [HDY 요청 - 아이콘 굽기 도구 연동] 전체 항목을 읽기 전용으로 열람한다.
        /// MemIconBaker가 "어떤 memId들에 모델이 있는지" 목록을 뽑아 굽기 대상 리스트를 만들 때 사용.
        /// </summary>
        public IReadOnlyList<Entry> EditorEntries => entries;
#endif
    }
}
