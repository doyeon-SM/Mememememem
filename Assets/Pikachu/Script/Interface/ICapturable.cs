// ============================================================================
// ICapturable.cs
// 포획 가능 객체 인터페이스
//
// [플레이어 담당자 필독]
// 플레이어가 멤을 포획할 때 이 인터페이스만 참조하면 됩니다.
// 멤 내부 구현(Mem.cs, MemStats.cs 등)을 직접 참조할 필요가 없습니다.
//
// [사용 예시]
// if (hit.collider.TryGetComponent<ICapturable>(out var capturable))
// {
//     float rate = capturable.GetCaptureRate(myCapsuleTier);
//     // UI에 포획 확률 표시
//     if (Random.value <= rate)
//         capturable.OnCaptureSuccess();
//     else
//         capturable.OnCaptureFail(shouldFlee: Random.value > 0.5f);
// }
// ============================================================================
using MemSystem.Data;

namespace MemSystem.Interface
{
    /// <summary>
    /// 포획 가능한 객체가 구현하는 인터페이스.
    /// 
    /// [설계 의도 - 의존성 역전]
    /// 플레이어 시스템은 ICapturable만 알면 됩니다.
    /// 멤 시스템의 구체적인 구현에 의존하지 않으므로,
    /// 양쪽이 독립적으로 개발/수정할 수 있습니다.
    /// </summary>
    public interface ICapturable
    {
        /// <summary>현재 등급 — 포획 난이도 계산에 사용됩니다.</summary>
        MemTier Tier { get; }

        /// <summary>현재 HP — 낮을수록 포획 확률이 올라갑니다.</summary>
        int CurrentHp { get; }

        /// <summary>최대 HP — 포획 확률 계산의 기준값입니다.</summary>
        int MaxHp { get; }

        /// <summary>
        /// 현재 포획 확률을 계산하여 반환합니다.
        /// 
        /// 공식: (1 - 현재HP/최대HP) × 등급보정 × 캡슐보정
        /// - HP가 낮을수록 확률 증가
        /// - 등급이 높을수록 확률 감소
        /// - 캡슐 등급이 높을수록 확률 증가
        /// </summary>
        /// <param name="capsuleTier">사용된 캡슐의 등급 (0부터 시작)</param>
        /// <returns>0.0 ~ 1.0 사이의 포획 확률</returns>
        float GetCaptureRate(int capsuleTier);

        /// <summary>
        /// 포획 성공 시 호출합니다.
        /// 내부적으로 MemSnapshot 생성 → 이벤트 발행 → 풀 반환이 처리됩니다.
        /// </summary>
        void OnCaptureSuccess();

        /// <summary>
        /// 포획 실패 시 호출합니다.
        /// </summary>
        /// <param name="shouldFlee">true면 멤이 도망(디스폰), false면 전투 속행</param>
        void OnCaptureFail(bool shouldFlee);
    }
}
