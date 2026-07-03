using System;
using UnityEngine;

namespace HDY.Mem
{
    /// <summary>
    /// 머의 스탯 구조체.
    /// Exploration(탐험)은 개체별로 런타임에 결정되는 값이라 MemData(SO) 기본값은 -99로 둔다.
    /// 소환/포획 데이터에서는 Exploration만 실제값으로 교체되고 나머지 5개 스탯은 SO 값 그대로 복사된다.
    /// </summary>
    [Serializable]
    public struct MemStat
    {
        public int Fabrication;
        public int Logging;
        public int Mining;
        public int Movement;
        public int Production;

        [Tooltip("SO 기본값 -99 = 미설정(런타임에 실제값으로 대체됨)")]
        public int Exploration;
    }
}
