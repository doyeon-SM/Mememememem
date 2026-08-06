using System;
using HDY.Forge;
using HDY.Item;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public struct DropCountChance
{
    [Min(1)] public int dropCount;
    [Min(0f)] public float weight;
}

[Serializable]
public struct ToolDropRule
{
    [Tooltip("ItemCatalogManager에 등록된 도구의 기본 Item Id를 입력합니다. 예: tool_axe")]
    public string toolItemId;

    // 기존 ItemData 직접 참조를 Item Id로 자동 이전하기 위한 호환 필드입니다.
    [FormerlySerializedAs("tool")]
    [SerializeField, HideInInspector] private ItemData legacyTool;

    [Tooltip("드롭 개수별 가중치입니다. 1개/2개를 각각 50으로 설정하면 50%/50%입니다.")]
    public DropCountChance[] chances;

    public string GetToolItemId()
    {
        if (!string.IsNullOrWhiteSpace(toolItemId))
        {
            return toolItemId;
        }

        return legacyTool != null ? legacyTool.Item_ID : string.Empty;
    }

#if UNITY_EDITOR
    public void MigrateLegacyToolReference()
    {
        if (string.IsNullOrWhiteSpace(toolItemId) && legacyTool != null)
        {
            toolItemId = legacyTool.Item_ID;
            legacyTool = null;
        }
    }
#endif
}

/// <summary>
/// 도구의 기본 Item Id를 기준으로 드롭 개수 확률을 정의합니다.
/// 강화 도구의 합성 ID도 자동으로 기본 Item Id 규칙을 사용합니다.
/// </summary>
[CreateAssetMenu(
    fileName = "ToolDropTable",
    menuName = "GH/Data/Tool Drop Table")]
public class ToolDropTable : ScriptableObject
{
    private const int BaseDropCount = 1;

    [SerializeField] private ToolDropRule[] rules = Array.Empty<ToolDropRule>();

    /// <summary>도구 Item Id에 등록된 가중치 중 하나를 추첨해 최소 1개의 드롭 개수를 반환합니다.</summary>
    public int RollDropCount(string toolItemId)
    {
        if (!TryGetRule(toolItemId, out ToolDropRule rule)
            || rule.chances == null
            || rule.chances.Length == 0)
        {
            return BaseDropCount;
        }

        float totalWeight = 0f;
        for (int i = 0; i < rule.chances.Length; i++)
        {
            totalWeight += Mathf.Max(0f, rule.chances[i].weight);
        }

        if (totalWeight <= 0f)
        {
            return BaseDropCount;
        }

        float roll = UnityEngine.Random.value * totalWeight;
        float accumulatedWeight = 0f;

        for (int i = 0; i < rule.chances.Length; i++)
        {
            DropCountChance chance = rule.chances[i];
            accumulatedWeight += Mathf.Max(0f, chance.weight);

            if (roll < accumulatedWeight)
            {
                return Mathf.Max(BaseDropCount, chance.dropCount);
            }
        }

        return BaseDropCount;
    }

    private bool TryGetRule(string toolItemId, out ToolDropRule result)
    {
        string normalizedToolItemId = NormalizeToolItemId(toolItemId);
        if (rules != null && !string.IsNullOrEmpty(normalizedToolItemId))
        {
            for (int i = 0; i < rules.Length; i++)
            {
                string ruleItemId = NormalizeToolItemId(rules[i].GetToolItemId());
                if (!string.Equals(ruleItemId, normalizedToolItemId, StringComparison.Ordinal))
                {
                    continue;
                }

                result = rules[i];
                return true;
            }
        }

        result = default;
        return false;
    }

    private static string NormalizeToolItemId(string toolItemId)
    {
        if (string.IsNullOrWhiteSpace(toolItemId))
        {
            return string.Empty;
        }

        string normalized = toolItemId.Trim();
        return ForgeInstanceRegistry.TryParseCompositeId(normalized, out string baseItemId, out _)
            ? baseItemId.Trim()
            : normalized;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (rules == null)
        {
            rules = Array.Empty<ToolDropRule>();
            return;
        }

        for (int i = 0; i < rules.Length; i++)
        {
            ToolDropRule rule = rules[i];
            rule.MigrateLegacyToolReference();
            rule.toolItemId = NormalizeToolItemId(rule.toolItemId);
            rules[i] = rule;

            if (string.IsNullOrEmpty(rule.toolItemId))
            {
                continue;
            }

            for (int j = i + 1; j < rules.Length; j++)
            {
                string comparedItemId = NormalizeToolItemId(rules[j].GetToolItemId());
                if (string.Equals(rule.toolItemId, comparedItemId, StringComparison.Ordinal))
                {
                    Debug.LogWarning(
                        $"[ToolDropTable] 중복 도구 Item Id 규칙이 등록되었습니다: {rule.toolItemId}",
                        this);
                }
            }
        }
    }
#endif
}
