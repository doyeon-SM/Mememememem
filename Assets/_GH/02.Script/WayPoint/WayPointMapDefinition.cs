using UnityEngine;

/// <summary>
/// 한 지역 지도의 표시 정보, 연결 씬, 선행 지도 개방 규칙을 정의하는 데이터입니다.
/// 스테이지와 지도는 일대일 관계가 아니며, 같은 스테이지에 여러 지도 정의를 사용할 수 있습니다.
/// <see cref="sceneName"/>은 Build Settings에 등록된 씬 이름과 정확히 일치해야 합니다.
/// </summary>
[CreateAssetMenu(fileName = "WayPointMap_", menuName = "GH/WayPoint Map")]
public class WayPointMapDefinition : ScriptableObject
{
    [Header("Map Info")]
    public string id;
    public string displayName;
    public Sprite mapSprite;

    [Header("Scene")]
    [Tooltip("Build Settings에 등록된 이 맵의 씬 이름입니다.")]
    public string sceneName;

    [Header("Unlock Rule")]
    [Tooltip("완료 조건 지도가 등록되지 않았을 때 이 지도를 처음부터 사용할지 결정합니다.")]
    public bool unlockedOnStart = true;

    [Tooltip("이 지도가 열리기 전에 모든 웨이포인트가 해금되어야 하는 지도들입니다. 여러 개를 등록하면 전부 완료해야 합니다.")]
    public System.Collections.Generic.List<WayPointMapDefinition> requiredCompletedMaps =
        new System.Collections.Generic.List<WayPointMapDefinition>();

    // 기존 단일 선행 지도 데이터의 직렬화 호환을 위해 유지합니다.
    // 새 데이터는 requiredCompletedMaps를 사용합니다.
    [HideInInspector]
    public WayPointMapDefinition requiredPreviousMap;

    /// <summary>지정 지도의 완료가 이 지도의 개방 조건에 포함되는지 확인합니다.</summary>
    public bool RequiresCompletionOf(WayPointMapDefinition mapDefinition)
    {
        if (mapDefinition == null)
        {
            return false;
        }

        if (requiredPreviousMap == mapDefinition)
        {
            return true;
        }

        return requiredCompletedMaps != null && requiredCompletedMaps.Contains(mapDefinition);
    }
}
