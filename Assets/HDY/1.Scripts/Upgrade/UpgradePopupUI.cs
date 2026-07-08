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
    /// [사용법] 다른 UI에서 UpgradePopupUI.Instance.Show(target)만 호출하면 된다. 확인 버튼을 누르면
    /// 팝업이 직접 TerritoryData에서 골드를 확인/차감하고(재료는 IMaterialInventory 연결 시 함께 확인/차감),
    /// 비용을 전부 낼 수 있을 때만 target.ApplyUpgrade()를 호출한 뒤 팝업을 닫는다.
    ///
    /// [재료 인벤토리 TODO] 프로젝트에 아직 재료 재고를 관리하는 인벤토리 시스템이 없어서, materialInventorySource가
    /// 비어있으면 재료 비용이 있어도 재료 조건 검사를 건너뛰고(경고 로그만 남기고) 통과시킨다. 나중에 실제 재고
    /// 시스템이 생기면 IMaterialInventory를 구현한 컴포넌트를 인스펙터에 연결하면 된다.
    ///
    /// [씬 싱글톤] TerritoryData처럼 DontDestroyOnLoad는 아니고, 이 씬(HDY_TestScene)에 하나만 배치되어 있다고
    /// 가정한다. 다른 UI가 Instance로 쉽게 접근할 수 있도록 static 참조만 제공한다.
    ///
    /// [재료 비용 행 표시] 재료 비용은 개수가 매번 달라질 수 있어(0개~여러 개), 그리드 슬롯처럼 씬에 미리
    /// 배치해두는 방식 대신 런타임에 필요한 만큼만 Instantiate한다(materialCostRowPrefab 사용, 팝업을 다시 열 때
    /// 기존 행을 재사용하고 모자란 만큼만 새로 만든다).
    /// </summary>
    public class UpgradePopupUI : MonoBehaviour
    {
        public static UpgradePopupUI Instance { get; private set; }

        [Header("데이터 참조")]
        [SerializeField] private TerritoryData territoryData;
        [Tooltip("IMaterialInventory를 구현한 컴포넌트를 연결. 재료 재고 시스템이 아직 없다면 비워둬도 된다(골드만 검사).")]
        [SerializeField] private MonoBehaviour materialInventorySource;
        [SerializeField] private ItemCatalogManager itemCatalogManager;

        [Header("팝업 루트 (평소에는 꺼져 있다가 Show()에서 켜짐)")]
        [SerializeField] private GameObject popupRoot;

        [Header("텍스트 / 버튼")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text goldCostText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("재료 비용 표시 (개수가 가변적이라 런타임 생성)")]
        [SerializeField] private Transform materialCostContainer;
        [SerializeField] private UpgradeMaterialCostRowUI materialCostRowPrefab;

        private readonly List<UpgradeMaterialCostRowUI> spawnedRows = new List<UpgradeMaterialCostRowUI>();
        private IUpgradable currentTarget;
        private IMaterialInventory MaterialInventory => materialInventorySource as IMaterialInventory;

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
            if (materialInventorySource != null && MaterialInventory == null)
            {
                Debug.LogWarning("[UpgradePopupUI] materialInventorySource가 IMaterialInventory를 구현하지 않습니다.", this);
            }

            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);

            if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);

            if (popupRoot != null) popupRoot.SetActive(false);
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

        public void Hide()
        {
            currentTarget = null;
            if (popupRoot != null) popupRoot.SetActive(false);
        }

        private void RefreshDisplay()
        {
            if (currentTarget == null) return;

            bool canUpgrade = currentTarget.CanUpgrade();
            var cost = currentTarget.GetUpgradeCost();

            if (titleText != null) titleText.text = currentTarget.GetUpgradeTitle();
            if (descriptionText != null) descriptionText.text = currentTarget.GetUpgradeDescription();
            if (goldCostText != null) goldCostText.text = cost.GoldCost.ToString();

            PopulateMaterialRows(cost.MaterialCosts);

            if (confirmButton != null) confirmButton.interactable = canUpgrade;
        }

        /// <summary>기존에 만들어둔 행을 재사용하고, 모자라면 새로 만든다(팝업을 여러 번 열어도 매번 파괴/생성하지 않도록).</summary>
        private void PopulateMaterialRows(List<UpgradeMaterialCost> materialCosts)
        {
            if (materialCostRowPrefab == null || materialCostContainer == null)
            {
                if (materialCosts != null && materialCosts.Count > 0)
                {
                    Debug.LogWarning("[UpgradePopupUI] materialCostRowPrefab/materialCostContainer가 비어있어 재료 비용을 표시할 수 없습니다.", this);
                }
                return;
            }

            int count = materialCosts != null ? materialCosts.Count : 0;

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
                        // [TODO] 재료 재고 시스템이 아직 없어, 연결 전까지는 재료 조건을 검사하지 않고 통과시킨다.
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
