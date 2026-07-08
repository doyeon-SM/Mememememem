using System;
using System.Collections.Generic;

namespace HDY.Upgrade
{
    /// <summary>
    /// 업그레이드 1회에 소비할 재료(아이템) 하나. Item_ID + 필요 수량으로 표현한다.
    /// Item_ID는 ItemCatalogManager에 등록된 ItemData.Item_ID와 매칭된다.
    /// </summary>
    [Serializable]
    public class UpgradeMaterialCost
    {
        public string Item_ID;
        public int Amount;
    }

    /// <summary>
    /// 업그레이드 1회에 필요한 비용 전체(골드 + 재료 목록).
    /// 공용 업그레이드 팝업(UpgradePopupUI)이 IUpgradable.GetUpgradeCost()를 통해 이 데이터를 받아
    /// 비용 표시/차감을 처리한다. 재료가 필요 없는 업그레이드는 MaterialCosts를 비워두면 된다
    /// (예: 멤창고 페이지 업그레이드는 현재 골드만 사용 -> GoldOnly 헬퍼 사용).
    /// </summary>
    [Serializable]
    public class UpgradeCost
    {
        public int GoldCost;
        public List<UpgradeMaterialCost> MaterialCosts = new List<UpgradeMaterialCost>();

        /// <summary>재료 없이 골드만 필요한 업그레이드 비용을 만든다.</summary>
        public static UpgradeCost GoldOnly(int gold)
        {
            return new UpgradeCost { GoldCost = gold };
        }
    }
}
