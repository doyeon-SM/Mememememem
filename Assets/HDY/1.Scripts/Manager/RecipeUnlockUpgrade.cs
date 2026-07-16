using HDY.Territory;
using HDY.Upgrade;
using UnityEngine;

namespace HDY.Recipe
{
    /// <summary>
    /// 레시피 하나(RecipeUnlockEntry)의 해금을 공용 업그레이드 팝업(UpgradePopupUI)에 연결하는 어댑터.
    ///
    /// [다른 IUpgradable 구현체와의 차이] MemStorageUpgrade/WarehouseUpgrade는 씬에 하나만 배치되는
    /// MonoBehaviour(대상이 "창고"처럼 항상 하나)였지만, 레시피는 목록 중 어떤 걸 선택했는지에 따라
    /// 대상이 매번 달라진다. 그래서 이 클래스는 MonoBehaviour가 아니라 일반 클래스이며,
    /// GoddessStatueUI가 슬롯을 선택할 때마다 new로 즉석에서 만들어 UpgradePopupUI.Show()에 넘긴다.
    ///
    /// [제목 = 아이템 이름] 팝업 제목(GetUpgradeTitle)에는 이 레시피가 해금하는 아이템의 실제 이름
    /// (ItemData.ItemName)을 표시한다 - recipeUnlockManager.FindRecipeItemData(entry.Item_ID)로 카탈로그에서
    /// 조회한다. 카탈로그에 등록이 안 돼 있는 등 못 찾으면 경고 로그를 남기고 Item_ID를 그대로 대신 표시한다
    /// (팝업 자체가 빈 제목으로 뜨는 것보다는 낫다).
    ///
    /// [확인 버튼 라벨 고정] 레시피 해금은 "레벨업"처럼 숫자가 올라가는 개념이 아니라 예/아니오로 끝나는
    /// 단일 동작이라, 확인 버튼 라벨(GetUpgradeDescription)은 "Unlock"으로 고정한다.
    ///
    /// [재료/골드 표시는 팝업이 범용으로 처리] GetUpgradeCost()가 반환하는 GoldCost/MaterialCosts만 채워주면,
    /// 재료 스크롤 뷰 표시 여부·골드 텍스트 표시 여부는 UpgradePopupUI.RefreshDisplay가 알아서 처리한다
    /// (재료가 하나도 없으면 스크롤 뷰를 꺼버리고, 골드가 0이면 골드 텍스트를 꺼버림).
    /// </summary>
    public class RecipeUnlockUpgrade : IUpgradable
    {
        private readonly RecipeUnlockEntry entry;
        private readonly RecipeUnlockManager recipeUnlockManager;
        private readonly TerritoryData territoryData;

        public RecipeUnlockUpgrade(RecipeUnlockEntry entry, RecipeUnlockManager recipeUnlockManager, TerritoryData territoryData)
        {
            this.entry = entry;
            this.recipeUnlockManager = recipeUnlockManager;
            this.territoryData = territoryData;
        }

        public string GetUpgradeTitle()
        {
            var itemData = recipeUnlockManager != null ? recipeUnlockManager.FindRecipeItemData(entry.Item_ID) : null;

            if (itemData == null || string.IsNullOrEmpty(itemData.ItemName))
            {
                Debug.LogWarning($"[RecipeUnlockUpgrade] Item_ID '{entry.Item_ID}'에 해당하는 ItemData(또는 ItemName)를 찾을 수 없어 Item_ID를 그대로 표시합니다.");
                return entry.Item_ID;
            }

            return itemData.ItemName;
        }

        public string GetUpgradeDescription()
        {
            return "Unlock";
        }

        /// <summary>
        /// 슬롯 자체가 이미 interactable=false로 걸러지지만(레벨 미달/이미 해금), 팝업이 열린 뒤에도
        /// 방어적으로 한 번 더 확인한다.
        /// </summary>
        public bool CanUpgrade()
        {
            if (entry == null || entry.IsUnlocked) return false;
            if (territoryData != null && territoryData.Level < entry.RequestTerritoryLevel) return false;

            return true;
        }

        public UpgradeCost GetUpgradeCost()
        {
            var cost = new UpgradeCost { GoldCost = entry.RequestGold };

            foreach (var material in entry.MaterialCosts)
            {
                cost.MaterialCosts.Add(new UpgradeMaterialCost { Item_ID = material.Item_ID, Amount = material.Amount });
            }

            return cost;
        }

        /// <summary>UpgradePopupUI가 결제(골드+재료)를 마친 뒤 호출한다. 여기서는 순수하게 해금 상태만 반영한다.</summary>
        public void ApplyUpgrade()
        {
            recipeUnlockManager?.ApplyUnlock(entry.Item_ID);
        }
    }
}
