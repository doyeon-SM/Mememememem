// ============================================================================
// MemSound.cs
// 멤 효과음 재생 컴포넌트 (3D 사운드)
//
// [담당자 안내]
// - Mem 루트 GameObject에 붙습니다. AudioSource는 Awake에서 자동 확보/설정합니다.
//   · voiceSource — 울음/포획/공격음 (프리팹에 있는 AudioSource를 사용)
//   · stepSource  — 발걸음 전용 (Awake에서 자동 추가). 울음과 피치·감쇠가 서로 간섭하지 않도록 분리했습니다.
//
// - 발걸음(footstepClip)
//   · 이동 중이면 자동으로 재생됩니다. 상태(FSM)에서 호출할 필요 없이 이 컴포넌트의 Update가 처리합니다.
//   · "걸은 거리"를 기준으로 밟기 때문에(footstepStride) 속도가 빨라지면 발소리도 자동으로 빨라집니다.
//   · 영지에서는 소리가 나면 안 되므로 TerritoryWanderSpawner가 SetFootstepsEnabled(false)를 호출합니다.
//
// - 울음(cryClip)
//   · 배회 중 가끔씩 재생됩니다. WanderState.Enter() → BeginCry() / Update() → TickCry() / Exit() → StopCry()
//   · 멤이 많아도 정신없지 않도록 "전역 게이트"를 둡니다. 아래 [울음 전역 게이트] 참고.
//
// - 클립은 Assets/Pikachu/Resource/Sound/ 안의 파일을 Mem_Base 프리팹에 물려두었습니다.
//   (폴더명이 Resources가 아니라 Resource라서 Resources.Load로는 못 불러옵니다.
//    새 효과음을 쓰려면 프리팹의 MemSound 슬롯에 드래그해서 교체하세요.)
//
// [울음 전역 게이트 — 겹침 방지 & 남발 방지]
// 멤 개체 수가 늘어나도 "귀에 들어오는 울음 횟수"는 일정하게 유지되어야 합니다.
// 그래서 개체별 타이머와 별개로, 씬 전체가 공유하는 static 게이트를 두 겹 겁니다.
//   1) 겹침 방지 — 누군가 울고 있는 동안에는(클립 길이만큼) 아무도 울지 않습니다.
//   2) 남발 방지 — 울음이 끝난 뒤에도 globalCryCooldownRange 만큼 전역 침묵을 유지합니다.
// 결과적으로 멤이 2마리든 20마리든 울음은 "한 번에 하나씩, 최소 N초 간격"으로만 들립니다.
// 빈도를 바꾸고 싶으면 globalCryCooldownRange만 조절하면 됩니다. (개체 설정은 건드릴 필요 없음)
// ============================================================================
using System.Collections;
using UnityEngine;
using MemSystem.Movement;

namespace MemSystem.Sound
{
    /// <summary>
    /// 멤의 효과음을 재생하는 컴포넌트.
    /// 이동 중 발걸음을 자동 재생하고, 배회 중 랜덤한 간격으로 울음소리를 재생합니다.
    /// </summary>
    public class MemSound : MonoBehaviour
    {
        // =================================================================
        // Inspector 설정
        // =================================================================

        [Header("클립")]
        [Tooltip("배회 중 재생할 울음소리 클립")]
        [SerializeField] private AudioClip cryClip;

        [Tooltip("캡슐에 맞았을 때 재생할 클립 (포획 성공/실패와 무관하게 시도 시점에 재생)")]
        [SerializeField] private AudioClip catchClip;

        [Tooltip("캡슐 피격음 볼륨 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float catchVolume = 0.9f;

        [Tooltip("공격이 플레이어에 닿는 순간 재생할 클립")]
        [SerializeField] private AudioClip attackClip;

        [Tooltip("공격음 볼륨 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float attackVolume = 0.8f;

        [Tooltip("공격 모션 시작부터 실제로 닿아 보이는 순간까지의 지연 (초).\n" +
                 "돌진 애니메이션이 플레이어에 닿는 타이밍에 맞춰 조절하세요. 0이면 즉시 재생합니다.")]
        [SerializeField] private float attackHitDelay = 0.25f;

        [Header("발걸음")]
        [Tooltip("이동 중 재생할 발걸음 클립. 한 걸음짜리 짧은 소리를 넣으세요.")]
        [SerializeField] private AudioClip footstepClip;

        [Tooltip("한 걸음 사이의 이동 거리(m). 작을수록 촘촘하게 밟습니다.\n" +
                 "거리 기준이라 이동 속도가 빨라지면 발소리도 자동으로 빨라집니다.")]
        [SerializeField] private float footstepStride = 0.6f;

        [Tooltip("발걸음 볼륨 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float footstepVolume = 0.35f;

        [Tooltip("발걸음마다 랜덤 적용할 피치 범위. 같은 소리가 반복되는 느낌을 줄여줍니다.")]
        [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.92f, 1.08f);

        [Tooltip("발걸음이 원본 볼륨으로 들리는 거리(m). 울음보다 훨씬 짧게 잡으세요.")]
        [SerializeField] private float footstepMinDistance = 2f;

        [Tooltip("발걸음이 들리는 최대 거리(m). 이 밖의 멤은 재생 자체를 건너뜁니다.")]
        [SerializeField] private float footstepMaxDistance = 10f;

        [Tooltip("여러 멤이 우르르 걸을 때 발소리가 뭉개지지 않도록 하는 전역 최소 간격(초).\n" +
                 "0.06이면 씬 전체에서 초당 최대 약 16걸음까지만 재생됩니다.")]
        [SerializeField] private float globalFootstepGap = 0.06f;

        [Header("울음 빈도 (개체)")]
        [Tooltip("울음 판정 간격 범위 (초). x=최소, y=최대.")]
        [SerializeField] private Vector2 cryIntervalRange = new Vector2(5f, 12f);

        [Tooltip("판정 시점에 실제로 울 확률 (0~1). 낮출수록 더 가끔 웁니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float cryChance = 0.6f;

        [Header("울음 빈도 (전역 — 여기만 만지면 됩니다)")]
        [Tooltip("울음이 끝난 뒤 씬 전체가 조용해야 하는 시간 범위(초). x=최소, y=최대.\n" +
                 "멤이 몇 마리든 이 간격보다 촘촘하게는 울지 않습니다. 시끄러우면 이 값을 키우세요.")]
        [SerializeField] private Vector2 globalCryCooldownRange = new Vector2(3f, 7f);

        [Tooltip("울음이 겹치지 않도록, 재생 중인 울음이 끝날 때까지 다른 멤을 막습니다.\n" +
                 "끄면 여러 마리가 동시에 울 수 있습니다. (권장: 켜기)")]
        [SerializeField] private bool blockOverlappingCry = true;

        [Header("재생 설정")]
        [Tooltip("울음소리 볼륨 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.7f;

        [Tooltip("재생마다 랜덤 적용할 피치 범위. 개체마다 목소리가 달라 보이는 효과.")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.15f);

        [Header("3D 감쇠 (울음/포획/공격)")]
        [Tooltip("이 거리 안에서는 원본 볼륨 그대로 들립니다.")]
        [SerializeField] private float minDistance = 3f;

        [Tooltip("이 거리를 넘으면 들리지 않습니다. 이 밖에 있으면 재생 자체를 건너뜁니다.")]
        [SerializeField] private float maxDistance = 25f;

        // =================================================================
        // 내부 상태
        // =================================================================

        /// <summary>울음/포획/공격 재생용 소스. 프리팹에 붙어 있는 AudioSource를 사용합니다.</summary>
        private AudioSource voiceSource;

        /// <summary>발걸음 전용 소스. 울음과 피치·감쇠가 섞이지 않도록 Awake에서 따로 추가합니다.</summary>
        private AudioSource stepSource;

        /// <summary>이동 속도를 읽어 발걸음 타이밍을 잡기 위한 참조.</summary>
        private MemMovement movement;

        /// <summary>다음 울음 판정까지 남은 시간 (초). 음수면 미예약.</summary>
        private float cryTimer = -1f;

        /// <summary>배회 중이라 울음 판정이 활성인지 여부.</summary>
        private bool crying;

        /// <summary>이 개체의 모든 효과음 음소거 여부.</summary>
        private bool muted;

        /// <summary>이 개체의 발걸음 재생 여부. 영지 소환 멤은 false로 설정합니다.</summary>
        private bool footstepsEnabled = true;

        /// <summary>마지막 발걸음 이후 걸은 거리(m). footstepStride를 넘으면 한 걸음 재생.</summary>
        private float stepDistance;

        /// <summary>현재 재생 중인 울음이 끝나는 시각 + 전역 쿨다운. 이 시각 전에는 아무도 울지 않습니다.</summary>
        private static float globalCryBlockedUntil = -999f;

        /// <summary>마지막으로 어떤 멤이든 발을 디딘 시각 (전역 공유).</summary>
        private static float lastGlobalFootstepTime = -999f;

        /// <summary>거리 판정용 리스너 캐시 (씬에 보통 하나).</summary>
        private static AudioListener cachedListener;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Awake()
        {
            movement = GetComponent<MemMovement>();

            // --- 울음/포획/공격용 소스 ---
            voiceSource = GetComponent<AudioSource>();
            if (voiceSource == null)
                voiceSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(voiceSource, minDistance, maxDistance);

            // --- 발걸음 전용 소스 ---
            // 같은 소스를 쓰면 울음이 pitch를 바꿀 때 재생 중인 발소리까지 같이 변조된다.
            stepSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(stepSource, footstepMinDistance, footstepMaxDistance);
        }

        /// <summary>AudioSource를 3D 효과음용으로 세팅합니다. (멤 위치에서 들려야 하므로 spatialBlend=1)</summary>
        private void ConfigureSource(AudioSource src, float srcMinDistance, float srcMaxDistance)
        {
            src.playOnAwake  = false;
            src.loop         = false;
            src.spatialBlend = 1f;
            src.dopplerLevel = 0f;
            src.rolloffMode  = AudioRolloffMode.Linear;
            src.minDistance  = srcMinDistance;
            src.maxDistance  = srcMaxDistance;
        }

        private void Update()
        {
            TickFootstep();
        }

        // =================================================================
        // 발걸음 — 이동 속도를 보고 스스로 재생 (FSM에서 호출할 필요 없음)
        // =================================================================

        /// <summary>
        /// 이동한 거리를 누적하다가 한 걸음 분(footstepStride)을 넘으면 발소리를 재생합니다.
        /// 시간이 아니라 거리 기준이라, 걷기/달리기 속도가 바뀌어도 자동으로 보폭이 맞습니다.
        /// </summary>
        private void TickFootstep()
        {
            if (muted || !footstepsEnabled || footstepClip == null || movement == null) return;

            float speed = movement.CurrentSpeed;

            // 멈춰 있으면 누적을 리셋해, 다시 걷기 시작할 때 첫 걸음이 바로 나오지 않게 한다.
            if (speed <= 0.1f)
            {
                stepDistance = 0f;
                return;
            }

            stepDistance += speed * Time.deltaTime;
            if (stepDistance < footstepStride) return;

            stepDistance = 0f;
            PlayFootstep();
        }

        /// <summary>발소리 1회 재생. 전역 간격과 가청 거리를 통과할 때만 실제로 울립니다.</summary>
        private void PlayFootstep()
        {
            // 여러 마리가 우르르 걸을 때 소리가 뭉개지지 않도록 전역 간격을 둔다
            if (Time.time - lastGlobalFootstepTime < globalFootstepGap) return;

            // 들리지도 않을 거리면 보이스 낭비 없이 스킵
            if (!IsListenerInRange(footstepMaxDistance)) return;

            lastGlobalFootstepTime = Time.time;

            stepSource.pitch = Random.Range(footstepPitchRange.x, footstepPitchRange.y);
            stepSource.PlayOneShot(footstepClip, footstepVolume);
        }

        /// <summary>
        /// 이 개체의 발걸음 재생을 켜고 끕니다.
        /// 영지(TerritoryWanderSpawner)에서 소환된 멤은 발소리가 나면 안 되므로 false로 설정합니다.
        /// 풀에 반환되면 자동으로 true로 돌아가므로, 소환할 때마다 명시적으로 지정해야 합니다.
        /// </summary>
        public void SetFootstepsEnabled(bool value)
        {
            footstepsEnabled = value;
            stepDistance = 0f;
            if (!footstepsEnabled && stepSource != null) stepSource.Stop();
        }

        // =================================================================
        // 배회 울음 — WanderState에서 호출
        // =================================================================

        /// <summary>
        /// 배회 울음 판정을 시작합니다. (WanderState.Enter에서 호출)
        /// 진입 직후 바로 울지 않도록 첫 간격을 랜덤하게 잡습니다.
        /// 이미 예약된 타이머가 있으면 유지합니다 —
        /// Idle↔Wander를 짧게 오갈 때마다 리셋되면 영영 울지 못하기 때문입니다.
        /// </summary>
        public void BeginCry()
        {
            crying = true;
            if (cryTimer < 0f) ScheduleNextCry();
        }

        /// <summary>
        /// 매 프레임 호출되어 울음 타이밍을 검사합니다. (WanderState.Update에서 호출)
        /// 타이머는 배회 중일 때만 흐릅니다.
        /// </summary>
        public void TickCry()
        {
            if (!crying) return;

            cryTimer -= Time.deltaTime;
            if (cryTimer > 0f) return;

            ScheduleNextCry();

            // "가끔씩" — 판정 시점마다 확률로만 실제 재생
            if (Random.value > cryChance) return;

            PlayCry();
        }

        /// <summary>
        /// 울음 판정을 정지합니다. (WanderState.Exit에서 호출)
        /// 남은 타이머는 그대로 두어 다음 배회에서 이어집니다.
        /// 이미 재생 중인 소리는 자연스럽게 끝까지 들립니다.
        /// </summary>
        public void StopCry()
        {
            crying = false;
        }

        /// <summary>
        /// 울음소리를 1회 재생합니다. 다른 시스템에서 직접 울리고 싶을 때도 호출할 수 있습니다.
        ///
        /// 전역 게이트를 통과하지 못하면(다른 멤이 울고 있거나 전역 쿨다운 중) 조용히 무시됩니다.
        /// 멤이 몇 마리든 울음은 한 번에 하나씩, 최소 간격을 두고만 들립니다.
        /// </summary>
        public void PlayCry()
        {
            if (muted) return;
            if (cryClip == null || voiceSource == null) return;

            // [전역 게이트] 겹침 방지 + 남발 방지.
            // 앞서 운 멤이 재생을 마치고 쿨다운까지 지나야 다음 울음이 허용된다.
            if (blockOverlappingCry && Time.time < globalCryBlockedUntil) return;

            // 들리지도 않을 거리면 보이스 낭비 없이 스킵.
            // (게이트를 점유하지 않으므로, 화면 밖 멤이 "울 차례"를 낭비하지 않는다)
            if (!IsListenerInRange(maxDistance)) return;

            float pitch = Random.Range(pitchRange.x, pitchRange.y);
            voiceSource.pitch = pitch;
            voiceSource.PlayOneShot(cryClip, volume);

            // 이 울음이 끝나는 시각 + 전역 쿨다운까지 다른 멤을 막는다.
            // 피치가 높으면 클립이 그만큼 짧게 끝나므로 재생 길이도 피치로 보정한다.
            float clipDuration = cryClip.length / Mathf.Max(0.01f, pitch);
            globalCryBlockedUntil = Time.time + clipDuration +
                                    Random.Range(globalCryCooldownRange.x, globalCryCooldownRange.y);
        }

        // =================================================================
        // 포획 효과음 — Mem.NotifyCaptureBallHit에서 호출
        // =================================================================

        /// <summary>
        /// 캡슐에 맞은 순간의 효과음을 재생합니다.
        /// 포획 성공/실패가 판정되기 전, 시도 시점에 무조건 한 번 울립니다.
        /// 플레이어 피드백이므로 울음소리와 달리 전역 간격·거리 제한을 받지 않습니다.
        /// </summary>
        public void PlayCatch()
        {
            if (muted) return;
            if (catchClip == null || voiceSource == null) return;

            voiceSource.pitch = 1f;
            voiceSource.PlayOneShot(catchClip, catchVolume);
        }

        // =================================================================
        // 공격 효과음 — CombatState.PerformAttack에서 호출
        // =================================================================

        /// <summary>
        /// 공격이 플레이어에 닿는 순간의 효과음을 재생합니다.
        ///
        /// 이 시스템은 공격 모션 시작과 동시에 데미지를 확정하므로(MemEvents.OnMemAttackPlayer),
        /// 코드상 "닿는 순간"이 따로 없습니다. 대신 attackHitDelay만큼 늦춰 재생해
        /// 돌진 애니메이션이 플레이어에 닿아 보이는 타이밍에 소리를 맞춥니다.
        /// </summary>
        public void PlayAttackHit()
        {
            if (muted || attackClip == null || voiceSource == null) return;

            if (attackHitDelay <= 0f)
            {
                PlayAttackClip();
                return;
            }

            StartCoroutine(PlayAttackHitDelayed());
        }

        private IEnumerator PlayAttackHitDelayed()
        {
            yield return new WaitForSeconds(attackHitDelay);
            PlayAttackClip();
        }

        private void PlayAttackClip()
        {
            // 지연 대기 중에 음소거되거나 클립이 교체됐을 수 있으므로 재확인
            if (muted || attackClip == null || voiceSource == null) return;

            voiceSource.pitch = 1f;
            voiceSource.PlayOneShot(attackClip, attackVolume);
        }

        // =================================================================
        // 음소거
        // =================================================================

        /// <summary>
        /// 이 개체의 모든 효과음을 켜고 끕니다. (발걸음만 끄려면 SetFootstepsEnabled를 쓰세요)
        /// 풀에 반환되면 자동으로 해제되므로, 필요하면 소환할 때마다 명시적으로 지정해야 합니다.
        /// </summary>
        public void SetMuted(bool value)
        {
            muted = value;
            if (!muted) return;

            if (voiceSource != null) voiceSource.Stop();
            if (stepSource != null) stepSource.Stop();
        }

        // =================================================================
        // 풀 반환 처리 — Mem.ResetForPool에서 호출
        // =================================================================

        /// <summary>
        /// 풀 반환 시 재생 중인 소리를 끊고 판정을 정지합니다.
        /// (풀에서 꺼내 쓸 때 이전 개체의 소리가 이어지지 않도록)
        /// </summary>
        public void ResetSound()
        {
            crying = false;
            cryTimer = -1f;
            muted = false;
            footstepsEnabled = true;
            stepDistance = 0f;

            if (voiceSource != null) voiceSource.Stop();
            if (stepSource != null) stepSource.Stop();
        }

        // =================================================================
        // 내부 유틸
        // =================================================================

        /// <summary>다음 울음 판정까지의 시간을 랜덤 간격으로 예약합니다.</summary>
        private void ScheduleNextCry()
        {
            cryTimer = Random.Range(cryIntervalRange.x, cryIntervalRange.y);
        }

        /// <summary>리스너(플레이어 카메라)가 지정한 가청 거리 안에 있는지 검사합니다.</summary>
        private bool IsListenerInRange(float audibleDistance)
        {
            if (cachedListener == null)
                cachedListener = FindFirstObjectByType<AudioListener>();

            if (cachedListener == null) return true; // 리스너를 못 찾으면 판정 생략

            float sqrDistance = (cachedListener.transform.position - transform.position).sqrMagnitude;
            return sqrDistance <= audibleDistance * audibleDistance;
        }
    }
}
