using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.UI
{
    /// <summary>정렬 기준 8가지. MemId는 오름차순(낮은순), 나머지는 전부 내림차순(높은순).</summary>
    public enum MemSortCriteria
    {
        MemId,
        Tier,
        Crafting,
        Logging,
        Mining,
        Transport,
        Farming,
        Exploration
    }

    /// <summary>
    /// 멤 창고 정렬 버튼 8개를 담당.
    /// 버튼 클릭을 감지해서 OnSortRequested 이벤트로 상위(MemStorageUI)에 알리기만 한다.
    /// 실제 정렬 로직(카탈로그 조회, 비교, 재배치)은 MemStorageUI(컨트롤러)가 담당한다.
    ///
    /// [HDY 요청 - 버튼 사이 구분 이미지] 버튼 8개 사이사이에 구분 이미지 7개(sortButtonSeparatorImages)가
    /// 배치되어 있다. HideSortButtonsExcept로 버튼이 3개(MemId/Tier/스탯1종)만 남으면 구분 이미지도 2개만
    /// 켜고 나머지 5개는 끈다(SetSeparatorImagesVisible). ShowAllSortButtons로 8개가 전부 보이면 구분
    /// 이미지도 7개 전부 켠다.
    ///
    /// [HDY 요청 - 정렬 버튼 알파 강조] 버튼을 클릭하면 그 버튼의 targetGraphic 알파를 1(불투명)로,
    /// 나머지 버튼들은 inactiveSortButtonAlpha(기본 0.4)로 낮춘다(UpdateButtonAlphas) - 지금 어떤 기준으로
    /// 정렬 중인지 한눈에 보여주기 위함이다. 아직 한 번도 클릭하지 않은 최초 상태(currentSortCriteria가
    /// null)에서는 8개 전부 inactiveSortButtonAlpha로 시작한다.
    /// </summary>
    public class MemStorageUI_Sort : MonoBehaviour
    {
        [Header("정렬 버튼 (총 8개, 미리 배치된 버튼을 연결)")]
        [SerializeField] private Button sortByMemIdButton;       // MemId 오름차순(낮은순)
        [SerializeField] private Button sortByTierButton;        // 등급 내림차순(높은순)
        [SerializeField] private Button sortByCraftingButton;    // 제작 내림차순(높은순)
        [SerializeField] private Button sortByLoggingButton;     // 벌목 내림차순(높은순)
        [SerializeField] private Button sortByMiningButton;      // 채광 내림차순(높은순)
        [SerializeField] private Button sortByTransportButton;   // 이동 내림차순(높은순)
        [SerializeField] private Button sortByFarmingButton;     // 생산 내림차순(높은순)
        [SerializeField] private Button sortByExplorationButton; // 탐험 내림차순(높은순)

        [Header("버튼 사이 구분 이미지 (총 7개, 8개 버튼 사이사이에 배치, HDY 요청)")]
        [Tooltip("정렬 버튼 8개 사이에 놓인 구분 이미지 7개. ShowAllSortButtons()에서는 7개 다 켜지고, HideSortButtonsExcept()로 3개(MemId/Tier/스탯1종)만 남을 때는 이 중 2개만 켜고 나머지 5개는 끈다 - 버튼이 3개면 사이 간격도 2개면 충분하다(레이아웃 그룹으로 자동 정렬되는 걸 가정, 어떤 이미지가 켜지는지는 상관없고 켜지는 개수만 맞으면 된다).")]
        [SerializeField] private Image[] sortButtonSeparatorImages = new Image[7];

        [Header("정렬 버튼 알파 강조 (HDY 요청)")]
        [Tooltip("정렬 중인(마지막으로 클릭된) 버튼은 알파 1, 나머지 버튼들은 이 값으로 낮춘다.")]
        [SerializeField] private float inactiveSortButtonAlpha = 0.4f;

        /// <summary>클릭에 대응하는 버튼-기준 쌍. 버튼 알파를 일괄 갱신할 때(UpdateButtonAlphas) 순회한다.</summary>
        private struct SortButtonEntry
        {
            public Button Button;
            public MemSortCriteria Criteria;
        }

        private readonly List<SortButtonEntry> sortButtonEntries = new List<SortButtonEntry>();

        // 마지막으로 클릭된(=현재 정렬 중인) 기준. 아직 한 번도 클릭하지 않았으면 null(8개 전부 비활성 알파).
        private MemSortCriteria? currentSortCriteria;

        /// <summary>정렬 버튼이 클릭되었을 때 발생. MemStorageUI(컨트롤러)가 구독해서 실제 정렬을 수행한다.</summary>
        public event Action<MemSortCriteria> OnSortRequested;

        private void Awake()
        {
            Bind(sortByMemIdButton, MemSortCriteria.MemId);
            Bind(sortByTierButton, MemSortCriteria.Tier);
            Bind(sortByCraftingButton, MemSortCriteria.Crafting);
            Bind(sortByLoggingButton, MemSortCriteria.Logging);
            Bind(sortByMiningButton, MemSortCriteria.Mining);
            Bind(sortByTransportButton, MemSortCriteria.Transport);
            Bind(sortByFarmingButton, MemSortCriteria.Farming);
            Bind(sortByExplorationButton, MemSortCriteria.Exploration);

            // [HDY 요청] 아직 아무 것도 클릭하지 않은 최초 상태 - 8개 전부 비활성 알파로 시작한다.
            UpdateButtonAlphas();
        }

        private void Bind(Button button, MemSortCriteria criteria)
        {
            if (button == null)
            {
                Debug.LogWarning($"[MemStorageUI_Sort] {criteria} 정렬 버튼이 비어있습니다.", this);
                return;
            }

            sortButtonEntries.Add(new SortButtonEntry { Button = button, Criteria = criteria });

            button.onClick.AddListener(() =>
            {
                // [HDY 요청 - 정렬 버튼 알파 강조] 클릭된 버튼만 알파 1, 나머지는 inactiveSortButtonAlpha로.
                currentSortCriteria = criteria;
                UpdateButtonAlphas();
                OnSortRequested?.Invoke(criteria);
            });
        }

        /// <summary>
        /// [협업용 - 현재 사용되는 곳 없음] 다른 시스템(예: 시설 배치)에서 특정 멤 스탯(MemStatClass)만
        /// 정렬 기준으로 쓸 수 있게 하고 싶을 때 호출하는 함수.
        /// 지정한 스탯(ms) + Tier + MemId 버튼 3개만 남기고, 나머지 5개 스탯 정렬 버튼은 숨긴다(비활성화).
        /// 예: HideSortButtonsExcept(MemStatClass.Crafting) -> Crafting/Tier/MemId만 보이고
        /// Logging/Mining/Transport/Farming/Exploration 버튼은 숨겨짐.
        /// </summary>
        public void HideSortButtonsExcept(MemStatClass ms)
        {
            var keepCriteria = ToSortCriteria(ms);

            SetButtonVisible(sortByMemIdButton, true);  // MemId는 항상 남긴다
            SetButtonVisible(sortByTierButton, true);   // Tier도 항상 남긴다
            SetButtonVisible(sortByCraftingButton, keepCriteria == MemSortCriteria.Crafting);
            SetButtonVisible(sortByLoggingButton, keepCriteria == MemSortCriteria.Logging);
            SetButtonVisible(sortByMiningButton, keepCriteria == MemSortCriteria.Mining);
            SetButtonVisible(sortByTransportButton, keepCriteria == MemSortCriteria.Transport);
            SetButtonVisible(sortByFarmingButton, keepCriteria == MemSortCriteria.Farming);
            SetButtonVisible(sortByExplorationButton, keepCriteria == MemSortCriteria.Exploration);

            // [HDY 요청] 버튼이 3개(MemId/Tier/스탯1종)만 남으면 사이 구분 이미지도 2개만 있으면 충분하다.
            // 7개 중 5개는 끈다.
            SetSeparatorImagesVisible(2);

            Debug.Log($"[MemStorageUI_Sort] 정렬 버튼 숨기기 적용: {ms} + Tier + MemId만 표시");
        }

        /// <summary>
        /// [협업용 - 현재 사용되는 곳 없음] HideSortButtonsExcept 등으로 일부 정렬 버튼이 숨겨진 상태를
        /// 되돌리지 않고, 8개 정렬 버튼을 전부 다시 보이게(활성화) 만든다.
        /// </summary>
        public void ShowAllSortButtons()
        {
            SetButtonVisible(sortByMemIdButton, true);
            SetButtonVisible(sortByTierButton, true);
            SetButtonVisible(sortByCraftingButton, true);
            SetButtonVisible(sortByLoggingButton, true);
            SetButtonVisible(sortByMiningButton, true);
            SetButtonVisible(sortByTransportButton, true);
            SetButtonVisible(sortByFarmingButton, true);
            SetButtonVisible(sortByExplorationButton, true);

            // [HDY 요청] 버튼 8개가 전부 보이면 사이 구분 이미지도 7개 전부 켠다.
            SetSeparatorImagesVisible(sortButtonSeparatorImages != null ? sortButtonSeparatorImages.Length : 0);

            Debug.Log("[MemStorageUI_Sort] 정렬 버튼 8개 전부 표시");
        }

        /// <summary>CommonClassEnum.cs의 MemStatClass를 이 클래스의 MemSortCriteria로 변환한다.</summary>
        private static MemSortCriteria ToSortCriteria(MemStatClass ms)
        {
            switch (ms)
            {
                case MemStatClass.Crafting: return MemSortCriteria.Crafting;
                case MemStatClass.Logging: return MemSortCriteria.Logging;
                case MemStatClass.Mining: return MemSortCriteria.Mining;
                case MemStatClass.Transport: return MemSortCriteria.Transport;
                case MemStatClass.Farming: return MemSortCriteria.Farming;
                case MemStatClass.Exploration: return MemSortCriteria.Exploration;
                default: return MemSortCriteria.MemId;
            }
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// [HDY 요청] 버튼 사이 구분 이미지 중 배열 앞에서부터 visibleCount개만 켜고 나머지는 끈다.
        /// 버튼들이 레이아웃 그룹으로 자동 배치된다고 가정하므로 어떤 이미지가 켜지는지는 상관없고
        /// 켜지는 개수만 맞으면 된다.
        /// </summary>
        private void SetSeparatorImagesVisible(int visibleCount)
        {
            if (sortButtonSeparatorImages == null) return;

            for (int i = 0; i < sortButtonSeparatorImages.Length; i++)
            {
                if (sortButtonSeparatorImages[i] == null) continue;
                sortButtonSeparatorImages[i].gameObject.SetActive(i < visibleCount);
            }
        }

        /// <summary>
        /// [HDY 요청 - 정렬 버튼 알파 강조] currentSortCriteria와 일치하는 버튼은 알파 1(불투명), 나머지는
        /// inactiveSortButtonAlpha로 낮춘다. currentSortCriteria가 null이면(아직 클릭한 적 없음) 8개 전부
        /// inactiveSortButtonAlpha가 된다.
        /// </summary>
        private void UpdateButtonAlphas()
        {
            for (int i = 0; i < sortButtonEntries.Count; i++)
            {
                var entry = sortButtonEntries[i];
                bool isActive = currentSortCriteria.HasValue && currentSortCriteria.Value == entry.Criteria;
                SetButtonAlpha(entry.Button, isActive ? 1f : inactiveSortButtonAlpha);
            }
        }

        /// <summary>버튼의 targetGraphic(보통 버튼 배경 Image)의 알파만 바꾼다. 색상 자체는 건드리지 않는다.</summary>
        private static void SetButtonAlpha(Button button, float alpha)
        {
            if (button == null || button.targetGraphic == null) return;

            var color = button.targetGraphic.color;
            color.a = alpha;
            button.targetGraphic.color = color;
        }
    }
}
