using UnityEngine;

namespace HDY.Mem
{
    /// <summary>
    /// 개별 머(Mem) 정의 SO.
    /// MemCatalogManager가 Mem_ID를 키로 딕셔너리에 로드하여 탐색하는 것을 전제로 함.
    /// </summary>
    [CreateAssetMenu(fileName = "Mem_", menuName = "HDY/Mem/Mem Data", order = 0)]
    public class MemData : ScriptableObject
    {
        [Header("식별")]
        public string Mem_ID;
        public string MemName;
        public MemClass MemClass;

        [Header("기본 능력치")]
        public int MemHP;
        public int MemHunger;

        [Header("스탯 (Exploration은 개체별 런타임 값이라 기본 -99)")]
        public MemStat MemStat;

        [Header("포획")]
        [Tooltip("시작 포획확률 (0~1)")]
        [Range(0f, 1f)]
        public float BaseCaptureRate;

        // 에셋 최초 생성/Reset 시 Exploration을 미설정 표시값(-99)으로 초기화
        private void Reset()
        {
            MemStat.Exploration = -99;
        }
    }
}
