public enum FacilityStopReason
{
    Starvation = 0, // 1분 마다 음식을 소비하는데 허기량만큼 소비할 수 있는 음식이 없을 때, 가동 중지
    CompleteCrafting = 1, // 제작 - 목표수량만큼 제작이 진행되며 제작이 완료되었을 때, 가동 중지
    CancelCrafting = 2 // 제작 - 제작을 진행하는 도중 플레이어가 제작 취소를 할 수 있으며, 이때 가동 중지 상태
}