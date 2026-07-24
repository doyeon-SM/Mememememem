using UnityEngine;
using HDY.Upgrade;
using KMS.InventoryDuped;

namespace HDY.Inventory
{
    /// <summary>
    /// 인벤토리 칸(5칸씩) 확장을 공용 업그레이드 팝업(UpgradePopupUI)에 연결하는 어댑터.
    /// 실제 칸 언락은 PlayerInventory가 담당하고(이미 잠금/언락 칸수 개념을 갖고 있음), 이 클래스는
    /// IUpgradable을 구현해서 "이번 업그레이드에 얼마가 드는지 / 더 업그레이드할 수 있는지"만 계산한다.
    ///
    /// [비용표 공유 - InventoryUpgradeCostTable] 인벤토리 업그레이드는 어느 씬에서 실행하든(창고 패널의
    /// 테스트용 PlayerInventory든, 실제 플레이어의 인벤토리든) 같은 플레이어의 같은 진행 상태를 다루는
    /// 것이다(씬 전환 시 저장->로드로 데이터가 옮겨 탈 뿐, 규칙 자체는 항상 동일해야 한다). 그래서
    /// 비용표(골드+재료 단계별 목록)는 이 컴포넌트가 직접 들고 있지 않고 공용 ScriptableObject 에셋
    /// (InventoryUpgradeCostTable) 하나를 참조해서 읽는다 - 여러 씬의 InventoryUpgrade 컴포넌트가 같은
    /// 에셋 파일을 함께 가리키면 비용 수정은 한 곳에서만 하면 된다.
    /// </summary>
    public class InventoryUpgrade : MonoBehaviour, IUpgradable
    {
        [Header("데이터 참조")]
        [SerializeField] private PlayerInventory playerInventory;

        [Header("단계별 필요 골드+재료 (공용 에셋 - 여러 씬의 InventoryUpgrade가 같은 파일을 참조해야 한다)")]
        [SerializeField] private InventoryUpgradeCostTable costTable;

        private void Awake()
        {
            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();

            if (playerInventory == null) Debug.LogWarning("[InventoryUpgrade] playerInventory가 비어있습니다.", this);
            if (costTable == null) Debug.LogWarning("[InventoryUpgrade] costTable이 비어있습니다. InventoryUpgradeCostTable 에셋을 연결하세요.", this);
        }

        /// <summary>현재 언락 칸수 기준으로 몇 번째 업그레이드 단계인지(0부터 시작). 이미 최대치면 costTable.Steps 범위를 벗어난다.</summary>
        private int GetCurrentStepIndex()
        {
            if (playerInventory == null) return -1;

            int step = playerInventory.SlotsPerInventoryUpgrade;
            if (step <= 0) return -1;

            int progressed = playerInventory.UnlockedInventorySlotCount - playerInventory.StartingInventorySlotCount;
            return progressed / step;
        }

        public bool CanUpgrade()
        {
            if (playerInventory == null || costTable == null) return false;
            if (playerInventory.UnlockedInventorySlotCount >= playerInventory.MaxInventorySlotCount) return false;

            int stepIndex = GetCurrentStepIndex();
            return stepIndex >= 0 && stepIndex < costTable.Steps.Count;
        }

        public UpgradeCost GetUpgradeCost()
        {
            if (costTable == null)
            {
                Debug.LogWarning("[InventoryUpgrade] costTable이 비어있어 비용을 계산할 수 없습니다.", this);
                return UpgradeCost.GoldOnly(0);
            }

            int stepIndex = GetCurrentStepIndex();

            if (stepIndex < 0 || stepIndex >= costTable.Steps.Count)
            {
                Debug.LogWarning($"[InventoryUpgrade] 단계({stepIndex})에 해당하는 비용 데이터가 없습니다. costTable의 Steps 크기를 확인하세요.", this);
                return UpgradeCost.GoldOnly(0);
            }

            var step = costTable.Steps[stepIndex];
            var cost = new UpgradeCost { GoldCost = step.GoldCost };

            foreach (var material in step.MaterialCosts)
            {
                cost.MaterialCosts.Add(new UpgradeMaterialCost { Item_ID = material.Item_ID, Amount = material.Amount });
            }

            return cost;
        }

        public string GetUpgradeTitle()
        {
            return "인벤토리 확장";
        }

        /// <summary>확인 버튼 라벨에 그대로 들어가는 짧은 문구. 최대치면 "MAX", 아니면 "현재칸수 → 다음칸수".</summary>
        public string GetUpgradeDescription()
        {
            if (playerInventory == null) return string.Empty;

            if (!CanUpgrade())
            {
                return "MAX";
            }

            int next = Mathf.Min(playerInventory.MaxInventorySlotCount, playerInventory.UnlockedInventorySlotCount + playerInventory.SlotsPerInventoryUpgrade);
            return $"{playerInventory.UnlockedInventorySlotCount}칸 → {next}칸";
        }

        /// <summary>UpgradePopupUI가 비용 지불을 마친 뒤 호출한다. 여기서는 순수하게 칸 언락만 담당한다.</summary>
        public void ApplyUpgrade()
        {
            if (playerInventory == null) return;
            playerInventory.UnlockNextInventorySlots();
        }
    }
}
