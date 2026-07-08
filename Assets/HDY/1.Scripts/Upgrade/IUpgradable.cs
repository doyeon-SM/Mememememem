namespace HDY.Upgrade
{
    /// <summary>
    /// 공용 업그레이드 팝업(UpgradePopupUI)이 다루는 업그레이드 대상이 구현해야 하는 인터페이스.
    /// 팝업은 이 인터페이스만 알고 있으며, 실제로 무엇을 업그레이드하는지(멤창고 페이지, 추후 다른 기능 등)는 모른다.
    ///
    /// [흐름] UpgradePopupUI.Show(target) 호출 -> CanUpgrade()/GetUpgradeCost()로 팝업에 표시할 내용을 계산
    /// -> 확인 버튼 클릭 시 팝업이 비용(골드/재료)을 확인하고 차감까지 마치면 ApplyUpgrade()를 호출한다.
    /// 즉 ApplyUpgrade()가 호출되는 시점에는 비용 지불이 이미 끝난 상태이므로, 구현체는 실제 효과만 적용하면 된다.
    /// </summary>
    public interface IUpgradable
    {
        /// <summary>팝업 상단에 표시할 제목 (예: "멤창고 페이지 확장").</summary>
        string GetUpgradeTitle();

        /// <summary>
        /// 확인(업그레이드) 버튼에 표시할 짧은 설명 (예: "2 → 3"). 팝업에 별도 설명 영역이 없고 버튼 라벨로
        /// 바로 쓰이므로, 한 줄에 들어갈 만큼 짧게 반환해야 한다. 자세한 설명이 필요하면 GetUpgradeTitle 쪽에서 다룬다.
        /// </summary>
        string GetUpgradeDescription();

        /// <summary>지금 업그레이드를 시도할 수 있는 상태인지(이미 최대치에 도달했다면 false).</summary>
        bool CanUpgrade();

        /// <summary>이번 업그레이드 1회에 필요한 비용(골드/재료). 팝업이 열릴 때마다 다시 호출되므로 항상 최신 상태를 반환해야 한다.</summary>
        UpgradeCost GetUpgradeCost();

        /// <summary>비용 지불이 끝난 뒤 실제 업그레이드 효과를 적용한다. 비용 검사/차감은 팝업이 이미 끝낸 뒤에 호출된다.</summary>
        void ApplyUpgrade();
    }
}
