using System;
using HDY.Item;
using UnityEngine;

[Serializable]
public struct DropCountChance
{
    [Min(1)] public int dropCount;
    [Min(0f)] public float weight;
}

[Serializable]
public struct ToolDropRule
{
    [Tooltip("같은 CommonClass 등급의 도구도 구분할 수 있도록 ItemData SO를 직접 지정합니다.")]
    public ItemData tool;

    [Tooltip("드롭 개수별 가중치입니다. 1개/2개를 각각 50으로 설정하면 50%/50%입니다.")]
    public DropCountChance[] chances;
}

/// <summary>
/// 동일 등급이어도 성능이 다른 도구를 ItemData 직접 참조로 구분해 드롭 개수 확률을 정의합니다.
/// </summary>
[CreateAssetMenu(
    fileName = "ToolDropTable",
    menuName = "GH/Data/Tool Drop Table")]
public class ToolDropTable : ScriptableObject
{
    private const int BaseDropCount = 1;

    [SerializeField] private ToolDropRule[] rules = Array.Empty<ToolDropRule>();

    /// <summary>도구에 등록된 가중치 중 하나를 추첨해 최소 1개의 드롭 개수를 반환합니다.</summary>
    public int RollDropCount(ItemData tool)
    {
        if (!TryGetRule(tool, out ToolDropRule rule) ||
            rule.chances == null ||
            rule.chances.Length == 0)
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

    private bool TryGetRule(ItemData tool, out ToolDropRule result)
    {
        if (tool != null)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i].tool != tool)
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
            if (rules[i].tool == null)
            {
                continue;
            }

            for (int j = i + 1; j < rules.Length; j++)
            {
                if (rules[i].tool == rules[j].tool)
                {
                    Debug.LogWarning(
                        $"[ToolDropTable] 중복 도구 규칙이 등록되었습니다: {rules[i].tool.name}",
                        this);
                }
            }
        }
    }
#endif
}
