using System;
using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;
using MemSystem.Events;
using PikachuMem = MemSystem.Core.Mem;

namespace HDY.Capture
{
    /// <summary>
    /// 포획된 멤 한 마리를 식별하기 위한 저장 항목.
    /// KeyId는 보통 GUID 문자열이지만, 빈 칸(멤이 없는 칸)은 KeyId가 EmptyKeyId("-99999")로 고정되고
    /// MemId/ExplorationStat는 비워둔다(각각 null, 0). IsEmpty로 빈 칸 여부를 판별한다.
    /// </summary>
    [Serializable]
    public class CapturedMemEntry
    {
        /// <summary>빈 칸(멤 없음)을 나타내는 KeyId 값.</summary>
        public const string EmptyKeyId = "-99999";

        public string KeyId;
        public string MemId;
        public int ExplorationStat;

        /// <summary>이 멤이 활성화(예: 영지에 배치되어 근무 중) 상태인지 여부. 기본값 false.</summary>
        public bool IsActive;

        /// <summary>이 항목이 빈 칸(멤 없음)인지 여부.</summary>
        public bool IsEmpty => KeyId == EmptyKeyId;

        /// <summary>빈 칸 항목을 새로 만든다.</summary>
        public static CapturedMemEntry CreateEmpty()
        {
            return new CapturedMemEntry
            {
                KeyId = EmptyKeyId,
                MemId = null,
                ExplorationStat = 0,
                IsActive = false
            };
        }
    }

    /// <summary>
    /// 포획에 성공한 멤 데이터를 메모리에 보관하는 매니저(멤 창고).
    /// [교통정리] Pikachu 팀의 MemEvents.OnMemCaptured 이벤트를 구독하여 MemSnapshot을 받아,
    /// KeyId(GUID)를 부여한 뒤 필요한 값(MemId, 탐험 스탯)만 CapturedMemEntry로 저장한다.
    ///
    /// [창고 최대치 & 빈 칸] 목록은 항상 최대치(SlotsPerPage x MaxPages)만큼 미리 생성되어 있으며,
    /// 빈 칸은 삭제되지 않고 CapturedMemEntry.CreateEmpty()로 채워진 채 자리를 유지한다(그리드에서 빈 칸으로도 멤을
    /// 이동시킬 수 있도록 하기 위함). 새 멤을 포획하면 목록 맨 뒤에 추가하는 대신 첫 번째 빈 칸을 찾아 채운다.
    /// 최대치(SlotsPerPage, MaxPages)는 Inspector에서 조정 가능하며, 나중에 값을 늘리면 기존 데이터는 그대로 둔 채
    /// 모자란 만큼 빈 칸이 자동으로 추가된다(EnsureCapacity).
    ///
    /// [페이지 언락 (멤창고 업그레이드)] 목록 자체는 항상 MaxCapacity만큼 전부 채워져 있지만, 실제로 "사용 가능한"
    /// 칸은 UnlockedPageCount(시작 StartingPageCount 페이지)만큼으로 제한된다. 포획 시 빈 칸을 찾을 때도
    /// 언락된 범위(UnlockedCapacity) 안에서만 찾으므로, 언락되지 않은 페이지에는 멤이 채워지지 않는다.
    /// 업그레이드(비용 확인/차감)는 이 클래스의 책임이 아니다 - MemStorageUpgrade(IUpgradable 구현체)가 비용을
    /// 계산하고, 공용 업그레이드 팝업(UpgradePopupUI)이 비용을 다 낸 뒤에만 UnlockNextPage()를 호출해준다.
    /// 이 클래스는 순수하게 "페이지 수를 늘리고, 그만큼 포획 가능 범위를 넓히는" 역할만 한다.
    ///
    /// [Awake 타이밍 방어] UnlockedPageCount/UnlockedCapacity는 Awake()에서 한 번만 초기화하지 않고, 프로퍼티를
    /// 읽을 때마다 EnsureUnlockedPageCount()로 스스로 보정한 뒤 반환한다. 플레이 도중 스크립트를 수정해 컴파일이
    /// 일어나면(도메인 리로드) 새로 추가된 필드는 이전 값이 없어 C# 기본값(0)으로 초기화되는데, Awake는 리로드 후
    /// 다시 호출되지 않으므로 한 번만 초기화하는 방식은 그 상태로 계속 0에 머무를 수 있다(실제로 이 문제로 한동안
    /// 포획 가능 칸이 0으로 계산되어 포획한 멤이 조용히 방생 처리된 적이 있었다). 프로퍼티 접근 시점마다 보정하면
    /// 이런 타이밍 문제와 무관하게 항상 올바른 값을 반환한다.
    ///
    /// [방어 코드 - 창고가 가득 찬 경우] 포획 자체(Pikachu 쪽 OnCaptureSuccess)는 이미 성공해 월드의 멤 오브젝트는
    /// 처리가 끝난 상태이지만, HDY 창고에 (언락된 범위 안에서) 빈 칸이 하나도 없으면 그 포획 데이터를 저장하지
    /// 않고 그대로 놓아준다(방생). 방생 시 OnMemReleasedDueToFullStorage 이벤트가 발행되므로, 추후 UI에서 안내
    /// 메시지 등을 붙일 수 있다.
    ///
    /// [정렬] 이 클래스는 정렬 기준(멤 ID/등급/스탯 등)을 전혀 알지 못한다. 실제 비교/정렬 판단은 카탈로그(MemData)에
    /// 접근 가능한 상위(MemStorageUI)가 하고, 이 클래스는 ApplySortedOrder로 "정렬된 결과를 그대로 적용"만 담당한다.
    ///
    /// 파일 저장 등 영속화 로직은 다음 단계에서 추가 예정.
    /// 씬에 배치되어 DontDestroyOnLoad로 유지되는 파괴불가 싱글톤.
    /// </summary>
    public class MemCaptureManager : MonoBehaviour
    {
        public static MemCaptureManager Instance { get; private set; }

        [Header("창고 최대치 설정 (추후 변경 가능하도록 노출, 기본 48칸 x 10페이지)")]
        [SerializeField] private int slotsPerPage = 48;
        [SerializeField] private int maxPages = 10;

        public int SlotsPerPage => slotsPerPage;
        public int MaxPages => maxPages;
        public int MaxCapacity => slotsPerPage * maxPages;

        [Header("페이지 언락 (멤창고 업그레이드로 늘어나는 사용 가능 페이지 수)")]
        [Tooltip("업그레이드 없이 시작할 때 기본으로 사용 가능한 페이지 수")]
        [SerializeField] private int startingPageCount = 2;
        [SerializeField] private int unlockedPageCount;

        public int StartingPageCount => startingPageCount;

        /// <summary>
        /// 언락된 페이지 수. Awake() 실행 시점/순서(특히 플레이 도중 스크립트 컴파일로 인한 도메인 리로드)에
        /// 의존하지 않도록, 읽을 때마다 EnsureUnlockedPageCount()로 스스로 보정한 뒤 반환한다.
        /// </summary>
        public int UnlockedPageCount
        {
            get
            {
                EnsureUnlockedPageCount();
                return unlockedPageCount;
            }
        }

        /// <summary>현재 언락된 페이지 기준으로 실제 포획에 사용할 수 있는 칸 수. 이 값도 읽을 때마다 보정된다.</summary>
        public int UnlockedCapacity
        {
            get
            {
                EnsureUnlockedPageCount();
                return unlockedPageCount * slotsPerPage;
            }
        }

        /// <summary>언락된 페이지 수가 바뀔 때(업그레이드 성공 시) 발행. UI(MemStorageUI_Grid 등)가 구독해서 이동 가능한 페이지 범위를 갱신한다.</summary>
        public event Action OnStorageCapacityChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureCapacity();
            EnsureUnlockedPageCount();
        }

        [Header("포획된 멤 목록 (런타임 메모리 보관, 최대치만큼 빈 칸 포함해서 미리 채워짐)")]
        [SerializeField] private List<CapturedMemEntry> capturedMems = new List<CapturedMemEntry>();

        public IReadOnlyList<CapturedMemEntry> CapturedMems => capturedMems;

        /// <summary>목록에 새 멤이 저장되거나 위치/순서가 바뀔 때마다 발행. UI 등에서 이 이벤트를 구독해 갱신하면 된다.</summary>
        public event Action OnCapturedMemsChanged;

        /// <summary>
        /// 창고가 가득 차서 포획한 멤을 저장하지 못하고 그대로 놓아줄(방생) 때 발행.
        /// 저장은 되지 않으므로 OnCapturedMemsChanged는 발행되지 않는다. UI에서 "창고가 가득 찼습니다" 같은
        /// 안내를 붙이고 싶을 때 이 이벤트를 구독하면 된다.
        /// </summary>
        public event Action<MemSnapshot> OnMemReleasedDueToFullStorage;

        [Header("KeyId -> 항목 딕셔너리 (추후 채워짐)")]
        private Dictionary<string, CapturedMemEntry> capturedMemDictionary = new Dictionary<string, CapturedMemEntry>();

        private void OnEnable()
        {
            MemEvents.OnMemCaptured += HandleMemCaptured;
        }

        private void OnDisable()
        {
            MemEvents.OnMemCaptured -= HandleMemCaptured;
        }

        /// <summary>
        /// 목록이 현재 최대치(MaxCapacity)만큼 채워져 있는지 확인하고, 모자라면 빈 칸을 뒤에 추가로 채운다.
        /// 최대치가 나중에 늘어나도(슬롯당 개수/최대 페이지 수 조정) 기존 데이터는 그대로 유지된 채 빈 칸만 추가된다.
        /// </summary>
        private void EnsureCapacity()
        {
            int targetCapacity = MaxCapacity;

            if (capturedMems.Count > targetCapacity)
            {
                Debug.LogWarning($"[MemCaptureManager] 목록 크기({capturedMems.Count})가 현재 최대치({targetCapacity})보다 큽니다. 초과분은 삭제하지 않고 유지하지만, 그리드에서 정상적으로 표시되지 않을 수 있습니다.");
                return;
            }

            while (capturedMems.Count < targetCapacity)
            {
                capturedMems.Add(CapturedMemEntry.CreateEmpty());
            }

            Debug.Log($"[MemCaptureManager] 창고 용량 확인 완료: {capturedMems.Count} / {targetCapacity}");
        }

        /// <summary>
        /// 언락된 페이지 수를 시작값~최대치 범위 안으로 보정한다(0 이하면 시작 페이지 수로 초기화).
        /// Awake뿐 아니라 UnlockedPageCount/UnlockedCapacity 프로퍼티를 읽을 때마다도 호출되므로,
        /// Awake 실행 타이밍과 무관하게 항상 유효한 값을 보장한다.
        /// </summary>
        private void EnsureUnlockedPageCount()
        {
            if (unlockedPageCount <= 0)
            {
                unlockedPageCount = startingPageCount;
            }

            unlockedPageCount = Mathf.Clamp(unlockedPageCount, startingPageCount, maxPages);
        }

        /// <summary>
        /// 다음 페이지를 1개 언락한다(사용 가능 페이지 수 +1). 이미 최대 페이지에 도달했으면 아무 것도 하지 않고
        /// false를 반환한다. 비용 확인/차감은 호출하는 쪽(MemStorageUpgrade/UpgradePopupUI)의 책임이며,
        /// 이 메서드는 순수하게 사용 가능 범위만 넓힌다.
        /// </summary>
        public bool UnlockNextPage()
        {
            EnsureUnlockedPageCount();

            if (unlockedPageCount >= maxPages) return false;

            unlockedPageCount++;
            Debug.Log($"[MemCaptureManager] 멤창고 페이지 언락: {unlockedPageCount} / {maxPages}");

            OnStorageCapacityChanged?.Invoke();
            return true;
        }

        private void HandleMemCaptured(PikachuMem mem, MemSnapshot snapshot)
        {
            int emptyIndex = FindFirstEmptyIndex();
            if (emptyIndex < 0)
            {
                // 방어 코드: 언락된 범위 안에 빈 칸이 하나도 없으면(가득 참) 포획 자체는 성공했더라도 저장하지 않고 놓아준다(방생).
                Debug.LogWarning($"[MemCaptureManager] 창고가 가득 차서 포획한 멤을 놓아주었습니다(방생): MemId={snapshot.memId}, Exploration={snapshot.explorationStat}, UnlockedCapacity={UnlockedCapacity}");
                OnMemReleasedDueToFullStorage?.Invoke(snapshot);
                return;
            }

            capturedMems[emptyIndex] = new CapturedMemEntry
            {
                KeyId = Guid.NewGuid().ToString(),
                MemId = snapshot.memId,
                ExplorationStat = snapshot.explorationStat,
                IsActive = false
            };

            Debug.Log($"[MemCaptureManager] 포획 데이터 저장: index={emptyIndex} / MemId={snapshot.memId} / Exploration={snapshot.explorationStat}");

            OnCapturedMemsChanged?.Invoke();
        }

        /// <summary>목록에서 첫 번째 빈 칸(IsEmpty)의 인덱스를 찾는다. 언락된 범위(UnlockedCapacity) 안에서만 찾으며, 없으면 -1.</summary>
        private int FindFirstEmptyIndex()
        {
            int limit = Mathf.Min(capturedMems.Count, UnlockedCapacity);

            for (int i = 0; i < limit; i++)
            {
                if (capturedMems[i].IsEmpty) return i;
            }

            return -1;
        }

        /// <summary>
        /// 지정한 두 인덱스(전체 목록 기준, 페이지 오프셋 포함)의 항목 위치를 서로 바꾼다.
        /// 두 인덱스 모두 실제 범위 안이면 되고, 빈 칸 <-> 빈 칸, 빈 칸 <-> 채워진 칸, 채워진 칸 <-> 채워진 칸 교체를 모두 지원한다.
        /// 창고 그리드에서 슬롯을 드래그앤드롭으로 옮길 때 사용된다.
        /// </summary>
        public void SwapEntries(int indexA, int indexB)
        {
            if (indexA == indexB) return;

            if (indexA < 0 || indexA >= capturedMems.Count || indexB < 0 || indexB >= capturedMems.Count)
            {
                Debug.LogWarning($"[MemCaptureManager] SwapEntries 범위 오류: indexA={indexA}, indexB={indexB}, count={capturedMems.Count}");
                return;
            }

            (capturedMems[indexA], capturedMems[indexB]) = (capturedMems[indexB], capturedMems[indexA]);
            Debug.Log($"[MemCaptureManager] 멤 위치 교체: index {indexA} <-> {indexB}");

            OnCapturedMemsChanged?.Invoke();
        }

        /// <summary>
        /// 정렬된 순서를 적용한다. 정렬 기준(멤 ID/등급/스탯 등) 판단은 이 클래스가 하지 않는다 - 카탈로그(MemData)에
        /// 접근 가능한 상위(MemStorageUI)가 이미 정렬해서 넘겨준 결과를 그대로 반영만 한다.
        /// 빈 칸이 아닌 항목들을 앞에서부터(index 0부터) 채우고, 나머지 칸은 전부 빈 칸으로 채운다(빈 칸은 뒤로 몰림).
        /// </summary>
        /// <param name="sortedNonEmptyEntries">빈 칸을 제외한, 원하는 순서로 미리 정렬된 항목 목록</param>
        public void ApplySortedOrder(IReadOnlyList<CapturedMemEntry> sortedNonEmptyEntries)
        {
            if (sortedNonEmptyEntries == null) return;

            if (sortedNonEmptyEntries.Count > capturedMems.Count)
            {
                Debug.LogWarning($"[MemCaptureManager] ApplySortedOrder 실패: 정렬된 항목 수({sortedNonEmptyEntries.Count})가 창고 크기({capturedMems.Count})보다 많습니다.");
                return;
            }

            for (int i = 0; i < capturedMems.Count; i++)
            {
                capturedMems[i] = i < sortedNonEmptyEntries.Count ? sortedNonEmptyEntries[i] : CapturedMemEntry.CreateEmpty();
            }

            Debug.Log($"[MemCaptureManager] 정렬 순서 적용 완료: 채워진 항목 {sortedNonEmptyEntries.Count}개 / 전체 {capturedMems.Count}칸");

            OnCapturedMemsChanged?.Invoke();
        }



        /// <summary>
        /// 해당 코드는 테스트함수며, 혹시나 커밋실수를 하여 저장될경우 꼭 제거부탁드립니다.
        /// </summary>
        [ContextMenu("Function: Add Test Mem")]
        public void AddTestMemForDebug()
        {
            int emptyIndex = FindFirstEmptyIndex();
            if (emptyIndex < 0)
            {
                Debug.LogWarning("[MemCaptureManager Test] 창고가 가득 차서 테스트 멤을 추가할 수 없습니다!");
                return;
            }

            capturedMems[emptyIndex] = new CapturedMemEntry
            {
                KeyId = Guid.NewGuid().ToString(),
                MemId = "Mem_Normal_01",
                ExplorationStat = UnityEngine.Random.Range(20, 101),
                IsActive = false
            };

            Debug.Log($"<color=cyan>[Test] 테스트 멤 주입 성공!</color> Index: {emptyIndex} | MemId: {capturedMems[emptyIndex].MemId} | 스탯: {capturedMems[emptyIndex].ExplorationStat}");

            OnCapturedMemsChanged?.Invoke();
        }
    }
}
