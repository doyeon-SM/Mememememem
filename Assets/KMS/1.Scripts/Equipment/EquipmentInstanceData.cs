using System;
using System.Collections.Generic;

namespace KMS.Equipment
{
    /// <summary>
    /// [멤] 장비 개체(인스턴스) 하나의 런타임 상태. 도구 대장간의 HDY.Forge.ForgeInstanceData와 같은 역할이지만,
    /// 도구 전용 필드(ToolType/TierIndex/과열)가 장비에는 전혀 맞지 않아 별도 타입으로 뒀다(사용자 확정 사양).
    ///
    /// ItemStack.itemId에는 이 개체를 가리키는 합성 ID("{BaseItemId}@{InstanceId}")가 저장되어, 기존 인벤토리/
    /// 스택 구조를 바꾸지 않고도 개체별로 다른 강화 상태/특수옵션을 구분할 수 있다. 합성 ID의 생성/파싱 규칙은
    /// 도구와 동일해야 하므로 HDY.Forge.ForgeInstanceRegistry의 static 헬퍼를 그대로 재사용한다
    /// (규칙을 두 곳에서 따로 정의하면 반드시 어긋난다).
    ///
    /// [멤] 인스턴스는 "지연 생성"된다 - 순수 Item_ID 상태면 강화 0/옵션 없음으로 동작하고, 강화·연마·특수옵션이
    /// 실제로 생기는 순간에만 만들어진다(도구 Forge와 동일한 방식, 세이브 크기도 불필요하게 늘지 않는다).
    /// </summary>
    [Serializable]
    public class EquipmentInstanceData
    {
        /// <summary>[멤] 장신구가 가질 수 있는 특수옵션 개수. 지금은 1개이지만 이 상수만 늘리면 확장된다.</summary>
        public const int MaxSpecialOptionCount = 1;

        /// <summary>이 개체의 고유 식별자(GUID 문자열). 합성 ID의 '@' 뒷부분과 동일하다.</summary>
        public string InstanceId;

        /// <summary>템플릿 Item_ID (예: armor_head_test). 장비는 승급이 없어 이 값은 바뀌지 않는다.</summary>
        public string BaseItemId;

        /// <summary>이 장비의 부위. 템플릿에서 복사해두어 매번 카탈로그를 조회하지 않아도 되게 한다.</summary>
        public EquipSlotType EquipSlot;

        /// <summary>
        /// 강화 레벨(방어구 전용). 강화 로직 자체는 무기와 함께 추후에 제작할 예정이라, 지금은 항상 0이고
        /// 저장/복원과 표시 경로만 미리 뚫어둔 자리다.
        /// </summary>
        public int EnhanceLevel;

        /// <summary>
        /// 연마로 붙은 옵션(방어구 전용). 강화와 마찬가지로 로직은 추후 제작이며 지금은 항상 비어있다.
        /// 도구의 ForgeRefinementSlotData와 달리 등급(Grade) 개념 없이 스탯 종류/수치만 갖는다.
        /// </summary>
        public List<EquipmentOptionData> RefinementOptions = new List<EquipmentOptionData>();

        /// <summary>
        /// 특수옵션(장신구 전용). 전승 합성으로 옮겨지는 것은 오직 이 목록뿐이며, 기본옵션은
        /// 아이템 종류가 결정하므로(EquipmentItemData.BaseOptionStatType/Value) 여기 들어오지 않는다.
        /// </summary>
        public List<EquipmentOptionData> SpecialOptions = new List<EquipmentOptionData>();

        /// <summary>합성 ID 문자열("{BaseItemId}@{InstanceId}")을 만든다.</summary>
        public string BuildCompositeId()
        {
            return HDY.Forge.ForgeInstanceRegistry.BuildCompositeId(BaseItemId, InstanceId);
        }

        /// <summary>특수옵션 목록을 깊은 복사해서 돌려준다(전승 시 두 개체가 같은 객체를 공유하지 않도록).</summary>
        public List<EquipmentOptionData> CloneSpecialOptions()
        {
            var clone = new List<EquipmentOptionData>();
            if (SpecialOptions == null) return clone;

            foreach (var option in SpecialOptions)
            {
                if (option != null) clone.Add(option.Clone());
            }

            return clone;
        }
    }
}
