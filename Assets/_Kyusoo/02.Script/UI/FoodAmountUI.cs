using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FoodAmountUI : MonoBehaviour
{
    [Header("상시 UI 요소 바인딩")]
    [SerializeField] private Slider foodSlider;
    [SerializeField] private TextMeshProUGUI foodAmountText;

    private bool isSubscribed = false;

    private void Start()
    {
        if (ConsumeFoodSystem.Instance != null)
        {
            RefreshUI(ConsumeFoodSystem.Instance.CurrentSatiety, ConsumeFoodSystem.Instance.MaxSatiety);
        }

        if (foodAmountText != null)
        {
            foodAmountText.text = $"0";
        }
    }

    private void OnEnable()
    {
        RegisterEvent();
    }

    private void OnDisable()
    {
        UnregisterEvent();
    }

    private void Update()
    {
        if (ConsumeFoodSystem.Instance != null && !isSubscribed)
        {
            RegisterEvent();
        }
    }

    private void RegisterEvent()
    {
        if (isSubscribed || ConsumeFoodSystem.Instance == null) return;

        ConsumeFoodSystem.Instance.OnFoodAmountChanged += RefreshUI;
        isSubscribed = true;

        RefreshUI(ConsumeFoodSystem.Instance.CurrentSatiety, ConsumeFoodSystem.Instance.MaxSatiety);
    }

    private void UnregisterEvent()
    {
        if (!isSubscribed || ConsumeFoodSystem.Instance == null) return;

        ConsumeFoodSystem.Instance.OnFoodAmountChanged -= RefreshUI;
        isSubscribed = false;
    }

    public void RefreshUI(int currentSatiety, int maxSatiety)
    {
        if (foodAmountText != null)
        {
            foodAmountText.text = $"{currentSatiety}";
        }

        if (foodSlider != null)
        {
            foodSlider.maxValue = maxSatiety;
            foodSlider.value = currentSatiety;
        }
    }
}