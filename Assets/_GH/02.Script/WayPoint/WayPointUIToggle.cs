using UnityEngine;

/// <summary>
/// PlayerHUD와 UI 버튼의 지도 요청을 현재 <see cref="WayPointManager"/>에 전달합니다.
/// 키 입력은 PlayerHUD가 담당하므로 이 컴포넌트에서는 입력을 직접 구독하거나 폴링하지 않습니다.
/// </summary>
public class WayPointUIToggle : MonoBehaviour
{
    [Header("Close Input")]
    [SerializeField] private bool closeMapOnEscape = true;

    private void Update()
    {
        WayPointManager manager = WayPointManager.Instance;
        if (!closeMapOnEscape || manager == null || !manager.IsMapOpen)
        {
            return;
        }

        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            manager.CloseMap();
        }
    }

    /// <summary>탐험 출발 버튼에서 호출해 이동 가능한 지도를 엽니다.</summary>
    public void OpenTravelMap()
    {
        if (!TryGetManager(out WayPointManager manager))
        {
            return;
        }

        manager.OpenTravelMap();
    }

    /// <summary>지도 보기 버튼에서 호출해 보기 전용 지도를 엽니다.</summary>
    public void OpenPreviewMap()
    {
        if (!TryGetManager(out WayPointManager manager))
        {
            return;
        }

        manager.OpenPreviewMap();
    }

    /// <summary>PlayerHUD 또는 버튼에서 호출해 보기 전용 지도를 열거나 닫습니다.</summary>
    public void TogglePreviewMap()
    {
        if (!TryGetManager(out WayPointManager manager))
        {
            return;
        }

        manager.TogglePreviewMap();
    }

    private bool TryGetManager(out WayPointManager manager)
    {
        manager = WayPointManager.Instance;
        if (manager != null)
        {
            return true;
        }

        Debug.LogWarning("[WayPointUIToggle] WayPointManager is missing.", this);
        return false;
    }
}
