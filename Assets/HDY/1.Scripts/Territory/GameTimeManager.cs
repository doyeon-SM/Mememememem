using System;
using UnityEngine;
using TMPro;

namespace HDY.Territory
{
    /// <summary>
    /// 게임에서 쓰는 두 가지 시간을 계산/포맷해서 제공하는 매니저.
    ///
    /// 1) 리얼타임 - 실제 대한민국 표준시(KST, UTC+9)를 "00시 00분" 형식으로 제공한다. 시스템의 로컬
    ///    시간대 설정과 무관하게 항상 UTC 기준으로 9시간을 더해 계산하므로, 빌드/실행 환경이 어디든
    ///    KST로 정확히 표시된다.
    /// 2) 인게임 시간 - TerritoryData.ElapsedTime(게임 시작 이후 누적 초, 저장/불러오기로 이미 복원되는
    ///    값 - _Kyusoo팀 RecordManager가 관리, 수정하지 않음)을 dayLengthSeconds(기본 20분 = 1200초)로
    ///    나눈 나머지를 "하루 안에서 흐른 시간"으로 보고 "00분 00초" 형식으로 제공한다. dayLengthSeconds가
    ///    지날 때마다 0으로 돌아가는 것이 "하루가 초기화된다"는 뜻이다.
    ///
    /// [elapsedTime 누적 주체] TerritoryData.elapsedTime 자체는 이 매니저가 매 프레임(Update)
    /// territoryData.AddTime(Time.deltaTime)을 호출해서 쌓는다. 필드 자체의 의미(게임 시작 이후 누적된
    /// 총 시간)는 그대로 유지하고, "하루" 개념은 이 매니저가 나머지 연산으로 파생시킬 뿐이다 - 그래서
    /// 기존에 이 값을 저장/복원하는 RecordManager 쪽 코드는 전혀 손댈 필요가 없다.
    ///
    /// [Text 연결 - 드래그만 하면 끝] realTimeText/inGameTimeText 인스펙터 필드에 TMP_Text를 연결해두면,
    /// 이 매니저가 값이 바뀔 때마다 알아서 text를 갱신한다 - 별도로 이벤트를 구독하는 스크립트를 짤 필요가
    /// 없다. 비워두면 텍스트 갱신 없이 데이터 계산/이벤트 발행만 한다(다른 스크립트가 이벤트를 구독해서
    /// 직접 표시하고 싶은 경우에도 그대로 문제없이 동작).
    ///
    /// [표시 문자열은 실제로 바뀔 때만 갱신] 매 프레임 시/분/초를 정수로 비교해서, 실제로 값이 바뀐
    /// 경우에만 문자열을 새로 만들어 Text에 반영하고 OnRealTimeTextChanged/OnInGameTimeTextChanged를
    /// 발행한다(매 프레임 문자열을 새로 만들면 불필요한 GC가 생기기 때문).
    ///
    /// [하루가 바뀔 때 - 상점 초기화 연동] 인게임 시간이 dayLengthSeconds를 넘어 다음 "하루"로 넘어가는
    /// 순간 OnInGameDayChanged를 발행한다. ShopStockManager가 이 이벤트를 구독해서 상점별 재입고 주기
    /// (RestockIntervalMinutes를 이 매니저의 DayLengthSeconds로 환산한 "며칠마다")가 지났는지 확인하고
    /// 재입고를 실행한다 - 리얼타임(DateTime.UtcNow) 대신 이 인게임 하루 주기를 기준으로 삼는다.
    ///
    /// [초기 동기화는 Awake에서] 다른 매니저(ShopStockManager 등)가 자신의 Start()에서
    /// GameTimeManager.CurrentInGameDay/DayLengthSeconds를 안전하게 참조할 수 있도록, 초기 동기화
    /// (SyncInitialState)를 Start가 아니라 Awake 끝에서 수행한다 - Unity는 "씬의 모든 Awake가 끝난
    /// 뒤에만 첫 Start가 호출됨"을 보장하므로, 이렇게 하면 Awake들끼리의 실행 순서와 무관하게 다른
    /// 매니저의 Start 시점에는 이 값이 항상 최신 상태다. 씬 시작 시(불러오기 등으로 elapsedTime이 이미
    /// 커진 상태 포함) 첫 동기화에서는 OnInGameDayChanged를 발행하지 않는다 - 그렇지 않으면 불러온 직후
    /// "며칠치" 하루가 한꺼번에 넘어간 것처럼 보여 상점이 즉시 리셋되는 등 원치 않는 부작용이 생길 수 있다.
    ///
    /// [싱글톤 해결 패턴] ItemCatalogManager와 동일한 Resolve(existing) 폴백을 제공한다 - 다른 스크립트가
    /// 인스펙터 참조가 비어있을 때 1) 기존 참조 2) Instance 3) 씬 검색 순으로 찾을 수 있다.
    /// [DontDestroyOnLoad - 임시 조치] 저장/불러오기 시스템이 아직 없어서, TerritoryData/
    /// TerritoryExpansionManager와 마찬가지로 이 매니저도 임시로 DontDestroyOnLoad를 사용한다.
    /// TODO: 저장/불러오기 시스템이 추가되면 생명주기 관리 방식을 다시 검토해야 한다.
    ///
    /// [UIManager 연결] UIManager.GameTime 프로퍼티로 이 매니저에 접근할 수 있다. Text를 직접 이 컴포넌트에
    /// 연결하지 않고 다른 화면에서도 같은 값을 쓰고 싶다면, OnRealTimeTextChanged/OnInGameTimeTextChanged/
    /// OnInGameDayChanged 이벤트를 구독하거나 GetRealTimeText()/GetInGameTimeText()로 직접 조회하면 된다.
    /// </summary>
    public class GameTimeManager : MonoBehaviour
    {
        public static GameTimeManager Instance { get; private set; }

        [Header("데이터 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private TerritoryData territoryData;

        [Header("인게임 하루 길이 (초 단위, 기본 20분 = 1200초)")]
        [SerializeField] private float dayLengthSeconds = 20f * 60f;

        [Header("Text 연결 (선택 사항 - 비워두면 데이터 계산/이벤트 발행만 하고 화면에는 표시하지 않음)")]
        [Tooltip("\"00시 00분\" 형식으로 표시할 TMP_Text. 값이 바뀔 때마다 이 매니저가 알아서 text를 갱신한다.")]
        [SerializeField] private TMP_Text realTimeText;
        //[Tooltip("\"00분 00초\" 형식으로 표시할 TMP_Text. 값이 바뀔 때마다 이 매니저가 알아서 text를 갱신한다.")]
        //[SerializeField] private TMP_Text inGameTimeText;

        /// <summary>인게임 하루 길이(초). ShopStockManager 등 다른 매니저가 자신의 주기를 "며칠"로 환산할 때 참조한다.</summary>
        public float DayLengthSeconds => dayLengthSeconds;

        /// <summary>대한민국 표준시(KST, UTC+9) 기준 현재 시각. 시스템 로컬 시간대와 무관하게 항상 KST로 계산한다.</summary>
        public DateTime CurrentRealTimeKst => DateTime.UtcNow.AddHours(9);

        /// <summary>인게임 하루 안에서 흐른 시간(초). 0 이상 dayLengthSeconds 미만.</summary>
        public float InGameTimeOfDaySeconds { get; private set; }

        /// <summary>게임 시작 이후 몇 번째 "하루"인지(0부터 시작).</summary>
        public int CurrentInGameDay { get; private set; }

        private int lastRealHour = -1;
        private int lastRealMinute = -1;
        private int lastInGameMinutes = -1;
        private int lastInGameSeconds = -1;

        private string lastRealTimeText = string.Empty;
        //private string lastInGameTimeText = string.Empty;

        /// <summary>리얼타임 표시 문자열("00시 00분")이 바뀔 때마다(분 단위) 발행.</summary>
        public event Action<string> OnRealTimeTextChanged;

        /// <summary>인게임 시간 표시 문자열("00분 00초")이 바뀔 때마다(초 단위) 발행.</summary>
        //public event Action<string> OnInGameTimeTextChanged;

        /// <summary>인게임 하루가 넘어갈 때(다음 CurrentInGameDay로 진입할 때)마다 발행 - ShopStockManager가 재입고 판단에 구독.</summary>
        public event Action OnInGameDayChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameTimeManager] 씬에 GameTimeManager가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData == null) Debug.LogWarning("[GameTimeManager] territoryData를 찾을 수 없습니다. 인게임 시간을 계산할 수 없습니다.", this);

            if (dayLengthSeconds <= 0f)
            {
                Debug.LogWarning("[GameTimeManager] dayLengthSeconds가 0 이하로 설정되어 있어 기본값(1200초 = 20분)으로 대체합니다.", this);
                dayLengthSeconds = 20f * 60f;
            }

            // Start가 아니라 Awake에서 동기화한다 - 다른 매니저의 Start()에서 이 값을 안전하게 참조할 수
            // 있도록 하기 위함(Unity는 모든 Awake가 끝난 뒤에만 Start를 호출하는 것을 보장한다).
            SyncInitialState();
        }

        private void Update()
        {
            if (territoryData != null)
            {
                territoryData.AddTime(Time.deltaTime);
            }

            RefreshInGameTime();
            RefreshRealTime();
        }

        /// <summary>씬 시작 시(불러오기로 elapsedTime이 이미 커진 상태 포함) 첫 상태를 이벤트 발행 없이 맞춰두고,
        /// 연결된 Text가 있으면 초기 문구도 바로 반영한다(첫 프레임에 빈 텍스트가 보이지 않도록).</summary>
        private void SyncInitialState()
        {
            float elapsed = territoryData != null ? territoryData.ElapsedTime : 0f;
            CurrentInGameDay = Mathf.FloorToInt(elapsed / dayLengthSeconds);
            InGameTimeOfDaySeconds = elapsed % dayLengthSeconds;

            lastInGameMinutes = Mathf.FloorToInt(InGameTimeOfDaySeconds / 60f);
            lastInGameSeconds = Mathf.FloorToInt(InGameTimeOfDaySeconds % 60f);
            //lastInGameTimeText = FormatInGameTime(lastInGameMinutes, lastInGameSeconds);
            //ApplyInGameTimeText(lastInGameTimeText);

            var kst = CurrentRealTimeKst;
            lastRealHour = kst.Hour;
            lastRealMinute = kst.Minute;
            lastRealTimeText = FormatRealTime(lastRealHour, lastRealMinute);
            ApplyRealTimeText(lastRealTimeText);
        }

        /// <summary>TerritoryData.ElapsedTime을 dayLengthSeconds로 나눈 나머지/몫으로 하루 안 시간/날짜를 갱신한다.
        /// 분·초가 실제로 바뀐 경우에만 문자열을 새로 만들어 Text에 반영하고 이벤트를 발행하며, 날짜가 넘어간
        /// 경우에만 OnInGameDayChanged를 발행한다.</summary>
        private void RefreshInGameTime()
        {
            if (territoryData == null) return;

            float elapsed = territoryData.ElapsedTime;
            int day = Mathf.FloorToInt(elapsed / dayLengthSeconds);
            float timeOfDay = elapsed % dayLengthSeconds;

            InGameTimeOfDaySeconds = timeOfDay;

            if (day != CurrentInGameDay)
            {
                CurrentInGameDay = day;
                OnInGameDayChanged?.Invoke();
            }

            int minutes = Mathf.FloorToInt(timeOfDay / 60f);
            int seconds = Mathf.FloorToInt(timeOfDay % 60f);

            if (minutes == lastInGameMinutes && seconds == lastInGameSeconds) return;

            lastInGameMinutes = minutes;
            lastInGameSeconds = seconds;

            //lastInGameTimeText = FormatInGameTime(minutes, seconds);
            //ApplyInGameTimeText(lastInGameTimeText);
            //OnInGameTimeTextChanged?.Invoke(lastInGameTimeText);
        }

        /// <summary>KST 시/분을 정수로 비교해서 실제로 바뀐 경우에만 문자열을 새로 만들어 Text에 반영하고 이벤트를 발행한다.</summary>
        private void RefreshRealTime()
        {
            var kst = CurrentRealTimeKst;
            int hour = kst.Hour;
            int minute = kst.Minute;

            if (hour == lastRealHour && minute == lastRealMinute) return;

            lastRealHour = hour;
            lastRealMinute = minute;

            lastRealTimeText = FormatRealTime(hour, minute);
            ApplyRealTimeText(lastRealTimeText);
            OnRealTimeTextChanged?.Invoke(lastRealTimeText);
        }

        /// <summary>realTimeText가 연결되어 있으면 그 Text에 바로 반영한다(비어있으면 아무 것도 하지 않음).</summary>
        private void ApplyRealTimeText(string text)
        {
            if (realTimeText != null) realTimeText.text = text;
        }

        /// <summary>inGameTimeText가 연결되어 있으면 그 Text에 바로 반영한다(비어있으면 아무 것도 하지 않음).</summary>
        //private void ApplyInGameTimeText(string text)
        //{
        //    if (inGameTimeText != null) inGameTimeText.text = text;
        //}

        private static string FormatInGameTime(int minutes, int seconds)
        {
            return $"{minutes:00}분 {seconds:00}초";
        }

        private static string FormatRealTime(int hour, int minute)
        {
            return $"{hour:00}시 {minute:00}분";
        }

        /// <summary>지금 기준 리얼타임(KST) 표시 문자열을 반환한다("00시 00분"). Update에서 매 프레임 최신 상태로 유지된다.</summary>
        public string GetRealTimeText() => lastRealTimeText;

        /// <summary>지금 기준 인게임 시간 표시 문자열을 반환한다("00분 00초"). Update에서 매 프레임 최신 상태로 유지된다.</summary>
        //public string GetInGameTimeText() => lastInGameTimeText;

        /// <summary>
        /// 다른 스크립트가 들고 있는 GameTimeManager 참조가 비어있을 때 쓰는 공용 폴백 탐색.
        /// 1) 이미 참조가 있으면 그대로 반환, 2) 없으면 싱글톤(Instance), 3) 그래도 없으면 씬 전체에서 검색.
        /// </summary>
        public static GameTimeManager Resolve(GameTimeManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<GameTimeManager>();
            if (found == null)
            {
                Debug.LogWarning("[GameTimeManager] 씬에서 GameTimeManager를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
