using System;
using System.Collections.Generic;
using UnityEngine;

namespace KMS.InventoryDuped
{

[Serializable]
public class ItemEffectData
{
    public ItemEffectType type;
    public int amount;

    public bool isOverTime;
    public float duration;
    public float tickInterval;
}

[CreateAssetMenu(fileName = "New Item Definition", menuName = "Scriptable Objects/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header ("기본 설정")]
    public string itemId;
    public string displayName;
    public Sprite icon;
    public int value; // 아이템 효과로 분리되었기 때문에 삭제하거나 다른 값으로 이용
    public int maxStack = 1;

    [Header ("아이템 구분")]
    public ItemCategory category;
    public ItemHoldType holdType;
    public ItemUseAction useAction;

    [Header ("모델링")]
    public GameObject modelPrefab;

    [Header ("아이템 효과")]
    public List<ItemEffectData> effects = new List<ItemEffectData>();


    // 이 아이템의 해당 타입의 효과값을 반환해주는 도움 함수
    // int hunger = item.GetEffectAmount(ItemEffectType.Hunger); 라는 식으로 접근해서 수치를 할 수 있음
    public int GetEffectAmount(ItemEffectType type)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            ItemEffectData effect = effects[i];

            if (effect != null && effect.type == type)
            {
                return effect.amount;
            }
        }

        return 0;
    }
}

}
