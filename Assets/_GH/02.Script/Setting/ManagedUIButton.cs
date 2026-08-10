using UnityEngine;
using System;
using KMS.Audio;

/// <summary>
/// Unity Button의 OnClick에서 Managed UI를 ID로 제어하기 위한 중계 컴포넌트입니다.
/// 이 컴포넌트는 Panel이 아니라 버튼 또는 단축키를 소유한 오브젝트에 붙입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ManagedUIButton : MonoBehaviour
{
    [Tooltip("현재 씬의 SceneUIManager에 등록한 Managed UI ID입니다.")]
    [SerializeField] private string managedUIId;

    public void Open()
    {
        if (SceneUIManager.TryOpenManagedUI(managedUIId))
        {
            PlayOptionClick();
        }
    }

    public void Close()
    {
        if (SceneUIManager.TryCloseManagedUI(managedUIId))
        {
            PlayOptionClick();
        }
    }

    public void Toggle()
    {
        if (SceneUIManager.TryToggleManagedUI(managedUIId))
        {
            PlayOptionClick();
        }
    }

    private void PlayOptionClick()
    {
        if (string.Equals(managedUIId, "Option", StringComparison.OrdinalIgnoreCase))
        {
            KMSUIAudio.PlayClick();
        }
    }
}
