using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KMS
{
    /// <summary>
    /// 플레이어 체력 자연회복을 담당한다. 두 채널이 독립적으로(동시에) 진행된다.
    ///
    /// 1) 배고픔 채널: HP가 최대가 아니고 배고픔이 충분하면 배고픔을 소비하고
    ///    "최대체력의 N%"를 일정 시간에 걸쳐 회복한 뒤, 완료 후 쿨타임을 갖고 다시 반복한다.
    /// 2) 음식 채널: 음식을 먹을 때마다(EatEffects의 Heal) "최대체력의 N%" 회복 작업을
    ///    큐에 넣는다. 이미 진행 중인 작업이 있으면 그 작업이 끝난 뒤 순서대로 이어서 진행한다.
    ///
    /// 두 채널 모두 tickInterval(기본 1초)마다 한 번씩 갱신되며, 실제 회복량은 그 순간의
    /// "부족한 체력(최대 - 현재)"을 넘지 않도록 클램프한다. 이미 최대체력이라 이번 틱에
    /// 회복할 게 없어도 작업 자체는 중단하지 않고 남은 시간만큼 계속 진행한다 - 그래야
    /// 도중에 체력이 깎이면(예: 멤에게 피격) 남은 시간 동안 다시 회복할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerHealthRegenController : MonoBehaviour
    {
        [Header("배고픔 회복")]
        [Tooltip("회복 1회 시작 시 소비하는 배고픔 수치.")]
        [SerializeField, Min(0f)] private float hungerRecoverCost = 5f;
        [Tooltip("1회 회복량 - 최대체력 대비 퍼센트(%).")]
        [SerializeField, Min(0f)] private float hungerRecoverPercent = 5f;
        [Tooltip("회복이 진행되는 시간(초).")]
        [SerializeField, Min(0.1f)] private float hungerRecoverDuration = 5f;
        [Tooltip("회복 완료 후 다음 회복 시작까지의 대기 시간(초).")]
        [SerializeField, Min(0f)] private float hungerRecoverCooldown = 5f;

        [Header("음식 회복")]
        [Tooltip("음식 하나(EatEffects의 Heal)가 회복되는 데 걸리는 시간(초). 여러 개를 먹으면 순서대로 이어서 진행된다.")]
        [SerializeField, Min(0.1f)] private float foodRecoverDuration = 2f;

        [Header("갱신 주기")]
        [Tooltip("체력 수치가 실제로 갱신되는 간격(초). 시각적 보간(트윈)과는 별개의 게임 로직 틱이다.")]
        [SerializeField, Min(0.1f)] private float tickInterval = 1f;

        private enum HungerChannelState { Idle, Healing, Cooldown }

        private PlayerStats stats;
        private Coroutine tickRoutine;

        private HungerChannelState hungerState = HungerChannelState.Idle;
        private float hungerRemainingHp;
        private int hungerRemainingTicks;
        private float hungerCooldownRemaining;

        private struct FoodHealJob
        {
            public float remainingHp;
            public int remainingTicks;
        }

        private readonly Queue<FoodHealJob> foodQueue = new Queue<FoodHealJob>();
        private FoodHealJob? activeFoodJob;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (tickRoutine == null) tickRoutine = StartCoroutine(TickLoop());
        }

        private void OnDisable()
        {
            if (tickRoutine != null)
            {
                StopCoroutine(tickRoutine);
                tickRoutine = null;
            }
        }

        /// <summary>음식을 먹어서 얻은 Heal 효과를 큐에 추가한다. healPercent는 최대체력 대비 %다.
        /// 이미 진행 중인 음식 회복 작업이 있으면 그 작업이 끝난 뒤 이어서 진행된다.</summary>
        public void EnqueueFoodHeal(float healPercent)
        {
            if (stats == null || healPercent <= 0f) return;

            float targetHp = stats.MaxHealth * (healPercent / 100f);
            int ticks = Mathf.Max(1, Mathf.RoundToInt(foodRecoverDuration / tickInterval));
            foodQueue.Enqueue(new FoodHealJob { remainingHp = targetHp, remainingTicks = ticks });
        }

        /// <summary>사망/부활 시 진행 중이던 모든 회복 상태를 초기화한다.</summary>
        public void ResetChannels()
        {
            hungerState = HungerChannelState.Idle;
            hungerRemainingHp = 0f;
            hungerRemainingTicks = 0;
            hungerCooldownRemaining = 0f;
            activeFoodJob = null;
            foodQueue.Clear();
        }

        private IEnumerator TickLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, tickInterval));
            while (true)
            {
                yield return wait;
                ProcessTick();
            }
        }

        private void ProcessTick()
        {
            if (stats == null || !stats.IsAlive) return;

            float desiredThisTick = 0f;

            // ---- 배고픔 채널 ----
            switch (hungerState)
            {
                case HungerChannelState.Healing:
                    desiredThisTick += ConsumeChannelTick(ref hungerRemainingHp, ref hungerRemainingTicks);
                    if (hungerRemainingTicks <= 0)
                    {
                        hungerState = HungerChannelState.Cooldown;
                        hungerCooldownRemaining = hungerRecoverCooldown;
                    }
                    break;

                case HungerChannelState.Cooldown:
                    hungerCooldownRemaining -= tickInterval;
                    if (hungerCooldownRemaining <= 0f)
                    {
                        hungerState = HungerChannelState.Idle;
                    }
                    break;

                case HungerChannelState.Idle:
                    TryStartHungerHealing();
                    break;
            }

            // ---- 음식 채널 ----
            if (!activeFoodJob.HasValue && foodQueue.Count > 0)
            {
                activeFoodJob = foodQueue.Dequeue();
            }

            if (activeFoodJob.HasValue)
            {
                FoodHealJob job = activeFoodJob.Value;
                desiredThisTick += ConsumeChannelTick(ref job.remainingHp, ref job.remainingTicks);
                activeFoodJob = job.remainingTicks > 0 ? job : (FoodHealJob?)null;
            }

            if (desiredThisTick <= 0f) return;

            float missingHealth = Mathf.Max(0f, stats.MaxHealth - stats.CurrentHealth);
            float actualHeal = Mathf.Min(desiredThisTick, missingHealth);
            if (actualHeal > 0f)
            {
                stats.Heal(actualHeal);
            }
        }

        /// <summary>남은 회복량을 남은 틱 수만큼 균등 분배해 이번 틱 몫을 계산하고 차감한다.
        /// 실제로 체력에 반영됐는지 여부와 무관하게(=최대체력이라 0을 회복했어도) 시간은 항상 이렇게
        /// 소모된다 - 그래야 정해진 시간(예: 5초/2초) 안에 작업이 끝난다.</summary>
        private static float ConsumeChannelTick(ref float remainingHp, ref int remainingTicks)
        {
            if (remainingTicks <= 0) return 0f;

            float share = remainingHp / remainingTicks;
            remainingHp -= share;
            remainingTicks -= 1;
            return share;
        }

        private void TryStartHungerHealing()
        {
            if (stats.CurrentHealth >= stats.MaxHealth) return;
            if (!stats.TryConsumeHungerExact(hungerRecoverCost)) return;

            hungerRemainingHp = stats.MaxHealth * (hungerRecoverPercent / 100f);
            hungerRemainingTicks = Mathf.Max(1, Mathf.RoundToInt(hungerRecoverDuration / tickInterval));
            hungerState = HungerChannelState.Healing;
        }
    }
}
