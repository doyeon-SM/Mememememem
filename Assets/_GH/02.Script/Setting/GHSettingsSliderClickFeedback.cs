using KMS.Audio;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 환경설정 슬라이더를 누르기 시작할 때 클릭음을 한 번만 재생합니다.
/// 값 변경 이벤트에 직접 연결하지 않아 드래그 중에는 소리가 반복되지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GHSettingsSliderClickFeedback : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
        {
            KMSUIAudio.PlayClick();
        }
    }
}
