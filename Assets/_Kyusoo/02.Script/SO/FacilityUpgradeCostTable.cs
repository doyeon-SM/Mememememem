using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Recipe;

[CreateAssetMenu(fileName = "FacilityUpgradeCostTable", menuName = "KKS/Building/Facility Upgrade Cost Table")]
public class FacilityUpgradeCostTable : ScriptableObject
{
    [Serializable]
    public class Step
    {
        public int GoldCost;
        public List<Recipe_Requset_Item_Data> MaterialCosts = new List<Recipe_Requset_Item_Data>();
    }

    public List<Step> Steps = new List<Step>();
}
