using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Territory;
using KMS.Audio;

namespace HDY.UI
{
    /// <summary>
    /// 영지 레벨업 시 잠깐 떴다가 사라지는 알림 패널.
    /// TerritoryData.OnLevelChanged를 구독해서 레벨이 오를 때마다 자동으로 표시된다.
    ///
    /// [닫히는 조건 - 클릭 또는 1초, 둘 중 먼저 오는 쪽] Show()에서 1초 뒤 자동으로 Hide()하는
    /// 코루틴을 시작해두고, 그 전에 패널을 클릭하면 dismissButton 클릭 핸들러가 즉시 그 코루틴을
    /// 정지시키고 Hide()를 호출한다 - 코루틴과 클릭이 서로 경쟁하는 게 아니라, 클릭이 코루틴을
    /// 무효화시키는 방식이라 둘 다 실행돼서 중복 처리되는 일이 없다.
    ///
    /// [버그 수정 - CanvasGroup으로 표시 전환] 예전에는 Awake()에서 자기 자신을 SetActive(false)로
    /// 끄고, Show()/Hide()에서도 SetActive를 그대로 토글했다. 그런데 자기 자신의 Awake() 안에서
    /// SetActive(false)를 호출하면 그 활성화 사이클의 OnEnable이 아예 스킵되어(Unity 특성)
    /// territoryData.OnLevelChanged 구독 자체가 걸리지 못하는 문제가 있었다(실제로 발생 - 레벨업
    /// 로그는 찍히는데 이 패널은 전혀 반응하지 않았음). TutorialDialogueUI.cs가 이미 겪고 고쳐둔 것과
    /// 동일한 문제라, 그 해결 방식을 그대로 가져와 CanvasGroup으로 화면 표시/입력만 껐다 켠다 - 오브젝트
    /// 자체는 항상 활성 상태로 유지되어 OnEnable의 구독이 끊기지 않는다.
    ///
    /// [버그 수정 - 씬 입장 시 오작동 방지] _Kyusoo의 TerritoryRecordData.ApplyData()(세이브 데이터를
    /// 씬에 적용하는 코드, 영지 씬에 들어올 때마다 실행됨)가 복원된 레벨 값으로 다른 UI(HUD 등)를 강제로
    /// 갱신시키기 위해, 리플렉션으로 TerritoryData.OnLevelChanged를 저장/복원 시점마다 직접 재호출한다
    /// (levelEvent?.DynamicInvoke(liveTerritoryData.Level)). 이 신호는 "진짜 레벨업"이 아니라 "복원
    /// 알림"이라 실제로 레벨이 오르지 않았는데도 이 패널이 반응해서, 씬에 들어올 때마다 레벨업 팝업이
    /// 뜨는 문제가 있었다. 저장/복원 로직(다른 팀 파일)은 건드리지 않고, 이 컴포넌트가 이번 활성화
    /// 사이클에서 "처음 받는" OnLevelChanged 신호만 방어적으로 무시하는 방식으로 고쳤다 - 씬에 들어와
    /// 구독을 걸자마자 오는 신호가 바로 그 복원 신호이기 때문이다. 그 이후에 오는 신호부터는 정상적으로
    /// 진짜 레벨업으로 간주해 팝업을 띄운다(같은 씬에 머무는 동안 실제로 레벨업이 여러 번 일어나도 전부
    /// 정상 반응 - 무시하는 건 재활성화 직후 딱 한 번뿐).
    ///
    /// [이미지] iconImage는 코드에서 스프라이트를 바꾸지 않는다 - 인스펙터에 미리 설정해둔 단일
    /// 고정 이미지를 그대로 사용한다(레벨 공통 이미지 1개, 요청하신 방식).
    ///
    /// [사운드 - HDY 요청] KMS Audio 시스템(KMSAudioService.Play2D)의 영지 레벨업 전용 GameSfxId인
    /// GameSfxId.TerritoryLevelUp을 재생한다.
    ///
    /// [참조 확보 - EnsureReferences 패턴] territoryData는 Awake뿐 아니라 OnEnable에서도 다시 확보를
    /// 시도한다(TerritoryData.Resolve(existing) - 이미 있으면 그대로, 없으면 싱글톤 Instance, 그래도
    /// 없으면 씬 검색).
    /// [교통정리] HDY 폴더 소속. KMS/Kyusoo/_GH/Pikachu 파일은 수정하지 않음(KMS Audio는 호출만 함).
    /// </summary>
    public class TerritoryLevelUpPanelUI : MonoBehaviour
    {
        private const float AutoHideSeconds = 2f;

        [Header("데이터 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private TerritoryData territoryData;

        [Header("표시 UI 참조")]
        [Tooltip("레벨 공통으로 쓰는 단일 고정 이미지. 인스펙터에서 미리 설정, 코드에서 변경하지 않음.")]
        [SerializeField] private Image iconImage;
        [Tooltip("\"Lv.N 달성\" 형식으로 표시할 텍스트.")]
        [SerializeField] private TMP_Text levelText;
        [Tooltip("패널 전체(또는 배경)를 덮어 클릭을 감지하는 버튼. 클릭 시 즉시 닫힌다.")]
        [SerializeField] private Button dismissButton;

        private Coroutine autoHideRoutine;
        private CanvasGroup canvasGroup;

        // [버그 수정 - 씬 입장 시 오작동 방지] 이번 활성화 사이클에서 OnLevelChanged를 아직 한 번도
        // 받지 못했으면 true. 첫 신호는 세이브 복원 알림일 가능성이 높아 무시하고, 그 다음부터 반응한다.
        private bool awaitingFirstLevelSignal;

        private void Awake()
        {
            EnsureReferences();

            if (iconImage == null) Debug.LogWarning("[TerritoryLevelUpPanelUI] iconImage가 비어있습니다.", this);
            if (levelText == null) Debug.LogWarning("[TerritoryLevelUpPanelUI] levelText가 비어있습니다.", this);

            if (dismissButton != null)
            {
                dismissButton.onClick.AddListener(HandleDismissClicked);
            }
            else
            {
                Debug.LogWarning("[TerritoryLevelUpPanelUI] dismissButton이 비어있습니다. 클릭으로 닫을 수 없습니다.", this);
            }

            // [버그 수정] 오브젝트 자신을 SetActive(false)로 끄면 이번 활성화 사이클의 OnEnable이 아예
            // 스킵되어 OnLevelChanged 구독이 걸리지 못한다(실제로 발생했던 버그). CanvasGroup으로 화면
            // 표시/입력만 껐다 켜서, 오브젝트 자체는 항상 활성 상태로 유지해 구독이 끊기지 않게 한다.
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void OnEnable()
        {
            EnsureReferences();

            // [버그 수정 - 씬 입장 시 오작동 방지] 다시 활성화될 때마다(=씬에 들어올 때마다) 리셋한다.
            // 이번에 구독을 걸고 나서 처음 오는 OnLevelChanged 한 번은 무시한다.
            awaitingFirstLevelSignal = true;

            if (territoryData != null)
            {
                territoryData.OnLevelChanged -= HandleLevelChanged; // 중복 구독 방지
                territoryData.OnLevelChanged += HandleLevelChanged;
            }
        }

        private void OnDisable()
        {
            if (territoryData != null)
            {
                territoryData.OnLevelChanged -= HandleLevelChanged;
            }

            if (autoHideRoutine != null)
            {
                StopCoroutine(autoHideRoutine);
                autoHideRoutine = null;
            }
        }

        /// <summary>
        /// territoryData가 비어있으면 다시 확보를 시도한다. Awake/OnEnable 양쪽에서 호출해서,
        /// 초기화 순서 문제로 최초 확보에 실패했더라도 재활성화 시점에 다시 구독을 걸 수 있게 한다.
        /// </summary>
        private void EnsureReferences()
        {
            territoryData = TerritoryData.Resolve(territoryData);
        }

        private void HandleLevelChanged(int level)
        {
            // [버그 수정 - 씬 입장 시 오작동 방지] _Kyusoo의 TerritoryRecordData.ApplyData()가 세이브
            // 데이터를 씬에 적용할 때마다(영지 씬에 들어올 때마다) 복원된 레벨로 이 이벤트를 리플렉션으로
            // 강제 재호출한다 - 진짜 레벨업이 아니라 "복원 알림"이다. 이번 활성화 사이클에서 처음 받는
            // 신호가 바로 그 복원 알림일 가능성이 높으므로, 팝업을 띄우지 않고 조용히 넘어간다.
            // 그 다음부터 오는 신호는 실제 레벨업으로 간주해 정상적으로 팝업을 띄운다.
            if (awaitingFirstLevelSignal)
            {
                awaitingFirstLevelSignal = false;
                return;
            }

            Show(level);
        }

        /// <summary>패널을 켜고 텍스트/사운드를 세팅한 뒤, 1초 뒤 자동으로 닫히는 타이머를 시작한다.</summary>
        private void Show(int level)
        {
            if (levelText != null)
            {
                levelText.text = $"Lv.{level} 달성";
            }

            SetVisible(true);

            // [HDY 요청 - 사운드] 영지 레벨업 전용 효과음.
            KMSAudioService.Play2D(GameSfxId.TerritoryLevelUp);

            if (autoHideRoutine != null)
            {
                StopCoroutine(autoHideRoutine);
            }
            autoHideRoutine = StartCoroutine(AutoHideAfterDelay());
        }

        private IEnumerator AutoHideAfterDelay()
        {
            yield return new WaitForSeconds(AutoHideSeconds);
            autoHideRoutine = null;
            Hide();
        }

        /// <summary>dismissButton 클릭 - 자동 닫힘 타이머를 무효화하고 즉시 닫는다.</summary>
        private void HandleDismissClicked()
        {
            if (autoHideRoutine != null)
            {
                StopCoroutine(autoHideRoutine);
                autoHideRoutine = null;
            }

            Hide();
        }

        private void Hide()
        {
            SetVisible(false);
        }

        /// <summary>오브젝트 자체는 항상 활성 상태로 유지한 채, CanvasGroup으로 화면 표시/입력만 토글한다.</summary>
        private void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
