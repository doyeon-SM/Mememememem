using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MemSystem.Data;
using HDY.Capture;

namespace HDY.UI
{
    /// <summary>
    /// 멤 창고 그리드(6x8, 48칸) 담당.
    /// 슬롯 생성, 페이지 이동, 슬롯에 데이터 채우기만 담당한다.
    /// 정보 패널 표시는 MemStorageUI_Info가, 데이터 조회/전달은 MemStorageUI(컨트롤러)가 담당한다.
    /// </summary>
    public class MemStorageUI_Grid : MonoBehaviour
    {
        private const int Columns = 6;
        private const int Rows = 8;
        public const int PageSize = Columns * Rows;

        [Header("그리드 (6x8 GridLayoutGroup이 설정된 부모)")]
        [SerializeField] private Transform gridParent;
        [SerializeField] private MemSlotUI slotPrefab;

        [Header("페이지 이동 (48마리 초과 시 사용)")]
        [SerializeField] private Button prevPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TMP_Text pageIndicatorText;

        private readonly List<MemSlotUI> slots = new List<MemSlotUI>();
        private int currentPageIndex;

        // 페이지 이동(이전/다음) 클릭 시 다시 그리기 위해 마지막으로 받은 데이터를 캐싱해둔다.
        private IReadOnlyList<CapturedMemEntry> cachedCapturedMems;
        private Func<string, MemData> cachedFindMemData;

        /// <summary>슬롯이 클릭되었을 때 발생. MemStorageUI(컨트롤러)가 구독해서 정보 패널로 전달한다.</summary>
        public event Action<CapturedMemEntry, MemData> OnSlotClicked;

        private void Awake()
        {
            if (slotPrefab == null) Debug.LogWarning("[MemStorageUI_Grid] slotPrefab이 비어있습니다. 그리드 슬롯이 생성되지 않습니다.", this);
            if (gridParent == null) Debug.LogWarning("[MemStorageUI_Grid] gridParent가 비어있습니다. 그리드 슬롯이 생성되지 않습니다.", this);

            BuildSlots();

            if (prevPageButton != null) prevPageButton.onClick.AddListener(GoToPrevPage);
            if (nextPageButton != null) nextPageButton.onClick.AddListener(GoToNextPage);
        }

        /// <summary>슬롯 프리팹으로 6x8(48개) 슬롯을 그리드 부모 아래에 생성한다.</summary>
        private void BuildSlots()
        {
            if (slotPrefab == null || gridParent == null)
            {
                Debug.LogWarning("[MemStorageUI_Grid] BuildSlots 중단: slotPrefab 또는 gridParent가 없습니다.", this);
                return;
            }

            if (slots.Count > 0) return; // 이미 생성됨

            for (int i = 0; i < PageSize; i++)
            {
                var slot = Instantiate(slotPrefab, gridParent);
                slot.OnSlotClicked += HandleSlotClicked;
                slots.Add(slot);
            }

            Debug.Log($"[MemStorageUI_Grid] 슬롯 {slots.Count}개 생성 완료 (부모: {gridParent.name})");
        }

        private void HandleSlotClicked(CapturedMemEntry entry, MemData data)
        {
            OnSlotClicked?.Invoke(entry, data);
        }

        /// <summary>창고 UI가 처음 열릴 때 호출. 가장 최근 페이지(마지막 페이지)부터 보여준다.</summary>
        public void ShowInitial(IReadOnlyList<CapturedMemEntry> capturedMems, Func<string, MemData> findMemData)
        {
            currentPageIndex = GetLastPageIndex(capturedMems != null ? capturedMems.Count : 0);
            Populate(capturedMems, findMemData);
        }

        /// <summary>새로 멤이 포획되는 등 데이터가 바뀌었을 때 호출. 마지막 페이지를 보고 있었다면 계속 마지막 페이지를 따라간다.</summary>
        public void NotifyDataChanged(IReadOnlyList<CapturedMemEntry> capturedMems, Func<string, MemData> findMemData)
        {
            int count = capturedMems != null ? capturedMems.Count : 0;
            bool wasOnLastPage = currentPageIndex >= GetLastPageIndex(count);
            if (wasOnLastPage)
            {
                currentPageIndex = GetLastPageIndex(count);
            }

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

        /// <summary>데이터를 캐싱하고 현재 페이지 기준으로 그리드를 다시 채운다. 포획된(리스트에 저장된) 순서 그대로 표시.</summary>
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
                Debug.LogWarning("[MemStorageUI_Grid] Populate 중단: 생성된 슬롯이 0개입니다 (BuildSlots 실패 여부 확인 필요).", this);
                return;
            }

            int totalPages = GetTotalPages(capturedMems.Count);
            currentPageIndex = Mathf.Clamp(currentPageIndex, 0, totalPages - 1);

            int startIndex = currentPageIndex * PageSize;

            for (int i = 0; i < slots.Count; i++)
            {
                int globalIndex = startIndex + i;
                if (globalIndex < capturedMems.Count)
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

            if (pageIndicatorText != null)
            {
                pageIndicatorText.text = $"{currentPageIndex + 1} / {totalPages}";
            }

            if (prevPageButton != null) prevPageButton.interactable = currentPageIndex > 0;
            if (nextPageButton != null) nextPageButton.interactable = currentPageIndex < totalPages - 1;

            Debug.Log($"[MemStorageUI_Grid] Populate 완료: 슬롯={slots.Count}, 포획수={capturedMems.Count}, 페이지={currentPageIndex + 1}/{totalPages}");
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
