using System.Collections.Generic;
using UnityEngine;

namespace HDY.Item
{
    /// <summary>
    /// 아이템 데이터(ItemData)를 보관하는 매니저.
    /// Item_ID를 키로 하는 딕셔너리 탐색을 전제로 함.
    /// 현재 단계에서는 데이터 컨테이너 역할만 하며, 로드/탐색 등의 기능은 추후 추가 예정.
    /// 씬에 배치되어 DontDestroyOnLoad로 유지되는 파괴불가 싱글톤.
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
        }

        [Header("아이템 데이터 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<ItemData> itemDataList = new List<ItemData>();

        public IReadOnlyList<ItemData> ItemDataList => itemDataList;

        [Header("Item_ID -> ItemData 딕셔너리 (추후 채워짐)")]
        private Dictionary<string, ItemData> itemDictionary = new Dictionary<string, ItemData>();
    }
}
