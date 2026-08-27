/// <summary>
/// (멤) SceneUIManager가 관리하는 UI 패널이 열리고/닫힐 때 알림을 받고 싶으면 구현합니다.
///
/// SceneUIManager는 managedUIObjects에 등록된 GameObject를 열고 닫을 때(SetActive 전환 시점),
/// 그 오브젝트에 이 인터페이스가 붙어 있으면 OnManagedUIOpened()/OnManagedUIClosed()를 호출해줍니다.
/// 이미 열려 있는 패널을 다시 열려고 하거나, 이미 닫혀 있는 패널을 다시 닫으려는 "상태 변화가 없는" 호출은
/// 알림이 오지 않습니다(실제로 화면 상태가 전환될 때만 호출됨).
///
/// 예: 상점 UI는 이 인터페이스로 "열릴 때마다 기본 상점으로 리셋"하는 로직을 자기 자신 안에 둘 수 있고,
/// SceneUIManager는 상점이 무엇인지 전혀 몰라도 됩니다(느슨한 결합).
/// </summary>
public interface IManagedUIPanel
{
    /// <summary>이 패널이 SceneUIManager에 의해 실제로 열렸을 때(닫힌 상태 -> 열린 상태) 호출됩니다.</summary>
    void OnManagedUIOpened();

    /// <summary>이 패널이 SceneUIManager에 의해 실제로 닫혔을 때(열린 상태 -> 닫힌 상태) 호출됩니다.</summary>
    void OnManagedUIClosed();
}
