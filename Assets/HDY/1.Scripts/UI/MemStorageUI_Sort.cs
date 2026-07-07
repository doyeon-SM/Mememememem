using System;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.UI
{
    /// <summary>
    /// 멤 창고 정렬 기준 8가지.
    /// MemId는 오름차순(낮은순), 나머지(Tier/생산 스탯 5종/탐험)는 전부 내림차순(높은순).
    /// </summary>
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
    }
}
