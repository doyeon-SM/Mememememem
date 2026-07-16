// ============================================================================
// IMemState.cs
// FSM 상태 인터페이스
//
// [담당자 안내]
// 멤 AI의 각 상태(Idle, Wander, Combat, Flee, Captured)가 이 인터페이스를 구현합니다.
// 새로운 상태를 추가하려면 이 인터페이스를 구현하는 클래스를 만들고,
// MemAI에 상태 인스턴스와 전환 조건을 추가하면 됩니다.
// ============================================================================
namespace MemSystem.AI
{
    /// <summary>
    /// 멤 AI 유한 상태 머신(FSM)의 개별 상태 인터페이스.
    /// 
    /// [상태 생명주기]
    /// Enter() → Update() (매 프레임) → Exit()
    /// 
    /// [상태 전환]
    /// Update() 내부에서 조건을 체크하고,
    /// ai.TransitionTo(ai.다른State)를 호출하면 전환됩니다.
    /// </summary>
    public interface IMemState
    {
        /// <summary>
        /// 상태 진입 시 1회 호출됩니다.
        /// 이동 정지, 애니메이션 전환, 타이머 초기화 등 초기 설정을 수행합니다.
        /// </summary>
        /// <param name="ai">이 멤의 AI 컨트롤러</param>
        void Enter(MemAI ai);

        /// <summary>
        /// 매 프레임 호출됩니다.
        /// 상태별 행동 로직(이동, 공격 등)과 전환 조건 체크를 수행합니다.
        /// </summary>
        /// <param name="ai">이 멤의 AI 컨트롤러</param>
        void Update(MemAI ai);

        /// <summary>
        /// 상태 이탈 시 1회 호출됩니다.
        /// 이동 정지 등 정리(cleanup) 로직을 수행합니다.
        /// </summary>
        /// <param name="ai">이 멤의 AI 컨트롤러</param>
        void Exit(MemAI ai);
    }
}
