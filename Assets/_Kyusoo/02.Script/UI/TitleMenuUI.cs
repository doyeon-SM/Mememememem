using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using GH.Loading; // 🌟 LoadingManager 네임스페이스 추가

/// <summary>
/// 타이틀 화면의 버튼 상태 제어 및 LoadingManager 연동을 통한 씬 전환을 관리하는 UI 스크립트
/// </summary>
public class TitleMenuUI : MonoBehaviour
{
    [Header("타이틀 메뉴 버튼 참조")]
    [SerializeField] private Button gameStartButton;    // 최초 실행 전용 [게임 시작]
    [SerializeField] private Button newGameButton;      // 기존 유저 전용 [새로 하기]
    [SerializeField] private Button continueGameButton; // 기존 유저 전용 [이어 하기]
    [SerializeField] private Button resetButton;        // 테스트/초기화 전용 [최초 실행 기록 리셋]

    [Header("기본 씬 설정")]
    [SerializeField] private string defaultStartScene = "Main_World_2";

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
        StartNewGameWithLoadingScreen();
    }

    /// <summary>
    /// [새로 하기] 버튼 클릭 (기존 유저)
    /// </summary>
    public void OnClickNewGame()
    {
        StartNewGameWithLoadingScreen();
    }

    /// <summary>
    /// [이어 하기] 버튼 클릭 (기존 유저)
    /// </summary>
    public void OnClickContinue()
    {
        string targetScene = defaultStartScene;

        // 세이브 파일에서 저장된 마지막 씬 이름 추출
        if (RecordManager.Instance != null)
        {
            SaveData saveData = RecordManager.Instance.ReadRawSaveFileOnly();
            if (saveData != null && !string.IsNullOrEmpty(saveData.lastPlayScene))
            {
                targetScene = saveData.lastPlayScene;
            }
        }

        // LoadingManager를 통한 로딩 화면 처리 및 씬 이동
        if (LoadingManager.Instance != null)
        {
            Debug.Log($"<color=cyan>[TitleMenuUI]</color> LoadingManager를 통해 이어하기 이동: {targetScene}");
            bool success = LoadingManager.Instance.LoadScene(targetScene, string.Empty);

            if (!success && RecordManager.Instance != null)
            {
                RecordManager.Instance.ContinueGame(defaultStartScene);
            }
        }
        else if (RecordManager.Instance != null)
        {
            Debug.LogWarning("[TitleMenuUI] LoadingManager 인스턴스가 없어 RecordManager 직접 로딩으로 우회합니다.");
            RecordManager.Instance.ContinueGame(defaultStartScene);
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

    /// <summary>
    /// 새 게임을 위한 세이브 파일을 사전 준비하고 LoadingManager를 통해 씬을 로드합니다.
    /// </summary>
    private void StartNewGameWithLoadingScreen()
    {
        PrepareNewGameSaveFile();

        if (LoadingManager.Instance != null)
        {
            Debug.Log($"<color=cyan>[TitleMenuUI]</color> LoadingManager를 통해 새 게임 로딩 시작: {defaultStartScene}");
            bool success = LoadingManager.Instance.LoadScene(defaultStartScene, string.Empty);

            if (!success && RecordManager.Instance != null)
            {
                RecordManager.Instance.StartNewGame(defaultStartScene);
            }
        }
        else if (RecordManager.Instance != null)
        {
            Debug.LogWarning("[TitleMenuUI] LoadingManager 인스턴스가 없어 RecordManager 직접 로딩으로 우회합니다.");
            RecordManager.Instance.StartNewGame(defaultStartScene);
        }
    }

    /// <summary>
    /// 기존 세이브 파일을 제거하고 초기화된 SaveData JSON 파일을 생성합니다.
    /// </summary>
    private void PrepareNewGameSaveFile()
    {
        if (RecordManager.Instance == null) return;

        try
        {
            string savePath = RecordManager.Instance.SaveFilePath;
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            SaveData defaultData = new SaveData
            {
                lastPlayScene = defaultStartScene,
                lastSaveTime = DateTime.UtcNow.ToString("o")
            };

            File.WriteAllText(savePath, JsonUtility.ToJson(defaultData, true));
            Debug.Log("<color=lime>[TitleMenuUI]</color> 새 게임 시작용 세이브 파일 사전 생성 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TitleMenuUI] 새 세이브 파일 생성 중 오류 발생: {e.Message}");
        }
    }
}