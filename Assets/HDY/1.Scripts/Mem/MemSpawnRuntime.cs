using UnityEngine;

namespace HDY.Mem
{
    /// <summary>
    /// 오브젝트 풀링으로 소환된 머 인스턴스에 붙는 런타임 데이터 컴포넌트.
    /// MemStat은 MemSO 값을 그대로 복사하되 Exploration만 개체별 실제값으로 채워서 사용한다.
    /// </summary>
    public class MemSpawnRuntime : MonoBehaviour
    {
        [Header("참조")]
        public MemData MemSO;

        [Header("스탯 (MemSO 복사본, Exploration만 실제값)")]
        public MemStat MemStat;
    }
}
