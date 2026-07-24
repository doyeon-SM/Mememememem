using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Recipe;

namespace HDY.Inventory
{
    /// <summary>
    /// 창고 확장 업그레이드의 단계별 비용표(골드+재료)를 담는 데이터 에셋.
    /// InventoryUpgradeCostTable과 완전히 동일한 목적/구조다 - 비용표를 WarehouseUpgrade 컴포넌트가
    /// 직접 들고 있지 않고 별도 ScriptableObject 에셋으로 분리해서, 인스펙터에서 값을 바꿀 때 씬을 열지
    /// 않고도 에셋 파일만 열어 수정할 수 있게 하고, 나중에 창고가 여러 씬/인스턴스에 배치되더라도 같은
    /// 비용표 하나를 그대로 공유할 수 있게 한다.
    /// [HDY 요청] 인벤토리 쪽(InventoryUpgradeCostTable)과 Step 구조가 완전히 같지만, 두 업그레이드가
    /// 서로 다른 비용 체계를 가질 수 있어야 해서(창고 확장과 인벤토리 확장은 밸런스가 다를 수 있음)
    /// 일부러 별도 타입으로 분리했다 - 하나로 합치면 두 업그레이드의 비용표 에셋이 실수로 뒤섞일 위험이 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "WarehouseUpgradeCostTable", menuName = "HDY/Inventory/Warehouse Upgrade Cost Table")]
    public class WarehouseUpgradeCostTable : ScriptableObject
    {
        /// <summary>업그레이드 한 단계에 필요한 골드 + 재료(RecipeRequsetItemData 재사용).</summary>
        [Serializable]
        public class Step
        {
            public int GoldCost;
            public List<Recipe_Requset_Item_Data> MaterialCosts = new List<Recipe_Requset_Item_Data>();
        }

        [Tooltip("시작 행 -> 다음 행, 순서대로 입력.")]
        public List<Step> Steps = new List<Step>();
    }
}
