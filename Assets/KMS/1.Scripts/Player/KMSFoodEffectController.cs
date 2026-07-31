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
    /// KMS 플레이어의 음식 포만감 구간과 구간에 묶인 효과를 관리한다.
    /// 일반 음식은 오른쪽 normalSatiety에 합쳐지고, 효과 음식은 최신 순서로 왼쪽에 삽입한다.
    /// 최대 허기 초과와 실제 허기 소모는 오른쪽(일반 허기, 오래된 효과)부터 처리한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class KMSFoodEffectController : MonoBehaviour
    {
        private const float Epsilon = 0.001f;

        [SerializeField] private PlayerStats stats;
        [SerializeField, Min(0f)] private float normalSatiety;
        [SerializeField] private List<KMSFoodEffectSegment> effectSegments =
            new List<KMSFoodEffectSegment>();

        private bool initialized;

        public float NormalSatiety => normalSatiety;
        public IReadOnlyList<KMSFoodEffectSegment> EffectSegments => effectSegments;
        public event Action Changed;

        public float MoveSpeedMultiplier
        {
            get
            {
                float percent = GetActiveEffectTotal(EffectType.Speed);
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

        public void InitializeAsNormal(float currentHunger, bool notify = true)
        {
            normalSatiety = Mathf.Max(0f, currentHunger);
            effectSegments.Clear();
            initialized = true;
            if (notify) Changed?.Invoke();
        }

        public void RegisterNormalRestoration(float restoredAmount)
        {
            EnsureInitialized();
            if (restoredAmount <= Epsilon) return;

            normalSatiety += restoredAmount;
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
            if (gameplayEffects.Count == 0)
            {
                float restored = Mathf.Min(satietyAmount, maxHunger - resultingHunger);
                if (restored <= Epsilon) return false;

                normalSatiety += restored;
                resultingHunger += restored;
            }
            else
            {
                float insertedSatiety = Mathf.Min(satietyAmount, maxHunger);
                if (insertedSatiety <= Epsilon) return false;

                effectSegments.Insert(0, new KMSFoodEffectSegment(
                    item.Item_ID,
                    insertedSatiety,
                    gameplayEffects));

                resultingHunger = Mathf.Min(maxHunger, resultingHunger + insertedSatiety);
                TrimFromRight(GetTrackedSatiety() - resultingHunger);
            }

            Changed?.Invoke();
            return true;
        }

        public void ConsumeSatiety(float consumedAmount)
        {
            EnsureInitialized();
            float remaining = Mathf.Max(0f, consumedAmount);
            if (remaining <= Epsilon) return;

            float normalConsumed = Mathf.Min(normalSatiety, remaining);
            normalSatiety -= normalConsumed;
            remaining -= normalConsumed;

            for (int i = effectSegments.Count - 1; i >= 0 && remaining > Epsilon; i--)
            {
                KMSFoodEffectSegment segment = effectSegments[i];
                if (segment == null)
                {
                    effectSegments.RemoveAt(i);
                    continue;
                }

                remaining -= segment.Consume(remaining);
                if (segment.RemainingSatiety <= Epsilon)
                {
                    effectSegments.RemoveAt(i);
                }
            }

            Changed?.Invoke();
        }

        public float GetActiveEffectTotal(EffectType effectType)
        {
            float total = 0f;
            for (int i = 0; i < effectSegments.Count; i++)
            {
                KMSFoodEffectSegment segment = effectSegments[i];
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
                layoutVersion = 2,
                normalSatiety = normalSatiety,
                segments = new KMSFoodEffectSegmentSaveData[effectSegments.Count]
            };

            for (int i = 0; i < effectSegments.Count; i++)
            {
                KMSFoodEffectSegment segment = effectSegments[i];
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
            effectSegments.Clear();

            if (data == null)
            {
                InitializeAsNormal(currentHunger);
                return;
            }

            normalSatiety = Mathf.Max(0f, data.normalSatiety);

            if (data.segments != null)
            {
                bool legacyOrder = data.layoutVersion < 2;
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

                    if (restoredEffects.Count == 0)
                    {
                        normalSatiety += savedSegment.remainingSatiety;
                    }
                    else
                    {
                        effectSegments.Add(new KMSFoodEffectSegment(
                            savedSegment.itemId,
                            savedSegment.remainingSatiety,
                            restoredEffects));
                    }
                }
            }

            float clampedCurrentHunger = Mathf.Max(0f, currentHunger);
            float tracked = GetTrackedSatiety();
            if (tracked > clampedCurrentHunger)
            {
                TrimFromRight(tracked - clampedCurrentHunger);
            }
            else if (tracked < clampedCurrentHunger)
            {
                normalSatiety += clampedCurrentHunger - tracked;
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
            float total = normalSatiety;
            for (int i = 0; i < effectSegments.Count; i++)
            {
                KMSFoodEffectSegment segment = effectSegments[i];
                if (segment != null) total += Mathf.Max(0f, segment.RemainingSatiety);
            }

            return total;
        }

        private void TrimFromRight(float amount)
        {
            float remaining = Mathf.Max(0f, amount);
            if (remaining <= Epsilon) return;

            float normalTrimmed = Mathf.Min(normalSatiety, remaining);
            normalSatiety -= normalTrimmed;
            remaining -= normalTrimmed;

            for (int i = effectSegments.Count - 1; i >= 0 && remaining > Epsilon; i--)
            {
                KMSFoodEffectSegment segment = effectSegments[i];
                if (segment == null)
                {
                    effectSegments.RemoveAt(i);
                    continue;
                }

                remaining -= segment.Consume(remaining);
                if (segment.RemainingSatiety <= Epsilon) effectSegments.RemoveAt(i);
            }
        }

        private static bool HasGameplayEffects(ItemData item)
        {
            if (item == null || item.EatEffects == null) return false;

            for (int i = 0; i < item.EatEffects.Count; i++)
            {
                ItemEffect effect = item.EatEffects[i];
                if (effect != null
                    && effect.Effect != EffectType.Satiety
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
