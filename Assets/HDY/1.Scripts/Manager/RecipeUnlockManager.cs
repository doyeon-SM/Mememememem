using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Item;
using HDY.Territory;

namespace HDY.Recipe
{
    /// <summary>
    /// 제작법 하나(Item_ID)와 해금 조건/여부를 짝지어 보관하는 항목.
    /// Item_ID는 ItemCatalogManager에 등록된 ItemData.Item_ID와 매칭된다(SO를 직접 들고 있지 않음).
    /// RequestTerritoryLevel: 이 레시피를 해금하기 위해 필요한 영지 레벨(기본값 1).
    /// RequestGold: 여신상 UI에서 이 레시피를 구매할 때 필요한 골드.
    /// IsUnlocked: 실제 해금 여부(두 조건을 모두 만족해야 true가 됨).
    /// </summary>
    [Serializable]
    public class RecipeUnlockEntry
    {
        [Tooltip("ItemCatalogManager에 등록된 ItemData.Item_ID와 매칭되는 값")]
        public string Item_ID;

        [Tooltip("이 레시피를 해금하기 위해 필요한 영지 레벨")]
        public int RequestTerritoryLevel = 1;

        [Tooltip("여신상 UI에서 이 레시피를 구매할 때 필요한 골드")]
        public int RequestGold;

        public bool IsUnlocked;
    }

    /// <summary>
    /// 영지에서 사용 가능한 제작법의 해금 여부를 보관하는 매니저.
    /// 제작법은 ItemData(SO)를 직접 참조하지 않고 Item_ID(string)로 관리하며, 실제 아이템 정보가 필요하면
    /// ItemCatalogManager에서 탐색한다.
    ///
    /// [해금 조건] 레시피 해금은 두 조건을 모두 만족해야 한다: (1) 영지 레벨이 RequestTerritoryLevel 이상
    /// (영지 레벨은 TerritoryData.Level), (2) 여신상 UI에서 RequestGold만큼 골드를 지불하고 구매.
    /// TryPurchase가 두 조건을 확인하고 골드 차감 + IsUnlocked 반영까지 한 번에 처리한다.
    ///
    /// [저장/불러오기 대응] TerritoryData와 함께 저장/불러오기 시스템이 이 매니저의 생명주기를 직접 관리할
    /// 예정이라, 더 이상 DontDestroyOnLoad 싱글톤을 쓰지 않는다(일반 컴포넌트).
    /// </summary>
    public class RecipeUnlockManager : MonoBehaviour
    {
        [Header("아이템 카탈로그 참조 (Item_ID -> ItemData 탐색용, 비어있으면 자동 탐색)")]
        [SerializeField] private ItemCatalogManager itemCatalogManager;

        private void Awake()
        {
            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);
        }

        [Header("제작법 해금 목록 (인스펙터에서 Item_ID + 요구 레벨/골드 + 해금여부를 함께 등록/확인)")]
        [SerializeField] private List<RecipeUnlockEntry> recipeUnlocks = new List<RecipeUnlockEntry>();

        public IReadOnlyList<RecipeUnlockEntry> RecipeUnlocks => recipeUnlocks;

        /// <summary>해당 제작법(Item_ID)의 해금 여부를 반환한다. 목록에 없으면 false.</summary>
        public bool IsUnlocked(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            return entry != null && entry.IsUnlocked;
        }

        /// <summary>해당 제작법(Item_ID)을 강제로 해금 처리한다(디버그/치트용). 목록에 없으면 새 항목을 추가해 해금 처리.</summary>
        public void Unlock(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            if (entry != null)
            {
                entry.IsUnlocked = true;
            }
            else
            {
                recipeUnlocks.Add(new RecipeUnlockEntry { Item_ID = itemId, RequestTerritoryLevel = 1, IsUnlocked = true });
            }

            Debug.Log($"[RecipeUnlockManager] 레시피 강제 해금: Item_ID={itemId}");
        }

        /// <summary>해당 제작법(Item_ID)을 다시 잠금 처리한다. 목록에 없으면 아무 동작도 하지 않는다.</summary>
        public void Lock(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            if (entry != null)
            {
                entry.IsUnlocked = false;
            }
        }

        /// <summary>해당 제작법(Item_ID)의 요구 영지 레벨을 반환한다. 목록에 없으면 기본값 1.</summary>
        public int GetRequiredTerritoryLevel(string itemId)
        {
            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            return entry != null ? entry.RequestTerritoryLevel : 1;
        }

        /// <summary>
        /// 해당 레시피가 지금 해금을 "시도"할 수 있는 상태인지 확인한다 - 영지 레벨 조건을 만족하고
        /// 아직 해금되지 않은 상태여야 true. 여신상 UI에서 슬롯의 상호작용 가능 여부를 결정하는 데 쓰인다.
        /// </summary>
        public bool CanAttemptUnlock(string itemId, int currentTerritoryLevel)
        {
            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            if (entry == null || entry.IsUnlocked) return false;

            return currentTerritoryLevel >= entry.RequestTerritoryLevel;
        }

        /// <summary>
        /// 영지 레벨 조건과 골드를 모두 확인해서 실제 구매(해금)를 시도한다.
        /// 조건을 만족하지 못하거나(레벨 부족/이미 해금됨) 골드가 부족하면 아무 것도 바뀌지 않고 false를 반환한다.
        /// 성공하면 골드가 차감되고 IsUnlocked가 true로 바뀐다.
        /// </summary>
        public bool TryPurchase(string itemId, TerritoryData territoryData)
        {
            if (territoryData == null) return false;

            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            if (entry == null || entry.IsUnlocked) return false;
            if (territoryData.Level < entry.RequestTerritoryLevel) return false;
            if (!territoryData.TrySpendGold(entry.RequestGold)) return false;

            entry.IsUnlocked = true;
            Debug.Log($"[RecipeUnlockManager] 레시피 구매 완료: Item_ID={itemId}, 지불 골드={entry.RequestGold}");
            return true;
        }

        /// <summary>Item_ID로 실제 ItemData(SO)를 카탈로그에서 찾는다. 여신상 UI에서 아이콘 표시 등에 사용.</summary>
        public ItemData FindRecipeItemData(string itemId)
        {
            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);
            return itemCatalogManager != null ? itemCatalogManager.FindItemData(itemId) : null;
        }
    }
}
