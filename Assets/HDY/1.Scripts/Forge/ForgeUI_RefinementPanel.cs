using System;
using HDY.Item;
using HDY.Inventory;
using KMS.InventoryDuped;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Forge
{
    /// <summary>
    /// 대장간 UI의 연마 탭 전용 패널.
    ///
    /// [하단 목록은 ForgeUI 공용] 이 패널은 자체 목록을 갖지 않는다 - 하단 목록(4개 탭 공용 스크롤)은
    /// ForgeUI가 스캔·표시를 전담하고, 사용자가 그 목록에서 클릭한 도구를 <see cref="HandleToolSelected"/>로
    /// 넘겨받기만 한다. (과거에는 이 패널도 자체적으로 같은 목록 Content에 슬롯을 만들어서 ForgeUI/다른
    /// 패널과 슬롯이 섞여 보이거나, 탭이 꺼지면 안 보이는 문제가 있었다 - 목록 소유권을 ForgeUI로
    /// 단일화해서 해결.)
    ///
    /// [실행 후 하단 목록 갱신] 연마 실행은 이 패널이 직접 ForgeManager를 호출하기 때문에, 하단 목록을
    /// 들고 있는 ForgeUI는 실행 시점을 알 방법이 없다. 그래서 실행 후 <see cref="RefinementExecuted"/>
    /// 이벤트를 쏴서 ForgeUI가 자기 목록을 다시 그리게 한다.
    ///
    /// [잠금] lockToggle이 켜진 칸은 이번 연마 시도에서 보호되어 바뀌지 않는다. 잠금 상태는 저장되지 않고
    /// 이 패널 세션 안에서만 유지되며, 도구를 다시 선택하거나(HandleToolSelected) 패널이 비활성화되면
    /// (OnDisable, 즉 탭 전환 시) 자동으로 초기화된다.
    /// </summary>
    public class ForgeUI_RefinementPanel : MonoBehaviour
    {
        [Serializable]
        public class SlotRowUI
        {
            [Tooltip("이 칸에 실제 슬롯 데이터가 있을 때만 활성화되는 루트")]
            public GameObject root;
            public TMP_Text infoText;
            public Toggle lockToggle;

            [Tooltip("잠금 토글 색상을 반영할 대상 (비워두면 색상 변경 없음 - 보통 토글의 배경 Image나 라벨 Text)")]
            public Graphic lockColorTarget;
        }

        [Header("가운데 - 선택된 도구")]
        [SerializeField] private ForgeToolSlotUI selectedSlotDisplay;
        [SerializeField] private GameObject selectedEmptyHint;

        [Header("연마칸 표시 (최대 5칸, 배열 인덱스 = 슬롯 인덱스)")]
        [SerializeField] private SlotRowUI[] slotRows = new SlotRowUI[5];

        [Header("잠금 토글 색상")]
        [SerializeField] private Color lockedColor = Color.black;
        [SerializeField] private Color unlockedColor = Color.white;

        [Header("비용 / 실행")]
        [SerializeField] private Image stoneIconImage;
        [SerializeField] private TMP_Text stoneCostText;
        [SerializeField] private TMP_Text goldCostText;
        [SerializeField] private Button executeButton;
        [SerializeField] private Color normalTextColor = Color.white;
        [SerializeField] private Color shortageTextColor = Color.red;

        [Header("참조")]
        [SerializeField] private ForgeManager forgeManager;
        [SerializeField] private ItemCatalogManager catalogManager;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private WarehouseInventory warehouseInventory;

        /// <summary>연마를 실제로 시도했을 때(재료/골드 충분 - 결과와 무관하게 항상 무언가 바뀜) 발생. ForgeUI가 하단 목록 갱신에 사용한다.</summary>
        public event Action RefinementExecuted;

        private ItemStack selectedStack;
        private readonly bool[] lockedSlots = new bool[5];

        private void Awake()
        {
            if (forgeManager == null) forgeManager = ForgeManager.Instance;
            catalogManager = ItemCatalogManager.Resolve(catalogManager);

            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();

            if (executeButton != null) executeButton.onClick.AddListener(HandleExecuteClicked);

            for (int i = 0; i < slotRows.Length; i++)
            {
                int capturedIndex = i; // 클로저 캡처 방지용 지역 변수
                if (slotRows[i]?.lockToggle != null)
                {
                    slotRows[i].lockToggle.onValueChanged.AddListener(value => HandleLockToggleChanged(capturedIndex, value));
                }
            }
        }

        private void OnEnable()
        {
            SubscribeInventoryEvents(true);
            ClearLockState();
            RefreshSelection();
        }

        private void OnDisable()
        {
            SubscribeInventoryEvents(false);
        }

        /// <summary>ForgeUI가 모든 슬롯에 동일한 툴팁 UI 인스턴스를 동기화할 때 호출한다.</summary>
        public void SetTooltipUI(ItemTooltipUI tooltipUI)
        {
            selectedSlotDisplay?.SetTooltipUI(tooltipUI);
        }

        private void SubscribeInventoryEvents(bool subscribe)
        {
            // 목록 갱신은 ForgeUI가 전담하므로, 여기서는 재료(연마석) 보유량 등 비용 미리보기 갱신용으로만 구독한다.
            if (playerInventory != null)
            {
                if (subscribe) playerInventory.OnInventoryChanged += HandleContainersChanged;
                else playerInventory.OnInventoryChanged -= HandleContainersChanged;
            }

            if (warehouseInventory != null)
            {
                if (subscribe) warehouseInventory.OnStorageChanged += HandleContainersChanged;
                else warehouseInventory.OnStorageChanged -= HandleContainersChanged;
            }
        }

        private void HandleContainersChanged()
        {
            RefreshSelection();
        }

        private void ClearLockState()
        {
            for (int i = 0; i < lockedSlots.Length; i++) lockedSlots[i] = false;
        }

        /// <summary>ForgeUI 하단 공용 목록에서 도구가 클릭되면 호출된다.</summary>
        public void HandleToolSelected(ItemStack stack)
        {
            selectedStack = stack;
            ClearLockState(); // 도구를 새로 선택하면 잠금은 초기화된다.
            RefreshSelection();
        }

        private void HandleLockToggleChanged(int index, bool isLocked)
        {
            if (index < 0 || index >= lockedSlots.Length) return;
            lockedSlots[index] = isLocked;
            ApplyLockColor(index, isLocked);
            RefreshCostPreview();
        }

        private void ApplyLockColor(int index, bool isLocked)
        {
            if (index < 0 || index >= slotRows.Length) return;
            var target = slotRows[index]?.lockColorTarget;
            if (target == null) return;

            target.color = isLocked ? lockedColor : unlockedColor;
        }

        private void RefreshSelection()
        {
            bool hasSelection = selectedStack != null && !selectedStack.IsEmpty;
            if (selectedEmptyHint != null) selectedEmptyHint.SetActive(!hasSelection);

            if (!hasSelection)
            {
                selectedSlotDisplay?.Clear();
                SetAllSlotRowsHidden();
                SetExecuteInteractable(false);
                if (stoneIconImage != null) stoneIconImage.enabled = false;
                if (stoneCostText != null) stoneCostText.text = "-";
                if (goldCostText != null) goldCostText.text = "-";
                return;
            }

            var displayData = catalogManager != null ? catalogManager.FindItemData(selectedStack.itemId) : null;
            selectedSlotDisplay?.Bind(selectedStack, displayData);

            if (forgeManager != null && forgeManager.TryPeekRefinementSlots(selectedStack, out var slots))
            {
                BindSlotRows(slots);
            }
            else
            {
                SetAllSlotRowsHidden();
            }

            RefreshCostPreview();
        }

        private void BindSlotRows(ForgeRefinementSlotData[] slots)
        {
            for (int i = 0; i < slotRows.Length; i++)
            {
                var row = slotRows[i];
                if (row == null) continue;

                bool hasSlot = slots != null && i < slots.Length && slots[i] != null;
                if (row.root != null) row.root.SetActive(hasSlot);
                if (!hasSlot) continue;

                var slotData = slots[i];

                if (row.infoText != null)
                {
                    row.infoText.text = $"{slotData.Grade} / {slotData.DisplayName}+{slotData.Value:0.#}";
                }

                if (row.lockToggle != null)
                {
                    row.lockToggle.SetIsOnWithoutNotify(lockedSlots[i]);
                }

                ApplyLockColor(i, lockedSlots[i]);
            }
        }

        private void SetAllSlotRowsHidden()
        {
            foreach (var row in slotRows)
            {
                if (row?.root != null) row.root.SetActive(false);
            }
        }

        private void RefreshCostPreview()
        {
            bool hasSelection = selectedStack != null && !selectedStack.IsEmpty;
            if (!hasSelection || forgeManager == null)
            {
                SetExecuteInteractable(false);
                return;
            }

            var preview = forgeManager.GetRefinementPreview(selectedStack, lockedSlots);

            if (stoneIconImage != null)
            {
                var stoneData = !string.IsNullOrEmpty(preview.MaterialItemId) && catalogManager != null
                    ? catalogManager.FindItemData(preview.MaterialItemId)
                    : null;

                stoneIconImage.sprite = stoneData != null ? stoneData.ItemIcon : null;
                stoneIconImage.enabled = stoneData != null && stoneData.ItemIcon != null;
            }

            bool stoneShortage = preview.MaterialOwned < preview.MaterialCost;
            if (stoneCostText != null)
            {
                stoneCostText.text = $"{preview.MaterialOwned} / {preview.MaterialCost}";
                stoneCostText.color = stoneShortage ? shortageTextColor : normalTextColor;
            }

            bool goldShortage = preview.GoldOwned < preview.GoldCost;
            if (goldCostText != null)
            {
                goldCostText.text = $"{preview.GoldOwned} / {preview.GoldCost}";
                goldCostText.color = goldShortage ? shortageTextColor : normalTextColor;
            }

            bool canExecute = preview.BlockReason == RefinementFailReason.None && !stoneShortage && !goldShortage;
            SetExecuteInteractable(canExecute);
        }

        private void SetExecuteInteractable(bool value)
        {
            if (executeButton != null) executeButton.interactable = value;
        }

        private void HandleExecuteClicked()
        {
            if (selectedStack == null || selectedStack.IsEmpty || forgeManager == null) return;

            var outcome = forgeManager.TryRefine(selectedStack, lockedSlots);

            RefreshSelection();

            if (outcome.Attempted)
            {
                RefinementExecuted?.Invoke();
            }
        }
    }
}
