using UnityEngine;
using UnityEngine.Serialization;

public enum WayPointUnlockType
{
    Interact,
    Start,
    ExternalAction
}

/// <summary>
/// 웨이포인트의 고유 ID, 지도 표시 정보, 소속 맵과 해금 방식을 정의하는 데이터입니다.
/// ID는 저장 데이터와 씬 간 목적지 탐색에 사용되므로 중복되거나 변경되면 안 됩니다.
/// </summary>
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
    [Tooltip("해금된 웨이포인트의 툴팁 Image에 표시할 스프라이트입니다.")]
    public Sprite tooltipIcon;
    public Vector2 mapPosition;

    [Header("Unlock")]
    [FormerlySerializedAs("unlockTiming")]
    public WayPointUnlockType unlockType = WayPointUnlockType.Interact;
    public string lockedMessage;

    public bool IsUnlockedOnInitialize => unlockType == WayPointUnlockType.Start;
    public bool CanUnlockByInteraction => unlockType == WayPointUnlockType.Interact;
    public bool CanUnlockByExternalAction => unlockType == WayPointUnlockType.ExternalAction;
}
