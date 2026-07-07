using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CraftingSlotUI : MonoBehaviour
{
    [Header("슬롯 내부 UI 컴포넌트")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemName;
    [SerializeField] private Button clickBtn;

    private ProductItemData itemData;

    /// <summary>
    /// 슬롯하나당 아이템 이미지와 이름을 추가하는 함수
    /// </summary>
    public void Setup(ProductItemData data)
    {
        itemData = data;

        if (itemIcon != null) itemIcon.sprite = data.itemIcon;
        if (itemName != null) itemName.text = data.itemName;

        if (clickBtn != null)
        {
            clickBtn.onClick.RemoveAllListeners();
            clickBtn.onClick.AddListener(() =>
            {
                ProductionPanelUI.Instance.OnSelectItemProduce(itemData);
            });
        }
    }
}