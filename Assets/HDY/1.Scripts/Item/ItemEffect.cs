using System;

namespace HDY.Item
{
    /// <summary>
    /// UseAction이 Eat인 아이템이 가질 수 있는 단일 효과.
    /// ItemData.EatEffects 리스트로 여러 개를 동시에 가질 수 있음 (확장성 고려).
    /// </summary>
    [Serializable]
    public class ItemEffect
    {
        public EffectType Effect;
        public float Value;
    }
}
