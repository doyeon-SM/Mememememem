using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Territory;
using HDY.Item;

namespace HDY.Upgrade
{
    /// <summary>
    /// 공용 업그레이드 팝업 UI.
    /// 멤창고 페이지 확장뿐 아니라, 앞으로 골드/재료를 소비해 무언가를 업그레이드하는 모든 화면에서
    /// 재사용할 목적으로 만들어졌다. 팝업은 IUpgradable 인터페이스만 알고 있으며, 실제로 무엇이
    /// 업그레이드되는지는 전혀 모른다(대상 쪽이 CanUpgrade/GetUpgradeCost/ApplyUpgrade를 구현).
    ///
    /// [레이아웃] 별도의 설명(Description) 영역은 없다. 대신:
    /// - 확인(업그레이드) 버튼 라벨(confirmButtonLabel)에 IUpgradable.GetUpgradeDescription()의 짧은 문구
    ///   (예: "2 → 3")를 그대로 표시한다.
    /// - 원래 설명이 있던 자리에는 이번 업그레이드에 필요한 재료 목록을 2열 그리드(스크롤 뷰, 뷰포트에는 2행까지만
    ///   보이고 그 이상은 스크롤로 확인)로 표시한다. 그리드 자체(열 개수 고정, 셀 크기, 뷰포트 높이)는 씬의
    ///   materialCostContainer에 배치된 GridLayoutGroup/ScrollRect 설정으로 제어되며, 이 스크립트는 필요한
    ///   개수만큼 materialCostRowPrefab을 채워 넣는 역할만 한다.
    /// - 이번 업그레이드에 필요한 재료가 하나도 없으면(예: 멤창고 페이지 업그레이드는 골드만 사용) 재료 스크롤 뷰
    ///   자체를 꺼서 빈 영역이 보이지 않도록 한다(materialScrollRect 기준으로 토글).
    /// - 골드 비용이 0 이하면(예: 영지 확장은 재료만 사용) goldCostText 자체를 비활성화한다.
    ///
    /// [사용법] 다른 UI에서 UpgradePopupUI.Instance.Show(target)만 호출하면 된다. 확인 버튼을 누르면
    /// 팝업이 직접 TerritoryData에서 골드를 확인/차감하고(재료는 IMaterialInventory 연결 시 함께 확인/차감),
    /// 비용을 전부 낼 수 있을 때만 target.ApplyUpgrade()를 호출한 뒤 팝업을 닫는다.
    ///
    /// [팝업 닫힘 알림] OnPopupClosed 이벤트는 Hide()가 호출될 때마다(확인 성공 후 자동으로 닫히든, 취소
    /// 버튼으로 직접 닫든 관계없이) 발행된다. 호출한 쪽(예: GoddessStatueUI)이 "팝업이 어떤 이유로든 닫히면
    /// 선택 강조 표시를 꺼야 하는" 경우에 구독해서 쓸 수 있다.
    ///
    /// [재료 인벤토리 자동 연결] materialInventorySource를 인스펙터에서 비워두면, Awake에서 씬 전체를 훑어
    /// IMaterialInventory를 구현한 컴포넌트를 아무거나 찾아 자동으로 연결한다(FindMaterialInventorySource).
    /// 팝업은 구체적으로 어떤 구현체인지(예: CombinedMaterialInventory) 전혀 모르고 인터페이스만 보고
    /// 찾으므로, 재료 재고 시스템이 바뀌어도 이 클래스는 손댈 필요가 없다. 씬에 구현체가 여러 개면 그중
    /// 하나가 임의로 선택되니, 여러 개를 두고 특정 걸 쓰고 싶다면 인스펙터에서 직접 연결하면 된다(그러면
    /// 자동 탐색은 건너뛴다). 그래도 못 찾으면(구현체 자체가 없으면) 재료 조건 검사를 건너뛰고 통과시킨다
    /// (경고 로그만 남김) - 재료가 필요 없는 업그레이드(예: 멤창고 페이지 확장)는 이 상태로도 문제없이 동작한다.
    ///
    /// [씬 싱글톤] TerritoryData처럼 DontDestroyOnLoad는 아니고, 이 씬(HDY_TestScene)에 하나만 배치되어 있다고
    /// 가정한다. 다른 UI가 Instance로 쉽게 접근할 수 있도록 static 참조만 제공한다.
    /// </summary>
    public class UpgradePopupUI : MonoBehaviour
    {
        public static UpgradePopupUI Instance { get; private set; }

        [Header("데이터 참조")]
        [SerializeField] private TerritoryData territoryData;
        [Tooltip("IMaterialInventory를 구현한 컴포넌트를 연결. 비워두면 Awake에서 씬을 훑어 자동으로 찾는다(FindMaterialInventorySource). 재료 재고 시스템이 아예 없다면 비워둔 채로 둬도 된다(골드만 검사).")]
        [SerializeField] private MonoBehaviour materialInventorySource;
        [SerializeField] private ItemCatalogManager itemCatalogManager;

        [Header("팝업 루트 (평소에는 꺼져 있다가 Show()에서 켜짐)")]
        [SerializeField] private GameObject popupRoot;

        [Header("제목 / 골드 텍스트")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("골드 비용이 0 이하면(재료만 필요한 업그레이드) 이 텍스트 자체를 비활성화한다.")]
        [SerializeField] private TMP_Text goldCostText;

        [Header("확인 / 취소 버튼")]
        [Tooltip("확인 버튼 자체. interactable로 업그레이드 가능 여부를 표시한다.")]
        [SerializeField] private Button confirmButton;
        [Tooltip("확인 버튼에 표시할 라벨. 별도 설명 영역이 없으므로 IUpgradable.GetUpgradeDescription()의 짧은 문구(예: \"2 → 3\")가 여기로 들어간다.")]
        [SerializeField] private TMP_Text confirmButtonLabel;
        [SerializeField] private Button cancelButton;

        [Header("재료 비용 표시 (스크롤 뷰 안 2열 그리드, 뷰포트는 2행까지만 보이고 나머지는 스크롤)")]
        [Tooltip("재료 스크롤 뷰 루트(ScrollRect가 붙은 오브젝트). 필요 재료가 하나도 없으면 이 오브젝트 자체를 비활성화한다.")]
        [SerializeField] private ScrollRect materialScrollRect;
        [Tooltip("재료 행(materialCostRowPrefab)들이 실제로 생성되는 부모. GridLayoutGroup(2열 고정)이 붙어있어야 한다.")]
        [SerializeField] private Transform materialCostContainer;
        [SerializeField] private UpgradeMaterialCostRowUI materialCostRowPrefab;

        private readonly List<UpgradeMaterialCostRowUI> spawnedRows = new List<UpgradeMaterialCostRowUI>();
        private IUpgradable currentTarget;
        private IMaterialInventory MaterialInventory => materialInventorySource as IMaterialInventory;

        /// <summary>팝업이 어떤 이유로든(확인 성공/취소) 닫힐 때마다 발행.</summary>
        public event Action OnPopupClosed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[UpgradePopupUI] 씬에 UpgradePopupUI가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (territoryData == null) Debug.LogWarning("[UpgradePopupUI] territoryData가 비어있습니다. 골드 확인/차감이 불가능합니다.", this);

            if (materialInventorySource == null)
            {
                materialInventorySource = FindMaterialInventorySource();

                if (materialInventorySource != null)
                {
                    Debug.Log($"[UpgradePopupUI] materialInventorySource 자동 연결: {materialInventorySource.GetType().Name} ({materialInventorySource.name})", this);
                }
                else
                {
                    Debug.LogWarning("[UpgradePopupUI] 씬에서 IMaterialInventory 구현체를 찾지 못했습니다. 재료 조건 검사를 건너뜁니다.", this);
                }
            }
            else if (MaterialInventory == null)
            {
                Debug.LogWarning("[UpgradePopupUI] materialInventorySource가 IMaterialInventory를 구현하지 않습니다.", this);
            }

            if (confirmButtonLabel == null) Debug.LogWarning("[UpgradePopupUI] confirmButtonLabel이 비어있습니다. 확인 버튼에 설명 문구가 표시되지 않습니다.", this);

            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);

            if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);

            if (popupRoot != null) popupRoot.SetActive(false);
        }

        /// <summary>
        /// materialInventorySource가 인스펙터에서 비어있을 때, 씬 전체에서 IMaterialInventory를 구현한
        /// MonoBehaviour를 아무거나 찾는다. 이 팝업은 구체적인 구현체 타입(예: CombinedMaterialInventory)을
        /// 전혀 몰라도 되도록, 타입이 아니라 인터페이스로만 찾는다. 씬에 구현체가 여러 개 있으면 그중 하나가
        /// (정렬 순서상 먼저 발견되는 것이) 임의로 선택되니, 특정 구현체를 쓰고 싶다면 인스펙터에서 직접 연결하면
        /// 이 자동 탐색 자체를 건너뛴다.
        /// </summary>
        private MonoBehaviour FindMaterialInventorySource()
        {
            var candidates = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            foreach (var candidate in candidates)
            {
                if (candidate is IMaterialInventory)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>업그레이드 대상을 받아 팝업을 연다. 열 때마다 최신 비용/가능 여부를 다시 계산해서 표시한다.</summary>
        public void Show(IUpgradable target)
        {
            if (target == null)
            {
                Debug.LogWarning("[UpgradePopupUI] Show 호출: target이 null입니다.", this);
                return;
            }

            currentTarget = target;

            if (popupRoot != null) popupRoot.SetActive(true);

            RefreshDisplay();
        }

        /// <summary>팝업을 닫는다(확인 성공 후 자동 호출/취소 버튼 클릭 모두 여기로 온다). 닫힐 때마다 OnPopupClosed를 발행한다.</summary>
        public void Hide()
        {
            currentTarget = null;
            if (popupRoot != null) popupRoot.SetActive(false);

            OnPopupClosed?.Invoke();
        }

        private void RefreshDisplay()
        {
            if (currentTarget == null) return;

            bool canUpgrade = currentTarget.CanUpgrade();
            var cost = currentTarget.GetUpgradeCost();

            if (titleText != null) titleText.text = currentTarget.GetUpgradeTitle();

            // 골드 비용이 0 이하면(재료만 필요한 업그레이드, 예: 영지 확장) 텍스트 자체를 감춘다.
            bool hasGoldCost = cost.GoldCost > 0;
            if (goldCostText != null)
            {
                goldCostText.gameObject.SetActive(hasGoldCost);
                if (hasGoldCost) goldCostText.text = cost.GoldCost.ToString();
            }

            if (confirmButtonLabel != null) confirmButtonLabel.text = currentTarget.GetUpgradeDescription();

            bool hasMaterialCosts = cost.MaterialCosts != null && cost.MaterialCosts.Count > 0;

            // 요구 재료가 하나도 없으면(예: 멤창고 페이지 업그레이드는 골드만 사용) 스크롤 뷰 자체를 꺼서
            // 빈 그리드 영역이 보이지 않도록 한다.
            if (materialScrollRect != null)
            {
                materialScrollRect.gameObject.SetActive(hasMaterialCosts);
            }

            if (hasMaterialCosts)
            {
                PopulateMaterialRows(cost.MaterialCosts);
                ResetMaterialScrollToTop();
            }

            if (confirmButton != null) confirmButton.interactable = canUpgrade;
        }

        /// <summary>기존에 만들어둔 행을 재사용하고, 모자라면 새로 만든다(팝업을 여러 번 열어도 매번 파괴/생성하지 않도록).</summary>
        private void PopulateMaterialRows(List<UpgradeMaterialCost> materialCosts)
        {
            if (materialCostRowPrefab == null || materialCostContainer == null)
            {
                Debug.LogWarning("[UpgradePopupUI] materialCostRowPrefab/materialCostContainer가 비어있어 재료 비용을 표시할 수 없습니다.", this);
                return;
            }

            int count = materialCosts.Count;

            while (spawnedRows.Count < count)
            {
                var row = Instantiate(materialCostRowPrefab, materialCostContainer);
                spawnedRows.Add(row);
            }

            for (int i = 0; i < spawnedRows.Count; i++)
            {
                if (i < count)
                {
                    var materialCost = materialCosts[i];
                    var itemData = itemCatalogManager != null ? itemCatalogManager.FindItemData(materialCost.Item_ID) : null;
                    spawnedRows[i].SetData(itemData, materialCost.Item_ID, materialCost.Amount);
                    spawnedRows[i].gameObject.SetActive(true);
                }
                else
                {
                    spawnedRows[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 재료 그리드(2열 x n행, GridLayoutGroup)는 행 개수가 바뀌면 콘텐츠 높이도 같이 바뀌므로, 스크롤 위치를
        /// 계산하기 전에 레이아웃을 먼저 강제로 갱신한 뒤 맨 위로 되돌린다. 팝업을 열 때마다 항상 첫 두 줄부터
        /// 보이도록 하기 위함이다(이전에 열었을 때 스크롤해뒀던 위치가 남아있지 않도록).
        /// </summary>
        private void ResetMaterialScrollToTop()
        {
            if (materialCostContainer is RectTransform contentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }

            if (materialScrollRect != null)
            {
                materialScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void HandleConfirmClicked()
        {
            if (currentTarget == null) return;

            if (!currentTarget.CanUpgrade())
            {
                Debug.LogWarning("[UpgradePopupUI] 더 이상 업그레이드할 수 없는 상태입니다.", this);
                return;
            }

            var cost = currentTarget.GetUpgradeCost();

            if (!TryPayCost(cost))
            {
                Debug.LogWarning("[UpgradePopupUI] 비용이 부족해 업그레이드를 진행하지 못했습니다.", this);
                return;
            }

            Debug.Log($"[UpgradePopupUI] 업그레이드 적용: {currentTarget.GetUpgradeTitle()}");
            currentTarget.ApplyUpgrade();

            Hide();
        }

        /// <summary>골드 + 재료 비용을 전부 확인한 뒤, 모두 충분할 때만 실제로 차감한다(일부만 차감되는 상황 방지).</summary>
        private bool TryPayCost(UpgradeCost cost)
        {
            if (territoryData == null)
            {
                Debug.LogWarning("[UpgradePopupUI] territoryData가 비어있어 골드를 확인/차감할 수 없습니다.", this);
                return false;
            }

            if (territoryData.Gold < cost.GoldCost) return false;

            var materialInventory = MaterialInventory;
            if (cost.MaterialCosts != null)
            {
                foreach (var materialCost in cost.MaterialCosts)
                {
                    if (materialInventory == null)
                    {
                        // 재료 재고 시스템이 연결되지 않은 상태 - 재료 조건을 검사하지 않고 통과시킨다.
                        Debug.LogWarning($"[UpgradePopupUI] 재료 인벤토리가 연결되지 않아 재료 조건({materialCost.Item_ID} x{materialCost.Amount})을 검사하지 않고 통과시킵니다.", this);
                        continue;
                    }

                    if (!materialInventory.HasEnough(materialCost.Item_ID, materialCost.Amount)) return false;
                }
            }

            territoryData.TrySpendGold(cost.GoldCost);

            if (materialInventory != null && cost.MaterialCosts != null)
            {
                foreach (var materialCost in cost.MaterialCosts)
                {
                    materialInventory.Consume(materialCost.Item_ID, materialCost.Amount);
                }
            }

            return true;
        }
    }
}
