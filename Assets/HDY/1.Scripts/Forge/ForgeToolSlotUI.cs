using System;
using HDY.Item;
using KMS.InventoryDuped;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Forge
{
    /// <summary>
    /// 대장간 하단 목록/가운데 선택 슬롯에 공용으로 쓰는 슬롯 UI.
    /// 아이콘과 강화 표시(+N)를 보여주고 클릭을 알린다. 실제 데이터 이동은 없고, 표시 + 클릭 전달만 담당한다.
    ///
    /// [툴팁] 인벤토리와 동일한 툴팁을 쓰기 위해 KMS 소유 ItemTooltipTriggerUI를 그대로 재사용한다 -
    /// 마우스 호버 감지/표시/위치추적 로직은 전부 그 컴포넌트가 처리하고, 여기서는 Bind/Clear 시점에
    /// SetItem만 호출해서 "지금 이 슬롯이 어떤 아이템을 보여주고 있는지"만 알려준다.
    /// (같은 프리팹에 ItemTooltipTriggerUI 컴포넌트를 추가하고 인스펙터에서 이 필드에 연결하면 된다.)
    /// </summary>
    public class ForgeToolSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text levelBadgeText;
        [SerializeField] private Button clickArea;

        [Header("툴팁 (선택 - 비워두면 툴팁 없이 동작)")]
        [SerializeField] private ItemTooltipTriggerUI tooltipTrigger;

        /// <summary>이 슬롯이 클릭되었을 때 발생. ForgeUI가 구독해서 선택 처리를 한다.</summary>
        public event Action<ForgeToolSlotUI> Clicked;

        /// <summary>이 슬롯 UI가 현재 표시하고 있는 실제 아이템 데이터(원본 참조, 창고/인벤토리에 그대로 있음).</summary>
        public ItemStack BoundStack { get; private set; }

        private void Awake()
        {
            if (clickArea != null)
            {
                clickArea.onClick.AddListener(() => Clicked?.Invoke(this));
            }
        }

        /// <summary>슬롯에 아이템을 표시한다. displayData는 ItemCatalogManager.FindItemData(stack.itemId) 결과를 그대로 넘기면 된다.</summary>
        public void Bind(ItemStack stack, ItemData displayData)
        {
            BoundStack = stack;

            if (iconImage != null)
            {
                iconImage.sprite = displayData != null ? displayData.ItemIcon : null;
                iconImage.enabled = displayData != null && displayData.ItemIcon != null;
            }

            if (levelBadgeText != null)
            {
                levelBadgeText.text = ExtractLevelSuffix(displayData);
            }

            if (tooltipTrigger != null)
            {
                tooltipTrigger.SetItem(displayData);
            }
        }

        /// <summary>빈 슬롯으로 표시한다(가운데 선택 슬롯이 비었을 때 등).</summary>
        public void Clear()
        {
            BoundStack = null;

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (levelBadgeText != null)
            {
                levelBadgeText.text = string.Empty;
            }

            if (tooltipTrigger != null)
            {
                tooltipTrigger.SetItem(null);
            }
        }

        /// <summary>ItemName 끝에 붙는 "+N" 강화 표시만 뽑아서 배지 텍스트로 쓴다(ForgeInstanceItemDataProvider가 붙여줌).</summary>
        private static string ExtractLevelSuffix(ItemData data)
        {
            if (data == null || string.IsNullOrEmpty(data.ItemName)) return string.Empty;

            int plusIndex = data.ItemName.LastIndexOf('+');
            return plusIndex >= 0 ? data.ItemName.Substring(plusIndex) : string.Empty;
        }
    }
}
