using KGH.Data;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.Item
{
    /// <summary>
    /// 개별 아이템 정의 SO.
    /// ItemCatalogManager가 Item_ID를 키로 딕셔너리에 로드하여 탐색하는 것을 전제로 함.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "HDY/Item/Item Data", order = 0)]
    public class ItemData : ScriptableObject
    {
        [Header("식별")]
        public string Item_ID;
        public string ItemName;
        public Sprite ItemIcon;
        public CommonClass ItemClass;

        [Header("수량")]
        [Tooltip("아이템 기본/생성 수량")]
        public int Value;
        [Tooltip("아이템 최대 스택 수량")]
        public int MaxStack;

        [Header("구분")]
        public ItemCategory Category;
        public UseAction UseAction;
        public ObjectType ObjectType;

        [Header("크기")]
        [Tooltip("설계도(BluePrint) 등 크기가 있는 아이템의 가로x세로 크기 문자열 (예: \"2x2\"). 없으면 빈 문자열.")]
        public string Size;

        [Header("섭취 효과 (UseAction == Eat 일 때만 사용)")]
        public List<ItemEffect> EatEffects = new List<ItemEffect>();
    }
}
