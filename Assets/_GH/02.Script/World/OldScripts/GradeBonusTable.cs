using KGH.Data;
using UnityEngine;
using System;
using HDY;

[Serializable]
public struct GradeBonus
{
    public CommonClass grade;

    [Header("채집 보너스")]
    [Min(0)]
    public int damageBonus;

    [Min(0)]
    public int dropBonus;
}

[CreateAssetMenu(
    fileName = "GradeBonusTable",
    menuName = "GH/Data/Grade Bonus Table")]
public class GradeBonusTable : ScriptableObject
{
    [SerializeField]
    private GradeBonus[] bonuses = Array.Empty<GradeBonus>();

    public bool TryGetBonus(CommonClass grade, out GradeBonus result)
    {
        for (int i = 0; i < bonuses.Length; i++)
        {
            if (bonuses[i].grade != grade)
                continue;

            result = bonuses[i];
            return true;
        }

        result = default;
        return false;
    }

    public int CalculateDamage(CommonClass grade, int baseDamage)
    {
        int damage = Mathf.Max(0, baseDamage);

        if (TryGetBonus(grade, out GradeBonus bonus))
        {
            damage += bonus.damageBonus;
        }

        return Mathf.Max(1, damage);
    }

    public int CalculateDropCount(CommonClass grade, int baseDropCount)
    {
        int dropCount = Mathf.Max(0, baseDropCount);

        if (TryGetBonus(grade, out GradeBonus bonus))
        {
            dropCount += bonus.dropBonus;
        }

        return Mathf.Max(0, dropCount);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (bonuses == null)
        {
            bonuses = Array.Empty<GradeBonus>();
            return;
        }

        for (int i = 0; i < bonuses.Length; i++)
        {
            for (int j = i + 1; j < bonuses.Length; j++)
            {
                if (bonuses[i].grade == bonuses[j].grade)
                {
                    Debug.LogWarning(
                        $"[GradeBonusTable] 중복 등급이 등록되었습니다: " +
                        $"{bonuses[i].grade}",
                        this);
                }
            }
        }
    }
#endif
}
