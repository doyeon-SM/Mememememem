using System;
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
        }

        private void Bind(Button button, MemSortCriteria criteria)
        {
            if (button == null)
            {
                Debug.LogWarning($"[MemStorageUI_Sort] {criteria} 정렬 버튼이 비어있습니다.", this);
                return;
            }

            button.onClick.AddListener(() => OnSortRequested?.Invoke(criteria));
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
    }
}
