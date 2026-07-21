using System;

namespace HDY.Forge
{
    /// <summary>
    /// 강화가 시도된 개별 도구 개체(인스턴스)의 런타임 상태.
    /// ItemStack.itemId(KMS 소유, 수정하지 않음)에는 이 인스턴스를 가리키는 합성 ID
    /// ("{BaseItemId}@{InstanceId}")가 그대로 저장되어, 기존 인벤토리/스택 구조를 바꾸지 않고도
    /// 개체별로 다른 강화 상태를 구분할 수 있다.
    /// </summary>
    [Serializable]
    public class ForgeInstanceData
    {
        /// <summary>이 개체의 고유 식별자(GUID 문자열). 합성 ID의 '@' 뒷부분과 동일하다.</summary>
        public string InstanceId;

        /// <summary>현재 티어의 실제 템플릿 Item_ID (예: tool_axe). 승급 성공 시 다음 티어 ID로 갱신된다.</summary>
        public string BaseItemId;

        /// <summary>도구 종류.</summary>
        public ForgeToolType ToolType;

        /// <summary>현재 티어 번호 (1부터 시작).</summary>
        public int TierIndex;

        /// <summary>현재 강화 레벨 (0~10). 강화가 불가능한 도구(예: 괭이)는 항상 0으로 유지된다.</summary>
        public int EnhanceLevel;

        /// <summary>모루 과열 수치 (0~1, 1 = 100%). 이 개체 전용으로 개별 관리되며, 강화/승급 성공 시 0으로 초기화된다.</summary>
        public float OverheatPercent;

        /// <summary>합성 ID 문자열("{BaseItemId}@{InstanceId}")을 만든다.</summary>
        public string BuildCompositeId()
        {
            return ForgeInstanceRegistry.BuildCompositeId(BaseItemId, InstanceId);
        }
    }
}
