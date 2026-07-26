using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하나의 스테이지와 그 스테이지에 속한 기본 지도 및 방문형 하위 지도를 정의합니다.
/// </summary>
[CreateAssetMenu(fileName = "WayPointStage_", menuName = "GH/WayPoint Stage")]
public class WayPointStageDefinition : ScriptableObject
{
    [Header("Stage Info")]
    public string id;
    public string displayName;

    [Header("Maps In This Stage")]
    [Tooltip("같은 스테이지에 속한 지도들입니다. 지상, 동굴처럼 씬별 지도를 모두 등록합니다.")]
    public List<WayPointMapDefinition> maps = new List<WayPointMapDefinition>();
    [Tooltip("스테이지를 대표하는 기본 지역 지도입니다. 방문 여부와 관계없이 지도 목록에 표시됩니다.")]
    public WayPointMapDefinition defaultMap;

    [Header("Unlock Rule")]
    [Tooltip("완료 조건 지도가 없을 때 이 스테이지를 처음부터 열지 결정합니다.")]
    public bool unlockedOnStart = true;
    [Tooltip("이 스테이지가 열리기 전에 모든 웨이포인트가 해금되어야 하는 지도들입니다. 여러 개면 전부 완료해야 합니다.")]
    public List<WayPointMapDefinition> requiredCompletedMaps = new List<WayPointMapDefinition>();

    /// <summary>지정 지도가 이 스테이지에 속하는지 확인합니다.</summary>
    public bool ContainsMap(WayPointMapDefinition mapDefinition)
    {
        return mapDefinition != null && maps != null && maps.Contains(mapDefinition);
    }

    /// <summary>명시된 기본 지도 또는 목록의 첫 번째 유효 지도를 반환합니다.</summary>
    public WayPointMapDefinition GetDefaultMap()
    {
        if (defaultMap != null && ContainsMap(defaultMap))
        {
            return defaultMap;
        }

        if (maps == null)
        {
            return null;
        }

        foreach (WayPointMapDefinition mapDefinition in maps)
        {
            if (mapDefinition != null)
            {
                return mapDefinition;
            }
        }

        return null;
    }

    /// <summary>지정 지도가 이 스테이지의 기본 지도인지 확인합니다.</summary>
    public bool IsDefaultMap(WayPointMapDefinition mapDefinition)
    {
        return mapDefinition != null && GetDefaultMap() == mapDefinition;
    }

    /// <summary>지정 지도의 완료가 이 스테이지 개방 조건에 포함되는지 확인합니다.</summary>
    public bool RequiresCompletionOf(WayPointMapDefinition mapDefinition)
    {
        return mapDefinition != null
            && requiredCompletedMaps != null
            && requiredCompletedMaps.Contains(mapDefinition);
    }
}
