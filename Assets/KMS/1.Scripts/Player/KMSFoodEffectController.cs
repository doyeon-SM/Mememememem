using System;
using System.Collections.Generic;
using HDY.Item;
using KMS.Persistence;
using UnityEngine;

namespace KMS
{
    [Serializable]
    public sealed class KMSActiveFoodEffect
    {
        [SerializeField] private EffectType effect;
        [SerializeField] private float value;

        public EffectType Effect => effect;
        public float Value => value;

        public KMSActiveFoodEffect(EffectType effect, float value)
        {
            this.effect = effect;
            this.value = value;
        }
    }

    [Serializable]
    public sealed class KMSFoodEffectSegment
    {
        [SerializeField] private string itemId;
        [SerializeField, Min(0f)] private float remainingSatiety;
        [SerializeField] private List<KMSActiveFoodEffect> effects = new List<KMSActiveFoodEffect>();

        public string ItemId => itemId;
        public float RemainingSatiety => remainingSatiety;
        public IReadOnlyList<KMSActiveFoodEffect> Effects => effects;

        public KMSFoodEffectSegment(
            string itemId,
            float remainingSatiety,
            List<KMSActiveFoodEffect> effects)
        {
            this.itemId = itemId;
            this.remainingSatiety = Mathf.Max(0f, remainingSatiety);
            this.effects = effects ?? new List<KMSActiveFoodEffect>();
        }

        public float GetEffectTotal(EffectType effectType)
        {
            float total = 0f;
            for (int i = 0; i < effects.Count; i++)
            {
                KMSActiveFoodEffect activeEffect = effects[i];
                if (activeEffect != null && activeEffect.Effect == effectType)
                {
                    total += activeEffect.Value;
                }
            }

            return total;
        }

        internal float Consume(float amount)
        {
            float consumed = Mathf.Min(remainingSatiety, Mathf.Max(0f, amount));
            remainingSatiety -= consumed;
            return consumed;
        }
    }

    /// <summary>
    /// KMS 플레이어의 음식 포만감을 "하나의 큐"로 관리한다.
    ///
    /// [HDY 요청 - KMS 승인 - 음식 큐 통합] 예전에는 효과 없는(포만감만 채우는) 음식은 별도의
    /// normalSatiety(단일 float)에 합쳐지고, 효과 있는 음식만 effectSegments 큐에 개별 구간으로
    /// 들어갔다. 문제는 normalSatiety가 "언제 먹었는지"와 무관하게 항상 가장 먼저 소비되도록 설계되어
    /// 있었다는 점이다 - 그래서 효과 음식을 먼저 먹고 포만감만 있는 음식을 나중에 먹으면, 나중에 먹은
    /// 포만감이 먼저 먹은 효과 음식보다 먼저 소비되어버리는 선입선출 위반이 있었다.
    ///
    /// 지금은 효과 유무와 상관없이 모든 음식을 하나의 segments 큐에 넣는다(효과 없는 음식은 그냥
    /// effects가 빈 리스트인 세그먼트). 새 음식은 항상 큐 맨 앞(index 0)에 삽입되고, 소비/오버플로우
    /// 트림은 항상 큐 뒤쪽(가장 오래된 세그먼트)부터 진행된다 - 실제 취식 순서가 곧 소비 순서가 된다.
    ///
    /// 단, 다음 비대칭 규칙은 기존 설계를 그대로 유지한다:
    /// - 효과 있는 음식은 배고픔이 가득 차 있어도 항상 전체 포만감만큼 큐에 삽입되고, 자리가 모자라면
    ///   가장 오래된 세그먼트부터 밀어내서(트림해서) 자리를 만든다 (효과를 반드시 온전히 적용하기 위함).
    /// - 효과 없는 음식은 남은 여유 공간만큼만 채워지고, 남는 포만감은 그냥 버려진다(다른 세그먼트를
    ///   밀어내지 않는다).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class KMSFoodEffectController : MonoBehaviour
    {
        private const float Epsilon = 0.001f;

        [SerializeField] private PlayerStats stats;
        [SerializeField] private List<KMSFoodEffectSegment> segments =
            new List<KMSFoodEffectSegment>();

        private bool initialized;

        /// <summary>큐 전체(효과 있는 음식 + 효과 없는 음식 모두). index 0 = 가장 최근에 먹은 음식(최신,
        /// 가장 늦게 소비됨), 마지막 index = 가장 먼저 먹은 음식(가장 오래됨, 가장 먼저 소비됨).</summary>
        public IReadOnlyList<KMSFoodEffectSegment> FoodSegments => segments;
        public event Action Changed;

        public float MoveSpeedMultiplier
        {
            get
            {
                float percent = GetActiveEffectTotal(EffectType.Speed);
                return Mathf.Max(0f, 1f + percent * 0.01f);
            }
        }

        /// <summary>
        /// 행운(Luck) 지속효과 총합을 채집량 배율로 환산한다. 예: +20 = 채집 개수 +20%.
        /// MoveSpeedMultiplier와 동일한 패턴 - 섭취한 음식의 포만감이 남아있는 동안만 유지된다.
        /// </summary>
        public float GatherAmountMultiplier
        {
            get
            {
                float percent = GetActiveEffectTotal(EffectType.Luck);
                return Mathf.Max(0f, 1f + percent * 0.01f);
            }
        }

        private void Reset()
        {
            stats = GetComponent<PlayerStats>();
        }

        private void Awake()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
        }

        /// <summary>큐 전체를 비우고, currentHunger가 있으면 효과 없는 단일 세그먼트 하나로 초기화한다.</summary>
        public void InitializeAsNormal(float currentHunger, bool notify = true)
        {
            segments.Clear();

            float clamped = Mathf.Max(0f, currentHunger);
            if (clamped > Epsilon)
            {
                segments.Add(new KMSFoodEffectSegment(null, clamped, new List<KMSActiveFoodEffect>()));
            }

            initialized = true;
            if (notify) Changed?.Invoke();
        }

        /// <summary>
        /// 음식을 먹는 경로가 아닌 다른 방식으로 회복된 포만감을 큐에 반영한다(예: 향후 추가될 수 있는
        /// 별도 회복 수단). 방금 생긴 포만감이므로 큐 맨 앞(최신, 가장 늦게 소비됨)에 넣는다.
        /// </summary>
        public void RegisterNormalRestoration(float restoredAmount)
        {
            EnsureInitialized();
            if (restoredAmount <= Epsilon) return;

            segments.Insert(0, new KMSFoodEffectSegment(null, restoredAmount, new List<KMSActiveFoodEffect>()));
            Changed?.Invoke();
        }

        public bool CanApplyFood(ItemData item, float satietyAmount, float maxHunger, float currentHunger)
        {
            if (item == null || satietyAmount <= Epsilon || maxHunger <= Epsilon) return false;
            return HasGameplayEffects(item) || currentHunger < maxHunger - Epsilon;
        }

        public bool ApplyFood(
            ItemData item,
            float satietyAmount,
            float maxHunger,
            float currentHunger,
            out float resultingHunger)
        {
            EnsureInitialized();
            resultingHunger = Mathf.Clamp(currentHunger, 0f, Mathf.Max(0f, maxHunger));
            if (!CanApplyFood(item, satietyAmount, maxHunger, currentHunger)) return false;

            List<KMSActiveFoodEffect> gameplayEffects = CopyGameplayEffects(item);
            bool hasEffects = gameplayEffects.Count > 0;

            // 효과 음식: 항상 전체 포만감만큼 삽입(자리 없으면 가장 오래된 세그먼트를 밀어냄).
            // 효과 없는 음식: 남은 여유 공간만큼만 삽입(다른 세그먼트를 밀어내지 않음).
            float insertedSatiety = hasEffects
                ? Mathf.Min(satietyAmount, maxHunger)
                : Mathf.Min(satietyAmount, maxHunger - resultingHunger);

            if (insertedSatiety <= Epsilon) return false;

            segments.Insert(0, new KMSFoodEffectSegment(item.Item_ID, insertedSatiety, gameplayEffects));
            resultingHunger = Mathf.Min(maxHunger, resultingHunger + insertedSatiety);
            TrimFromRight(GetTrackedSatiety() - resultingHunger);

            Changed?.Invoke();
            return true;
        }

        public void ConsumeSatiety(float consumedAmount)
        {
            EnsureInitialized();
            float remaining = Mathf.Max(0f, consumedAmount);
            if (remaining <= Epsilon) return;

            ConsumeFromTail(ref remaining);

            Changed?.Invoke();
        }

        public float GetActiveEffectTotal(EffectType effectType)
        {
            float total = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                KMSFoodEffectSegment segment = segments[i];
                if (segment != null && segment.RemainingSatiety > Epsilon)
                {
                    total += segment.GetEffectTotal(effectType);
                }
            }

            return total;
        }

        public KMSFoodEffectStateSaveData CaptureSaveData()
        {
            EnsureInitialized();
            var data = new KMSFoodEffectStateSaveData
            {
                layoutVersion = 3,
                normalSatiety = 0f,
                segments = new KMSFoodEffectSegmentSaveData[segments.Count]
            };

            for (int i = 0; i < segments.Count; i++)
            {
                KMSFoodEffectSegment segment = segments[i];
                var segmentData = new KMSFoodEffectSegmentSaveData
                {
                    itemId = segment != null ? segment.ItemId : string.Empty,
                    remainingSatiety = segment != null ? segment.RemainingSatiety : 0f,
                    effects = segment != null
                        ? new KMSFoodEffectValueSaveData[segment.Effects.Count]
                        : Array.Empty<KMSFoodEffectValueSaveData>()
                };

                if (segment != null)
                {
                    for (int effectIndex = 0; effectIndex < segment.Effects.Count; effectIndex++)
                    {
                        KMSActiveFoodEffect effect = segment.Effects[effectIndex];
                        segmentData.effects[effectIndex] = new KMSFoodEffectValueSaveData
                        {
                            effectType = effect != null ? (int)effect.Effect : 0,
                            value = effect != null ? effect.Value : 0f
                        };
                    }
                }

                data.segments[i] = segmentData;
            }

            return data;
        }

        public void RestoreSaveData(KMSFoodEffectStateSaveData data, float currentHunger)
        {
            segments.Clear();

            if (data == null)
            {
                InitializeAsNormal(currentHunger);
                return;
            }

            // layoutVersion < 2: 세그먼트 배열이 오래된 순으로 저장돼있어 뒤집어야 한다(예전 레이아웃).
            // layoutVersion < 3: 예전 모델은 효과 없는 포만감을 normalSatiety라는 별도 값으로 저장했고,
            // 그 값은 항상 가장 먼저 소비되는 우선순위였다. 새 모델에서 동일한 소비 우선순위를 갖는
            // 자리는 "가장 오래된(리스트 맨 뒤) 세그먼트"이므로, 복원 후 맨 뒤에 추가한다. 예전 데이터는
            // 일반 음식과 효과 음식 사이의 실제 취식 순서를 기록하지 않았으므로 완벽한 복원은 불가능하며,
            // 이 마이그레이션은 옛 소비 우선순위를 그대로 유지하는 최선의 근사치다.
            bool legacyOrder = data.layoutVersion < 2;
            bool legacyNormalBucket = data.layoutVersion < 3;

            if (data.segments != null)
            {
                for (int offset = 0; offset < data.segments.Length; offset++)
                {
                    int i = legacyOrder ? data.segments.Length - 1 - offset : offset;
                    KMSFoodEffectSegmentSaveData savedSegment = data.segments[i];
                    if (savedSegment == null || savedSegment.remainingSatiety <= Epsilon) continue;

                    var restoredEffects = new List<KMSActiveFoodEffect>();

                    if (savedSegment.effects != null)
                    {
                        for (int effectIndex = 0; effectIndex < savedSegment.effects.Length; effectIndex++)
                        {
                            KMSFoodEffectValueSaveData savedEffect = savedSegment.effects[effectIndex];
                            if (savedEffect == null || Mathf.Approximately(savedEffect.value, 0f)) continue;

                            restoredEffects.Add(new KMSActiveFoodEffect(
                                (EffectType)savedEffect.effectType,
                                savedEffect.value));
                        }
                    }

                    segments.Add(new KMSFoodEffectSegment(
                        savedSegment.itemId,
                        savedSegment.remainingSatiety,
                        restoredEffects));
                }
            }

            if (legacyNormalBucket && data.normalSatiety > Epsilon)
            {
                segments.Add(new KMSFoodEffectSegment(null, data.normalSatiety, new List<KMSActiveFoodEffect>()));
            }

            float clampedCurrentHunger = Mathf.Max(0f, currentHunger);
            float tracked = GetTrackedSatiety();
            if (tracked > clampedCurrentHunger)
            {
                TrimFromRight(tracked - clampedCurrentHunger);
            }
            else if (tracked < clampedCurrentHunger)
            {
                segments.Add(new KMSFoodEffectSegment(null, clampedCurrentHunger - tracked, new List<KMSActiveFoodEffect>()));
            }

            initialized = true;
            Changed?.Invoke();
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            float currentHunger = stats != null ? stats.CurrentHunger : 0f;
            InitializeAsNormal(currentHunger, false);
        }

        private float GetTrackedSatiety()
        {
            float total = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                KMSFoodEffectSegment segment = segments[i];
                if (segment != null) total += Mathf.Max(0f, segment.RemainingSatiety);
            }

            return total;
        }

        /// <summary>큐 뒤쪽(가장 오래된 세그먼트)부터 remaining만큼 소비한다. ConsumeSatiety(공개, 자연
        /// 소비)와 TrimFromRight(내부, 오버플로우 정리) 양쪽에서 공유하는 핵심 로직.</summary>
        private void ConsumeFromTail(ref float remaining)
        {
            for (int i = segments.Count - 1; i >= 0 && remaining > Epsilon; i--)
            {
                KMSFoodEffectSegment segment = segments[i];
                if (segment == null)
                {
                    segments.RemoveAt(i);
                    continue;
                }

                remaining -= segment.Consume(remaining);
                if (segment.RemainingSatiety <= Epsilon)
                {
                    segments.RemoveAt(i);
                }
            }
        }

        private void TrimFromRight(float amount)
        {
            float remaining = Mathf.Max(0f, amount);
            if (remaining <= Epsilon) return;

            ConsumeFromTail(ref remaining);
        }

        private static bool HasGameplayEffects(ItemData item)
        {
            if (item == null || item.EatEffects == null) return false;

            for (int i = 0; i < item.EatEffects.Count; i++)
            {
                ItemEffect effect = item.EatEffects[i];
                if (effect != null
                    && effect.Effect != EffectType.Satiety
                    && effect.Effect != EffectType.Heal
                    && !Mathf.Approximately(effect.Value, 0f))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<KMSActiveFoodEffect> CopyGameplayEffects(ItemData item)
        {
            var result = new List<KMSActiveFoodEffect>();
            if (item == null || item.EatEffects == null) return result;

            for (int i = 0; i < item.EatEffects.Count; i++)
            {
                ItemEffect effect = item.EatEffects[i];
                if (effect == null
                    || effect.Effect == EffectType.Satiety
                    || effect.Effect == EffectType.Heal
                    || Mathf.Approximately(effect.Value, 0f))
                {
                    continue;
                }

                result.Add(new KMSActiveFoodEffect(effect.Effect, effect.Value));
            }

            return result;
        }
    }
}
