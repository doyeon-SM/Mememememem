using UnityEngine;

/// <summary>
/// <see cref="WayPointDefinition"/>의 실행 중 해금 상태와 현재 씬의 스톤 참조를 보관합니다.
/// 다른 씬에 속한 웨이포인트는 해금 상태가 유지되어도 <see cref="Stone"/>이 null일 수 있습니다.
/// </summary>
public class WayPointRunTime
{
    public WayPointDefinition Definition;
    public bool IsActive;
    public WayPointStone Stone;

    /// <summary>정의에서 가져온 웨이포인트 고유 ID입니다.</summary>
    public string Id => Definition != null ? Definition.id : string.Empty;

    /// <summary>지도 UI에 표시할 웨이포인트 이름입니다.</summary>
    public string DisplayName => Definition != null ? Definition.displayName : string.Empty;

    /// <summary>지정 정의로 잠긴 초기 런타임 상태를 생성합니다.</summary>
    public WayPointRunTime(WayPointDefinition definition)
    {
        Definition = definition;
    }

}
