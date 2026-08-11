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
    /// [HDY 요청 - 헤더/미들/버튼 텍스트] 제목은 GetUpgradeTitle(), 팝업 중간 설명은 GetUpgradeMiddleText()
    /// ("추가 페이지 확장 비용" 또는 최대치면 "MAX"), 확인 버튼 라벨은 GetUpgradeButtonText()("확장" 고정)로
    /// 각각 나뉜다.
    ///
    /// [territoryData 자동 탐색] 이 컴포넌트는 씬에 상시 배치된 오브젝트가 아니라 멤창고 UI 프리팹의
    /// 일부라, HUD 버튼으로 열릴 때마다 UIManager가 새로 Instantiate한다. territoryData는 다른 씬
    /// 오브젝트(TerritoryData)를 가리키는 참조라 프리팹 에셋 자체에 저장될 수 없으므로(ShopUI.territoryData와
    /// 동일한 이유), Awake에서 씬을 훑어 자동으로 채운다.
    ///
    /// [HDY 요청 - Awake 순서 방어] captureManager/territoryData를 Awake()에서만 한 번 채우고 끝내면,
    /// 씬에 미리 배치된 오브젝트들 사이에서는 Awake 실행 순서가 보장되지 않는다는 문제가 있었다 - 예를 들어
    /// 이 컴포넌트의 Awake가 MemCaptureManager의 Awake보다 먼저 실행되면 MemCaptureManager.Instance가 아직
    /// null이라 captureManager 확보에 실패하고, 예전에는 재시도 로직이 없어 CanUpgrade()가 영원히 false를
    /// 반환해 업그레이드 버튼이 계속 숨겨진 채로 남는 문제가 있었다(MemStorageUI.RefreshUpgradeButtonState가
    /// 그 결과를 보고 SetActive(false)로 꺼버림). 그래서 MemStorageUI_Grid의 EnsureCaptureManager()와 동일한
    /// 패턴으로, 외부에서 호출되는 모든 진입점(CanUpgrade/GetUpgradeCost/GetUpgradeMiddleText/ApplyUpgrade)
    /// 맨 앞에서 EnsureReferences()를 다시 호출해서, 아직 확보 못 했으면 호출될 때마다 다시 시도한다(이미
    /// 확보되어 있으면 아무 것도 하지 않으므로 여러 번 호출해도 안전하다).
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
            EnsureReferences();

            if (captureManager == null) Debug.LogWarning("[MemStorageUpgrade] captureManager가 비어있습니다.", this);
            if (territoryData == null) Debug.LogWarning("[MemStorageUpgrade] territoryData가 비어있습니다. 골드 확인은 UpgradePopupUI가 별도로 처리합니다.", this);
        }

        /// <summary>
        /// [HDY 요청] captureManager/territoryData가 비어있으면 다시 확보를 시도한다. Awake뿐 아니라
        /// CanUpgrade/GetUpgradeCost/GetUpgradeMiddleText/ApplyUpgrade 등 외부에서 호출되는 모든 진입점
        /// 맨 앞에서 호출해서, Awake 실행 순서 문제로 최초 확보에 실패했더라도 이후 호출 시점에 다시 시도할
        /// 수 있게 한다.
        /// </summary>
        private void EnsureReferences()
        {
            if (captureManager == null) captureManager = MemCaptureManager.Instance;
            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();
        }

        /// <summary>현재 언락된 페이지 수 기준으로 몇 번째 업그레이드 단계인지(0부터 시작). 이미 최대치면 goldCostPerStep 범위를 벗어난다.</summary>
        private int GetCurrentStepIndex()
        {
            EnsureReferences();
            if (captureManager == null) return -1;
            return captureManager.UnlockedPageCount - captureManager.StartingPageCount;
        }

        public bool CanUpgrade()
        {
            EnsureReferences();
            if (captureManager == null) return false;
            return captureManager.UnlockedPageCount < captureManager.MaxPages;
        }

        public UpgradeCost GetUpgradeCost()
        {
            EnsureReferences();

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
            return "멤 창고 확장";
        }

        /// <summary>[HDY 요청] 팝업 중간에 표시할 문구. 최대치면 "MAX", 아니면 "추가 페이지 확장 비용".</summary>
        public string GetUpgradeMiddleText()
        {
            EnsureReferences();
            if (captureManager == null) return string.Empty;

            return CanUpgrade() ? "추가 페이지 확장 비용" : "MAX";
        }

        /// <summary>[HDY 요청] 확인 버튼에 표시할 고정 문구.</summary>
        public string GetUpgradeButtonText()
        {
            return "확장";
        }

        /// <summary>UpgradePopupUI가 비용 지불을 마친 뒤 호출한다. 여기서는 순수하게 페이지 언락만 담당한다.</summary>
        public void ApplyUpgrade()
        {
            EnsureReferences();
            if (captureManager == null) return;
            captureManager.UnlockNextPage();
        }
    }
}
