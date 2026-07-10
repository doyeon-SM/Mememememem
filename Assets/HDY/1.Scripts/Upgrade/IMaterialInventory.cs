namespace HDY.Upgrade
{
    /// <summary>
    /// 재료(아이템) 보유량을 조회/차감하는 인터페이스.
    ///
    /// [구현체] CombinedMaterialInventory(HDY.Inventory)가 실제 구현체다. 인벤토리(PlayerInventory)와
    /// 창고(WarehouseInventory)를 합산해서 확인/차감한다.
    ///
    /// [GetAmount 추가 이력] 원래는 HasEnough/Consume만 있었다(업그레이드 팝업은 "충분한지"만 알면 됐음).
    /// 상점 판매 UI(ShopUI)는 "지금 몇 개를 팔 수 있는지"를 화면에 보여주고 수량 스테퍼의 최대값으로도
    /// 써야 해서, 정확한 보유 수량이 필요해 이 메서드를 추가했다.
    /// </summary>
    public interface IMaterialInventory
    {
        /// <summary>itemId 재료를 amount만큼 소비할 수 있을 만큼 보유하고 있는지.</summary>
        bool HasEnough(string itemId, int amount);

        /// <summary>itemId 재료를 amount만큼 실제로 차감한다. HasEnough로 이미 확인된 뒤 호출된다고 가정한다.</summary>
        void Consume(string itemId, int amount);

        /// <summary>itemId 재료를 현재 몇 개 보유하고 있는지(인벤토리+창고 합산). 상점 판매 UI의 최대 판매 가능 수량 계산 등에 사용한다.</summary>
        int GetAmount(string itemId);
    }
}
