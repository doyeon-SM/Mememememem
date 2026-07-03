// ============================================================================
// MemData.cs
// ScriptableObject — 개별 멤 정의 데이터 (에디터에서 설정)
//
// [담당자 안내]
// - Unity Inspector에서 멤의 모든 속성을 설정할 수 있는 데이터 에셋입니다.
// - Project 창 우클릭 → Create → Mem → MemData 로 새 에셋을 생성합니다.
// - ProductionStats: 생산 스탯 5종 (제작/벌목/채광/이동/생산)
// - SpawnCondition: 출현 조건 (허용 지역, 낮/밤, 가중치)
// ============================================================================
using UnityEngine;

namespace MemSystem.Data
{
    /// <summary>
    /// 개별 멤의 정적 데이터를 정의하는 ScriptableObject.
    /// 
    /// 하나의 에셋 = 하나의 멤 종류.
    /// 예: Mem_Rare_001.asset, Mem_Epic_001.asset 등
    /// 
    /// [다른 시스템 담당자 참고]
    /// - 플레이어: 포획 시 ICapturable 인터페이스를 통해 이 데이터에 접근합니다.
    /// - 영지: 포획 후 MemSnapshot(DTO)으로 변환되어 전달됩니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMemData", menuName = "Mem/MemData")]
    public class MemData : ScriptableObject
    {
        // =====================================================================
        // 기본 정보
        // =====================================================================

        [Header("기본 정보")]
        [Tooltip("고유 식별자 (예: mem_rare_001). 다른 시스템에서 멤을 식별하는 데 사용됩니다.")]
        public string memId;

        [Tooltip("게임 내 표시 이름")]
        public string memName;

        [Tooltip("등급 — MemTierTable의 등급별 고정 스펙이 자동 적용됩니다.")]
        public MemTier tier;

        [Tooltip("성격 — 플레이어에 대한 적대 조건을 결정합니다. (온순/평범/난폭)")]
        public MemPersonality personality;

        // =====================================================================
        // 외형
        // =====================================================================

        [Header("외형")]
        [Tooltip("등급별 FBX 모델 프리팹 참조. MemVisual에서 런타임에 스왑합니다.")]
        public GameObject modelPrefab;

        // =====================================================================
        // 스탯
        // =====================================================================

        [Header("스탯")]
        [Tooltip("최대 체력 — MemTierTable에서 등급별 고정값으로 덮어쓸 수 있습니다.")]
        public int maxHp = 10;

        [Tooltip("최대 허기 — MemTierTable에서 등급별 고정값으로 덮어쓸 수 있습니다.")]
        public int maxHunger = 10;

        [Tooltip("생산 스탯 (5종: 제작/벌목/채광/이동/생산). 시설 배치 시 최소 1단계 이상 필요.")]
        public ProductionStats productionStats;

        [Tooltip("탐험 스탯 — 등급별 범위 내에서 개체마다 다양하게 결정됩니다.")]
        public int explorationStat;

        // =====================================================================
        // 전투 파라미터
        // =====================================================================

        [Header("전투")]
        [Tooltip("멤의 공격력 (플레이어에게 가하는 데미지). 온순 멤은 사용하지 않습니다.")]
        public int attackDamage = 1;

        [Tooltip("공격 사거리 (이 거리 안에 플레이어가 있으면 공격 시도)")]
        public float attackRange = 2.0f;

        [Tooltip("공격 쿨타임 (초 단위). 연속 공격 방지.")]
        public float attackCooldown = 1.5f;

        [Tooltip("인식 범위 — 난폭(Aggressive) 멤이 이 범위 내 플레이어를 감지하면 선제 공격합니다.")]
        public float detectionRange = 10f;

        [Tooltip("도주 임계 HP 비율 (0.0~1.0). 현재 HP가 이 비율 이하면 도주 상태로 전환됩니다.")]
        [Range(0f, 1f)]
        public float fleeHpThreshold = 0.3f;

        // =====================================================================
        // 스폰 조건
        // =====================================================================

        [Header("스폰 조건")]
        [Tooltip("출현 조건 (허용 지역, 시간대, 가중치). MemSpawner가 참조합니다.")]
        public SpawnCondition spawnCondition;
    }

    // =========================================================================
    // 생산 스탯 구조체
    // =========================================================================

    /// <summary>
    /// 생산 스탯 구조체 (5종).
    /// 각 스탯은 0~5 단계의 레벨을 가집니다.
    /// 영지 시설 배치 시 해당 스탯이 최소 1단계 이상이어야 투입 가능합니다.
    /// 
    /// [영지 담당자 참고]
    /// GetStat(ProductionStatType) 메서드로 특정 스탯 레벨을 조회할 수 있습니다.
    /// </summary>
    [System.Serializable]
    public struct ProductionStats
    {
        [Tooltip("제작 — 제작대, 주방 배치 (제작 속도 증가)")]
        [Range(0, 5)] public int crafting;

        [Tooltip("벌목 — 벌목장 배치 (원목 생산량 증가)")]
        [Range(0, 5)] public int logging;

        [Tooltip("채광 — 채굴장 배치 (원석 생산량 증가)")]
        [Range(0, 5)] public int mining;

        [Tooltip("이동 — 운반 시설, 발전기 배치 (회수 시설 수 및 전력 생산량 증가)")]
        [Range(0, 5)] public int transport;

        [Tooltip("생산 — 밭, 목장 배치 (재료 생산량 증가)")]
        [Range(0, 5)] public int farming;

        /// <summary>
        /// 특정 생산 스탯 타입의 레벨을 반환합니다.
        /// 영지 시설 배치 시 이 메서드로 해당 스탯 레벨을 확인합니다.
        /// </summary>
        /// <param name="type">조회할 스탯 종류</param>
        /// <returns>해당 스탯의 레벨 (0~5)</returns>
        public int GetStat(ProductionStatType type)
        {
            return type switch
            {
                ProductionStatType.Crafting => crafting,
                ProductionStatType.Logging => logging,
                ProductionStatType.Mining => mining,
                ProductionStatType.Transport => transport,
                ProductionStatType.Farming => farming,
                _ => 0
            };
        }
    }

    // =========================================================================
    // 스폰 조건 구조체
    // =========================================================================

    /// <summary>
    /// 멤의 스폰(출현) 조건을 정의하는 구조체.
    /// MemSpawner가 스폰 대상 멤을 결정할 때 이 조건을 참조합니다.
    /// 
    /// [월드 담당자 참고]
    /// - allowedZoneIds: 이 멤이 출현할 수 있는 지역 ID 목록
    /// - spawnWeight: 같은 구역 내 다른 멤 대비 출현 비중 (높을수록 자주 출현)
    /// </summary>
    [System.Serializable]
    public class SpawnCondition
    {
        [Tooltip("출현 가능 지역 ID 목록 (월드 시스템의 지역 ID와 매칭)")]
        public string[] allowedZoneIds;

        [Tooltip("낮 시간대 출현 여부")]
        public bool canSpawnDay = true;

        [Tooltip("밤 시간대 출현 여부")]
        public bool canSpawnNight = true;

        [Tooltip("스폰 가중치 — 같은 구역 내 다른 멤 대비 출현 비중 (높을수록 자주 출현)")]
        [Range(0.1f, 10f)]
        public float spawnWeight = 1f;
    }
}
