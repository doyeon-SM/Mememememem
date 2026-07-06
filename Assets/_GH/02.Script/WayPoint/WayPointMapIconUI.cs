using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WayPointMapIconUI : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;

    private WayPointMapUI owner;
    private WayPointRunTime state;

    public string Id => state != null ? state.Id : string.Empty;

    // Connect this icon to one waypoint state and its owning map UI.
    public void Initialize(WayPointMapUI newOwner, WayPointRunTime newState)
    {
        owner = newOwner;
        state = newState;

        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        Refresh();
    }

    // Apply the ScriptableObject Map Position as this icon's anchored UI position.
    public void SetMapPosition(Vector2 anchoredPosition)
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = anchoredPosition;
        }
    }

    public void Refresh()
    {
        if (state == null || state.Definition == null || iconImage == null)
        {
            return;
        }

        Sprite sprite = state.IsActive
            ? state.Definition.activeMapIcon
            : state.Definition.unlockMapIcon;

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;

        if (button != null)
        {
            button.interactable = state.IsActive;
        }
    }

    private void HandleClick()
    {
        if (owner == null || state == null)
        {
            return;
        }

        owner.TravelTo(state.Id);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner == null || state == null)
        {
            return;
        }

        owner.ShowTooltip(state, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (owner == null)
        {
            return;
        }

        owner.MoveTooltip(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner == null)
        {
            return;
        }

        owner.HideTooltip();
    }
}
