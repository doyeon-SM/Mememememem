namespace HDY.Tutorial
{
    /// <summary>
    /// 튜토리얼 스텝이 활성화되는 조건의 종류.
    ///
    /// [확장 방법] 새 조건이 필요해지면 여기에 항목만 추가하고, TutorialManager.NotifyTriggerFired(...)를
    /// 호출해주는 바인더를 하나 만들면 된다. TutorialManager 핵심 로직이나 기존 스텝 데이터는 건드릴
    /// 필요가 없다.
    /// </summary>
    public enum TutorialTriggerType
    {
        /// <summary>이전 스텝이 끝나는 즉시 별도 조건 없이 활성화.</summary>
        Manual,

        /// <summary>특정 씬에 진입했을 때. TriggerParam = 씬 이름 (예: "Main_World_2").</summary>
        SceneEnter,

        /// <summary>영지 레벨이 특정 값 이상에 도달했을 때. TriggerParam = 레벨(정수 문자열).</summary>
        LevelReached,

        // ===== 아래는 이후 배치(시야 감지 / 멤 포획 / 상자 / 웨이포인트 / 생산 감시 / UI 패널 오픈
        // 감지 바인더)에서 실제로 연결할 예정인 자리표시자 항목들. 지금은 NotifyTriggerFired를
        // 호출해줄 발신자가 아직 없어 대기 상태로만 남는다. =====

        /// <summary>월드 오브젝트(채집물)를 카메라 시야에서 처음 포착했을 때. (TutorialSightDetector 연동됨)</summary>
        ObjectSighted,

        /// <summary>멤을 카메라 시야에서 처음 포착했을 때. (TutorialSightDetector 연동됨)</summary>
        MemSighted,

        /// <summary>웨이포인트를 카메라 시야에서 처음 포착했을 때. (TutorialSightDetector 연동됨)</summary>
        WaypointSighted,

        /// <summary>상자를 카메라 시야에서 처음 포착했을 때. (TutorialSightDetector 연동됨)</summary>
        ChestSighted,

        /// <summary>멤 포획에 성공했을 때 (MemEvents.OnMemCaptured 연동 예정).</summary>
        MemCaptured,

        /// <summary>상자를 열었을 때 (Chest.OpenChest 연동 예정).</summary>
        ChestOpened,

        /// <summary>웨이포인트를 처음 해금했을 때 (WayPointManager.OnWayPointUnlocked 연동 예정).</summary>
        WaypointUnlocked,

        /// <summary>
        /// 특정 UI 패널(여신상/탐험대/대장간 등)을 처음 열었을 때. TriggerParam = 패널 식별 키.
        /// UIManager에 "패널 열림" 이벤트가 아직 없어 별도 바인더 배치 전까지는 대기만 한다
        /// (UIManager는 HDY 소유라 이벤트 추가에 크로스팀 이슈는 없음).
        /// </summary>
        UIPanelOpened,
    }
}
