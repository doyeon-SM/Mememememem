using DG.Tweening;
using GH.Loading;
using KMS.Audio;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TitleMenuUI : MonoBehaviour
{
    [Header("타이틀 메뉴 버튼 참조")]
    [SerializeField] private Button gameStartButton;    // 최초 실행 전용 [게임 시작]
    [SerializeField] private Button newGameButton;      // 기존 유저 전용 [새로 하기]
    [SerializeField] private Button continueGameButton; // 기존 유저 전용 [이어 하기]
    [SerializeField] private Button resetButton;        // 테스트 전용 [최초 실행 기록 리셋]
    [SerializeField] private Button quitButton;         // 🌟 [요구사항 1] 종료 버튼 추가

    [Header("기본 씬 설정")]
    [SerializeField] private string defaultStartScene = "Main_World_3";

    private void Start()
    {
        RefreshButtonStates();
        BindButtonEvents();

        KMSAudioService.Play2D(GameSfxId.Title);
    }

    private void OnDestroy()
    {
        KMSAudioService.StopSfx(GameSfxId.Title);
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
        if (quitButton != null) quitButton.gameObject.SetActive(true); 

        SetAllButtonsInteractable(true);
    }

    /// <summary>
    /// 버튼 연타 및 중복 클릭을 방지하기 위한 인터랙션 제어
    /// </summary>
    private void SetAllButtonsInteractable(bool interactable)
    {
        if (gameStartButton != null) gameStartButton.interactable = interactable;
        if (newGameButton != null) newGameButton.interactable = interactable;
        if (continueGameButton != null) continueGameButton.interactable = interactable;
        if (resetButton != null) resetButton.interactable = interactable;
        if (quitButton != null) quitButton.interactable = interactable; // 🌟 [추가] 종료 버튼 인터랙션 제어
    }

    private void BindButtonEvents()
    {
        BindButton(gameStartButton, OnClickGameStart);
        BindButton(newGameButton, OnClickNewGame);
        BindButton(continueGameButton, OnClickContinue);
        BindButton(resetButton, OnClickResetFirstLaunch);
        BindButton(quitButton, OnClickQuit); // 🌟 [요구사항 2] 종료 버튼 이벤트 바인딩
    }

    private void BindButton(Button button, Action action)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => PlayButtonAnimationAndExecute(button, action));
    }

    private void PlayButtonAnimationAndExecute(Button targetButton, Action action)
    {
        if (targetButton == null) return;

        // 클릭 즉시 모든 타이틀 버튼 비활성화 (연타 방지)
        SetAllButtonsInteractable(false);

        targetButton.transform.DOKill();

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(targetButton.transform.DOScale(0.9f, 0.08f).SetEase(Ease.OutQuad));   // 살짝 작아짐
        seq.Append(targetButton.transform.DOScale(1.05f, 0.12f).SetEase(Ease.OutBack)); // 커지며 튀어오름
        seq.Append(targetButton.transform.DOScale(1f, 0.08f).SetEase(Ease.OutQuad));     // 기본 크기로 복귀
        seq.OnComplete(() =>
        {
            action?.Invoke();
        });
    }

    public void OnClickGameStart()
    {
        FirstLaunchManager.SetFirstLaunchCompleted();
        StartCoroutine(StartNewGameRoutine());
    }

    public void OnClickNewGame()
    {
        StartCoroutine(StartNewGameRoutine());
    }

    public void OnClickContinue()
    {
        StartCoroutine(ContinueGameRoutine());
    }

    public void OnClickResetFirstLaunch()
    {
        FirstLaunchManager.ResetFirstLaunch();
        RefreshButtonStates();
    }

    /// <summary>
    /// 🌟 [요구사항 2] 종료 버튼 클릭 시 GameQuitButton의 QuitGame 호출
    /// </summary>
    public void OnClickQuit()
    {
       
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }

    /// <summary>
    /// 새로 하기: 로딩 패널을 먼저 화면에 그리고 무거운 파일 생성을 뒤이어 수행
    /// </summary>
    private IEnumerator StartNewGameRoutine()
    {
        bool success = false;

        // 1. 로딩 패널 화면 생성
        if (LoadingManager.Instance != null)
        {
            success = LoadingManager.Instance.LoadScene(defaultStartScene, string.Empty);
        }

        if (success)
        {
            // 2. 유니티 캔버스가 로딩 패널 UI를 화면에 완전히 그릴 때까지 대기
            yield return new WaitForEndOfFrame();
            yield return null;

            // 3. 로딩 화면이 덮인 상태에서 파일 생성 실행 (화면 멈춤 현상 체감 차단)
            if (RecordManager.Instance != null)
            {
                RecordManager.Instance.PrepareNewGameFile(defaultStartScene);
            }
        }
        else
        {
            yield return null;
            if (RecordManager.Instance != null)
            {
                RecordManager.Instance.StartNewGame(defaultStartScene);
            }
            else
            {
                SetAllButtonsInteractable(true);
            }
        }
    }

    /// <summary>
    /// 이어 하기: 로딩 패널을 거치도록 일원화
    /// </summary>
    private IEnumerator ContinueGameRoutine()
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

        bool success = false;
        if (LoadingManager.Instance != null)
        {
            success = LoadingManager.Instance.LoadScene(targetScene, string.Empty);
        }

        if (!success)
        {
            Debug.LogWarning($"[TitleMenuUI] LoadingManager를 통한 이어하기 실패. Scene: {targetScene}");
            if (RecordManager.Instance != null)
            {
                RecordManager.Instance.ContinueGame(defaultStartScene);
            }
            else
            {
                SetAllButtonsInteractable(true);
            }
        }

        yield break;
    }
}