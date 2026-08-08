using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HDY.Tutorial
{
    /// <summary>
    /// 튜토리얼 스텝 시트(쉼표 구분 CSV)를 파싱해 TutorialStepData 런타임 인스턴스 목록을 만드는 매니저.
    /// HDY.Item.ItemCatalogManager와 동일한 패턴 - Awake 시 시트를 읽어 CSV 행마다
    /// ScriptableObject.CreateInstance&lt;TutorialStepData&gt;()로 채운다.
    ///
    /// [시트 컬럼] Step_ID, Trigger_Type, Trigger_Param, Dialogue_Lines, Objectives, Rewards, Highlight_Key
    /// - Trigger_Type: TutorialTriggerType enum 이름 그대로 (Manual, SceneEnter, LevelReached,
    ///   ObjectSighted, MemSighted, WaypointSighted, ChestSighted, MemCaptured, ChestOpened,
    ///   WaypointUnlocked, UIPanelOpened)
    /// - Trigger_Param: 트리거 종류에 따라 의미가 다름 (SceneEnter=씬 이름 / LevelReached=레벨 숫자 /
    ///   UIPanelOpened=패널 식별 키 / 나머지=이후 배치의 바인더가 정의하는 값. 지금은 비워둬도 됨)
    /// - Dialogue_Lines: 대사 여러 줄을 세미콜론(;)으로 구분. 예) "대사1;대사2;대사3"
    /// - Objectives: "목표키:표시이름:목표수량" 형식을 세미콜론으로 여러 개 나열.
    ///   예) "item_iron:철 주괴:1;item_woodplank:나무판자:1"
    /// - Rewards: "아이템ID:수량" 또는 "아이템ID:수량:refined" 형식을 세미콜론으로 여러 개 나열.
    ///   예) "item_baseblueprint:1;tool_shabby_axe:1:refined". 특수 아이템ID "gold"는 인벤토리가 아니라
    ///   TerritoryData.AddGold로 지급된다(예: "gold:100"). "refined"를 붙이면 지급 직후
    ///   PlayerDefaultItemTest와 동일한 고정 연마(Rare/DamageIncrease/1)가 강제 적용된다
    ///   (TutorialManager.GrantRewards 참고).
    /// - Highlight_Key: TutorialUIHighlightTarget에 등록된 UI 요소의 키. 비워두면 강조 없음(단, 시야
    ///   감지 트리거로 활성화된 스텝은 이 값이 비어있어도 감지된 월드 오브젝트를 자동으로 강조한다)
    ///
    /// [순서 = 진행 순서] TerritoryExpansionSteps/RecipeUnlocks와 동일하게, 시트의 행 순서가 곧
    /// 튜토리얼 진행 순서다(포지셔널 인덱스 하드 제약) - 순서를 바꾸고 싶으면 시트에서 행을 옮기면 된다.
    ///
    /// [쉼표 포함 대사 지원] ItemCatalogManager는 데이터에 쉼표가 없다는 전제로 단순 Split(',')를
    /// 쓰지만, 튜토리얼 대사에는 쉼표가 자연스럽게 들어가므로 큰따옴표로 감싼 필드의 쉼표는 무시하는
    /// 파서(SplitCsvLine)를 따로 쓴다. 엑셀에서 쉼표가 든 셀을 저장하면 자동으로 큰따옴표로 감싸주므로
    /// 특별히 신경 쓸 필요는 없다. 단, 세미콜론(;)과 콜론(:)은 위 형식에서 구분자로 예약되어 있으니
    /// 대사/이름 안에는 쓰지 않는다.
    /// </summary>
    public class TutorialCatalogManager : MonoBehaviour
    {
        public static TutorialCatalogManager Instance { get; private set; }

        [Header("튜토리얼 스텝 시트 (쉼표 구분 CSV, 행 순서 = 진행 순서)")]
        [SerializeField] private TextAsset tutorialStepSheet;

        private readonly List<TutorialStepData> stepList = new List<TutorialStepData>();
        public IReadOnlyList<TutorialStepData> AllSteps => stepList;

        private readonly Dictionary<string, TutorialStepData> stepDictionary = new Dictionary<string, TutorialStepData>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildFromSheet();
        }

        /// <summary>
        /// 시트를 파싱해 행마다 런타임 TutorialStepData 인스턴스를 만들고 순서 리스트 + Step_ID
        /// 딕셔너리에 채운다. Step_ID가 중복되면 먼저 등록된 항목을 유지한다.
        /// </summary>
        private void BuildFromSheet()
        {
            stepList.Clear();
            stepDictionary.Clear();

            if (tutorialStepSheet == null)
            {
                Debug.LogWarning("[TutorialCatalogManager] tutorialStepSheet가 비어있습니다.");
                return;
            }

            var lines = tutorialStepSheet.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = SplitCsvLine(line);
                if (cols.Count < 7)
                {
                    Debug.LogWarning($"[TutorialCatalogManager] 튜토리얼 시트 {i + 1}번째 줄 컬럼 수가 부족합니다: {line}");
                    continue;
                }

                var step = ParseStepRow(cols);
                if (step == null || string.IsNullOrEmpty(step.stepId)) continue;

                if (stepDictionary.ContainsKey(step.stepId))
                {
                    Debug.LogWarning($"[TutorialCatalogManager] Step_ID가 중복되었습니다: {step.stepId} (먼저 등록된 항목을 유지합니다)");
                    continue;
                }

                stepDictionary.Add(step.stepId, step);
                stepList.Add(step);
            }
        }

        /// <summary>시트 한 줄(컬럼 배열)을 런타임 TutorialStepData로 변환한다.</summary>
        private TutorialStepData ParseStepRow(List<string> cols)
        {
            var step = ScriptableObject.CreateInstance<TutorialStepData>();

            step.stepId = cols[0].Trim();
            step.triggerType = ParseTriggerType(cols[1]);
            step.triggerParam = cols[2].Trim();
            step.dialogueLines = ParseDialogueLines(cols[3]);
            step.objectives = ParseObjectives(cols[4]);
            step.rewards = ParseRewards(cols[5]);
            step.highlightKey = cols[6].Trim();

            return step;
        }

        private static TutorialTriggerType ParseTriggerType(string s)
        {
            return System.Enum.TryParse(s.Trim(), out TutorialTriggerType value) ? value : TutorialTriggerType.Manual;
        }

        /// <summary>"대사1;대사2;대사3" 형식을 파싱한다. 빈 문자열이면 빈 리스트를 반환한다.</summary>
        private static List<TutorialDialogueLine> ParseDialogueLines(string raw)
        {
            var lines = new List<TutorialDialogueLine>();
            if (string.IsNullOrWhiteSpace(raw)) return lines;

            foreach (var entry in raw.Split(';'))
            {
                var text = entry.Trim();
                if (string.IsNullOrEmpty(text)) continue;
                lines.Add(new TutorialDialogueLine { text = text });
            }
            return lines;
        }

        /// <summary>"item_iron:철 주괴:1;item_woodplank:나무판자:1" 형식을 파싱한다.</summary>
        private static List<TutorialObjectiveEntry> ParseObjectives(string raw)
        {
            var objectives = new List<TutorialObjectiveEntry>();
            if (string.IsNullOrWhiteSpace(raw)) return objectives;

            foreach (var entry in raw.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                var parts = entry.Split(':');
                if (parts.Length != 3) continue;

                if (!int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                {
                    continue;
                }

                objectives.Add(new TutorialObjectiveEntry
                {
                    objectiveKey = parts[0].Trim(),
                    displayLabel = parts[1].Trim(),
                    targetAmount = amount
                });
            }
            return objectives;
        }

        /// <summary>
        /// "item_baseblueprint:1" 또는 "tool_shabby_axe:1:refined" 형식을 파싱한다. 세 번째 값이
        /// "refined"(대소문자 무관)이면 applyFixedToolRefinement를 켠다. "gold:100"처럼 itemId가 "gold"인
        /// 항목도 이 메서드에서는 그냥 평범한 보상 항목으로 파싱되고, 실제 분기 처리는
        /// TutorialManager.GrantRewards에서 한다.
        /// </summary>
        private static List<TutorialRewardEntry> ParseRewards(string raw)
        {
            var rewards = new List<TutorialRewardEntry>();
            if (string.IsNullOrWhiteSpace(raw)) return rewards;

            foreach (var entry in raw.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                var parts = entry.Split(':');
                if (parts.Length != 2 && parts.Length != 3) continue;

                if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                {
                    continue;
                }

                bool refined = parts.Length == 3 &&
                    string.Equals(parts[2].Trim(), "refined", StringComparison.OrdinalIgnoreCase);

                rewards.Add(new TutorialRewardEntry
                {
                    itemId = parts[0].Trim(),
                    amount = amount,
                    applyFixedToolRefinement = refined
                });
            }
            return rewards;
        }

        /// <summary>
        /// 큰따옴표로 감싼 필드 안의 쉼표는 구분자로 취급하지 않는 CSV 한 줄 파서(RFC4180 기반 단순 구현).
        /// 대사 텍스트에 쉼표가 들어가는 경우를 대비한 것 - ItemCatalogManager의 단순 Split(',')와 다르다.
        /// </summary>
        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++; // 이스케이프된 큰따옴표(""") - 한 글자로 합쳐 담고 한 글자 더 건너뜀
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }

            result.Add(current.ToString());
            return result;
        }

        /// <summary>Step_ID로 TutorialStepData를 찾는다. 목록에 없으면 null.</summary>
        public TutorialStepData FindStep(string stepId)
        {
            if (string.IsNullOrEmpty(stepId)) return null;
            return stepDictionary.TryGetValue(stepId, out var step) ? step : null;
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 TutorialCatalogManager 참조가 비어있을 때 쓰는 공용 폴백 탐색.
        /// ItemCatalogManager.Resolve(existing)와 동일한 패턴.
        /// </summary>
        public static TutorialCatalogManager Resolve(TutorialCatalogManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<TutorialCatalogManager>();
            if (found == null)
            {
                Debug.LogWarning("[TutorialCatalogManager] 씬에서 TutorialCatalogManager를 찾을 수 없습니다.");
            }
            return found;
        }
    }
}
