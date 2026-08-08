using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    [Header("커서 이미지 설정")]
    [SerializeField] private Texture2D cursorTexture;
    [SerializeField] private Vector2 hotspot = Vector2.zero;

    private Vector2 lastHotspot;
    private Texture2D lastTexture;

    private void Start()
    {
        ApplyCursor();
    }

    private void Update()
    {
        // 인스펙터 창에서 수치나 이미지를 실시간으로 바꿨을 때 곧바로 반영되도록 체크
        if (hotspot != lastHotspot || cursorTexture != lastTexture)
        {
            ApplyCursor();
        }
    }

    private void ApplyCursor()
    {
        lastHotspot = hotspot;
        lastTexture = cursorTexture;

        if (cursorTexture != null)
        {
            // 세 번째 인자를 CursorMode.ForceSoftware로 주면 OS 간섭 없이 
            // 유니티 내부 소프트웨어 커서로 강제 구동되어 핫스팟 오프셋이 정확히 먹힙니다.
            Cursor.SetCursor(cursorTexture, hotspot, CursorMode.ForceSoftware);
        }
    }
}