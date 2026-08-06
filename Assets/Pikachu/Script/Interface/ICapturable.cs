// ============================================================================
// ICapturable.cs
// 포획 가능 객체 인터페이스
//
// [플레이어 담당자 필독]
// 플레이어가 멤을 포획할 때 이 인터페이스만 참조하면 됩니다.
// 멤 내부 구현(Mem.cs, MemStats.cs 등)을 직접 참조할 필요가 없습니다.
//
// ─────────────────────────────────────────────────────────────────────────────
// [포획 전체 흐름 — 플레이어 담당자 구현 참고서]
//
// [STEP 1] 조준 중 실시간 포획 확률 UI 표시
//   float rate = capturable.GetCaptureRate(myCapsuleTier);
//   // rate를 UI에 표시 (0.0 ~ 1.0 → 파센트로 변환하여 표시)
//
// [STEP 2] 캡슐 등 Trigger/Collider로 멤 명중 감지 시
//   capturable.NotifyCaptureBallHit(capsule.transform.position);
//   // → 멤 빛남+축소 연출 자동 재생
//   // → MemEvents.OnMemCaptureStarted 자동 발행
//   //    (이 이벤트를 구독하여 캡슐 흔들림 연출 재생)
//
// [STEP 3] 포획 판정 (조준 시탄, 또는 NotifyCaptureBallHit 후 일정 시간 후)
//   if (Random.value <= capturable.GetCaptureRate(myCapsuleTier))
//   {
//       capturable.OnCaptureSuccess();
//       // → MemEvents.OnMemCaptured 자동 발행
//       //    (이 이벤트를 구독하여 캡슐 반짝임 + 사라짐 연출 재생)
//   }
//   else
//   {
//       capturable.OnCaptureFail();
//       // → 멤이 캡슐에서 튀어나는 연출 자동 재생
//       // → MemEvents.OnMemCaptureFailed 자동 발행
//       //    (이 이벤트를 구독하여 캡슐 파열 이펙트 연출 재생)
//   }
// ─────────────────────────────────────────────────────────────────────────────
// ============================================================================
using MemSystem.Data;
using UnityEngine;

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
        /// <summary>중간 등급 — 포획 난이도 계산에 사용됩니다.</summary>
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
        ///
        /// [플레이어 담당자]
        /// 조준 UI에 이 값을 실시간으로 표시하세요.
        /// 캡슐 등급(capsuleTier)은 0(가장 낮은 등급)부터 시작합니다.
        /// </summary>
        /// <param name="capsuleTier">사용된 캡슐의 등급 (0부터 시작)</param>
        /// <returns>0.0 ~ 1.0 사이의 포획 확률</returns>
        float GetCaptureRate(int capsuleTier);

        /// <summary>
        /// 캡슐이 멤에 명중했을 때 호출합니다.
        ///
        /// 내부에서 자동 처리되는 사항:
        /// 1. 멤 빛남+축소(캡슐로 빨려들어가는) 연출 시작
        /// 2. AI 상태를 CapturedState로 전환 (이동 정지)
        /// 3. MemEvents.OnMemCaptureStarted 이벤트 발행
        ///
        /// [플레이어 담당자]
        /// 캡슐이 멤에 히트된 직후 이 메서드를 호출하세요.
        /// 캡슐의 월드 좌표를 전달하면 캡슐 트랜스폼 위치를 외부로 노출할 수 있습니다.
        /// (연출 수신은 MemEvents.OnMemCaptureStarted 이벤트를 구독하세요)
        ///
        /// 예시:
        ///   if (Physics.Raycast(..., out RaycastHit hit))
        ///     if (hit.collider.TryGetComponent<ICapturable>(out var c))
        ///       c.NotifyCaptureBallHit(capsule.transform.position);
        /// </summary>
        /// <param name="capsulePosition">캡슐이 명중한 순간의 월드 좌표</param>
        void NotifyCaptureBallHit(Vector3 capsulePosition);

        /// <summary>
        /// 포획 성공 시 호출합니다.
        /// 내부적으로 MemSnapshot 생성 → 이벤트 발행 → 풀 반환이 처리됩니다.
        ///
        /// [플레이어 담당자]
        /// 포획 판정이 성공으로 전환됩니다.
        /// 이 호출 후 MemEvents.OnMemCaptured 이벤트가 발행됩니다.
        /// (캡슐 반짝임 + 사라짐 연출은 해당 이벤트를 구독하세요)
        /// </summary>
        void OnCaptureSuccess();

        /// <summary>
        /// 포획 실패 시 호출합니다.
        ///
        /// 내부에서 자동 처리되는 사항:
        /// - 멤이 캡슐에서 튀어나오는 연출(빛남+팝업) 자동 재생
        /// - MemEvents.OnMemCaptureFailed 이벤트 발행
        ///
        /// [플레이어 담당자]
        /// 포획 판정이 실패로 전환됩니다.
        /// 캡슐 파열 이펙트는 MemEvents.OnMemCaptureFailed 이벤트를 구독하세요.
        /// 도망갈지(디스폰) 탈출할지(전투 속행)는 멤 내부 스탯(등급/성격)에 따라 자체적으로 결정됩니다.
        /// </summary>
        void OnCaptureFail();

        /// <summary>
        /// [Obsolete] 기존 코드와의 호환성을 위해 남겨둔 오버로드입니다.
        /// 도망 여부는 멤 내부에서 자체 결정하므로 매개변수 없는 OnCaptureFail()을 사용해 주세요.
        /// </summary>
        [System.Obsolete("도망 여부는 멤이 자체 판단합니다. 매개변수 없는 OnCaptureFail()을 사용하세요.")]
        void OnCaptureFail(bool shouldFlee);
    }
}
