// ============================================================================
// MemEvents.cs
// 정적 이벤트 버스 — 외부 시스템과의 느슨한 결합 통신 채널
//
// ★★★ [모든 담당자 필독] ★★★
// 멤 시스템과 연동하려면 이 이벤트를 구독하세요.
// 멤 시스템 내부 코드를 직접 참조할 필요가 없습니다.
//
// [구독 예시]
// private void OnEnable()  => MemEvents.OnMemCaptured += HandleCapture;
// private void OnDisable() => MemEvents.OnMemCaptured -= HandleCapture;
//
// ⚠️ 주의: OnEnable/OnDisable에서 반드시 구독/해제하세요. (메모리 누수 방지)
//
// ─────────────────────────────────────────────────────────────────────────────
// [플레이어 담당자 — 포획 연출 협업 가이드]
//
// 포획 연출은 아래 순서로 이벤트가 발행됩니다:
//
//  [캡슐 명중 순간]
//   1. ICapturable.NotifyCaptureBallHit(capsuleWorldPos) 직접 호출
//      → 멤 내부에서 빛남+축소 연출 자동 시작
//      → MemEvents.OnMemCaptureStarted 자동 발행
//
//  [캡슐 흔들림 — OnMemCaptureStarted 구독]
//   2. OnMemCaptureStarted(Mem mem, Vector3 capsulePos) 수신
//      → 캡슐 오브젝트의 흔들림 애니메이션 시작 (담당자 구현)
//
//  [포획 성공 — OnMemCaptured 구독]
//   3. OnMemCaptured(Mem mem, MemSnapshot snapshot) 수신
//      → 캡슐 반짝임 이펙트 + 캡슐 사라짐 연출 (담당자 구현)
//
//  [포획 실패 — OnMemCaptureFailed 구독]
//   4. OnMemCaptureFailed(Mem mem) 수신
//      → 캡슐 파열 이펙트 연출 (담당자 구현)
//      ※ 멤은 내부적으로 자동으로 캡슐에서 튀어나오는 연출을 재생합니다.
//
// ─────────────────────────────────────────────────────────────────────────────
// ============================================================================
using System;
using UnityEngine;

namespace MemSystem.Events
{
    /// <summary>
    /// 멤 시스템의 정적 이벤트 버스.
    /// 
    /// 외부 시스템별 구독 가이드:
    /// 
    /// [플레이어 시스템]
    /// - OnMemAttackPlayer   : 멤이 플레이어를 공격했을 때 (데미지 처리)
    /// - OnMemCaptureStarted : 캡슐에 멤이 빨려 들어가기 시작했을 때 → 캡슐 흔들림 연출 시작
    /// - OnMemCaptureFailed  : 포획 실패 시 → 캡슐 파열 이펙트 연출
    /// 
    /// [영지/창고 시스템]
    /// - OnMemCaptured: 포획 성공 시 MemSnapshot 수신 → 창고에 저장
    /// 
    /// [UI 시스템]
    /// - OnMemSpawned: HP바 등 UI 생성
    /// - OnMemDamaged: HP바 갱신
    /// - OnMemDespawned / OnMemFled: UI 정리
    /// 
    /// [도감 시스템]
    /// - OnMemCaptured: 최초 포획 시 도감 등록 처리
    /// </summary>
    public static class MemEvents
    {
        // =====================================================================
        // 라이프사이클 이벤트
        // =====================================================================

        /// <summary>
        /// 멤이 월드에 스폰 완료되었을 때 발행됩니다.
        /// UI 시스템에서 HP바 등을 생성할 때 사용합니다.
        /// </summary>
        public static Action<Core.Mem> OnMemSpawned;

        /// <summary>
        /// 멤이 자연 디스폰(플레이어 이탈 등)되었을 때 발행됩니다.
        /// </summary>
        public static Action<Core.Mem> OnMemDespawned;

        // =====================================================================
        // 전투 이벤트
        // =====================================================================

        /// <summary>
        /// 멤이 피격당했을 때 발행됩니다. (멤 인스턴스, 받은 데미지)
        /// UI 시스템에서 HP바를 갱신할 때 사용합니다.
        /// </summary>
        public static Action<Core.Mem, int> OnMemDamaged;

        /// <summary>
        /// 멤이 플레이어를 공격했을 때 발행됩니다. (멤 인스턴스, 가한 데미지)
        /// 플레이어 시스템에서 데미지 처리에 사용합니다.
        /// </summary>
        public static Action<Core.Mem, int> OnMemAttackPlayer;

        // =====================================================================
        // 포획 이벤트
        // =====================================================================

        /// <summary>
        /// 캡슐이 멤에 명중하여 흡수가 시작되는 순간 발행됩니다.
        /// (멤 인스턴스, 캡슐의 월드 좌표)
        ///
        /// [플레이어 담당자]
        /// 이 이벤트를 수신하여 캡슐 흔들흔들 연출을 시작하세요.
        /// 멤의 빛남+축소 연출은 멤 내부에서 자동으로 재생됩니다.
        ///
        /// 구독 예시:
        /// MemEvents.OnMemCaptureStarted += (mem, capsulePos) => capsule.PlayShakeAnim();
        /// </summary>
        public static Action<Core.Mem, Vector3> OnMemCaptureStarted;

        /// <summary>
        /// 포획 성공 시 발행됩니다. (멤 인스턴스, 직렬화된 스냅샷 데이터)
        /// 영지/창고 시스템은 MemSnapshot을 받아 저장합니다.
        /// 도감 시스템은 최초 포획 여부를 확인하여 등록합니다.
        ///
        /// [플레이어 담당자]
        /// 이 이벤트를 수신하여 캡슐 반짝임 이펙트 + 캡슐 사라짐 연출을 재생하세요.
        /// </summary>
        public static Action<Core.Mem, Data.MemSnapshot> OnMemCaptured;

        /// <summary>
        /// 포획 실패 시 발행됩니다.
        /// 멤은 내부적으로 캡슐에서 튀어나오는 연출을 자동 재생합니다.
        ///
        /// [플레이어 담당자]
        /// 이 이벤트를 수신하여 캡슐 파열 이펙트를 재생하세요.
        /// 멤의 탈출 연출(빛남+팝업)은 멤 내부에서 자동으로 처리됩니다.
        /// </summary>
        public static Action<Core.Mem> OnMemCaptureFailed;

        /// <summary>
        /// 멤이 도망쳐서 디스폰되었을 때 발행됩니다. (포획 실패 후 도주)
        /// </summary>
        public static Action<Core.Mem> OnMemFled;

        // =====================================================================
        // 유틸리티
        // =====================================================================

        /// <summary>
        /// 모든 이벤트 구독자를 해제합니다.
        /// 씬 전환 시 호출하여 잔여 구독으로 인한 메모리 누수를 방지합니다.
        /// 
        /// 호출 위치: 씬 매니저의 OnSceneUnloaded 등에서 호출해주세요.
        /// </summary>
        public static void ClearAll()
        {
            OnMemSpawned = null;
            OnMemDespawned = null;
            OnMemDamaged = null;
            OnMemAttackPlayer = null;
            OnMemCaptureStarted = null;
            OnMemCaptured = null;
            OnMemCaptureFailed = null;
            OnMemFled = null;
        }
    }
}
