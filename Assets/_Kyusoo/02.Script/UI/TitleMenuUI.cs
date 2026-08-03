using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면의 시작/새로하기/이어하기 및 최초 실행 초기화 버튼 이벤트를 관리하는 UI 스크립트
/// </summary>
public class TitleMenuUI : MonoBehaviour
{
    [Header("타이틀 메뉴 버튼 참조")]
    [SerializeField] private Button gameStartButton;    // 최초 실행 전용 [게임 시작]
    [SerializeField] private Button newGameButton;      // 기존 유저 전용 [새로 하기]
    [SerializeField] private Button continueGameButton; // 기존 유저 전용 [이어 하기]
    [SerializeField] private Button resetButton;        // 테스트/초기화 전용 [최초 실행 기록 리셋]

    private void Start()
    {
        RefreshButtonStates();
        BindButtonEvents();
    }

    /// <summary>
    /// 최초 실행 여부에 따라 버튼의 노출 상태를 새로고침합니다.
    /// </summary>
    public void RefreshButtonStates()
    {
        bool isFirstLaunchDone = FirstLaunchManager.IsFirstLaunchCompleted();

        if (!isFirstLaunchDone)
        {
            // 최초 실행 시: [게임 시작]만 활성화
            if (gameStartButton != null) gameStartButton.gameObject.SetActive(true);
            if (newGameButton != null) newGameButton.gameObject.SetActive(false);
            if (continueGameButton != null) continueGameButton.gameObject.SetActive(false);
        }
        else
        {
            // 실행 이력 존재 시: [새로 하기], [이어 하기]만 활성화
            if (gameStartButton != null) gameStartButton.gameObject.SetActive(false);
            if (newGameButton != null) newGameButton.gameObject.SetActive(true);
            if (continueGameButton != null) continueGameButton.gameObject.SetActive(true);
        }

        // 리셋 버튼은 언제든 테스트할 수 있도록 항상 표시
        if (resetButton != null) resetButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// 각 버튼에 클릭 이벤트를 연결합니다.
    /// </summary>
    private void BindButtonEvents()
    {
        if (gameStartButton != null)
        {
            gameStartButton.onClick.RemoveAllListeners();
            gameStartButton.onClick.AddListener(OnClickGameStart);
        }

        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(OnClickNewGame);
        }

        if (continueGameButton != null)
        {
            continueGameButton.onClick.RemoveAllListeners();
            continueGameButton.onClick.AddListener(OnClickContinue);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(OnClickResetFirstLaunch);
        }
    }

    /// <summary>
    /// [게임 시작] 버튼 클릭 (최초 실행 유저)
    /// </summary>
    public void OnClickGameStart()
    {
        FirstLaunchManager.SetFirstLaunchCompleted();

        if (RecordManager.Instance != null)
        {
            RecordManager.Instance.StartNewGame("Main_World_2");
        }
        else
        {
            Debug.LogError("[TitleMenuUI] RecordManager 인스턴스를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// [새로 하기] 버튼 클릭 (기존 유저)
    /// </summary>
    public void OnClickNewGame()
    {
        if (RecordManager.Instance != null)
        {
            RecordManager.Instance.StartNewGame("Main_World_2");
        }
        else
        {
            Debug.LogError("[TitleMenuUI] RecordManager 인스턴스를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// [이어 하기] 버튼 클릭 (기존 유저)
    /// </summary>
    public void OnClickContinue()
    {
        if (RecordManager.Instance != null)
        {
            RecordManager.Instance.ContinueGame("Main_World_2");
        }
        else
        {
            Debug.LogError("[TitleMenuUI] RecordManager 인스턴스를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// [최초 실행 기록 리셋] 버튼 클릭
    /// </summary>
    public void OnClickResetFirstLaunch()
    {
        FirstLaunchManager.ResetFirstLaunch();
        RefreshButtonStates(); 
    }
}