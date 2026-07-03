// ============================================================================
// MemSnapshot.cs
// 직렬화 가능한 데이터 전달 객체 (DTO)
// 
// [영지/창고 담당자 필독]
// 포획 성공 시, 멤의 런타임 상태를 이 객체로 복사하여 전달합니다.
// 런타임 MonoBehaviour(Mem.cs)는 Object Pool에 반환되므로 직접 참조하면 안 됩니다.
// 
// 사용법:
//   MemEvents.OnMemCaptured += (mem, snapshot) => { myStorage.Add(snapshot); };
// ============================================================================
using System;

namespace MemSystem.Data
{
    /// <summary>
    /// 멤의 런타임 상태를 직렬화 가능한 순수 데이터로 복사한 스냅샷.
    /// 
    /// [포획 흐름]
    /// 1. 플레이어가 멤을 포획 성공
    /// 2. Mem.OnCaptureSuccess() 내부에서 MemSnapshot.FromMem(this) 호출
    /// 3. MemEvents.OnMemCaptured 이벤트로 스냅샷 발행
    /// 4. 영지/창고 시스템이 이벤트를 구독하여 스냅샷 저장
    /// 5. 멤은 Object Pool에 반환됨 (재사용)
    /// 
    /// [직렬화]
    /// JSON 등으로 저장/로드 가능하도록 [Serializable] 표시되어 있습니다.
    /// </summary>
    [Serializable]
    public class MemSnapshot
    {
        // =====================================================================
        // 기본 정보
        // =====================================================================

        /// <summary>멤 고유 식별자 (예: "mem_rare_001")</summary>
        public string memId;

        /// <summary>멤 표시 이름</summary>
        public string memName;

        /// <summary>등급</summary>
        public MemTier tier;

        /// <summary>성격</summary>
        public MemPersonality personality;

        // =====================================================================
        // 스탯
        // =====================================================================

        /// <summary>최대 체력</summary>
        public int maxHp;

        /// <summary>최대 허기</summary>
        public int maxHunger;

        /// <summary>생산 스탯 (5종)</summary>
        public ProductionStats productionStats;

        /// <summary>탐험 스탯</summary>
        public int explorationStat;

        // =====================================================================
        // 포획 시점 상태
        // =====================================================================

        /// <summary>포획 당시 남은 HP</summary>
        public int capturedHp;

        /// <summary>포획 시각 (UTC Unix timestamp, 초 단위)</summary>
        public long capturedTimestamp;

        // =====================================================================
        // 팩토리 메서드
        // =====================================================================

        /// <summary>
        /// 멤 런타임 객체로부터 스냅샷을 생성합니다.
        /// Mem.OnCaptureSuccess() 내부에서 풀 반환 전에 호출됩니다.
        /// </summary>
        /// <param name="mem">포획된 멤 인스턴스</param>
        /// <returns>직렬화 가능한 멤 스냅샷</returns>
        public static MemSnapshot FromMem(Core.Mem mem)
        {
            var stats = mem.Stats;
            return new MemSnapshot
            {
                memId = stats.MemId,
                memName = stats.MemName,
                tier = stats.Tier,
                personality = stats.Personality,
                maxHp = stats.MaxHp,
                maxHunger = stats.MaxHunger,
                productionStats = stats.ProductionStats,
                explorationStat = stats.ExplorationStat,
                capturedHp = stats.CurrentHp,
                capturedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        public override string ToString()
        {
            return $"[MemSnapshot] {memName} ({tier}) HP:{capturedHp}/{maxHp}";
        }
    }
}
