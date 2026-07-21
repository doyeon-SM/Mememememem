using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MemSystem.Data;
using HDY.Capture;
using HDY.Mem;
using HDY.Item;
using HDY.Exploration;

namespace HDY.UI
{
    /// <summary>
    /// 탐험 중앙 패널. ProductionPanelUI와 비슷한 위치의 화면이지만, "시설 하나 = 작업 하나"인 생산 시설과 달리
    /// 이 패널은 지역(ExplorationZoneData) 여러 개를 좌우로 넘겨가며 각 지역의 독립된 탐험대 슬롯을 보여준다.
    /// 실제 진행 상태(배치된 멤/타이머/보상)는 이 패널이 아니라 ExplorationRuntime(싱글톤)이 지역별로 들고 있으므로,
    /// 패널이 열려있지 않아도(UIManager가 Destroy해도) 탐험은 백그라운드에서 계속 진행된다.
    ///
    /// [좌측 Grid / 우측 Info] 멤을 고르는 쪽(창고 그리드 + 정보 패널 + 정렬 버튼)은 기존 MemStorageUI +
    /// MemStorageUI_Grid/Info/Sort를 그대로 자식으로 붙여서 재사용한다 - 이 클래스는 그쪽과 직접 통신하지 않고,
    /// 오직 MemSlotUI의 드래그앤드롭 이벤트로만 연결된다.
    /// [중요 - MemStorageUI_Grid 단독 배치 금지] ProductionPanelUI.cs(Kyusoo)를 확인해보면, 그 패널은 애초에
    /// 멤 선택용 그리드를 자체적으로 갖고 있지 않다 - targetFacility.DeployedMems/DeployedMemEntries(이미 배치된
    /// 데이터)를 5개 슬롯에 그대로 표시할 뿐이고, 정렬/스왑/아이콘 조회 로직 자체가 그 파일에 존재하지 않는다.
    /// 즉 "Grid만 단독으로 둬도 되는 참고할 만한 가벼운 패턴"은 프로젝트 어디에도 없고, 정렬(MemStorageUI_Sort의
    /// OnSortRequested 처리)/이동(Grid의 OnSwapRequested 처리)/아이콘(findMemData 콜백 전달)은 전부
    /// MemStorageUI 하나만 실제로 구현하고 있다. 그래서 좌측에는 반드시 기존 창고 UI와 동일하게 MemStorageUI가
    /// grid/info/sort를 전부 자식으로 데리고 있는 구성 그대로 배치해야 한다(MemStorageUI_Grid만 따로 떼어
    /// 붙이면 슬롯은 보여도 정렬/이동/아이콘이 전부 동작하지 않는다).
    /// 정렬 버튼은 Awake에서 MemStorageUI_Sort.HideSortButtonsExcept(MemStatClass.Exploration)를 호출해
    /// MemId/Tier/탐험 3개만 남긴다.
    ///
    /// [탐험대 5슬롯 재사용] 새 슬롯 클래스를 만들지 않고 창고 그리드와 동일한 HDY.UI.MemSlotUI를 그대로 쓴다.
    /// 이 패널은 자신의 memSlots 5개 이벤트만 별도로 구독해서 "탐험 배치"라는 다른 의미로 해석한다 - 창고
    /// 그리드(MemStorageUI_Grid)는 자신이 구독한 슬롯에서만 이벤트를 받으므로 서로 간섭하지 않는다.
    /// 배치된 슬롯에는 항상(정렬 상태와 무관하게) 탐험 아이콘 + 그 멤의 실제 탐험레벨(CapturedMemEntry.ExplorationStat)
    /// 숫자를 표시한다 - 창고 그리드처럼 "정렬 기준에 따라 표시가 바뀌는" 개념이 없고, 탐험 슬롯은 애초에
    /// 탐험레벨을 보여주는 것 자체가 목적이므로 항상 고정으로 보여준다.
    ///
    /// [드래그로 배치/취소]
    /// - 창고 슬롯 -> 탐험 슬롯: 배치 시도(ExplorationRuntime.TryAssignMem)
    /// - 탐험 슬롯 -> 탐험 슬롯: 슬롯끼리 순서 교체(TrySwapSlots)
    /// - 탐험 슬롯 -> 그 외(창고 등) 또는 빈 공간: 배치 취소(TryRemoveMem) - OnSlotSwapRequested가 아예 발생하지
    ///   않는 "빈 공간에 드롭"까지 잡기 위해, 드래그 시작 슬롯을 기억해두고(OnSlotDragBegan) OnSlotSwapRequested가
    ///   같은 프레임에 오지 않은 채 OnSlotDragEnded가 오면 그것도 취소로 처리한다.
    /// - 우클릭: Idle 상태에서만 즉시 배치 해제. 진행 중에는 슬롯을 덮는 progressOverlay가 입력을 막아 애초에
    ///   우클릭/드래그가 슬롯에 닿지 않으므로, 진행 중 배치 해제는 오직 액션 버튼(취소)으로만 가능하다.
    ///
    /// [보상 아이콘 그리드 - 동적 생성] rewardIconGridParent(GridLayoutGroup 부착)에 rewardIconPrefab을 지역의
    /// 보상 개수만큼 Instantiate한다. 표시되는 최대수량 텍스트는 항상 ExplorationRuntime.GetBonusRatio(현재 배치된
    /// 멤들의 탐험레벨 합/요구치 비율)를 반영한 값이라, 멤을 배치/해제할 때마다(RefreshCurrentZoneDisplay 호출 시)
    /// 다시 계산되어 화면에 반영된다. 보상이 9개(MaxVisibleRewardSlots)를 초과하면 마지막 한 칸을 "..." 오버플로우
    /// 표시로 바꾸고, 그 칸에 마우스를 올리면(ExplorationRewardIconUI.OnPointerEntered) 남은 보상 목록을
    /// rewardOverflowPopup 안의 그리드(rewardOverflowPopupGridParent)에 같은 프리팹으로 채워 보여준다.
    /// </summary>
    public class ExplorationPanelUI : MonoBehaviour
    {
        private const int MaxVisibleRewardSlots = 9;
        private const int NormalRewardSlotCount = MaxVisibleRewardSlots - 1; // 8 - 나머지 1칸은 "..." 오버플로우용

        [Header("데이터 참조")]
        [SerializeField] private ExplorationZoneData[] zones;
        [SerializeField] private ExplorationRuntime runtime;
        [SerializeField] private MemCatalogManager memCatalogManager;
        [SerializeField] private ItemCatalogManager itemCatalogManager;

        [Header("지역 페이지 카드 (좌우 이동)")]
        [SerializeField] private Image zoneImage;
        [SerializeField] private TMP_Text zoneNameText;
        [Tooltip("요구 탐험레벨 + 소요시간을 함께 표시(소요시간은 줄바꿈 후 표시).")]
        [SerializeField] private TMP_Text requiredLevelText;
        [SerializeField] private Button prevZoneButton;
        [SerializeField] private Button nextZoneButton;

        [Header("보상 아이콘 그리드 (동적 생성)")]
        [Tooltip("보상 아이콘 한 칸의 프리팹(ExplorationRewardIconUI).")]
        [SerializeField] private ExplorationRewardIconUI rewardIconPrefab;
        [Tooltip("보상 아이콘들이 자동 생성될 부모. GridLayoutGroup이 붙어있어야 한다.")]
        [SerializeField] private Transform rewardIconGridParent;

        [Header("보상 초과 목록 팝업 (보상이 9개 초과일 때 마지막 칸에 마우스를 올리면 표시)")]
        [SerializeField] private GameObject rewardOverflowPopup;
        [Tooltip("팝업 안에서 남은 보상 아이콘들이 자동 생성될 부모. GridLayoutGroup이 붙어있어야 한다.")]
        [SerializeField] private Transform rewardOverflowPopupGridParent;

        [Header("탐험대 5슬롯")]
        [SerializeField] private MemSlotUI[] memSlots = new MemSlotUI[5];
        [Tooltip("진행 중/완료 대기 중일 때 5슬롯을 덮는 패널(남은 시간 표시 + 입력 차단).")]
        [SerializeField] private GameObject progressOverlay;
        [SerializeField] private TMP_Text remainingTimeText;
        [Tooltip("탐험 슬롯에 멤이 배치되면 항상 표시할 탐험 아이콘(그 옆에 실제 탐험레벨 숫자도 함께 표시됨).")]
        [SerializeField] private Sprite explorationStatIcon;

        [Header("하단 정보 / 액션 버튼")]
        [SerializeField] private TMP_Text explorationLevelSumText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionButtonText;

        [Header("멤 선택 그리드용 정렬 버튼 (MemId/Tier/탐험 3개만 남김)")]
        [SerializeField] private MemStorageUI_Sort sort;

        private int currentZoneIndex;

        // 우리 5슬롯 중 드래그가 시작된 로컬 인덱스. 드래그 중이 아니면 -1.
        private int pendingDragSourceLocalIndex = -1;

        // 드래그가 스왑/취소 중 하나로 이미 처리됐는지(OnSlotDragEnded에서 중복 처리 방지용).
        private bool dragHandledAsSwapOrRemoval;

        // 매 갱신마다 새로 Instantiate/Destroy하는 보상 아이콘 인스턴스들(메인 그리드 / 오버플로우 팝업 각각 별도 보관).
        private readonly List<ExplorationRewardIconUI> spawnedRewardIcons = new List<ExplorationRewardIconUI>();
        private readonly List<ExplorationRewardIconUI> spawnedOverflowPopupIcons = new List<ExplorationRewardIconUI>();

        private ExplorationZoneData CurrentZone =>
            (zones != null && zones.Length > 0)
                ? zones[Mathf.Clamp(currentZoneIndex, 0, zones.Length - 1)]
                : null;

        private void Awake()
        {
            runtime = ExplorationRuntime.Resolve(runtime);
            memCatalogManager = MemCatalogManager.Resolve(memCatalogManager);
            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);

            if (sort != null)
            {
                sort.HideSortButtonsExcept(MemStatClass.Exploration);
            }

            if (memSlots != null)
            {
                foreach (var slot in memSlots)
                {
                    if (slot == null) continue;

                    slot.OnSlotSwapRequested += HandleSlotSwapRequested;
                    slot.OnSlotDragBegan += HandleSlotDragBegan;
                    slot.OnSlotDragEnded += HandleSlotDragEnded;
                    slot.OnSlotRightClicked += HandleSlotRightClicked;
                }
            }

            if (prevZoneButton != null) prevZoneButton.onClick.AddListener(GoToPrevZone);
            if (nextZoneButton != null) nextZoneButton.onClick.AddListener(GoToNextZone);
            if (actionButton != null) actionButton.onClick.AddListener(HandleActionButtonClicked);

            HideRewardOverflowPopup();
        }

        private void OnEnable()
        {
            runtime = ExplorationRuntime.Resolve(runtime);
            ExplorationRuntime.OnMemDeploymentChanged += HandleExplorationChanged;

            currentZoneIndex = Mathf.Clamp(currentZoneIndex, 0, (zones != null ? zones.Length - 1 : 0));
            RefreshCurrentZoneDisplay();
        }

        private void OnDisable()
        {
            ExplorationRuntime.OnMemDeploymentChanged -= HandleExplorationChanged;
        }

        private void Update()
        {
            // 남은 시간은 진행 중일 때 매 프레임 부드럽게 갱신한다(구조 전체를 다시 그리는 RefreshCurrentZoneDisplay와는 분리).
            UpdateTimerDisplay();
        }

        private void HandleExplorationChanged()
        {
            RefreshCurrentZoneDisplay();
        }

        public void GoToPrevZone()
        {
            if (zones == null || zones.Length == 0) return;
            currentZoneIndex = Mathf.Max(0, currentZoneIndex - 1);
            RefreshCurrentZoneDisplay();
        }

        public void GoToNextZone()
        {
            if (zones == null || zones.Length == 0) return;
            currentZoneIndex = Mathf.Min(zones.Length - 1, currentZoneIndex + 1);
            RefreshCurrentZoneDisplay();
        }

        /// <summary>현재 페이지(지역)의 카드/슬롯/버튼/타이머를 전부 다시 그린다. 페이지 이동, 배치/해제/시작/취소/완료 등 구조가 바뀔 때마다 호출된다.</summary>
        private void RefreshCurrentZoneDisplay()
        {
            var zone = CurrentZone;
            if (zone == null)
            {
                Debug.LogWarning("[ExplorationPanelUI] 등록된 지역(zones)이 없습니다. 인스펙터에서 지역 SO를 등록해주세요.", this);
                return;
            }

            if (runtime == null)
            {
                Debug.LogWarning("[ExplorationPanelUI] ExplorationRuntime을 찾을 수 없어 탐험 상태를 표시할 수 없습니다.", this);
                return;
            }

            if (zoneImage != null) zoneImage.sprite = zone.zoneImage;
            if (zoneNameText != null) zoneNameText.text = zone.zoneName;

            if (requiredLevelText != null)
            {
                requiredLevelText.text = $"요구 탐험레벨: {zone.requiredExplorationLevel}\n소요시간: {FormatDuration(zone.explorationDuration)}";
            }

            RefreshRewardPreview(zone);
            RefreshMemSlots(zone);

            var state = runtime.GetState(zone);
            int sum = runtime.GetExplorationLevelSum(zone);

            if (explorationLevelSumText != null)
            {
                explorationLevelSumText.text = $"탐험레벨 합: {sum} / {zone.requiredExplorationLevel}";
            }

            if (progressOverlay != null) progressOverlay.SetActive(state != ExplorationState.Idle);

            RefreshActionButton(zone, state);
            UpdateTimerDisplay();

            if (prevZoneButton != null) prevZoneButton.interactable = currentZoneIndex > 0;
            if (nextZoneButton != null) nextZoneButton.interactable = zones != null && currentZoneIndex < zones.Length - 1;
        }

        /// <summary>
        /// 지역 카드에 보상 아이템 아이콘 + (보너스 배율이 반영된) 최대수량을 GridLayoutGroup에 동적으로 채운다.
        /// 보상이 MaxVisibleRewardSlots(9)개를 초과하면 앞의 NormalRewardSlotCount(8)개만 정상 표시하고, 마지막
        /// 한 칸은 "..." 오버플로우 표시로 바꾼 뒤 그 칸에 마우스를 올리면 남은 보상 목록을 팝업으로 보여준다.
        /// </summary>
        private void RefreshRewardPreview(ExplorationZoneData zone)
        {
            ClearSpawnedIcons(spawnedRewardIcons);
            HideRewardOverflowPopup();

            if (rewardIconPrefab == null || rewardIconGridParent == null) return;
            if (zone.rewards == null || zone.rewards.Count == 0) return;

            float ratio = runtime != null ? runtime.GetBonusRatio(zone) : 1f;
            int totalCount = zone.rewards.Count;
            bool hasOverflow = totalCount > MaxVisibleRewardSlots;
            int normalCount = hasOverflow ? NormalRewardSlotCount : totalCount;

            for (int i = 0; i < normalCount; i++)
            {
                var instance = Instantiate(rewardIconPrefab, rewardIconGridParent);
                ApplyRewardVisual(instance, zone.rewards[i], ratio);
                spawnedRewardIcons.Add(instance);
            }

            if (hasOverflow)
            {
                var overflowInstance = Instantiate(rewardIconPrefab, rewardIconGridParent);
                overflowInstance.SetOverflowIndicator();
                spawnedRewardIcons.Add(overflowInstance);

                int overflowStartIndex = normalCount;
                overflowInstance.OnPointerEntered += () => ShowRewardOverflowPopup(zone, overflowStartIndex, ratio);
                overflowInstance.OnPointerExited += HideRewardOverflowPopup;
            }
        }

        /// <summary>보상 아이콘 프리팹 한 칸에 아이템 아이콘 + 보너스 배율이 반영된 최대수량 텍스트를 채운다.</summary>
        private void ApplyRewardVisual(ExplorationRewardIconUI instance, ExplorationRewardEntry reward, float ratio)
        {
            if (reward == null) return;

            ItemData itemData = null;
            if (itemCatalogManager != null && !string.IsNullOrEmpty(reward.itemId))
            {
                itemData = itemCatalogManager.FindItemData(reward.itemId);
            }

            int scaledMax = Mathf.Max(reward.minAmount, Mathf.RoundToInt(reward.maxAmount * ratio));
            instance.SetItem(itemData != null ? itemData.ItemIcon : null, scaledMax.ToString());
        }

        /// <summary>오버플로우 칸에 마우스를 올렸을 때, 남은 보상(startIndex부터 끝까지)을 같은 프리팹으로 팝업 그리드에 채워 보여준다.</summary>
        private void ShowRewardOverflowPopup(ExplorationZoneData zone, int startIndex, float ratio)
        {
            if (rewardOverflowPopup == null || rewardOverflowPopupGridParent == null || rewardIconPrefab == null) return;
            if (zone.rewards == null) return;

            ClearSpawnedIcons(spawnedOverflowPopupIcons);

            for (int i = startIndex; i < zone.rewards.Count; i++)
            {
                var instance = Instantiate(rewardIconPrefab, rewardOverflowPopupGridParent);
                ApplyRewardVisual(instance, zone.rewards[i], ratio);
                spawnedOverflowPopupIcons.Add(instance);
            }

            rewardOverflowPopup.SetActive(true);
        }

        private void HideRewardOverflowPopup()
        {
            if (rewardOverflowPopup != null) rewardOverflowPopup.SetActive(false);
        }

        /// <summary>Instantiate로 만들어둔 보상 아이콘 인스턴스들을 전부 Destroy하고 목록을 비운다(다시 그리기 전 정리용).</summary>
        private void ClearSpawnedIcons(List<ExplorationRewardIconUI> list)
        {
            foreach (var instance in list)
            {
                if (instance != null) Destroy(instance.gameObject);
            }
            list.Clear();
        }

        /// <summary>
        /// 현재 지역에 배치된 멤들로 5슬롯을 채운다. 배치된 멤이 5마리 미만이면 나머지는 빈 슬롯으로 둔다.
        /// 채워진 슬롯에는 항상 탐험 아이콘 + 그 멤의 실제 탐험레벨(ExplorationStat) 숫자를 함께 표시한다.
        /// </summary>
        private void RefreshMemSlots(ExplorationZoneData zone)
        {
            if (memSlots == null) return;

            var assigned = runtime.GetAssignedEntries(zone);

            for (int i = 0; i < memSlots.Length; i++)
            {
                if (memSlots[i] == null) continue;

                if (i < assigned.Count && assigned[i] != null)
                {
                    var entry = assigned[i];
                    var data = memCatalogManager != null ? memCatalogManager.FindMemData(entry.MemId) : null;
                    var statInfo = new MemStatDisplayInfo(true, explorationStatIcon, entry.ExplorationStat.ToString());
                    memSlots[i].SetData(entry, data, statInfo);
                }
                else
                {
                    memSlots[i].Clear();
                }
            }
        }

        /// <summary>상태에 따라 액션 버튼의 문구(시작/취소/완료)와 활성화 여부를 갱신한다.</summary>
        private void RefreshActionButton(ExplorationZoneData zone, ExplorationState state)
        {
            if (actionButton == null) return;

            switch (state)
            {
                case ExplorationState.Idle:
                    if (actionButtonText != null) actionButtonText.text = "탐험 시작";
                    actionButton.interactable = runtime.CanStart(zone);
                    break;

                case ExplorationState.InProgress:
                    if (actionButtonText != null) actionButtonText.text = "탐험 취소";
                    actionButton.interactable = true;
                    break;

                case ExplorationState.ReadyToComplete:
                    if (actionButtonText != null) actionButtonText.text = "탐험 완료";
                    actionButton.interactable = true;
                    break;
            }
        }

        /// <summary>진행 중/완료 대기 중일 때 남은 시간(또는 "완료 가능") 텍스트를 갱신한다. Idle이면 감춘다.</summary>
        private void UpdateTimerDisplay()
        {
            if (remainingTimeText == null) return;

            var zone = CurrentZone;
            if (zone == null || runtime == null)
            {
                remainingTimeText.text = string.Empty;
                return;
            }

            var state = runtime.GetState(zone);

            if (state == ExplorationState.Idle)
            {
                remainingTimeText.text = string.Empty;
            }
            else if (state == ExplorationState.ReadyToComplete)
            {
                remainingTimeText.text = "완료 가능";
            }
            else
            {
                remainingTimeText.text = FormatDuration(runtime.GetRemainingTime(zone));
            }
        }

        /// <summary>초 단위 시간을 "MM:SS" 형식 문자열로 바꾼다(요구 탐험레벨 텍스트의 소요시간 표시, 남은 시간 표시 공용).</summary>
        private static string FormatDuration(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }

        /// <summary>상태에 따라 시작/취소/완료 중 하나를 실행한다(버튼 하나가 상태별로 다른 동작을 한다).</summary>
        private void HandleActionButtonClicked()
        {
            var zone = CurrentZone;
            if (zone == null || runtime == null) return;

            var state = runtime.GetState(zone);

            switch (state)
            {
                case ExplorationState.Idle:
                    runtime.TryStart(zone);
                    break;
                case ExplorationState.InProgress:
                    runtime.TryCancel(zone);
                    break;
                case ExplorationState.ReadyToComplete:
                    runtime.TryComplete(zone);
                    break;
            }

            RefreshCurrentZoneDisplay();
        }

        private void HandleSlotDragBegan(MemSlotUI slot)
        {
            int idx = System.Array.IndexOf(memSlots, slot);
            if (idx < 0) return;

            pendingDragSourceLocalIndex = idx;
            dragHandledAsSwapOrRemoval = false;
        }

        /// <summary>
        /// 드래그가 끝났는데(OnDrop을 거쳐) 스왑/취소 처리가 되지 않았다면, 빈 공간 등 유효하지 않은 위치에
        /// 놓인 것이므로 이것도 "드래그로 취소"로 처리한다(요청하신 드래그앤드롭을 통한 취소).
        /// </summary>
        private void HandleSlotDragEnded(MemSlotUI slot)
        {
            int idx = System.Array.IndexOf(memSlots, slot);
            if (idx < 0 || idx != pendingDragSourceLocalIndex)
            {
                pendingDragSourceLocalIndex = -1;
                return;
            }

            if (!dragHandledAsSwapOrRemoval)
            {
                var zone = CurrentZone;
                if (zone != null)
                {
                    var entries = runtime.GetAssignedEntries(zone);
                    if (idx < entries.Count)
                    {
                        runtime.TryRemoveMem(zone, entries[idx]);
                        RefreshCurrentZoneDisplay();
                    }
                }
            }

            pendingDragSourceLocalIndex = -1;
        }

        private void HandleSlotSwapRequested(MemSlotUI source, MemSlotUI target)
        {
            var zone = CurrentZone;
            if (zone == null) return;

            int sourceLocalIndex = System.Array.IndexOf(memSlots, source);
            int targetLocalIndex = System.Array.IndexOf(memSlots, target);

            if (sourceLocalIndex >= 0 && targetLocalIndex >= 0)
            {
                // 탐험대 슬롯끼리 순서 교체
                dragHandledAsSwapOrRemoval = true;
                runtime.TrySwapSlots(zone, sourceLocalIndex, targetLocalIndex);
                RefreshCurrentZoneDisplay();
            }
            else if (sourceLocalIndex >= 0 && targetLocalIndex < 0)
            {
                // 탐험대 슬롯 -> 바깥(창고 등)으로 드래그 = 배치 취소
                dragHandledAsSwapOrRemoval = true;
                var entries = runtime.GetAssignedEntries(zone);
                if (sourceLocalIndex < entries.Count)
                {
                    runtime.TryRemoveMem(zone, entries[sourceLocalIndex]);
                }
                RefreshCurrentZoneDisplay();
            }
            else if (sourceLocalIndex < 0 && targetLocalIndex >= 0)
            {
                // 창고 등 바깥 -> 탐험대 슬롯 = 배치 시도
                var entry = source.CachedEntry;
                if (entry != null)
                {
                    if (!runtime.TryAssignMem(zone, entry))
                    {
                        Debug.LogWarning($"[ExplorationPanelUI] {entry.MemId} 탐험대 배치 실패(진행 중이거나 슬롯이 가득 찼거나 다른 지역에 이미 배치됨).");
                    }
                    RefreshCurrentZoneDisplay();
                }
            }
            // 둘 다 우리 슬롯이 아니면(예: 창고 그리드 내부 이동) 이 패널과 무관하므로 무시한다.
        }

        private void HandleSlotRightClicked(MemSlotUI slot, CapturedMemEntry entry, MemData data)
        {
            if (entry == null) return;

            var zone = CurrentZone;
            if (zone == null) return;

            int idx = System.Array.IndexOf(memSlots, slot);
            if (idx < 0) return;

            if (!runtime.TryRemoveMem(zone, entry))
            {
                Debug.LogWarning("[ExplorationPanelUI] 탐험 중에는 우클릭으로 배치를 해제할 수 없습니다. 취소 버튼을 이용해주세요.");
            }

            RefreshCurrentZoneDisplay();
        }
    }
}
