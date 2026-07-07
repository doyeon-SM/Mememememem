using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MemSystem.Data;
using HDY.Capture;

namespace HDY.UI
{
    /// <summary>
    /// 멤 창고 그리드(6x8, 48칸) 담당.
    /// 슬롯과 페이지 점(dot)은 씬에 미리 배치되어 있으며, 이 스크립트는 그것들을 수집해 데이터만 채운다(런타임 Instantiate 없음).
    /// 슬롯 간 드래그앤드롭 요청을 받아 전체 목록 기준 인덱스로 변환해 상위(MemStorageUI)로 전달하는 역할도 한다.
    /// 정보 패널 표시는 MemStorageUI_Info가, 데이터 조회/전달 및 실제 위치 교체(데이터 반영)는 MemStorageUI(컨트롤러)가 담당한다.
    ///
    /// [빈 칸] MemCaptureManager의 목록은 항상 최대치만큼 미리 채워져 있고 빈 칸은 CapturedMemEntry.IsEmpty로 구분된다.
    /// 페이지 총 개수는 목록 전체 길이(=창고 최대치) 기준으로 계산되므로, 창고 최대치가 나중에 바뀌면 자동으로 반영된다.
    /// </summary>
    public class MemStorageUI_Grid : MonoBehaviour
    {
        private const int Columns = 6;
        private const int Rows = 8;
        public const int PageSize = Columns * Rows;
        private const int MaxPageDots = 10;

        [Header("그리드 (6x8 - 미리 배치된 슬롯의 부모)")]
        [SerializeField] private Transform gridParent;

        [Header("페이지 이동 (48마리 초과 시 사용)")]
        [SerializeField] private Button prevPageButton;
        [SerializeField] private Button nextPageButton;

        [Header("페이지 점 표시 (점 10개, 미리 배치된 부모)")]
        [SerializeField] private Transform pageDotsParent;
        [SerializeField] private float dotNormalScale = 1f;
        [SerializeField] private float dotActiveScale = 1.4f;

        private readonly List<MemSlotUI> slots = new List<MemSlotUI>();
        private readonly List<RectTransform> pageDots = new List<RectTransform>();
        private int currentPageIndex;

        // 페이지 이동(이전/다음) 클릭 시 다시 그리기 위해 마지막으로 받은 데이터를 캐싱해둔다.
        private IReadOnlyList<CapturedMemEntry> cachedCapturedMems;
        private Func<string, MemData> cachedFindMemData;

        /// <summary>슬롯이 클릭되었을 때 발생. MemStorageUI(컨트롤러)가 구독해서 정보 패널로 전달한다.</summary>
        public event Action<CapturedMemEntry, MemData> OnSlotClicked;

        /// <summary>
        /// 드래그앤드롭으로 두 슬롯의 위치를 바꿔달라는 요청. 전체 목록 기준 인덱스(페이지 오프셋 포함) 2개를 전달한다.
        /// 대상이 빈 칸인 경우도 포함된다(빈 칸으로 이동). MemStorageUI(컨트롤러)가 구독해서 실제 데이터(MemCaptureManager)에 반영한다.
        /// </summary>
        public event Action<int, int> OnSwapRequested;

        private void Awake()
        {
            if (gridParent == null) Debug.LogWarning("[MemStorageUI_Grid] gridParent가 비어있습니다. 미리 배치된 슬롯을 찾을 수 없습니다.", this);
            if (pageDotsParent == null) Debug.LogWarning("[MemStorageUI_Grid] pageDotsParent가 비어있습니다. 페이지 점이 표시되지 않습니다.", this);

            CollectSlots();
            CollectPageDots();

            if (prevPageButton != null) prevPageButton.onClick.AddListener(GoToPrevPage);
            if (nextPageButton != null) nextPageButton.onClick.AddListener(GoToNextPage);
        }

        /// <summary>씬에 미리 배치된 슬롯들을 gridParent 하위에서 찾아 수집한다(더 이상 런타임 Instantiate하지 않음).</summary>
        private void CollectSlots()
        {
            if (gridParent == null) return;
            if (slots.Count > 0) return; // 이미 수집됨

            gridParent.GetComponentsInChildren<MemSlotUI>(true, slots);

            foreach (var slot in slots)
            {
                slot.OnSlotClicked += HandleSlotClicked;
                slot.OnSlotSwapRequested += HandleSlotSwapRequested;
            }

            if (slots.Count != PageSize)
            {
                Debug.LogWarning($"[MemStorageUI_Grid] 미리 배치된 슬롯 개수({slots.Count})가 예상 개수({PageSize})와 다릅니다. gridParent 하위에 슬롯 {Columns}x{Rows}개가 배치되어 있는지 확인하세요.", this);
            }

            Debug.Log($"[MemStorageUI_Grid] 슬롯 {slots.Count}개 수집 완료 (부모: {gridParent.name})");
        }

        /// <summary>씬에 미리 배치된 페이지 점(dot)들을 pageDotsParent 하위에서 찾아 수집한다.</summary>
        private void CollectPageDots()
        {
            if (pageDotsParent == null) return;
            if (pageDots.Count > 0) return; // 이미 수집됨

            var images = pageDotsParent.GetComponentsInChildren<Image>(true);
            foreach (var image in images)
            {
                pageDots.Add(image.rectTransform);
            }

            if (pageDots.Count != MaxPageDots)
            {
                Debug.LogWarning($"[MemStorageUI_Grid] 페이지 점 개수({pageDots.Count})가 예상 개수({MaxPageDots})와 다릅니다.", this);
            }

            Debug.Log($"[MemStorageUI_Grid] 페이지 점 {pageDots.Count}개 수집 완료 (부모: {pageDotsParent.name})");
        }

        private void HandleSlotClicked(CapturedMemEntry entry, MemData data)
        {
            OnSlotClicked?.Invoke(entry, data);
        }

        /// <summary>슬롯 드래그앤드롭 결과를 받아, 전체 목록 기준 인덱스로 변환해 상위로 전달한다.</summary>
        private void HandleSlotSwapRequested(MemSlotUI sourceSlot, MemSlotUI targetSlot)
        {
            int sourceGlobalIndex = GetGlobalIndexOfSlot(sourceSlot);
            int targetGlobalIndex = GetGlobalIndexOfSlot(targetSlot);

            if (sourceGlobalIndex < 0 || targetGlobalIndex < 0)
            {
                Debug.LogWarning("[MemStorageUI_Grid] 교체 요청된 슬롯을 목록에서 찾을 수 없습니다.", this);
                return;
            }

            OnSwapRequested?.Invoke(sourceGlobalIndex, targetGlobalIndex);
        }

        /// <summary>현재 페이지 기준으로 슬롯의 로컬 인덱스를 전체 목록 기준 인덱스로 변환한다.</summary>
        private int GetGlobalIndexOfSlot(MemSlotUI slot)
        {
            int localIndex = slots.IndexOf(slot);
            if (localIndex < 0) return -1;
            return currentPageIndex * PageSize + localIndex;
        }

        /// <summary>창고 UI가 처음 열릴 때 호출. 실제로 채워진 멤이 있는 가장 마지막 페이지부터 보여준다(전부 비어있으면 첫 페이지).</summary>
        public void ShowInitial(IReadOnlyList<CapturedMemEntry> capturedMems, Func<string, MemData> findMemData)
        {
            currentPageIndex = GetLastNonEmptyPageIndex(capturedMems);
            Populate(capturedMems, findMemData);
        }

        /// <summary>새로 멤이 포획되거나 슬롯 위치가 바뀌는 등 데이터가 바뀌었을 때 호출. 보고 있던 페이지를 그대로 유지한 채 다시 채운다.</summary>
        public void NotifyDataChanged(IReadOnlyList<CapturedMemEntry> capturedMems, Func<string, MemData> findMemData)
        {
            Populate(capturedMems, findMemData);
        }

        private int GetTotalPages(int count)
        {
            return Mathf.Max(1, Mathf.CeilToInt(count / (float)PageSize));
        }

        private int GetLastPageIndex(int count)
        {
            return GetTotalPages(count) - 1;
        }

        /// <summary>실제 멤(비어있지 않은 항목)이 존재하는 페이지 중 가장 마지막 페이지의 인덱스를 찾는다. 없으면 0(첫 페이지).</summary>
        private int GetLastNonEmptyPageIndex(IReadOnlyList<CapturedMemEntry> capturedMems)
        {
            if (capturedMems == null || capturedMems.Count == 0) return 0;

            int totalPages = GetTotalPages(capturedMems.Count);

            for (int page = totalPages - 1; page >= 0; page--)
            {
                int start = page * PageSize;
                int end = Mathf.Min(start + PageSize, capturedMems.Count);

                for (int i = start; i < end; i++)
                {
                    if (!capturedMems[i].IsEmpty) return page;
                }
            }

            return 0;
        }

        /// <summary>데이터를 캐싱하고 현재 페이지 기준으로 그리드를 다시 채운다. 목록에 저장된 순서(빈 칸 포함) 그대로 표시.</summary>
        private void Populate(IReadOnlyList<CapturedMemEntry> capturedMems, Func<string, MemData> findMemData)
        {
            cachedCapturedMems = capturedMems;
            cachedFindMemData = findMemData;

            if (capturedMems == null)
            {
                Debug.LogWarning("[MemStorageUI_Grid] Populate 중단: capturedMems가 없습니다.", this);
                return;
            }

            if (slots.Count == 0)
            {
                Debug.LogWarning("[MemStorageUI_Grid] Populate 중단: 수집된 슬롯이 0개입니다 (CollectSlots 실패 여부 확인 필요).", this);
                return;
            }

            int totalPages = GetTotalPages(capturedMems.Count);
            currentPageIndex = Mathf.Clamp(currentPageIndex, 0, totalPages - 1);

            int startIndex = currentPageIndex * PageSize;

            for (int i = 0; i < slots.Count; i++)
            {
                int globalIndex = startIndex + i;
                if (globalIndex < capturedMems.Count && !capturedMems[globalIndex].IsEmpty)
                {
                    var entry = capturedMems[globalIndex];
                    var data = findMemData != null ? findMemData(entry.MemId) : null;
                    slots[i].SetData(entry, data);
                }
                else
                {
                    slots[i].Clear();
                }
            }

            UpdatePageDots(currentPageIndex, totalPages);

            if (prevPageButton != null) prevPageButton.interactable = currentPageIndex > 0;
            if (nextPageButton != null) nextPageButton.interactable = currentPageIndex < totalPages - 1;

            Debug.Log($"[MemStorageUI_Grid] Populate 완료: 슬롯={slots.Count}, 전체 칸수={capturedMems.Count}, 페이지={currentPageIndex + 1}/{totalPages}");
        }

        /// <summary>
        /// 페이지 점들의 활성/비활성 및 크기(현재 페이지 강조)를 갱신한다.
        /// 점은 최대 10개까지만 지원한다고 가정 - 총 페이지가 10개를 넘으면 넘는 페이지는 점으로 표시되지 않는다(경고 로그만 남김).
        /// </summary>
        private void UpdatePageDots(int pageIndex, int totalPages)
        {
            if (pageDots.Count == 0) return;

            int visibleCount = Mathf.Min(totalPages, pageDots.Count);

            for (int i = 0; i < pageDots.Count; i++)
            {
                bool isVisible = i < visibleCount;
                pageDots[i].gameObject.SetActive(isVisible);

                if (!isVisible) continue;

                bool isCurrent = i == pageIndex;
                pageDots[i].localScale = Vector3.one * (isCurrent ? dotActiveScale : dotNormalScale);
            }

            if (totalPages > pageDots.Count)
            {
                Debug.LogWarning($"[MemStorageUI_Grid] 총 페이지({totalPages})가 점 개수({pageDots.Count})보다 많습니다. {pageDots.Count}페이지를 초과하는 페이지는 점으로 표시되지 않습니다.", this);
            }
        }

        public void GoToPrevPage()
        {
            currentPageIndex = Mathf.Max(0, currentPageIndex - 1);
            Populate(cachedCapturedMems, cachedFindMemData);
        }

        public void GoToNextPage()
        {
            int count = cachedCapturedMems != null ? cachedCapturedMems.Count : 0;
            currentPageIndex = Mathf.Min(GetLastPageIndex(count), currentPageIndex + 1);
            Populate(cachedCapturedMems, cachedFindMemData);
        }
    }
}
