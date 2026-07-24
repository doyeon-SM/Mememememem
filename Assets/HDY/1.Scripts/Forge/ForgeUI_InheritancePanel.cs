using System;
using HDY.Item;
using KMS.InventoryDuped;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Forge
{
    /// <summary>
    /// 대장간 UI의 전승 탭 전용 패널.
    ///
    /// [하단 목록은 ForgeUI 공용] 이 패널은 자체 목록을 갖지 않는다 - 하단 목록(4개 탭 공용 스크롤)은
    /// ForgeUI가 스캔·표시를 전담하고, 사용자가 그 목록에서 클릭한 도구를 <see cref="HandleToolSelected"/>로
    /// 넘겨받기만 한다.
    ///
    /// [실행 후 하단 목록 갱신] 전승 실행은 이 패널이 직접 ForgeManager를 호출하기 때문에, 하단 목록을
    /// 들고 있는 ForgeUI는 실행 시점을 알 방법이 없다. 특히 전승은 재료 도구가 소멸(itemId/amount가
    /// 비워짐)하므로 하단 목록에서 그 슬롯이 즉시 사라져야 한다 - 그래서 실행 후
    /// <see cref="InheritanceExecuted"/> 이벤트를 쏴서 ForgeUI가 자기 목록을 다시 그리게 한다.
    ///
    /// [선택 순서] 첫 클릭 = 재료 도구, 이후 클릭 = 전승받을 도구. 재료/대상이 모두 찬 상태에서 또
    /// 클릭하면 그 아이템을 새 재료로 다시 선택(처음부터 다시 시작)한다. 가운데 재료 슬롯을 클릭하면
    /// 선택을 전부 초기화하고, 대상 슬롯을 클릭하면 대상만 초기화한다.
    ///
    /// [결과 미리보기 - 중요] 전승은 연마칸만 재료 것으로 넘어가고, 강화 레벨/티어 등 대상 자체의
    /// 정체성은 그대로 유지된다(ForgeManager.TryInherit 참고). 그래서 미리보기도 두 아이템의 서로 다른
    /// 부분을 조합해서 보여줘야 한다:
    /// - 아이콘/강화(+N) 표시 = 대상(target) 기준 - 전승해도 안 바뀌는 부분
    /// - 마우스 호버 시 뜨는 연마 효과 툴팁 = 재료(material) 기준 - 전승으로 새로 넘어오는 부분
    /// 대상을 아직 선택하지 않았으면(재료만 선택된 상태) 비교 대상이 없으므로 재료 자체를 그대로 보여준다.
    /// </summary>
    public class ForgeUI_InheritancePanel : MonoBehaviour
    {
        [Header("가운데 - 재료 / 전승받을 도구")]
        [SerializeField] private ForgeToolSlotUI materialSlotDisplay;
        [SerializeField] private GameObject materialEmptyHint;
        [SerializeField] private ForgeToolSlotUI targetSlotDisplay;
        [SerializeField] private GameObject targetEmptyHint;

        [Header("전승 결과 미리보기 (아이콘/강화=대상 기준, 연마 효과 툴팁=재료 기준)")]
        [Tooltip("ForgeSlotUI_Prefab 인스턴스를 배치하고 연결하면 된다. 클릭 이벤트는 사용하지 않고 표시+툴팁 용도로만 쓴다.")]
        [SerializeField] private ForgeToolSlotUI resultPreviewSlotDisplay;
        [SerializeField] private GameObject resultPreviewEmptyHint;

        [Header("안내 / 실행")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button executeButton;

        [Header("참조")]
        [SerializeField] private ForgeManager forgeManager;
        [SerializeField] private ItemCatalogManager catalogManager;

        /// <summary>전승을 실제로 시도해서 성공했을 때 발생. ForgeUI가 하단 목록 갱신에 사용한다(재료 도구 소멸 반영).</summary>
        public event Action InheritanceExecuted;

        private ItemStack materialStack;
        private ItemStack targetStack;

        private void Awake()
        {
            if (forgeManager == null) forgeManager = ForgeManager.Instance;
            catalogManager = ItemCatalogManager.Resolve(catalogManager);

            if (executeButton != null) executeButton.onClick.AddListener(HandleExecuteClicked);
            if (materialSlotDisplay != null) materialSlotDisplay.Clicked += _ => ClearSelection();
            if (targetSlotDisplay != null) targetSlotDisplay.Clicked += _ => ClearTargetOnly();
        }

        private void OnEnable()
        {
            ClearSelection();
        }

        /// <summary>ForgeUI가 모든 슬롯에 동일한 툴팁 UI 인스턴스를 동기화할 때 호출한다.</summary>
        public void SetTooltipUI(ItemTooltipUI tooltipUI)
        {
            materialSlotDisplay?.SetTooltipUI(tooltipUI);
            targetSlotDisplay?.SetTooltipUI(tooltipUI);
            resultPreviewSlotDisplay?.SetTooltipUI(tooltipUI);
        }

        /// <summary>ForgeUI 하단 공용 목록에서 도구가 클릭되면 호출된다.</summary>
        public void HandleToolSelected(ItemStack stack)
        {
            if (materialStack == null)
            {
                materialStack = stack;
            }
            else if (targetStack == null)
            {
                if (ReferenceEquals(stack, materialStack)) return; // 같은 스택 중복 선택 방지
                targetStack = stack;
            }
            else
            {
                // 재료/대상이 모두 찬 상태에서 또 클릭 - 새 재료부터 다시 선택 시작
                materialStack = stack;
                targetStack = null;
            }

            RefreshMiddlePanel();
        }

        private void ClearSelection()
        {
            materialStack = null;
            targetStack = null;
            RefreshMiddlePanel();
        }

        private void ClearTargetOnly()
        {
            targetStack = null;
            RefreshMiddlePanel();
        }

        private void RefreshMiddlePanel()
        {
            bool hasMaterial = materialStack != null && !materialStack.IsEmpty;
            bool hasTarget = targetStack != null && !targetStack.IsEmpty;

            if (materialEmptyHint != null) materialEmptyHint.SetActive(!hasMaterial);
            if (targetEmptyHint != null) targetEmptyHint.SetActive(!hasTarget);
            if (resultPreviewEmptyHint != null) resultPreviewEmptyHint.SetActive(!hasMaterial);

            var materialData = hasMaterial && catalogManager != null ? catalogManager.FindItemData(materialStack.itemId) : null;
            var targetData = hasTarget && catalogManager != null ? catalogManager.FindItemData(targetStack.itemId) : null;

            if (hasMaterial)
            {
                materialSlotDisplay?.Bind(materialStack, materialData);
            }
            else
            {
                materialSlotDisplay?.Clear();
            }

            if (hasTarget)
            {
                targetSlotDisplay?.Bind(targetStack, targetData);
            }
            else
            {
                targetSlotDisplay?.Clear();
            }

            RefreshResultPreview(hasMaterial, hasTarget, materialData, targetData);

            bool canExecute = hasMaterial && hasTarget;

            if (statusText != null)
            {
                statusText.text = !hasMaterial ? "재료 도구를 선택하세요"
                    : !hasTarget ? "전승받을 도구를 선택하세요"
                    : "실행 가능";
            }

            if (executeButton != null) executeButton.interactable = canExecute;
        }

        /// <summary>
        /// 결과 미리보기 = 대상의 아이콘/강화표시(전승해도 안 바뀜) + 재료의 연마 효과(전승으로 넘어옴).
        /// 대상이 아직 없으면 비교 대상이 없으므로 재료 자체를 그대로 보여준다.
        /// </summary>
        private void RefreshResultPreview(bool hasMaterial, bool hasTarget, ItemData materialData, ItemData targetData)
        {
            if (!hasMaterial)
            {
                resultPreviewSlotDisplay?.Clear();
                return;
            }

            if (!hasTarget)
            {
                resultPreviewSlotDisplay?.Bind(materialStack, materialData);
                return;
            }

            ForgeRefinementSlotData[] materialSlots = null;
            forgeManager?.TryPeekRefinementSlots(materialStack, out materialSlots);

            resultPreviewSlotDisplay?.BindPreview(targetStack, targetData, materialSlots);
        }

        private void HandleExecuteClicked()
        {
            if (materialStack == null || targetStack == null || forgeManager == null) return;

            var outcome = forgeManager.TryInherit(materialStack, targetStack);

            if (outcome.Attempted)
            {
                ClearSelection();
                InheritanceExecuted?.Invoke();
            }
        }
    }
}
