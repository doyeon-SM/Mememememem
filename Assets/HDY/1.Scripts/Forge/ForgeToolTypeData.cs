using System;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.Forge
{
    [Serializable]
    public class ForgeToolTierItemMapping
    {
        [Tooltip("ForgeTierData.Tiers의 TierIndex와 일치해야 함")]
        public int TierIndex;

        [Tooltip("이 티어에서 실제로 사용되는 ItemData의 Item_ID (예: tool_shabby_axe, tool_axe, tool_decent_axe)")]
        public string ItemId;
    }

    /// <summary>
    /// 도구 종류(도끼/곡괭이/괭이) 하나에 대한 대장간 설정.
    /// 몽둥이는 강화/승급이 모두 불가능하므로 이 자산을 만들지 않는다 - ForgeManager가 이 자산이 없는
    /// 아이템은 자동으로 "대장간 대상 아님"으로 처리하므로 ForgeUI 목록에서도 자연히 제외된다.
    /// </summary>
    [CreateAssetMenu(fileName = "ForgeToolType_", menuName = "HDY/Forge/Forge Tool Type Data", order = 1)]
    public class ForgeToolTypeData : ScriptableObject
    {
        [Header("도구 종류")]
        public ForgeToolType ToolType;

        [Header("가능 여부")]
        [Tooltip("도끼·곡괭이=true, 괭이=false")]
        public bool CanEnhance;

        [Tooltip("도끼·곡괭이·괭이 전부 true")]
        public bool CanPromote = true;

        [Header("데미지 계산")]
        [Tooltip("체크 해제 시 티어의 시작 데미지/증가폭 공식을 쓰지 않고, 그 티어 ItemData 자산에 적힌 Value를 그대로 사용한다. " +
                 "괭이처럼 승급해도 데미지가 고정인 도구는 이 값을 false로 둔다.")]
        public bool DamageScalesWithTier = true;

        [Header("티어별 실제 아이템 매핑 (티어가 늘어나면 항목만 추가)")]
        public List<ForgeToolTierItemMapping> TierItems = new List<ForgeToolTierItemMapping>();

        private Dictionary<int, string> tierItemLookup;

        private void OnEnable()
        {
            BuildLookup();
        }

        private void BuildLookup()
        {
            tierItemLookup = new Dictionary<int, string>();

            foreach (var mapping in TierItems)
            {
                if (mapping == null || string.IsNullOrEmpty(mapping.ItemId)) continue;

                if (!tierItemLookup.ContainsKey(mapping.TierIndex))
                {
                    tierItemLookup.Add(mapping.TierIndex, mapping.ItemId);
                }
                else
                {
                    Debug.LogWarning($"[ForgeToolTypeData:{ToolType}] TierIndex가 중복되었습니다: {mapping.TierIndex}");
                }
            }
        }

        /// <summary>이 도구 종류의 특정 티어에 해당하는 실제 Item_ID를 반환한다. 없으면 null.</summary>
        public string GetItemId(int tierIndex)
        {
            if (tierItemLookup == null) BuildLookup();
            return tierItemLookup.TryGetValue(tierIndex, out var id) ? id : null;
        }

        /// <summary>주어진 템플릿 Item_ID(합성 인스턴스 ID 아님)가 이 도구 종류의 몇 티어인지 역으로 찾는다. 없으면 -1.</summary>
        public int FindTierIndex(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return -1;
            if (tierItemLookup == null) BuildLookup();

            foreach (var pair in tierItemLookup)
            {
                if (pair.Value == itemId) return pair.Key;
            }

            return -1;
        }
    }
}
