using System;

namespace Mem.Shop
{
    /// <summary>
    /// 재료로 지불/획득할 때 사용되는 (Item_ID, 수량) 쌍.
    /// </summary>
    [Serializable]
    public class MaterialCost
    {
        public string Item_ID;
        public int Amount;
    }
}
