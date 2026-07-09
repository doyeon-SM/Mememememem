using KGH.Data;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Grade Bonus Table")]
public class GradeBonusTable : ScriptableObject
{
    public GradeBonus[] bonuses;

    public GradeBonus GetBonus(ItemGrade grade)
    {
        return System.Array.Find(bonuses, x => x.grade == grade);
    }
}
