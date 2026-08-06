using System;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.Forge
{
    /// <summary>
    /// 도구 티어(1~N단계) 전역 정의 하나. 등급/시작 데미지/레벨당 증가폭/강화석은
    /// 도구 종류(도끼/곡괭이/괭이)와 무관하게 티어 번호에만 종속된다.
    /// ItemClass는 참고용 메타데이터이며, 실제 표시되는 등급은 각 티어의 실제 ItemData 자산에
    /// 적힌 값이 우선이다(둘이 다르면 ItemData 쪽이 진실).
    /// </summary>
    [Serializable]
    public class ForgeTierEntry
    {
        [Tooltip("티어 번호 (1부터 시작, 이후 4~7단계 추가 시 이 리스트에 항목만 늘리면 됨)")]
        public int TierIndex;

        [Tooltip("참고용 등급. 티어 번호와 별개로 지정 가능 (예: 1~2단계=Rare, 3단계=Epic처럼 여러 티어가 같은 등급을 공유할 수 있음)")]
        public CommonClass ItemClass;

        [Tooltip("이 티어 0강 상태의 시작 데미지 (예: 1단계=1, 2단계=10, 3단계=20)")]
        public int BaseDamage;

        [Tooltip("강화 1레벨(1강~10강)당 데미지 증가폭 (예: 1단계=1, 2단계=1, 3단계=2)")]
        public int DamagePerEnhanceLevel;

        [Tooltip("이 티어에서 강화·승급 시 공통으로 소모되는 강화석의 Item_ID (예: 허름한=item_iron, 도구=item_diamond, 숙련자=item_temperedstone_dawn)")]
        public string TierMaterialItemId;
    }

    /// <summary>
    /// 전체 도구 티어(1~N단계) 테이블. 도구 종류(도끼/곡괭이/괭이) 공통으로 참조하는 단일 설정 자산.
    /// 4~7단계가 확정되는 대로 Tiers 리스트에 항목만 추가하면 되고 코드 수정은 필요 없다.
    /// </summary>
    [CreateAssetMenu(fileName = "ForgeTierData", menuName = "HDY/Forge/Forge Tier Data", order = 0)]
    public class ForgeTierData : ScriptableObject
    {
        [Header("티어 목록 (TierIndex 오름차순 권장)")]
        public List<ForgeTierEntry> Tiers = new List<ForgeTierEntry>();

        private Dictionary<int, ForgeTierEntry> tierLookup;

        private void OnEnable()
        {
            BuildLookup();
        }

        private void BuildLookup()
        {
            tierLookup = new Dictionary<int, ForgeTierEntry>();

            foreach (var tier in Tiers)
            {
                if (tier == null) continue;

                if (!tierLookup.ContainsKey(tier.TierIndex))
                {
                    tierLookup.Add(tier.TierIndex, tier);
                }
                else
                {
                    Debug.LogWarning($"[ForgeTierData] TierIndex가 중복되었습니다: {tier.TierIndex}");
                }
            }
        }

        /// <summary>TierIndex로 티어 정의를 찾는다. 없으면 null.</summary>
        public ForgeTierEntry GetTier(int tierIndex)
        {
            if (tierLookup == null) BuildLookup();
            return tierLookup.TryGetValue(tierIndex, out var entry) ? entry : null;
        }

        /// <summary>다음 티어(승급 대상)가 테이블에 정의되어 있는지 확인한다. 없으면 승급이 아직 불가능하다.</summary>
        public bool HasNextTier(int tierIndex)
        {
            return GetTier(tierIndex + 1) != null;
        }
    }
}
