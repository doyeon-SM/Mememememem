using TMPro;
using UnityEngine;
using HDY.Territory;

public class SatisFactoryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI satisfactionText;

    private void Start()
    {
        RecalculateSatisfaction();
    }

    public void RecalculateSatisfaction()
    {
        int totalSatisfaction = 0;
        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            totalSatisfaction = gridManager.GetTotalSatisfactionFromGrid();
        }

        TerritoryData territoryData = TerritoryData.Resolve(null);
        if (territoryData != null)
        {
            RecordManager.Instance?.SetPrivateFieldSafely(territoryData, "satisfaction", totalSatisfaction);
        }

        if (satisfactionText != null)
        {
            satisfactionText.text = totalSatisfaction.ToString();
        }
    }
}