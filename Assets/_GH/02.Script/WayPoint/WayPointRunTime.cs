using UnityEngine;

public class WayPointRunTime
{
    public WayPointDefinition Definition;
    public bool IsActive;
    public WayPointStone Stone;

    public string Id => Definition != null ? Definition.id : string.Empty;
    public string DisplayName => Definition != null ? Definition.displayName : string.Empty;

    public WayPointRunTime(WayPointDefinition definition)
    {
        Definition = definition;
    }

}
