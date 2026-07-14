using System;

namespace HDY.Recipe
{
    /// <summary>
    /// 레시피에 필요한 재료와 수량을 저장하기 위한 list data
    /// </summary>
    [Serializable]
    public class Recipe_Requset_Item_Data
    {
        public string Item_ID;
        public int Amount;
    }
}

