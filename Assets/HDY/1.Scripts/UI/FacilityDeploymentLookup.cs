using UnityEngine;
using MemSystem.Data;
using HDY.Capture;

namespace HDY.UI
{
    /// <summary>
    /// 포획된 멤(CapturedMemEntry)이 현재 어느 생산/제작 시설에 배치되어 있는지 찾아, 그 시설이 요구하는
    /// 생산 스탯 종류(ProductionStatType)를 조회하는 순수 조회 전용 헬퍼.
    ///
    /// [Kyusoo 코드 수정 없음] ProductionFacilityRuntime/ProductionCraftRuntime의 DeployedMemEntries와
    /// buildingData가 이미 public이라, 이 헬퍼는 그 두 컴포넌트를 씬에서 찾아 훑기만 할 뿐 Kyusoo 쪽 파일은
    /// 전혀 건드리지 않는다. MemStorageUI.HandleReleaseRequested에서 쓰는 BuildingRuntime.TryReleaseDeployedMem()과
    /// 달리, 이 헬퍼는 아무것도 바꾸지 않고 "찾기"만 하는 조회 전용 함수라는 점이 다르다.
    ///
    /// [생산시설 vs 제작시설] 두 클래스는 서로 다른 컴포넌트(각자 별도의 DeployedMemEntries/정적 이벤트를
    /// 가짐)라 둘 다 훑어야 어느 쪽에 배치되어 있어도 놓치지 않는다. 제작대(Workshop)는
    /// ProductionCraftRuntime, 나머지 생산시설(벌목장/채굴장/밭/목장/운반시설/발전기)은
    /// ProductionFacilityRuntime이 담당한다.
    ///
    /// [성능] 멤 창고 그리드는 최대 48칸을 한 번에 그리므로, 활성 멤이 많으면 슬롯 수만큼 이 조회가
    /// 반복 호출될 수 있다. 씬의 시설 개수가 그리 많지 않은 게임 규모라 지금은 매번 FindObjectsByType으로
    /// 다시 훑는 단순한 구현으로 충분하다고 보고 우선 이렇게 작성했다 - 나중에 체감 성능 문제가 생기면
    /// Populate() 한 번당 전체 시설을 미리 한 번만 스캔해서 Dictionary&lt;CapturedMemEntry, ProductionStatType&gt;로
    /// 캐싱해두고 슬롯마다 조회만 하도록 최적화할 수 있다.
    /// </summary>
    public static class FacilityDeploymentLookup
    {
        /// <summary>
        /// entry가 배치되어 있는 시설을 찾아 그 시설이 요구하는 생산 스탯 종류를 반환한다.
        /// 어느 시설에도 배치되어 있지 않으면(또는 entry가 null이면) false를 반환한다.
        /// </summary>
        public static bool TryGetRequiredStatType(CapturedMemEntry entry, out ProductionStatType statType)
        {
            statType = default;
            if (entry == null) return false;

            var facilities = Object.FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
            foreach (var facility in facilities)
            {
                if (facility == null || facility.buildingData == null) continue;
                if (facility.DeployedMemEntries != null && facility.DeployedMemEntries.Contains(entry))
                {
                    statType = ProductionCalculator.GetRequiredStatType(facility.buildingData.buildingType);
                    return true;
                }
            }

            var craftFacilities = Object.FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
            foreach (var craft in craftFacilities)
            {
                if (craft == null || craft.buildingData == null) continue;
                if (craft.DeployedMemEntries != null && craft.DeployedMemEntries.Contains(entry))
                {
                    statType = ProductionCalculator.GetRequiredStatType(craft.buildingData.buildingType);
                    return true;
                }
            }

            return false;
        }
    }
}
