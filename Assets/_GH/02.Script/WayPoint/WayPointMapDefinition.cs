using UnityEngine;

/// <summary>
/// 한 스테이지 지도의 표시 정보, 연결 씬, 선행 지도 개방 규칙을 정의하는 데이터입니다.
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
    public bool unlockedOnStart = true;
    public WayPointMapDefinition requiredPreviousMap;
}
