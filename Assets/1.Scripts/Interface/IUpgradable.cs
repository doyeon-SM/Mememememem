namespace HDY.Upgrade
{
    /// <summary>
    /// 공용 업그레이드 팝업(UpgradePopupUI)이 다루는 업그레이드 대상이 구현해야 하는 인터페이스.
    /// 팝업은 이 인터페이스만 알고 있으며, 실제로 무엇을 업그레이드하는지(멤창고 페이지, 추후 다른 기능 등)는 모른다.
    ///
    /// [흐름] UpgradePopupUI.Show(target) 호출 -> CanUpgrade()/GetUpgradeCost()로 팝업에 표시할 내용을 계산
    /// -> 확인 버튼 클릭 시 팝업이 비용(골드/재료)을 확인하고 차감까지 마치면 ApplyUpgrade()를 호출한다.
    /// 즉 ApplyUpgrade()가 호출되는 시점에는 비용 지불이 이미 끝난 상태이므로, 구현체는 실제 효과만 적용하면 된다.
    ///
    /// [HDY 요청 - 헤더/미들/버튼 텍스트 분리] 팝업 텍스트 자리가 제목(헤더) 하나뿐이었는데, 업그레이드마다
    /// 다른 문구를 보여줄 수 있도록 미들 텍스트와 버튼 텍스트를 분리했다. 예전에는 GetUpgradeDescription()
    /// 하나가 확인 버튼 라벨에 \"2 → 3\"처럼 동적인 값을 표시했는데, 이제 그 역할은 GetUpgradeMiddleText()가
    /// 맡고, GetUpgradeDescription()은 GetUpgradeButtonText()로 이름이 바뀌면서 \"확장\"/\"해금\"/\"강화\"처럼
    /// 최대치 여부와 무관하게 항상 같은 고정 문구만 반환하도록 역할이 명확해졌다(버튼의 활성/비활성 표시는
    /// interactable로 이미 따로 처리되므로 문구 자체가 바뀔 필요는 없다).
    /// </summary>
    public interface IUpgradable
    {
        /// <summary>팝업 상단에 표시할 제목 (예: "창고 확장").</summary>
        string GetUpgradeTitle();

        /// <summary>
        /// [HDY 요청] 팝업 중간에 표시할 설명 문구 (예: "추가 10칸 확장 비용", 최대치면 "MAX"). 화면에 실제로
        /// 연결되는 텍스트 오브젝트는 UpgradePopupUI.middleText다.
        /// </summary>
        string GetUpgradeMiddleText();

        /// <summary>
        /// [HDY 요청] 확인(업그레이드) 버튼에 표시할 고정 문구(예: "확장", "해금", "강화"). 최대치 여부와
        /// 무관하게 항상 같은 문구를 반환한다 - 버튼의 활성/비활성(interactable)은 팝업이 CanUpgrade()로
        /// 별도 처리하므로 문구 자체를 바꿀 필요는 없다.
        /// </summary>
        string GetUpgradeButtonText();

        /// <summary>지금 업그레이드를 시도할 수 있는 상태인지(이미 최대치에 도달했다면 false).</summary>
        bool CanUpgrade();

        /// <summary>이번 업그레이드 1회에 필요한 비용(골드/재료). 팝업이 열릴 때마다 다시 호출되므로 항상 최신 상태를 반환해야 한다.</summary>
        UpgradeCost GetUpgradeCost();

        /// <summary>비용 지불이 끝난 뒤 실제 업그레이드 효과를 적용한다. 비용 검사/차감은 팝업이 이미 끝낸 뒤에 호출된다.</summary>
        void ApplyUpgrade();
    }
}
