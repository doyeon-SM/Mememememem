using System.Collections.Generic;
using UnityEngine;
using HDY.Capture;
using HDY.Item;
using HDY.Inventory;

namespace HDY.Exploration
{
    /// <summary>
    /// 지역(ExplorationZoneData) 하나의 진행 상태.
    /// "탐험 시설을 여러 채 짓는" 방식이 아니라 "지역마다 독립된 탐험대"를 두는 방식이라, 이 진행 상태는
    /// 특정 씬 오브젝트나 SO 인스턴스가 아니라 변경되지 않는 zoneId를 키로 ExplorationRuntime이 들고 있다.
    /// </summary>
    public enum ExplorationState
    {
        Idle,            // 대기 중 - 멤 배치/해제 가능, 조건 충족 시 시작 가능
        InProgress,      // 진행 중 - 슬롯이 잠기고 남은 시간이 흐른다
        ReadyToComplete  // 시간 종료 - 완료 버튼으로 보상 수령 대기
    }

    /// <summary>
    /// 지역 하나의 런타임 진행 데이터(배치된 멤, 상태, 경과 시간). 순수 데이터 보관용 private 클래스.
    /// </summary>
    internal class ExplorationProgress
    {
        public readonly ExplorationZoneData zone;
        public readonly List<CapturedMemEntry> assignedEntries = new List<CapturedMemEntry>();
        public ExplorationState state = ExplorationState.Idle;
        public float elapsedTime;

        /// <summary>지정 지역 정의를 사용하는 초기 탐험 진행 데이터를 생성한다.</summary>
        public ExplorationProgress(ExplorationZoneData zone)
        {
            this.zone = zone;
        }
    }

    /// <summary>
    /// 탐험 시스템의 런타임 상태를 관리하는 싱글톤.
    /// [지역별 독립 진행] 씬에 "탐험 시설" 오브젝트를 여러 개 두는 대신, 지역(ExplorationZoneData) 하나하나가
    /// 곧 독립된 탐험대 슬롯 역할을 한다 - 지역 A가 진행 중이어도 지역 B에 별도로 멤을 배치해 동시에 진행할 수 있다.
    /// ExplorationPanelUI는 좌우로 지역 "페이지"를 넘기며 그 지역의 진행 상태(GetState 등)를 그대로 읽어 보여준다.
    ///
    /// [IsActive 재사용] 생산/제작 시설과 동일하게 CapturedMemEntry.IsActive를 그대로 쓴다. 다만 생산 시설과 달리
    /// "슬롯에 배치"와 "탐험 시작"은 별개 시점이다 - TryAssignMem 단계에서는 IsActive를 건드리지 않고, TryStart가
    /// 호출되어야 비로소 true가 된다(창고 그리드의 활성 표시/우클릭 해제 버튼도 이 시점부터 나타난다).
    ///
    /// [Kyusoo 생산/제작 시설과의 교차 배치 검사 생략] 탐험 중인 멤을 창고에서 드래그해 생산/제작 시설에 배치하는
    /// 것을 막는 IsActive 검사는 이번 범위에서 Kyusoo의 ProductionFacilityRuntime/ProductionCraftRuntime.TryAddMem에
    /// 추가하지 않기로 했다(요청 시 [HDY 요청]으로 추후 진행). 대신 창고 쪽 "해제하기" 버튼이 탐험 중인 멤을 잘못
    /// 풀어버리지 않도록 MemStorageUI.HandleReleaseRequested에서 TryGetExplorationInfo로 먼저 확인한다.
    ///
    /// [싱글톤 Resolve 패턴] 다른 매니저(MemCaptureManager 등)와 동일하게 DontDestroyOnLoad + Resolve(existing) 패턴을 따른다.
    /// </summary>
    public class ExplorationRuntime : MonoBehaviour
    {
        private const int MaxSlotCount = 5;

        public static ExplorationRuntime Instance { get; private set; }

        /// <summary>
        /// 지역 zoneId 단위로 배치/시작/취소/완료 등 진행 상태가 바뀔 때마다 발행. 창고 그리드(MemStorageUI_Grid)가
        /// 구독해서 활성 멤 아이콘을 자동 갱신하고, ExplorationPanelUI도 구독해서 현재 보고 있는 페이지를 다시 그린다.
        /// (ProductionFacilityRuntime.OnMemDeploymentChanged와 동일한 역할의 정적 이벤트)
        /// </summary>
        public static event System.Action OnMemDeploymentChanged;

        private readonly Dictionary<string, ExplorationProgress> progressByZoneId
            = new Dictionary<string, ExplorationProgress>(System.StringComparer.Ordinal);
        private readonly HashSet<ExplorationZoneData> invalidZoneIdWarnings
            = new HashSet<ExplorationZoneData>();
        private readonly HashSet<string> duplicateZoneIdWarnings
            = new HashSet<string>(System.StringComparer.Ordinal);

        private WarehouseInventory cachedWarehouse;
        private ItemCatalogManager cachedItemCatalog;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            foreach (var progress in progressByZoneId.Values)
            {
                var zone = progress.zone;

                if (progress.state != ExplorationState.InProgress) continue;

                progress.elapsedTime += Time.deltaTime;

                if (progress.elapsedTime >= zone.explorationDuration)
                {
                    progress.elapsedTime = zone.explorationDuration;
                    progress.state = ExplorationState.ReadyToComplete;

                    Debug.Log($"[ExplorationRuntime] '{zone.zoneName}' 탐험 시간 종료 - 완료 대기 상태로 전환.");
                    OnMemDeploymentChanged?.Invoke();
                }
            }
        }

        /// <summary>다른 스크립트가 들고 있는 참조가 비어있을 때 공용으로 쓰는 폴백 탐색(다른 매니저들과 동일한 패턴).</summary>
        public static ExplorationRuntime Resolve(ExplorationRuntime existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<ExplorationRuntime>();
            if (found == null)
            {
                Debug.LogWarning("[ExplorationRuntime] 씬에서 ExplorationRuntime을 찾을 수 없습니다.");
            }

            return found;
        }

        /// <summary>
        /// 지역의 zoneId를 기준으로 기존 진행 데이터를 가져오거나 새로 생성한다.
        /// zoneId는 저장과 시스템 간 식별에 사용하는 영구 키이므로 비어 있으면 진행 데이터를 만들지 않는다.
        /// 서로 다른 SO가 같은 zoneId를 사용하면 동일 지역으로 취급하되 최초로 등록된 지역 정의를 유지한다.
        /// </summary>
        private ExplorationProgress GetOrCreateProgress(ExplorationZoneData zone)
        {
            if (zone == null) return null;

            string zoneId = zone.zoneId != null ? zone.zoneId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(zoneId))
            {
                if (invalidZoneIdWarnings.Add(zone))
                {
                    Debug.LogWarning($"[ExplorationRuntime] '{zone.name}'의 zoneId가 비어 있어 탐험 진행 상태를 만들 수 없습니다.", zone);
                }

                return null;
            }

            if (!progressByZoneId.TryGetValue(zoneId, out var progress))
            {
                progress = new ExplorationProgress(zone);
                progressByZoneId[zoneId] = progress;
            }
            else if (progress.zone != zone && duplicateZoneIdWarnings.Add(zoneId))
            {
                Debug.LogWarning(
                    $"[ExplorationRuntime] 서로 다른 ExplorationZoneData가 같은 zoneId '{zoneId}'를 사용합니다. " +
                    $"'{progress.zone.name}'과 '{zone.name}'은 동일 지역 진행 상태를 공유합니다.",
                    zone);
            }

            return progress;
        }

        /// <summary>이 지역에 현재 배치된 멤 목록(슬롯 0번부터 순서대로). 빈 슬롯은 목록에 포함되지 않는다(항상 앞에서부터 채워짐).</summary>
        public IReadOnlyList<CapturedMemEntry> GetAssignedEntries(ExplorationZoneData zone)
        {
            var progress = GetOrCreateProgress(zone);
            return progress != null ? progress.assignedEntries : System.Array.Empty<CapturedMemEntry>();
        }

        public ExplorationState GetState(ExplorationZoneData zone)
        {
            var progress = GetOrCreateProgress(zone);
            return progress != null ? progress.state : ExplorationState.Idle;
        }

        public float GetElapsedTime(ExplorationZoneData zone)
        {
            var progress = GetOrCreateProgress(zone);
            return progress != null ? progress.elapsedTime : 0f;
        }

        /// <summary>탐험 종료까지 남은 시간(초). Idle이어도 0 이상의 값을 안전하게 반환한다.</summary>
        public float GetRemainingTime(ExplorationZoneData zone)
        {
            if (zone == null) return 0f;
            var progress = GetOrCreateProgress(zone);
            if (progress == null) return 0f;

            return Mathf.Max(0f, zone.explorationDuration - progress.elapsedTime);
        }

        /// <summary>현재 배치된 멤들의 탐험레벨(CapturedMemEntry.ExplorationStat) 합.</summary>
        public int GetExplorationLevelSum(ExplorationZoneData zone)
        {
            var progress = GetOrCreateProgress(zone);
            if (progress == null) return 0;

            int sum = 0;
            foreach (var entry in progress.assignedEntries)
            {
                if (entry != null) sum += entry.ExplorationStat;
            }

            return sum;
        }

        /// <summary>
        /// 현재 배치된 멤들의 탐험레벨 합이 요구치를 얼마나 초과했는지에 따른 보상 보너스 배율(각 보상의
        /// 최대수량에만 곱해진다 - 최소수량은 항상 고정). 탐험을 시작하려면 합이 요구치 이상이어야 하므로
        /// 항상 1 이상이다. 지역 카드의 보상 미리보기(ExplorationPanelUI.RefreshRewardPreview)와 실제 완료
        /// 지급(TryComplete)이 정확히 같은 배율을 보도록 계산을 여기 한 곳에만 둔다 - 지역을 아직 시작하지
        /// 않았어도(배치만 해둔 상태) 미리보기에 즉시 반영된다.
        /// </summary>
        public float GetBonusRatio(ExplorationZoneData zone)
        {
            if (zone == null || zone.requiredExplorationLevel <= 0) return 1f;

            int sum = GetExplorationLevelSum(zone);
            return Mathf.Max(1f, (float)sum / zone.requiredExplorationLevel);
        }

        /// <summary>
        /// 지역에 연결된 맵의 모든 웨이포인트가 해금되어 현재 탐험 가능한지 확인한다.
        /// requiredCompletedMap이 비어 있으면 웨이포인트 조건이 없는 지역으로 취급한다.
        /// 조건 맵이 있는데 WayPointManager가 없으면 해금 상태를 확인할 수 없으므로 탐험 불가로 처리한다.
        /// </summary>
        public bool IsZoneAvailable(ExplorationZoneData zone)
        {
            if (zone == null) return false;
            if (zone.requiredCompletedMap == null) return true;
            if (WayPointManager.Instance == null) return false;

            return WayPointManager.Instance.AreAllWayPointsUnlockedInMap(zone.requiredCompletedMap);
        }

        /// <summary>
        /// 지금 탐험을 시작할 수 있는 상태인지 확인한다. 연결 맵의 웨이포인트가 모두 해금되어 있고,
        /// 진행 상태가 Idle이며, 1마리 이상 배치돼 있고, 탐험레벨 합이 요구치 이상이어야 한다.
        /// </summary>
        public bool CanStart(ExplorationZoneData zone)
        {
            if (zone == null) return false;

            var progress = GetOrCreateProgress(zone);
            if (progress == null) return false;
            if (!IsZoneAvailable(zone)) return false;
            if (progress.state != ExplorationState.Idle) return false;
            if (progress.assignedEntries.Count == 0) return false;

            return GetExplorationLevelSum(zone) >= zone.requiredExplorationLevel;
        }

        /// <summary>이 멤이 이미 어느 지역엔가(상태 무관) 배치돼 있는지 확인한다. 한 멤을 여러 지역에 동시에 배치하는 것을 막기 위함.</summary>
        private bool IsAssignedToAnyZone(CapturedMemEntry entry)
        {
            foreach (var progress in progressByZoneId.Values)
            {
                if (progress.assignedEntries.Contains(entry)) return true;
            }

            return false;
        }

        /// <summary>
        /// 창고에서 드래그해온 멤을 이 지역의 탐험대 슬롯에 배치한다. 연결 맵의 웨이포인트가 모두 해금되고
        /// Idle 상태일 때만 가능하며, 슬롯이 가득 찼거나(5마리) 이미 다른 지역에 배치돼 있으면 실패한다.
        /// </summary>
        public bool TryAssignMem(ExplorationZoneData zone, CapturedMemEntry entry)
        {
            if (zone == null || entry == null) return false;

            var progress = GetOrCreateProgress(zone);
            if (progress == null) return false;

            if (!IsZoneAvailable(zone))
            {
                Debug.LogWarning($"[ExplorationRuntime] '{zone.zoneName}' 지역의 웨이포인트 해금 조건을 충족하지 않아 멤을 배치할 수 없습니다.");
                return false;
            }

            if (progress.state != ExplorationState.Idle)
            {
                Debug.LogWarning($"[ExplorationRuntime] '{zone.zoneName}'은(는) 이미 탐험이 진행 중이라 멤을 배치할 수 없습니다.");
                return false;
            }

            if (entry.IsActive)
            {
                Debug.LogWarning($"이미 다른 시설이나 탐험대에 배치되어 있습니다.");
                return false;
            }

            if (progress.assignedEntries.Count >= MaxSlotCount)
            {
                Debug.LogWarning($"[ExplorationRuntime] '{zone.zoneName}' 탐험대 슬롯이 가득 찼습니다({MaxSlotCount}마리).");
                return false;
            }

            if (progress.assignedEntries.Contains(entry)) return false;

            if (IsAssignedToAnyZone(entry))
            {
                Debug.LogWarning($"[ExplorationRuntime] {entry.MemId}은(는) 이미 다른 지역의 탐험대에 배치되어 있습니다.");
                return false;
            }

            progress.assignedEntries.Add(entry);
            OnMemDeploymentChanged?.Invoke();
            return true;
        }

        /// <summary>Idle 상태의 탐험 슬롯에서 멤을 뺀다(우클릭 해제 또는 드래그로 바깥에 놓았을 때). 진행 중이면 거부.</summary>
        public bool TryRemoveMem(ExplorationZoneData zone, CapturedMemEntry entry)
        {
            if (zone == null || entry == null) return false;

            var progress = GetOrCreateProgress(zone);
            if (progress == null) return false;

            if (progress.state != ExplorationState.Idle)
            {
                Debug.LogWarning($"[ExplorationRuntime] '{zone.zoneName}' 탐험 중에는 멤을 뺄 수 없습니다. 취소 버튼을 이용해주세요.");
                return false;
            }

            if (!progress.assignedEntries.Remove(entry)) return false;

            OnMemDeploymentChanged?.Invoke();
            return true;
        }

        /// <summary>탐험대 슬롯 두 칸의 순서를 서로 바꾼다(같은 지역 안에서 드래그앤드롭으로 재배치할 때). Idle 상태에서만 허용.</summary>
        public bool TrySwapSlots(ExplorationZoneData zone, int indexA, int indexB)
        {
            if (zone == null) return false;

            var progress = GetOrCreateProgress(zone);
            if (progress == null) return false;
            if (progress.state != ExplorationState.Idle) return false;

            var entries = progress.assignedEntries;
            if (indexA < 0 || indexA >= entries.Count || indexB < 0 || indexB >= entries.Count) return false;
            if (indexA == indexB) return false;

            (entries[indexA], entries[indexB]) = (entries[indexB], entries[indexA]);
            OnMemDeploymentChanged?.Invoke();
            return true;
        }

        /// <summary>탐험을 시작한다. CanStart를 통과해야 하며, 성공하면 배치된 멤 전원을 IsActive=true로 전환한다.</summary>
        public bool TryStart(ExplorationZoneData zone)
        {
            if (!CanStart(zone)) return false;

            var progress = GetOrCreateProgress(zone);
            if (progress == null) return false;
            progress.state = ExplorationState.InProgress;
            progress.elapsedTime = 0f;

            foreach (var entry in progress.assignedEntries)
            {
                if (entry != null) entry.IsActive = true;
            }

            Debug.Log($"[ExplorationRuntime] '{zone.zoneName}' 탐험 시작. 배치 {progress.assignedEntries.Count}마리, 탐험레벨 합 {GetExplorationLevelSum(zone)}/{zone.requiredExplorationLevel}.");

            OnMemDeploymentChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 진행 중인(또는 완료 대기 중인) 탐험을 취소한다. 보상은 지급되지 않는다. 배치된 멤은 슬롯에서 빠지지 않고
        /// 그대로 남되(다시 바로 시작할 수 있도록), IsActive만 false로 되돌리고 경과 시간을 0으로 리셋한다.
        /// </summary>
        public bool TryCancel(ExplorationZoneData zone)
        {
            if (zone == null) return false;

            var progress = GetOrCreateProgress(zone);
            if (progress == null) return false;
            if (progress.state == ExplorationState.Idle) return false;

            progress.state = ExplorationState.Idle;
            progress.elapsedTime = 0f;

            foreach (var entry in progress.assignedEntries)
            {
                if (entry != null) entry.IsActive = false;
            }

            Debug.Log($"[ExplorationRuntime] '{zone.zoneName}' 탐험 취소. 배치된 멤은 슬롯에 남아 바로 재시작할 수 있습니다.");

            OnMemDeploymentChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 완료 대기 상태(ReadyToComplete)의 탐험을 완료 처리한다. GetBonusRatio로 계산한 배율만큼 각 보상의
        /// 최대수량을 올린 뒤(최소수량은 고정) 모든 보상 항목을 랜덤 수량으로 지급한다. 창고에 전부 들어갈
        /// 공간이 없으면 아무것도 지급하지 않고 실패 처리한다(경고 로그만 남기고 상태는 그대로 유지 - 나중에
        /// 다시 시도 가능).
        /// </summary>
        public bool TryComplete(ExplorationZoneData zone)
        {
            if (zone == null) return false;

            var progress = GetOrCreateProgress(zone);
            if (progress == null) return false;

            if (progress.state != ExplorationState.ReadyToComplete)
            {
                Debug.LogWarning($"[ExplorationRuntime] '{zone.zoneName}'은(는) 아직 완료할 수 있는 상태가 아닙니다.");
                return false;
            }

            float ratio = GetBonusRatio(zone);

            var warehouse = ResolveWarehouse();
            if (warehouse == null)
            {
                Debug.LogWarning("[ExplorationRuntime] 씬에서 WarehouseInventory를 찾을 수 없어 보상을 지급할 수 없습니다.");
                return false;
            }

            cachedItemCatalog = ItemCatalogManager.Resolve(cachedItemCatalog);

            var requests = new List<(ItemData item, int amount)>();
            foreach (var reward in zone.rewards)
            {
                if (reward == null || string.IsNullOrEmpty(reward.itemId)) continue;

                var itemData = cachedItemCatalog != null ? cachedItemCatalog.FindItemData(reward.itemId) : null;
                if (itemData == null)
                {
                    Debug.LogWarning($"[ExplorationRuntime] 보상 아이템ID '{reward.itemId}'를 카탈로그에서 찾을 수 없어 이 항목은 건너뜁니다.");
                    continue;
                }

                int scaledMax = Mathf.Max(reward.minAmount, Mathf.RoundToInt(reward.maxAmount * ratio));
                int amount = Random.Range(reward.minAmount, scaledMax + 1);
                requests.Add((itemData, amount));
            }

            if (!warehouse.CanFitAll(requests))
            {
                Debug.LogWarning($"[ExplorationRuntime] 창고 공간이 부족해 '{zone.zoneName}' 탐험 보상을 지급할 수 없습니다. 창고를 정리한 뒤 다시 완료해주세요.");
                return false;
            }

            foreach (var (item, amount) in requests)
            {
                warehouse.AddItem(item, amount);
            }

            foreach (var entry in progress.assignedEntries)
            {
                if (entry != null) entry.IsActive = false;
            }
            progress.assignedEntries.Clear();
            progress.state = ExplorationState.Idle;
            progress.elapsedTime = 0f;

            Debug.Log($"[ExplorationRuntime] '{zone.zoneName}' 탐험 완료. 보상 {requests.Count}종 지급(배율 {ratio:F2}배).");

            OnMemDeploymentChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 이 멤이 지금 어딘가에서 탐험 중(Idle이 아닌 상태로 배치됨)인지 확인한다.
        /// MemStorageUI가 창고의 "해제하기" 버튼 처리 시 먼저 확인해서, 탐험 중인 멤을 잘못 풀어버리지 않도록 쓰인다.
        /// </summary>
        public bool TryGetExplorationInfo(CapturedMemEntry entry, out ExplorationZoneData zone)
        {
            zone = null;
            if (entry == null) return false;

            foreach (var progress in progressByZoneId.Values)
            {
                if (progress.state == ExplorationState.Idle) continue;
                if (!progress.assignedEntries.Contains(entry)) continue;

                zone = progress.zone;
                return true;
            }

            return false;
        }

        private WarehouseInventory ResolveWarehouse()
        {
            if (cachedWarehouse == null)
            {
                cachedWarehouse = FindFirstObjectByType<WarehouseInventory>();
            }

            return cachedWarehouse;
        }
    }
}
