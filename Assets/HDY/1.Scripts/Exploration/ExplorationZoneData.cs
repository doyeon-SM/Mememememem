using System.Collections.Generic;
using UnityEngine;

namespace HDY.Exploration
{
    /// <summary>
    /// 탐험 완료 시 지급할 보상 1건.
    /// 완료 시 리스트의 모든 항목을 각각 minAmount~maxAmount 범위에서 랜덤 수량으로 지급한다(일부만 뽑지 않음).
    /// maxAmount는 배치한 멤들의 탐험레벨 합이 요구탐험레벨을 초과한 비율만큼 상승해 기대값이 올라간다
    /// (minAmount는 항상 고정) - 실제 배율 계산은 ExplorationRuntime.TryComplete에서 처리한다.
    /// </summary>
    [System.Serializable]
    public class ExplorationRewardEntry
    {
        [Tooltip("보상 아이템의 Item_ID. HDY.Item.ItemData.Item_ID 문자열과 매칭된다(ItemCatalogManager로 조회).")]
        public string itemId;

        [Tooltip("최소 지급 수량. 보너스 배율과 무관하게 항상 고정된다.")]
        public int minAmount = 1;

        [Tooltip("기본 최대 지급 수량. 탐험레벨 초과 배율이 이 값에만 곱해진다.")]
        public int maxAmount = 1;
    }

    /// <summary>
    /// 탐험 보낼 수 있는 지역 1곳을 정의하는 SO.
    /// 실제 진행 상태(배치된 멤, 경과 시간 등)는 이 SO가 아니라 ExplorationRuntime이 zoneId별로 따로 들고 있다 -
    /// 이 SO는 순수하게 정적인 지역 정의 데이터다.
    /// </summary>
    [CreateAssetMenu(fileName = "ExplorationZone_", menuName = "HDY/Exploration/Exploration Zone Data", order = 0)]
    public class ExplorationZoneData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("저장 데이터와 런타임 진행 상태에서 사용하는 고유 식별자(예: zone_forest_01). 한 번 사용한 값은 변경하지 않는다.")]
        public string zoneId;

        [Tooltip("게임 내 표시 이름.")]
        public string zoneName;

        [Tooltip("탐험 패널의 지역 카드에 표시할 이미지.")]
        public Sprite zoneImage;

        [Header("탐험 조건")]
        [Tooltip("이 맵에 등록된 모든 웨이포인트가 해금되어야 탐험할 수 있다. 비워두면 웨이포인트 해금 조건 없이 탐험할 수 있다.")]
        public WayPointMapDefinition requiredCompletedMap;

        [Tooltip("이 지역을 탐험하기 위해 필요한 탐험레벨. 배치된 멤들의 CapturedMemEntry.ExplorationStat 합이 이 값 이상이어야 탐험 시작 가능.")]
        public int requiredExplorationLevel = 1;

        [Tooltip("탐험 소요 시간(초). 탐험 시작 후 실시간(Time.deltaTime 누적)으로 흐른다.")]
        public float explorationDuration = 60f;

        [Header("보상")]
        [Tooltip("탐험 완료 시 지급할 보상 목록. 리스트의 모든 항목을 지급한다(랜덤 선별 없음).")]
        public List<ExplorationRewardEntry> rewards = new List<ExplorationRewardEntry>();
    }
}
