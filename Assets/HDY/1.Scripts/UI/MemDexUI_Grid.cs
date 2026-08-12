using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MemSystem.Data;

namespace HDY.UI
{
    /// <summary>
    /// 도감 그리드. 5열 GridLayoutGroup + ScrollRect 기반의 연속 스크롤 그리드로,
    /// MemStorageUI_Grid(6x8 고정 페이지네이션, 미리 배치된 슬롯)와는 구조가 다르다 - 페이지 개념이 없고,
    /// 슬롯 개수도 고정이 아니라 MemCatalogManager에 등록된 항목 수만큼 달라진다.
    ///
    /// 그래서 슬롯을 씬에 미리 배치하지 않고, 필요한 만큼 런타임에 Instantiate한다(재료 비용 슬롯을
    /// 만들 때 쓴 것과 동일한 "필요한 만큼 생성 후 재사용" 패턴). contentParent에는 GridLayoutGroup이
    /// Constraint=Fixed Column Count, Constraint Count=5로 설정되어 있어야 5열로 줄바꿈된다.
    ///
    /// [HDY 요청 - 선택 표시] SetSelected(data)로 지정된 MemData와 참조가 일치하는 슬롯 하나에만
    /// MemDexSlotUI.SetSelected(true)를 켜고 나머지는 전부 끈다(ApplySelectionToSlots). 정렬 등으로
    /// Populate가 다시 호출돼도 selectedData는 그대로 기억하고 있다가 끝에서 다시 적용하므로, 정렬 순서가
    /// 바뀌어도 선택 표시가 같은 데이터를 계속 따라간다.
    /// </summary>
    public class MemDexUI_Grid : MonoBehaviour
    {
        [Header("스크롤 그리드 (Content에 GridLayoutGroup 5열 고정 필요)")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentParent;
        [SerializeField] private MemDexSlotUI slotPrefab;

        private readonly List<MemDexSlotUI> spawnedSlots = new List<MemDexSlotUI>();

        // [HDY 요청 - 선택 표시] 지금 선택된 것으로 표시해야 할 데이터. Populate가 다시 호출돼도(정렬 등)
        // 이 값은 유지되며, Populate 끝에서 다시 적용한다.
        private MemData selectedData;

        /// <summary>슬롯이 클릭되었을 때 발생. MemDexUI(컨트롤러)가 구독해서 정보 패널로 전달한다.</summary>
        public event Action<MemData> OnSlotClicked;

        private void Awake()
        {
            if (scrollRect == null) Debug.LogWarning("[MemDexUI_Grid] scrollRect가 비어있습니다. 스크롤 위치 리셋이 동작하지 않습니다.", this);
            if (contentParent == null) Debug.LogWarning("[MemDexUI_Grid] contentParent가 비어있습니다. 슬롯을 채울 수 없습니다.", this);
            if (slotPrefab == null) Debug.LogWarning("[MemDexUI_Grid] slotPrefab이 비어있습니다. 슬롯을 채울 수 없습니다.", this);
        }

        /// <summary>
        /// 주어진 순서(이미 정렬된 상태) 그대로 슬롯을 채운다. 필요한 만큼만 Instantiate하고 이후엔 재사용한다.
        /// </summary>
        /// <param name="isDiscoveredProvider">
        /// [HDY 요청 - 최초 포획 실루엣] 각 MemData가 최초 포획 기록이 있는지(발견되었는지) 판단하는 함수.
        /// 이 그리드는 MemDexRecordManager를 직접 조회하지 않는다 - 카탈로그 정렬/조회와 마찬가지로
        /// 발견 여부 판단도 상위(MemDexUI)의 책임이다. null이면 안전한 기본값으로 전부 발견된 것으로 처리한다.
        /// </param>
        public void Populate(IReadOnlyList<MemData> orderedData, Func<MemData, MemStatDisplayInfo> statDisplayProvider, Func<MemData, bool> isDiscoveredProvider)
        {
            if (slotPrefab == null || contentParent == null)
            {
                Debug.LogWarning("[MemDexUI_Grid] slotPrefab/contentParent가 비어있어 도감을 채울 수 없습니다.", this);
                return;
            }

            int count = orderedData != null ? orderedData.Count : 0;

            while (spawnedSlots.Count < count)
            {
                var slot = Instantiate(slotPrefab, contentParent);
                slot.OnSlotClicked += HandleSlotClicked;
                spawnedSlots.Add(slot);
            }

            for (int i = 0; i < spawnedSlots.Count; i++)
            {
                if (i < count)
                {
                    var data = orderedData[i];
                    var statInfo = statDisplayProvider != null ? statDisplayProvider(data) : MemStatDisplayInfo.Hidden;
                    var isDiscovered = isDiscoveredProvider != null ? isDiscoveredProvider(data) : true;
                    spawnedSlots[i].SetData(data, statInfo, isDiscovered);
                    spawnedSlots[i].gameObject.SetActive(true);
                }
                else
                {
                    spawnedSlots[i].gameObject.SetActive(false);
                }
            }

            Debug.Log($"[MemDexUI_Grid] Populate 완료: {count}개 항목");

            ApplySelectionToSlots();
            ResetScrollToTop();
        }

        /// <summary>
        /// [HDY 요청 - 선택 표시] 지금부터 data로 넘어온 항목의 슬롯만 선택 표시를 켠다(null이면 전부 끔).
        /// MemDexUI가 슬롯 클릭을 받아 정보 패널을 갱신할 때 함께 호출한다.
        /// </summary>
        public void SetSelected(MemData data)
        {
            selectedData = data;
            ApplySelectionToSlots();
        }

        private void ApplySelectionToSlots()
        {
            foreach (var slot in spawnedSlots)
            {
                slot.SetSelected(selectedData != null && ReferenceEquals(slot.BoundData, selectedData));
            }
        }

        private void HandleSlotClicked(MemData data)
        {
            OnSlotClicked?.Invoke(data);
        }

        /// <summary>정렬 등으로 목록이 다시 채워지면(콘텐츠 높이가 바뀔 수 있으므로) 레이아웃을 갱신하고 스크롤을 맨 위로 되돌린다.</summary>
        private void ResetScrollToTop()
        {
            if (contentParent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
            }

            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }
    }
}
