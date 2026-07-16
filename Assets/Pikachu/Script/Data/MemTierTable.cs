// ============================================================================
// MemTierTable.cs
// ScriptableObject — 등급별 고정 스펙 테이블
//
// [담당자 안내]
// - 모든 등급의 고정 수치(HP, 허기, 생산/소비량, 탐험 범위)를 한 곳에서 관리합니다.
// - 밸런싱 수정 시 이 에셋 하나만 변경하면 전체 등급에 반영됩니다.
// - Project 창 우클릭 → Create → Mem → MemTierTable 로 에셋을 생성합니다.
// ============================================================================
using UnityEngine;

namespace MemSystem.Data
{
    /// <summary>
    /// 등급별 고정 스펙을 관리하는 테이블 에셋.
    /// 
    /// 기획서 참조: [2.2. 등급별 상세 스펙 (5단계)]
    /// 
    /// 사용 흐름:
    /// 1. MemFactory가 멤을 초기화할 때 이 테이블에서 등급 스펙을 조회
    /// 2. MemStats.ApplyTierSpec()으로 등급별 고정값을 덮어씀
    /// 3. 개별 MemData에는 개체별 차이값(성격, 모델 등)만 설정
    /// </summary>
    [CreateAssetMenu(fileName = "MemTierTable", menuName = "Mem/MemTierTable")]
    public class MemTierTable : ScriptableObject
    {
        [Tooltip("등급별 스펙 배열 — Rare, Epic, Unique, Legendary, Mythic 순서로 설정해주세요.")]
        public MemTierSpec[] specs;

        /// <summary>
        /// 특정 등급의 스펙을 조회합니다.
        /// </summary>
        /// <param name="tier">조회할 등급</param>
        /// <returns>해당 등급의 스펙. 미정의 시 null 반환 + 경고 로그</returns>
        public MemTierSpec GetSpec(MemTier tier)
        {
            if (specs == null) return null;

            for (int i = 0; i < specs.Length; i++)
            {
                if (specs[i].tier == tier)
                    return specs[i];
            }

            Debug.LogWarning($"[MemTierTable] 등급 '{tier}'에 대한 스펙이 정의되어 있지 않습니다. " +
                             $"MemTierTable 에셋에서 해당 등급을 추가해주세요.");
            return null;
        }
    }

    /// <summary>
    /// 개별 등급의 고정 스펙 정의.
    /// 
    /// 기획서 기반 예시 (레어):
    /// - baseHp: 10, baseHunger: 10
    /// - consumePerTick: 1, producePerTick: 2
    /// - explorationMin: 20, explorationMax: 100
    /// - primaryStatLevel: 1, secondaryStatLevel: 0~1
    /// </summary>
    [System.Serializable]
    public class MemTierSpec
    {
        [Tooltip("등급")]
        public MemTier tier;

        [Header("기본 스탯")]
        [Tooltip("기본 체력 (등급별 고정)")]
        public int baseHp = 10;

        [Tooltip("기본 허기 (등급별 고정)")]
        public int baseHunger = 10;

        [Header("영지 작업 (분 단위 틱 연산)")]
        [Tooltip("분당 허기 소비량 — 작업 중인 멤이 1분마다 소모하는 허기")]
        public int consumePerTick = 1;

        [Tooltip("분당 자원 생산량 — 시설 배치 시 1분마다 생산하는 자원량")]
        public int producePerTick = 2;

        [Header("탐험 스탯 범위")]
        [Tooltip("탐험 스탯 최솟값")]
        public int explorationMin = 20;

        [Tooltip("탐험 스탯 최댓값")]
        public int explorationMax = 100;

        [Header("생산 스탯 구성")]
        [Tooltip("이 등급에서 가질 수 있는 주(主) 생산 스탯 최대 레벨")]
        [Range(1, 5)] public int primaryStatLevel = 1;

        [Tooltip("이 등급에서 가질 수 있는 부(副) 생산 스탯 레벨 (0이면 없음)")]
        [Range(0, 5)] public int secondaryStatLevel = 0;

        /// <summary>
        /// 이 등급의 탐험 스탯 범위 내에서 랜덤 값을 생성합니다.
        /// 멤 개체가 소환될 때마다 호출되어 개체별 다양성을 부여합니다.
        /// </summary>
        /// <returns>explorationMin ~ explorationMax 사이의 랜덤 정수</returns>
        public int RollExplorationStat()
        {
            return Random.Range(explorationMin, explorationMax + 1);
        }
    }
}
