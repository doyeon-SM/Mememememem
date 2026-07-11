using UnityEngine;
using UnityEngine.Serialization;

public enum WayPointUnlockType
{
    Interact,
    Start,
    ExternalAction
}

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
    [FormerlySerializedAs("unlockTiming")]
    public WayPointUnlockType unlockType = WayPointUnlockType.Interact;
    public string lockedMessage;

    public bool IsUnlockedOnInitialize => unlockType == WayPointUnlockType.Start;
    public bool CanUnlockByInteraction => unlockType == WayPointUnlockType.Interact;
    public bool CanUnlockByExternalAction => unlockType == WayPointUnlockType.ExternalAction;
}
