using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Item;

public class RecipeSlotUI : MonoBehaviour
{
    [Header("슬롯 내부에 바인딩될 UI 컴포넌트")]
    [SerializeField] private Image itemImage; 
    [SerializeField] private TextMeshProUGUI itemName; 
    [SerializeField] private Button clickBtn;

    /// <summary>
    /// 찾은 ItemData SO 정보와 함께 클릭 이벤트를 연결
    /// </summary>
    public void SetupSlot(ItemData data, System.Action onSelectedCallback)
    {
        if (data == null) return;

        if (itemImage != null) itemImage.sprite = data.ItemIcon;
        if (itemName != null) itemName.text = data.ItemName;

        if (clickBtn != null)
        {
            clickBtn.onClick.RemoveAllListeners();
            clickBtn.onClick.AddListener(() => onSelectedCallback?.Invoke());
        }
    }
}