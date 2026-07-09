using UnityEngine;

[CreateAssetMenu(fileName = "WayPointMap_", menuName = "GH/WayPoint Map")]
public class WayPointMapDefinition : ScriptableObject
{
    [Header("Map Info")]
    public string id;
    public string displayName;
    public Sprite mapSprite;

    [Header("Unlock Rule")]
    public bool unlockedOnStart = true;
    public WayPointMapDefinition requiredPreviousMap;
}
