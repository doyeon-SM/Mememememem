using HDY.Territory;
using HDY.Upgrade;

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
    /// [제목/설명 고정] 레시피 해금은 "레벨업"처럼 숫자가 올라가는 개념이 아니라 예/아니오로 끝나는
    /// 단일 동작이라, 제목과 확인 버튼 라벨 모두 "Unlock"으로 고정한다.
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
            return "Unlock";
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
