using HDY.Item;
using UnityEngine;

namespace KMS.Combat
{
    [CreateAssetMenu(fileName = "Item_Weapon", menuName = "KMS/Combat/Weapon Item Data")]
    public class WeaponItemData : ItemData
    {
        [Header("Melee Attack")]
        [Min(0.1f)] public float AttackDistance = 3f;
        [Min(0f)] public float AttackCooldown = 0.5f;
    }
}
