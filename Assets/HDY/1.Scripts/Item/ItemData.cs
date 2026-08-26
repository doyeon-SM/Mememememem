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

        // [HDY 요청 - KMS 크로스 승인] 몽둥이 등 도구 내구도 시스템 추가.
        [Header("내구도")]
        [Tooltip("0이면 내구도 없음(소모되지 않는 일반 아이템). 시트의 Durability 컬럼과 매핑된다. " +
                 "이 값은 \"최대\" 내구도(카탈로그 기준값)이며, 아이템 개체별 \"현재\" 내구도는 " +
                 "KMS.InventoryDuped.ItemStack.durability에 슬롯 단위로 저장된다. " +
                 "도구가 멤을 타격해 데미지를 입힐 때마다 1씩 감소한다(PlayerHarvestController 참고).")]
        public int MaxDurability;

        // [멤] 스킬북 / 궁극의 스킬북 전용. 이 아이템을 사용(우클릭)했을 때 획득하는 스킬의 고유 ID.
        // 스킬북이 아닌 아이템은 빈 문자열로 둔다. ItemCatalog.csv의 선택적 트레일링 컬럼에서 파싱된다.
        [Header("스킬 (SkillBook / UltimateSkillBook 전용)")]
        [Tooltip("이 스킬북을 사용했을 때 획득할 스킬의 Skill_ID. 스킬북 카테고리가 아니면 사용되지 않는다.")]
        public string Skill_ID;

        [Header("섭취 효과 (UseAction == Eat 일 때만 사용)")]
        public List<ItemEffect> EatEffects = new List<ItemEffect>();
    }
}
