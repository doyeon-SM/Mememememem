namespace HDY.Upgrade
{
    /// <summary>
    /// 재료(아이템) 보유량을 조회/차감하는 인터페이스.
    ///
    /// [TODO] 프로젝트에 아직 재료 재고를 관리하는 인벤토리 시스템이 없다(밥통/FoodStorageEntry는 음식 전용이라
    /// 재사용하지 않음). 실제 재고 시스템이 만들어지면 이 인터페이스를 구현한 컴포넌트를 UpgradePopupUI의
    /// materialInventorySource에 연결하면 된다.
    ///
    /// 그 전까지는 UpgradePopupUI에 구현체가 연결되지 않은 상태로 둬도 문제 없다 - 재료 비용이 있는 업그레이드를
    /// 만나면 재료 조건 검사를 건너뛰고(경고 로그만 남기고) 통과시킨다. 현재 멤창고 페이지 업그레이드는
    /// 골드만 사용하므로 이 인터페이스가 당장 필요하지는 않다.
    /// </summary>
    public interface IMaterialInventory
    {
        /// <summary>itemId 재료를 amount만큼 소비할 수 있을 만큼 보유하고 있는지.</summary>
        bool HasEnough(string itemId, int amount);

        /// <summary>itemId 재료를 amount만큼 실제로 차감한다. HasEnough로 이미 확인된 뒤 호출된다고 가정한다.</summary>
        void Consume(string itemId, int amount);
    }
}
