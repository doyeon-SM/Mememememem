using UnityEngine;
using UnityEngine.EventSystems;

public class QuickSlotScrollBlockerUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public static bool isPointerOver;

    private static int pointerOverCount;
    private bool isPointerInside;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isPointerInside) return;

        isPointerInside = true;
        pointerOverCount++;
        isPointerOver = pointerOverCount > 0;
    }

    public void OnPointerExit(PointerEventData eventData) { ClearPointerInside(); }
    private void OnDisable() { ClearPointerInside(); }

    private void ClearPointerInside()
    {
        if (!isPointerInside) return;

        isPointerInside = false;
        pointerOverCount = Mathf.Max(0, pointerOverCount - 1);
        isPointerOver = pointerOverCount > 0;
    }
}
