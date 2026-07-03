using UnityEngine;

[CreateAssetMenu(fileName = "WayPoint_", menuName = "GH/WayPoint")]
public class WayPointDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite mapIcon;
    public Vector2 mapPosition;
    public string lockedMessage;
}

