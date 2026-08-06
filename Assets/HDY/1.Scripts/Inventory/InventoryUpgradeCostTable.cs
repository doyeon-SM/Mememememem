using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Recipe;

namespace HDY.Inventory
{
    /// <summary>
    /// 인벤토리 칸 확장 업그레이드의 단계별 비용표(골드+재료)를 담는 공용 데이터 에셋.
    ///
    /// [공용 데이터인 이유] 인벤토리 업그레이드는 어느 씬에서 실행하든(창고 패널에서 테스트용
    /// PlayerInventory를 업그레이드하든, 실제 플레이어의 인벤토리를 업그레이드하든) 같은 플레이어의
    /// 같은 진행 상태를 다루는 것이다(씬 전환 시 저장->로드로 데이터가 옮겨 탈 뿐, 규칙 자체는 항상
    /// 동일해야 한다). 그래서 비용표를 InventoryUpgrade 컴포넌트가 각자 인스펙터에 들고 있는 대신,
    /// 이 ScriptableObject 에셋 하나로 분리해서 여러 InventoryUpgrade 컴포넌트(Territory_HDY /
    /// Main_World 등)가 함께 참조한다. 에셋은 프로젝트에 단 하나만 만들어서 공유해야 한다 - 각자
    /// 복사본을 만들면 이 분리의 의미가 없어진다.
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryUpgradeCostTable", menuName = "HDY/Inventory/Inventory Upgrade Cost Table")]
    public class InventoryUpgradeCostTable : ScriptableObject
    {
        /// <summary>업그레이드 한 단계에 필요한 골드 + 재료(RecipeRequsetItemData 재사용).</summary>
        [Serializable]
        public class Step
        {
            public int GoldCost;
            public List<Recipe_Requset_Item_Data> MaterialCosts = new List<Recipe_Requset_Item_Data>();
        }

        [Tooltip("시작 칸수 -> 최대 칸수까지, 순서대로 입력. 이 리스트는 모든 InventoryUpgrade 컴포넌트가 함께 참조한다.")]
        public List<Step> Steps = new List<Step>();
    }
}
