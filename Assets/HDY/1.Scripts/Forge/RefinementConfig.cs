using System;
using UnityEngine;

namespace HDY.Forge
{
    /// <summary>
    /// 연마 시스템 전역 설정. 도구 종류(도끼/곡괭이/괭이) 공통으로 사용하는 단일 설정 자산.
    ///
    /// [비용] 기본비용은 도구가 가진 "전체 슬롯 중 최고 등급" 기준으로 1회 부과되고,
    /// 잠금비용은 이번 연마에서 잠근 슬롯 각각의 등급 기준으로 합산된다.
    /// 총 연마석 = BaseCost(최고등급) + Σ LockCost(잠근 슬롯의 등급)
    /// 총 골드 = FixedGoldCost (고정)
    /// </summary>
    [CreateAssetMenu(fileName = "RefinementConfig", menuName = "HDY/Forge/Refinement Config", order = 2)]
    public class RefinementConfig : ScriptableObject
    {
        [Header("연마칸 개수 확률 (1~5칸, 도구 제작 즉시 1회 결정)")]
        [Tooltip("인덱스 0=1칸, 1=2칸 ... 4=5칸. 합이 100이 아니어도 상대 가중치로 처리된다.")]
        [SerializeField] private float[] slotCountWeights = new float[] { 20f, 20f, 20f, 20f, 20f };

        [Header("등급변화확률 (공용, 도구 종류 무관)")]
        [Tooltip("인덱스 0=Rare→Epic, 1=Epic→Unique, 2=Unique→Legendary, 3=Legendary→Myth. Myth는 더 오를 곳이 없어 항상 옵션만 재판정된다.")]
        [Range(0f, 1f)]
        [SerializeField] private float[] gradeUpChance = new float[] { 0.3f, 0.2f, 0.1f, 0.05f };

        [Header("기본비용 (연마석, 등급별 - 도구 전체 슬롯 중 최고 등급 기준 1회)")]
        [Tooltip("인덱스는 CommonClass 순서(Rare,Epic,Unique,Legendary,Myth)와 동일. 기본값: 1/5/10/15/20")]
        [SerializeField] private int[] baseCostByGrade = new int[] { 1, 5, 10, 15, 20 };

        [Header("잠금비용 (연마석, 등급별 - 이번 시도에서 잠근 슬롯마다 추가)")]
        [Tooltip("인덱스는 CommonClass 순서와 동일. 기본값: 1/2/3/4/5")]
        [SerializeField] private int[] lockCostByGrade = new int[] { 1, 2, 3, 4, 5 };

        [Header("골드 비용 (고정)")]
        [SerializeField] private int fixedGoldCost = 1000;

        [Header("연마석 재료 Item_ID (단일 아이템, 수량만 다르게 소모)")]
        [SerializeField] private string refinementMaterialItemId;

        [Header("연마 옵션 데이터 (txt, 탭 구분, 컬럼: 등급/종류/수치/확률 - ItemCatalog.txt와 동일한 형식)")]
        [SerializeField] private TextAsset optionDataCsv;

        private RefinementOptionTable optionTable;

        public string RefinementMaterialItemId => refinementMaterialItemId;
        public int FixedGoldCost => fixedGoldCost;

        private void OnEnable()
        {
            BuildOptionTable();
        }

        private void BuildOptionTable()
        {
            var rows = optionDataCsv != null
                ? RefinementOptionCsvParser.Parse(optionDataCsv.text)
                : new System.Collections.Generic.List<RefinementOptionRow>();

            optionTable = new RefinementOptionTable(rows);
        }

        /// <summary>옵션 테이블을 가져온다. txt가 아직 로드되지 않았으면 즉시 빌드한다.</summary>
        public RefinementOptionTable GetOptionTable()
        {
            if (optionTable == null) BuildOptionTable();
            return optionTable;
        }

        /// <summary>도구 제작(첫 등록) 시 슬롯 개수(1~5)를 확률 가중치로 뽑는다.</summary>
        public int RollSlotCount()
        {
            if (slotCountWeights == null || slotCountWeights.Length == 0) return 1;

            float total = 0f;
            foreach (var w in slotCountWeights) total += Mathf.Max(0f, w);

            if (total <= 0f) return 1;

            float roll = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;

            for (int i = 0; i < slotCountWeights.Length; i++)
            {
                cumulative += Mathf.Max(0f, slotCountWeights[i]);
                if (roll <= cumulative) return i + 1; // 인덱스 0 = 1칸
            }

            return slotCountWeights.Length; // 폴백: 최대 칸수
        }

        /// <summary>현재 등급에서 다음 등급으로 오를지 확률 판정한다. 이미 최고 등급(Myth)이면 항상 false.</summary>
        public bool RollGradeUp(CommonClass currentGrade)
        {
            int gradeIndex = (int)currentGrade;

            if (gradeIndex < 0 || gradeIndex >= gradeUpChance.Length) return false; // Myth 등 - 더 오를 곳 없음
            if (gradeUpChance[gradeIndex] <= 0f) return false;

            return UnityEngine.Random.value < gradeUpChance[gradeIndex];
        }

        /// <summary>다음 등급을 반환한다. 이미 최고 등급이면 그대로 반환.</summary>
        public CommonClass GetNextGrade(CommonClass currentGrade)
        {
            int nextIndex = (int)currentGrade + 1;
            int maxIndex = Enum.GetValues(typeof(CommonClass)).Length - 1;
            return (CommonClass)Mathf.Min(nextIndex, maxIndex);
        }

        public bool IsMaxGrade(CommonClass grade)
        {
            int maxIndex = Enum.GetValues(typeof(CommonClass)).Length - 1;
            return (int)grade >= maxIndex;
        }

        public int GetBaseCost(CommonClass grade)
        {
            int index = (int)grade;
            return (baseCostByGrade != null && index >= 0 && index < baseCostByGrade.Length) ? baseCostByGrade[index] : 0;
        }

        public int GetLockCost(CommonClass grade)
        {
            int index = (int)grade;
            return (lockCostByGrade != null && index >= 0 && index < lockCostByGrade.Length) ? lockCostByGrade[index] : 0;
        }
    }
}
