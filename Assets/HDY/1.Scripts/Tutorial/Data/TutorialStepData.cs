using System;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.Tutorial
{
    /// <summary>
    /// 튜토리얼 대사 한 줄. highlightTargetKey는 이후 하이라이트 배치에서 사용할 자리로,
    /// 지금은 비워두면 아무 효과도 없다(CSV에는 아직 이 값을 담는 컬럼이 없다).
    /// </summary>
    [Serializable]
    public class TutorialDialogueLine
    {
        [TextArea(1, 3)]
        public string text;

        [Tooltip("(예약) 이후 배치에서 이 줄과 함께 강조 표시할 대상 식별 키. 지금은 미사용")]
        public string highlightTargetKey;
    }

    /// <summary>
    /// 퀘스트형 목표 하나. targetAmount가 1이면 단순 완료형 목표(예: 상자 열기)로도 쓸 수 있다.
    /// 진행 수치(currentAmount)는 여기 담지 않는다 - 이 클래스는 순수 정의 데이터이고,
    /// 실제 진행 상태는 TutorialManager가 런타임에서만 관리한다.
    /// </summary>
    [Serializable]
    public class TutorialObjectiveEntry
    {
        [Tooltip("목표 판정에 쓰는 식별 키. 예: 아이템 ID(item_iron), 또는 바인더가 정의하는 임의의 키")]
        public string objectiveKey;

        [Tooltip("HUD에 표시할 이름. 예: '철 주괴'")]
        public string displayLabel;

        [Min(1)]
        public int targetAmount = 1;
    }

    /// <summary>스텝 완료 시 지급할 보상 하나.</summary>
    [Serializable]
    public class TutorialRewardEntry
    {
        public string itemId;

        [Min(1)]
        public int amount = 1;
    }

    /// <summary>
    /// 튜토리얼 스텝 하나의 정의(순수 데이터). 런타임 진행 상태(현재 대사 인덱스, 목표 진행도,
    /// 완료 여부 등)는 전혀 담지 않으며, 전부 TutorialManager가 별도로 관리한다.
    ///
    /// [HDY 요청 - 시트 마이그레이션] 개별 스텝을 SO 에셋으로 하나씩 만들던 방식에서, ItemCatalogManager와
    /// 동일한 패턴의 CSV 시트 기반으로 전환했다. 이제 이 클래스의 인스턴스는 사람이 직접 Create 메뉴로
    /// 만드는 게 아니라, TutorialCatalogManager가 Awake 시 시트를 파싱해 행마다
    /// ScriptableObject.CreateInstance&lt;TutorialStepData&gt;()로 채워 넣는다. 그래서 CreateAssetMenu를
    /// 붙이지 않았다 - 실수로 개별 에셋을 만들어도 시트 기반 목록에는 포함되지 않는다.
    /// </summary>
    public class TutorialStepData : ScriptableObject
    {
        [Header("식별자")]
        [Tooltip("스텝 고유 ID (CSV Step_ID 컬럼). 시트 내에서 유일해야 함")]
        public string stepId;

        [Header("활성화 조건")]
        public TutorialTriggerType triggerType = TutorialTriggerType.Manual;

        [Tooltip("트리거 종류에 따라 의미가 달라지는 보조 파라미터.\n" +
                 "SceneEnter = 씬 이름 / LevelReached = 레벨 숫자 / 나머지 = 이후 배치의 바인더가 정의하는 값")]
        public string triggerParam;

        [Header("대사 (순서대로 '다음' 버튼으로 진행)")]
        public List<TutorialDialogueLine> dialogueLines = new List<TutorialDialogueLine>();

        [Header("목표 (비워두면 대사만 보고 바로 완료되는 스텝)")]
        public List<TutorialObjectiveEntry> objectives = new List<TutorialObjectiveEntry>();

        [Header("완료 보상")]
        public List<TutorialRewardEntry> rewards = new List<TutorialRewardEntry>();
    }
}
