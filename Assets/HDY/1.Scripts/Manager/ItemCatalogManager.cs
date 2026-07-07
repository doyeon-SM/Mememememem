using System.Collections.Generic;
using UnityEngine;

namespace HDY.Item
{
    /// <summary>
    /// 아이템 데이터(ItemData)를 보관하는 매니저.
    /// Item_ID를 키로 하는 딕셔너리 탐색을 전제로 함.
    /// 씬에 배치되어 DontDestroyOnLoad로 유지되는 파괴불가 싱글톤 (ItemCatalogManager는 계속 싱글톤 유지).
    /// </summary>
    public class ItemCatalogManager : MonoBehaviour
    {
        public static ItemCatalogManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildDictionary();
        }

        [Header("아이템 데이터 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<ItemData> itemDataList = new List<ItemData>();

        public IReadOnlyList<ItemData> ItemDataList => itemDataList;

        [Header("Item_ID -> ItemData 딕셔너리")]
        private Dictionary<string, ItemData> itemDictionary = new Dictionary<string, ItemData>();

        /// <summary>itemDataList를 Item_ID 기준으로 딕셔너리에 채운다. Item_ID가 중복되면 먼저 등록된 항목을 유지한다.</summary>
        private void BuildDictionary()
        {
            itemDictionary.Clear();

            foreach (var data in itemDataList)
            {
                if (data == null || string.IsNullOrEmpty(data.Item_ID)) continue;

                if (!itemDictionary.ContainsKey(data.Item_ID))
                {
                    itemDictionary.Add(data.Item_ID, data);
                }
                else
                {
                    Debug.LogWarning($"[ItemCatalogManager] Item_ID가 중복되었습니다: {data.Item_ID} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>Item_ID로 ItemData를 찾는다. 목록에 없으면 null.</summary>
        public ItemData FindItemData(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            return itemDictionary.TryGetValue(itemId, out var data) ? data : null;
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 ItemCatalogManager 참조가 비어있을 때 공용으로 쓰는 폴백 탐색.
        /// 1) 이미 참조가 있으면 그대로 반환, 2) 없으면 싱글톤(Instance), 3) 그래도 없으면 씬 전체에서 검색.
        /// (RecipeUnlockManager, GoddessStatueUI 등 여러 곳에서 동일한 폴백 로직을 반복하지 않기 위한 헬퍼)
        /// </summary>
        public static ItemCatalogManager Resolve(ItemCatalogManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<ItemCatalogManager>();
            if (found == null)
            {
                Debug.LogWarning("[ItemCatalogManager] 씬에서 ItemCatalogManager를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
