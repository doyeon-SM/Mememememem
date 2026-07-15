using KMS;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 지도 단축키와 UI 버튼 요청을 현재 <see cref="WayPointManager"/>에 전달합니다.
/// </summary>
public class WayPointUIToggle : MonoBehaviour
{
        [Header("Input")]
        [SerializeField] private KeyCode legacyToggleKey = KeyCode.M;
/*    [Header("Ref")]
    [SerializeField] private PlayerInput playerInput;*/

    private void OnEnable()
    {
        //if (playerInput == null) return;
        
    }
    private void Update()
    {
        if (!WasTogglePressed())
        {
            return;
        }

        if (!TryGetManager(out WayPointManager manager))
        {
            return;
        }

        manager.ToggleShortcutMap();
    }

    /// <summary>탐험 출발 Button의 OnClick에서 호출해 웨이포인트 이동 지도를 엽니다.</summary>
    public void OpenTravelMap()
    {
        if (!TryGetManager(out WayPointManager manager))
        {
            return;
        }

        manager.OpenTravelMap();
        
    }

    /// <summary>지도 보기 Button의 OnClick에서 호출해 보기 전용 지도를 엽니다.</summary>
    public void OpenPreviewMap()
    {
        if (!TryGetManager(out WayPointManager manager))
        {
            return;
        }

        manager.OpenPreviewMap();
    }

    private bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.mKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(legacyToggleKey);
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
