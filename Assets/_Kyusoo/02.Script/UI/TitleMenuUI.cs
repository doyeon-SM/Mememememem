using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
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
        BindButton(gameStartButton, OnClickGameStart);
        BindButton(newGameButton, OnClickNewGame);
        BindButton(continueGameButton, OnClickContinue);
        BindButton(resetButton, OnClickResetFirstLaunch);
    }

    /// <summary>
    /// 버튼 공통 바인딩 및 DOTween 클릭 팝업 연출 적용
    /// </summary>
    private void BindButton(Button button, Action action)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => PlayButtonAnimationAndExecute(button, action));
    }

    /// <summary>
    /// 버튼이 눌릴 때 꾹 눌렸다가 탄성 있게 튀어오르는 연출 후 액션 실행
    /// </summary>
    private void PlayButtonAnimationAndExecute(Button targetButton, Action action)
    {
        if (targetButton == null) return;

        targetButton.interactable = false;

        targetButton.transform.DOKill();

        
        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(targetButton.transform.DOScale(0.9f, 0.08f).SetEase(Ease.OutQuad));   // 살짝 작아짐
        seq.Append(targetButton.transform.DOScale(1.05f, 0.12f).SetEase(Ease.OutBack)); // 커지며 튀어오름
        seq.Append(targetButton.transform.DOScale(1f, 0.08f).SetEase(Ease.OutQuad));     // 기본 크기로 복귀
        seq.OnComplete(() =>
        {
            targetButton.interactable = true;
            action?.Invoke(); 
        });
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