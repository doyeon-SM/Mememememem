using System;
using System.Collections.Generic;

namespace HDY.Tutorial
{
    /// <summary>
    /// 튜토리얼 진행 상태의 순수 데이터 스냅샷. MonoBehaviour/ScriptableObject가 아닌 일반 클래스라서
    /// JsonUtility 등으로 바로 직렬화할 수 있다.
    ///
    /// [저장 연동 완료] Kyusoo의 TutorialRecordData(IRecord)가 TutorialManager.CaptureSnapshot()/
    /// ApplySnapshot(snapshot)을 그대로 호출해 세이브 파일에 직렬화/복원한다.
    ///
    /// [HDY 요청 - 대사 재출력 버그 수정] currentDialogueLineIndex(지금까지 넘긴 대사 줄 번호)가
    /// 원래 이 스냅샷에 없었다. 그래서 씬 재진입/재접속으로 ApplySnapshot이 호출되면 currentStepIndex는
    /// 정확히 복원되는데 currentDialogueLineIndex는 항상 기본값(0)으로 남아, 이미 대사를 다 본 뒤
    /// 목표만 남은 스텝에서도 대사가 처음부터 다시 나오는 문제가 있었다. 이 필드를 추가해서 같이
    /// 저장/복원하도록 고쳤다(TutorialManager.CaptureSnapshot/ApplySnapshot 참고).
    /// </summary>
    [Serializable]
    public class TutorialProgressSnapshot
    {
        public int currentStepIndex = -1;
        public bool currentStepAwaitingTrigger;
        public int currentDialogueLineIndex;
        public List<string> completedStepIds = new List<string>();

        // Dictionary는 JsonUtility가 직렬화하지 못해 키/값을 병렬 리스트로 나눠 저장한다.
        public List<string> objectiveProgressKeys = new List<string>();
        public List<int> objectiveProgressValues = new List<int>();
    }
}
