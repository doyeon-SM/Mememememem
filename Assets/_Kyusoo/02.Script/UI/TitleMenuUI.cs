using System;
using UnityEngine;
using UnityEngine.UI;
using GH.Loading;

public class TitleMenuUI : MonoBehaviour
{
    [Header("타이틀 메뉴 버튼 참조")]
    [SerializeField] private Button gameStartButton;    // 최초 실행 전용 [게임 시작]
    [SerializeField] private Button newGameButton;      // 기존 유저 전용 [새로 하기]
    [SerializeField] private Button continueGameButton; // 기존 유저 전용 [이어 하기]
    [SerializeField] private Button resetButton;        // 테스트 전용 [최초 실행 기록 리셋]

    [Header("기본 씬 설정")]
    [SerializeField] private string defaultStartScene = "Main_World_3";

    private void Start()
    {
        RefreshButtonStates();
        BindButtonEvents();
    }

    public void RefreshButtonStates()
    {
        bool isFirstLaunchDone = FirstLaunchManager.IsFirstLaunchCompleted();

        if (!isFirstLaunchDone)
        {
            if (gameStartButton != null) gameStartButton.gameObject.SetActive(true);
            if (newGameButton != null) newGameButton.gameObject.SetActive(false);
            if (continueGameButton != null) continueGameButton.gameObject.SetActive(false);
        }
        else
        {
            if (gameStartButton != null) gameStartButton.gameObject.SetActive(false);
            if (newGameButton != null) newGameButton.gameObject.SetActive(true);
            if (continueGameButton != null) continueGameButton.gameObject.SetActive(true);
        }

        if (resetButton != null) resetButton.gameObject.SetActive(true);
    }

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

    public void OnClickGameStart()
    {
        FirstLaunchManager.SetFirstLaunchCompleted();
        StartNewGameWithLoadingScreen();
    }

    public void OnClickNewGame()
    {
        StartNewGameWithLoadingScreen();
    }

    public void OnClickContinue()
    {
        string targetScene = defaultStartScene;

        if (RecordManager.Instance != null)
        {
            SaveData saveData = RecordManager.Instance.ReadRawSaveFileOnly();
            if (saveData != null && !string.IsNullOrEmpty(saveData.lastPlayScene))
            {
                targetScene = saveData.lastPlayScene;
            }
        }

        if (LoadingManager.Instance != null)
        {
            bool success = LoadingManager.Instance.LoadScene(targetScene, string.Empty);
            if (!success && RecordManager.Instance != null)
            {
                RecordManager.Instance.ContinueGame(defaultStartScene);
            }
        }
        else if (RecordManager.Instance != null)
        {
            RecordManager.Instance.ContinueGame(defaultStartScene);
        }
    }

    public void OnClickResetFirstLaunch()
    {
        FirstLaunchManager.ResetFirstLaunch();
        RefreshButtonStates();
    }

    private void StartNewGameWithLoadingScreen()
    {
        if (RecordManager.Instance != null)
        {
            RecordManager.Instance.PrepareNewGameFile(defaultStartScene);
        }

        if (LoadingManager.Instance != null)
        {
            bool success = LoadingManager.Instance.LoadScene(defaultStartScene, string.Empty);
            if (!success && RecordManager.Instance != null)
            {
                RecordManager.Instance.StartNewGame(defaultStartScene);
            }
        }
        else if (RecordManager.Instance != null)
        {
            RecordManager.Instance.StartNewGame(defaultStartScene);
        }
    }
}