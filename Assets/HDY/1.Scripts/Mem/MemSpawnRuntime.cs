using UnityEngine;

namespace Mem.Mem
{
    /// <summary>
    /// 오브젝트 풀링으로 소환된 머 인스턴스에 붙는 런타임 데이터 컴포넌트.
    /// [TEST] 현재는 SO 참조와 탐험(Exploration) 스탯만 노출. 이후 확장 예정.
    /// </summary>
    public class MemSpawnRuntime : MonoBehaviour
    {
        [Header("참조")]
        public MemData MemSO;

        [Header("스탯 (TEST: 탐험 스탯만 노출)")]
        public int Exploration;
    }
}
