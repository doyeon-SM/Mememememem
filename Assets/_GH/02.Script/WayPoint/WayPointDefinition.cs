using UnityEngine;

[CreateAssetMenu(fileName = "WayPoint_", menuName = "GH/WayPoint")]
public class WayPointDefinition : ScriptableObject
{
    [Header("Info")]
    public string id;
    public string displayName;
    [TextArea]
    public string tooltipDescription;

    [Header("Map")]
    public WayPointMapDefinition mapDefinition;
    public Sprite unlockMapIcon;
    public Sprite activeMapIcon;
    public Vector2 mapPosition;

    [Header("Unlock")]
    public bool unlockedOnStart;
    public string lockedMessage;
}
