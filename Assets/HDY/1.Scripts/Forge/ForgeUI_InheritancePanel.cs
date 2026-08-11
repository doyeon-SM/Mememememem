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
    /// [HDY 요청 - 선택 순서 변경] UI 디자인이 왼쪽부터 "전승받을 도구칸 + 재료 도구칸 = 결과칸"으로
    /// 바뀌면서, 선택 순서도 그에 맞춰 뒤집었다: 첫 클릭 = 전승받을 도구(targetStack, 왼쪽칸), 이후 클릭 =
    /// 재료 도구(materialStack, 중앙칸). 대상/재료가 모두 찬 상태에서 또 클릭하면 그 아이템을 새 대상으로
    /// 다시 선택(처음부터 다시 시작)한다. 왼쪽 대상 슬롯을 클릭하면 선택을 전부 초기화하고(이제 대상이
    /// 먼저 선택되는 기준점이므로), 중앙 재료 슬롯을 클릭하면 재료만 초기화한다(예전과 클릭별 초기화 범위가
    /// 서로 뒤바뀜 - materialSlotDisplay/targetSlotDisplay 필드 자체의 씬 배치나 의미는 그대로다).
    ///
    /// [도구 종류 제한] 대상과 ObjectType(벌목/채굴/채집 대상 - ItemData 기준)이 다른 도구는 애초에
    /// 재료로 선택되지 않는다(<see cref="IsSameObjectType"/>). ForgeManager.TryInherit도 동일한 기준으로
    /// 최종 거부하지만, 선택 단계에서 먼저 걸러야 실행 버튼을 눌렀을 때 아무 안내 없이 조용히 실패하는
    /// 것을 막을 수 있다.
    ///
    /// [결과 미리보기 - 중요] 전승은 연마칸만 재료 것으로 넘어가고, 강화 레벨/티어 등 대상 자체의
    /// 정체성은 그대로 유지된다(ForgeManager.TryInherit 참고). 그래서 미리보기도 두 아이템의 서로 다른
    /// 부분을 조합해서 보여줘야 한다:
    /// - 아이콘/강화(+N) 표시 = 대상(target) 기준 - 전승해도 안 바뀌는 부분
    /// - 마우스 호버 시 뜨는 연마 효과 툴팁 = 재료(material) 기준 - 전승으로 새로 넘어오는 부분
    /// 대상이 먼저 선택되는 구조라, 재료가 없으면(대상만 선택된 상태) 비교할 연마 효과가 없으므로 미리보기
    /// 자체를 비운다.
    ///
    /// [HDY 요청 - 안내 문구] statusText는 세 단계로 갱신된다:
    /// - 둘 다 비었을 때: "전승 받을 도구를 왼쪽칸에 넣으세요"
    /// - 대상만 찼을 때: "연마를 옮길 재료 도구를 중앙칸에 넣으세요"
    /// - 둘 다 차서 결과 미리보기가 가능할 때: "오른쪽 칸에서 결과를 미리 볼 수 있습니다."
    /// </summary>
    public class ForgeUI_InheritancePanel : MonoBehaviour
    {
        [Header("왼쪽 - 전승받을 도구 / 중앙 - 재료 (HDY 요청으로 순서 변경)")]
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

        /// <summary>[HDY 요청 - 하단 목록 상태 표시] 지금 재료로 선택된 도구. ForgeUI가 하단 목록에서 "전승 대기 중"/"전승불가" 표시를 판단하는 데 쓴다.</summary>
        public ItemStack MaterialStack => materialStack;

        /// <summary>[HDY 요청 - 하단 목록 상태 표시] 지금 전승받을 도구로 선택된 대상. ForgeUI가 하단 목록에서 "전승 대기 중" 표시를 판단하는 데 쓴다.</summary>
        public ItemStack TargetStack => targetStack;

        private ItemStack materialStack;
        private ItemStack targetStack;

        private void Awake()
        {
            if (forgeManager == null) forgeManager = ForgeManager.Instance;
            catalogManager = ItemCatalogManager.Resolve(catalogManager);

            if (executeButton != null) executeButton.onClick.AddListener(HandleExecuteClicked);

            // [HDY 요청 - 선택 순서 변경] 대상(왼쪽)이 이제 기준점이라 클릭 시 전체 초기화, 재료(중앙)는
            // 자기 자신만 초기화한다 - 예전과 반대.
            if (materialSlotDisplay != null) materialSlotDisplay.Clicked += _ => ClearMaterialOnly();
            if (targetSlotDisplay != null) targetSlotDisplay.Clicked += _ => ClearSelection();
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

        /// <summary>
        /// ForgeUI 하단 공용 목록에서 도구가 클릭되면 호출된다. [HDY 요청] 첫 클릭 = 전승받을 도구(대상),
        /// 이후 클릭 = 재료 도구.
        /// </summary>
        public void HandleToolSelected(ItemStack stack)
        {
            if (targetStack == null)
            {
                targetStack = stack;
            }
            else if (materialStack == null)
            {
                if (ReferenceEquals(stack, targetStack)) return; // 같은 스택 중복 선택 방지

                // 대상과 ObjectType(벌목/채굴/채집 대상)이 다른 도구는 재료로 선택할 수 없다.
                if (!IsSameObjectType(targetStack, stack))
                {
                    if (statusText != null) statusText.text = "전승받을 도구와 같은 종류의 도구만 선택할 수 있습니다";
                    return;
                }

                materialStack = stack;
            }
            else
            {
                // 대상/재료가 모두 찬 상태에서 또 클릭 - 새 대상부터 다시 선택 시작
                targetStack = stack;
                materialStack = null;
            }

            RefreshMiddlePanel();
        }

        /// <summary>
        /// 두 도구의 ItemData.ObjectType(벌목/채굴/채집 대상)이 같은지 확인한다.
        /// catalogManager가 없거나 ItemData 조회에 실패하면 안전하게 통과시키고, 최종 판정은
        /// ForgeManager.TryInherit(같은 기준으로 재검증함)에 맡긴다.
        /// </summary>
        private bool IsSameObjectType(ItemStack a, ItemStack b)
        {
            if (catalogManager == null || a == null || b == null) return true;

            var dataA = catalogManager.FindItemData(a.itemId);
            var dataB = catalogManager.FindItemData(b.itemId);

            if (dataA == null || dataB == null) return true;

            return dataA.ObjectType == dataB.ObjectType;
        }

        private void ClearSelection()
        {
            materialStack = null;
            targetStack = null;
            RefreshMiddlePanel();
        }

        private void ClearMaterialOnly()
        {
            materialStack = null;
            RefreshMiddlePanel();
        }

        private void RefreshMiddlePanel()
        {
            bool hasMaterial = materialStack != null && !materialStack.IsEmpty;
            bool hasTarget = targetStack != null && !targetStack.IsEmpty;

            if (materialEmptyHint != null) materialEmptyHint.SetActive(!hasMaterial);
            if (targetEmptyHint != null) targetEmptyHint.SetActive(!hasTarget);
            if (resultPreviewEmptyHint != null) resultPreviewEmptyHint.SetActive(!hasMaterial || !hasTarget);

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

            RefreshResultPreview(hasMaterial, hasTarget, targetData);

            bool canExecute = hasMaterial && hasTarget;

            // [HDY 요청 - 안내 문구]
            if (statusText != null)
            {
                statusText.text = !hasTarget ? "전승 받을 도구를 왼쪽칸에 넣으세요"
                    : !hasMaterial ? "연마를 옮길 재료 도구를 중앙칸에 넣으세요"
                    : "오른쪽 칸에서 결과를 미리 볼 수 있습니다.";
            }

            if (executeButton != null) executeButton.interactable = canExecute;
        }

        /// <summary>
        /// 결과 미리보기 = 대상의 아이콘/강화표시(전승해도 안 바뀜) + 재료의 연마 효과(전승으로 넘어옴).
        /// [HDY 요청 - 선택 순서 변경] 대상이 먼저 선택되는 구조라, 재료가 아직 없으면(대상만 선택된 상태)
        /// 비교할 연마 효과가 없으므로 미리보기 자체를 비운다(예전에는 재료가 먼저라 재료만 그대로 보여주는
        /// 분기가 있었지만, 이제 그 상태 자체가 나올 수 없어 제거했다).
        /// </summary>
        private void RefreshResultPreview(bool hasMaterial, bool hasTarget, ItemData targetData)
        {
            if (!hasMaterial || !hasTarget)
            {
                resultPreviewSlotDisplay?.Clear();
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
