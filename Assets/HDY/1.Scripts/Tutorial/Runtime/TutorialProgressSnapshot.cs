using System;
using System.Collections.Generic;

namespace HDY.Tutorial
{
    /// <summary>
    /// 튜토리얼 진행 상태의 순수 데이터 스냅샷. MonoBehaviour/ScriptableObject가 아닌 일반 클래스라서
    /// JsonUtility 등으로 바로 직렬화할 수 있다.
    ///
    /// [저장 연동 예정] 팀원이 저장 시스템을 붙일 때, TutorialManager.CaptureSnapshot()으로 이 객체를
    /// 받아 원하는 저장 파일에 기록하고, 불러올 때는 TutorialManager.ApplySnapshot(snapshot)에
    /// 그대로 넘기면 된다. 지금은 이 두 메서드를 아무도 호출하지 않으므로, 항상 "최초 시작" 상태로
    /// 테스트된다.
    /// </summary>
    [Serializable]
    public class TutorialProgressSnapshot
    {
        public int currentStepIndex = -1;
        public bool currentStepAwaitingTrigger;
        public List<string> completedStepIds = new List<string>();

        // Dictionary는 JsonUtility가 직렬화하지 못해 키/값을 병렬 리스트로 나눠 저장한다.
        public List<string> objectiveProgressKeys = new List<string>();
        public List<int> objectiveProgressValues = new List<int>();
    }
}
