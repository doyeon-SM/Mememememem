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
    /// </summary>
    public class MemDexUI_Grid : MonoBehaviour
    {
        [Header("스크롤 그리드 (Content에 GridLayoutGroup 5열 고정 필요)")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentParent;
        [SerializeField] private MemDexSlotUI slotPrefab;

        private readonly List<MemDexSlotUI> spawnedSlots = new List<MemDexSlotUI>();

        /// <summary>슬롯이 클릭되었을 때 발생. MemDexUI(컨트롤러)가 구독해서 정보 패널로 전달한다.</summary>
        public event Action<MemData> OnSlotClicked;

        private void Awake()
        {
            if (scrollRect == null) Debug.LogWarning("[MemDexUI_Grid] scrollRect가 비어있습니다. 스크롤 위치 리셋이 동작하지 않습니다.", this);
            if (contentParent == null) Debug.LogWarning("[MemDexUI_Grid] contentParent가 비어있습니다. 슬롯을 채울 수 없습니다.", this);
            if (slotPrefab == null) Debug.LogWarning("[MemDexUI_Grid] slotPrefab이 비어있습니다. 슬롯을 채울 수 없습니다.", this);
        }

        /// <summary>주어진 순서(이미 정렬된 상태) 그대로 슬롯을 채운다. 필요한 만큼만 Instantiate하고 이후엔 재사용한다.</summary>
        public void Populate(IReadOnlyList<MemData> orderedData, Func<MemData, MemStatDisplayInfo> statDisplayProvider)
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
                    spawnedSlots[i].SetData(data, statInfo);
                    spawnedSlots[i].gameObject.SetActive(true);
                }
                else
                {
                    spawnedSlots[i].gameObject.SetActive(false);
                }
            }

            Debug.Log($"[MemDexUI_Grid] Populate 완료: {count}개 항목");

            ResetScrollToTop();
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
