using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Recipe;

/// <summary>
/// 음식 창고 슬롯 확장 업그레이드의 단계별 필요 비용(골드+재료)을 저장하는 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "FoodWarehouseUpgradeCostTable", menuName = "KKS/Inventory/Food Warehouse Upgrade Cost Table")]
public class FoodWarehouseUpgradeCostTable : ScriptableObject
{
    [Serializable]
    public class Step
    {
        public int GoldCost;
        public List<Recipe_Requset_Item_Data> MaterialCosts = new List<Recipe_Requset_Item_Data>();
    }

    [Tooltip("1슬롯 확장 단위별 비용을 순서대로 입력하세요.")]
    public List<Step> Steps = new List<Step>();
}
