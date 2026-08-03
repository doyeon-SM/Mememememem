using UnityEngine;

/// <summary>
/// PlayerPrefs를 이용하여 게임 최초 실행 여부를 관리하는 클래스
/// </summary>
public static class FirstLaunchManager
{
    private const string FirstLaunchKey = "IsFirstLaunchCompleted";

    /// <summary>
    /// 최초 실행 상태를 확인합니다.
    /// - 최초 실행 시: false 반환
    /// - 이미 게임을 시작한 이후: true 반환
    /// </summary>
    public static bool IsFirstLaunchCompleted()
    {
        // 0이면 false (최초 실행 미완료), 1이면 true (최초 실행 완료)
        return PlayerPrefs.GetInt(FirstLaunchKey, 0) == 1;
    }

    /// <summary>
    /// 게임 시작 시 최초 실행 상태를 true로 전환합니다.
    /// </summary>
    public static void SetFirstLaunchCompleted()
    {
        PlayerPrefs.SetInt(FirstLaunchKey, 1);
        PlayerPrefs.Save(); 
        Debug.Log("<color=lime>[FirstLaunchManager]</color> 최초 실행 상태가 true(완료)로 전환되었습니다.");
    }

    /// <summary>
    /// (에디터/테스트용) 최초 실행 기록을 리셋합니다.
    /// </summary>
    public static void ResetFirstLaunch()
    {
        PlayerPrefs.DeleteKey(FirstLaunchKey);
        PlayerPrefs.Save();
        Debug.Log("<color=yellow>[FirstLaunchManager]</color> 최초 실행 기록이 리셋(초기화)되었습니다.");
    }
}