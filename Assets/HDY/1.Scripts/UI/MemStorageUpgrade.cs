using System.Collections.Generic;
using UnityEngine;
using HDY.Upgrade;
using HDY.Capture;
using HDY.Territory;

namespace HDY.UI
{
    /// <summary>
    /// 멤창고 페이지 확장을 공용 업그레이드 팝업(UpgradePopupUI)에 연결하기 위한 어댑터.
    /// 실제 페이지 잠금 해제는 MemCaptureManager가 담당하고(이미 슬롯/페이지 개념을 갖고 있음), 이 클래스는
    /// IUpgradable을 구현해서 "이번 업그레이드에 얼마가 드는지 / 더 업그레이드할 수 있는지"만 계산해준다.
    ///
    /// [비용 표] goldCostPerStep의 각 원소는 "현재 언락 페이지 수 - 시작 페이지 수" 번째 업그레이드에 필요한 골드다.
    /// 예: 시작 2페이지 -> 최대 10페이지면 업그레이드는 총 8단계이므로 goldCostPerStep 크기도 8이어야 한다.
    /// RecipeUnlockManager와 마찬가지로 기획 확정 전까지는 인스펙터에서 단계별 금액을 직접 입력하는 방식이다.
    ///
    /// [설명 문구] UpgradePopupUI에는 별도 설명 영역이 없고, GetUpgradeDescription()의 결과가 그대로 확인
    /// 버튼의 라벨로 쓰인다. 그래서 "멤창고를 2페이지에서 3페이지로 확장합니다" 같은 긴 문장 대신
    /// "2 → 3"처럼 버튼 한 줄에 들어가는 짧은 형식으로 반환한다.
    /// </summary>
    public class MemStorageUpgrade : MonoBehaviour, IUpgradable
    {
        [Header("데이터 참조")]
        [SerializeField] private MemCaptureManager captureManager;
        [SerializeField] private TerritoryData territoryData;

        [Header("단계별 필요 골드 (시작 페이지 -> 최대 페이지까지, 순서대로 입력)")]
        [SerializeField] private List<int> goldCostPerStep = new List<int>();

        private void Awake()
        {
            if (captureManager == null) captureManager = MemCaptureManager.Instance;

            if (captureManager == null) Debug.LogWarning("[MemStorageUpgrade] captureManager가 비어있습니다.", this);
            if (territoryData == null) Debug.LogWarning("[MemStorageUpgrade] territoryData가 비어있습니다. 골드 확인은 UpgradePopupUI가 별도로 처리합니다.", this);
        }

        /// <summary>현재 언락된 페이지 수 기준으로 몇 번째 업그레이드 단계인지(0부터 시작). 이미 최대치면 goldCostPerStep 범위를 벗어난다.</summary>
        private int GetCurrentStepIndex()
        {
            if (captureManager == null) return -1;
            return captureManager.UnlockedPageCount - captureManager.StartingPageCount;
        }

        public bool CanUpgrade()
        {
            if (captureManager == null) return false;
            return captureManager.UnlockedPageCount < captureManager.MaxPages;
        }

        public UpgradeCost GetUpgradeCost()
        {
            int stepIndex = GetCurrentStepIndex();
            if (stepIndex < 0 || stepIndex >= goldCostPerStep.Count)
            {
                Debug.LogWarning($"[MemStorageUpgrade] 단계({stepIndex})에 해당하는 비용 데이터가 없습니다. goldCostPerStep 크기를 확인하세요.", this);
                return UpgradeCost.GoldOnly(0);
            }

            return UpgradeCost.GoldOnly(goldCostPerStep[stepIndex]);
        }

        public string GetUpgradeTitle()
        {
            return "멤창고 페이지 확장";
        }

        /// <summary>확인 버튼 라벨에 그대로 들어가는 짧은 문구. 최대치면 "MAX", 아니면 "현재페이지 → 다음페이지".</summary>
        public string GetUpgradeDescription()
        {
            if (captureManager == null) return string.Empty;

            if (!CanUpgrade())
            {
                return "MAX";
            }

            return $"{captureManager.UnlockedPageCount} → {captureManager.UnlockedPageCount + 1}";
        }

        /// <summary>UpgradePopupUI가 비용 지불을 마친 뒤 호출한다. 여기서는 순수하게 페이지 언락만 담당한다.</summary>
        public void ApplyUpgrade()
        {
            if (captureManager == null) return;
            captureManager.UnlockNextPage();
        }
    }
}
