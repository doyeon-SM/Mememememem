// ============================================================================
// MemSound.cs
// 멤 효과음 재생 컴포넌트 (3D 사운드)
//
// [담당자 안내]
// - Mem 루트 GameObject에 붙습니다. AudioSource는 Awake에서 자동 확보/설정합니다.
// - 현재 사용처: 배회(WanderState) 중 가끔씩 울음소리.
//   WanderState.Enter() → BeginCry() / Update() → TickCry() / Exit() → StopCry()
// - 울음 간격/확률/볼륨/피치는 전부 Inspector에서 조절합니다.
// - 여러 멤이 동시에 울어서 뭉개지는 것을 막기 위해
//   "전역 최소 간격(globalCryGap)"을 두어 프레임당 한 마리만 울게 제한합니다.
// - 클립은 Assets/Pikachu/Resource/Sound/mem.mp3 을 Mem_Base 프리팹에 물려두었습니다.
//   (폴더명이 Resources가 아니라 Resource라서 Resources.Load로는 못 불러옵니다.
//    새 효과음을 쓰려면 프리팹의 MemSound.cryClip에 드래그해서 교체하세요.)
// ============================================================================
using System.Collections;
using UnityEngine;

namespace MemSystem.Sound
{
    /// <summary>
    /// 멤의 효과음을 재생하는 컴포넌트.
    /// 배회 중 랜덤한 간격으로 울음소리를 재생합니다.
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

        [Header("울음 빈도")]
        [Tooltip("울음 판정 간격 범위 (초). x=최소, y=최대.")]
        [SerializeField] private Vector2 cryIntervalRange = new Vector2(5f, 12f);

        [Tooltip("판정 시점에 실제로 울 확률 (0~1). 낮출수록 더 가끔 웁니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float cryChance = 0.6f;

        [Tooltip("여러 멤이 동시에 울지 않도록 하는 전역 최소 간격 (초).")]
        [SerializeField] private float globalCryGap = 0.4f;

        [Header("재생 설정")]
        [Tooltip("울음소리 볼륨 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.7f;

        [Tooltip("재생마다 랜덤 적용할 피치 범위. 개체마다 목소리가 달라 보이는 효과.")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.15f);

        [Header("3D 감쇠")]
        [Tooltip("이 거리 안에서는 원본 볼륨 그대로 들립니다.")]
        [SerializeField] private float minDistance = 3f;

        [Tooltip("이 거리를 넘으면 들리지 않습니다. 이 밖에 있으면 재생 자체를 건너뜁니다.")]
        [SerializeField] private float maxDistance = 25f;

        // =================================================================
        // 내부 상태
        // =================================================================

        private AudioSource source;

        /// <summary>다음 울음 판정까지 남은 시간 (초). 음수면 미예약.</summary>
        private float cryTimer = -1f;

        /// <summary>배회 중이라 울음 판정이 활성인지 여부.</summary>
        private bool crying;

        /// <summary>이 개체의 모든 효과음 음소거 여부. 영지 소환 멤에 사용합니다.</summary>
        private bool muted;

        /// <summary>마지막으로 어떤 멤이든 운 시각 (전역 공유).</summary>
        private static float lastGlobalCryTime = -999f;

        /// <summary>거리 판정용 리스너 캐시 (씬에 보통 하나).</summary>
        private static AudioListener cachedListener;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();

            // 3D 사운드로 강제 세팅 — 멤 위치에서 들려야 하므로 spatialBlend=1.
            source.playOnAwake  = false;
            source.loop         = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode  = AudioRolloffMode.Linear;
            source.minDistance  = minDistance;
            source.maxDistance  = maxDistance;
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
        /// 울음소리를 즉시 1회 재생합니다.
        /// 다른 시스템에서 직접 울리고 싶을 때도 호출할 수 있습니다.
        /// </summary>
        public void PlayCry()
        {
            if (muted) return;
            if (cryClip == null || source == null) return;

            // 여러 멤이 겹쳐 우는 것 방지
            if (Time.time - lastGlobalCryTime < globalCryGap) return;

            // 들리지도 않을 거리면 보이스 낭비 없이 스킵
            if (!IsListenerInRange()) return;

            lastGlobalCryTime = Time.time;

            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.PlayOneShot(cryClip, volume);
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
            if (catchClip == null || source == null) return;

            source.pitch = 1f;
            source.PlayOneShot(catchClip, catchVolume);
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
            if (muted || attackClip == null || source == null) return;

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
            if (muted || attackClip == null || source == null) return;

            source.pitch = 1f;
            source.PlayOneShot(attackClip, attackVolume);
        }

        // =================================================================
        // 음소거 — 영지 소환 시 사용
        // =================================================================

        /// <summary>
        /// 이 개체의 모든 효과음을 켜고 끕니다.
        /// 영지(TerritoryWanderSpawner)에서 소환된 멤은 조용해야 하므로 true로 설정합니다.
        /// 풀에 반환되면 자동으로 해제되므로, 소환할 때마다 명시적으로 지정해야 합니다.
        /// </summary>
        public void SetMuted(bool value)
        {
            muted = value;
            if (muted && source != null) source.Stop();
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
            if (source != null) source.Stop();
        }

        // =================================================================
        // 내부 유틸
        // =================================================================

        /// <summary>다음 울음 판정까지의 시간을 랜덤 간격으로 예약합니다.</summary>
        private void ScheduleNextCry()
        {
            cryTimer = Random.Range(cryIntervalRange.x, cryIntervalRange.y);
        }

        /// <summary>리스너(플레이어 카메라)가 가청 범위 안에 있는지 검사합니다.</summary>
        private bool IsListenerInRange()
        {
            if (cachedListener == null)
                cachedListener = FindFirstObjectByType<AudioListener>();

            if (cachedListener == null) return true; // 리스너를 못 찾으면 판정 생략

            float sqrDistance = (cachedListener.transform.position - transform.position).sqrMagnitude;
            return sqrDistance <= maxDistance * maxDistance;
        }
    }
}
