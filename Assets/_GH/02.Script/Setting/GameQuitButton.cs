using UnityEngine;

/// <summary>UI Button의 OnClick에서 에디터 플레이 또는 빌드된 게임을 종료합니다.</summary>
[DisallowMultipleComponent]
public sealed class GameQuitButton : MonoBehaviour
{
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
