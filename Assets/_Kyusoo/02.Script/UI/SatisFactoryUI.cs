using TMPro;
using UnityEngine;

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

        if (satisfactionText != null)
        {
            satisfactionText.text = totalSatisfaction.ToString();
        }

    }
}
