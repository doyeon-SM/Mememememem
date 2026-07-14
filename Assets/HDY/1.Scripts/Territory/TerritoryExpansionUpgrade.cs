using HDY.Upgrade;

namespace HDY.Territory
{
    /// <summary>
    /// 영지 확장 한 단계(TerritoryExpansionEntry)를 공용 업그레이드 팝업(UpgradePopupUI)에 연결하는 어댑터.
    /// RecipeUnlockUpgrade와 마찬가지로 대상이 매번 달라질 수 있어(어떤 단계를 선택했는지) MonoBehaviour가
    /// 아니라 일반 클래스이며, GoddessStatueUI가 슬롯을 선택할 때마다 new로 즉석에서 만들어 넘긴다.
    /// 골드는 요구하지 않으므로 GetUpgradeCost()의 GoldCost는 항상 0이다.
    /// </summary>
    public class TerritoryExpansionUpgrade : IUpgradable
    {
        private readonly TerritoryExpansionEntry entry;
        private readonly TerritoryExpansionManager territoryExpansionManager;
        private readonly TerritoryData territoryData;

        public TerritoryExpansionUpgrade(TerritoryExpansionEntry entry, TerritoryExpansionManager territoryExpansionManager, TerritoryData territoryData)
        {
            this.entry = entry;
            this.territoryExpansionManager = territoryExpansionManager;
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

        /// <summary>슬롯 자체가 이미 interactable=false로 걸러지지만, 팝업이 열린 뒤에도 방어적으로 한 번 더 확인한다.</summary>
        public bool CanUpgrade()
        {
            if (territoryExpansionManager == null || entry == null || entry.IsExpanded) return false;
            if (territoryData != null && territoryData.Level < entry.RequestTerritoryLevel) return false;

            return true;
        }

        public UpgradeCost GetUpgradeCost()
        {
            var cost = UpgradeCost.GoldOnly(0);

            foreach (var material in entry.MaterialCosts)
            {
                cost.MaterialCosts.Add(new HDY.Upgrade.UpgradeMaterialCost { Item_ID = material.Item_ID, Amount = material.Amount });
            }

            return cost;
        }

        /// <summary>UpgradePopupUI가 재료 결제를 마친 뒤 호출한다. 여기서는 순수하게 확장 적용만 담당한다.</summary>
        public void ApplyUpgrade()
        {
            territoryExpansionManager?.ApplyExpand(entry);
        }
    }
}
