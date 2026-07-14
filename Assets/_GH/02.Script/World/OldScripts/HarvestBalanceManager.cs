using HDY;
using UnityEngine;

public class HarvestBalanceManager : MonoBehaviour
{
    public static HarvestBalanceManager Instance { get; private set; }

    [SerializeField] private GradeBonusTable gradeBonusTable;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 보너스 데미지 계산
    /// </summary>
    /// <param name="grade"></param>
    /// <param name="baseDamage"></param>
    /// <returns></returns>
    public int CalculateDamage(CommonClass grade,int baseDamage)
    {
        if (gradeBonusTable == null)
        {
            return Mathf.Max(1, baseDamage);
        }

        return gradeBonusTable.CalculateDamage(
            grade,
            baseDamage);
    }
    /// <summary>
    /// 보너스 드랍 수량 계산
    /// </summary>
    /// <param name="grade"></param>
    /// <param name="baseDropCount"></param>
    /// <returns></returns>
    public int CalculateDropCount(
        CommonClass grade,
        int baseDropCount)
    {
        if (gradeBonusTable == null)
        {
            return Mathf.Max(0, baseDropCount);
        }

        return gradeBonusTable.CalculateDropCount(
            grade,
            baseDropCount);
    }
}