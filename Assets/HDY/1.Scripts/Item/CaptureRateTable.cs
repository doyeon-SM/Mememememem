using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mem.Capture
{
    /// <summary>
    /// 등급별 HP 브래킷 확률 보정치 (100% / 60% / 30% / 3% 4단계).
    /// 값은 비워둔 채로 생성되며, 이후 기획자가 직접 채워넣는다.
    /// </summary>
    [Serializable]
    public struct HPBracketRates
    {
        [Tooltip("HP비율 > 60% 구간 확률 보정치")]
        public float At100;
        [Tooltip("30% < HP비율 <= 60% 구간 확률 보정치")]
        public float At60;
        [Tooltip("3% < HP비율 <= 30% 구간 확률 보정치")]
        public float At30;
        [Tooltip("HP비율 <= 3% 구간 확률 보정치")]
        public float At3;
    }

    [Serializable]
    public class MemClassRateEntry
    {
        public CommonClass MemClass;
        public HPBracketRates Rates;
    }

    /// <summary>
    /// 멤 등급별 HP 브래킷 포획 확률 보정 테이블 (전역 SO).
    /// </summary>
    [CreateAssetMenu(fileName = "CaptureRateTable", menuName = "Mem/Capture Rate Table", order = 0)]
    public class CaptureRateTable : ScriptableObject
    {
        [Header("등급별 HP브래킷 확률 (값은 직접 채워넣을 것)")]
        public List<MemClassRateEntry> Entries = new List<MemClassRateEntry>();

        /// <summary>
        /// 멤 등급과 현재 HP비율(0~1)에 해당하는 보정 확률을 반환.
        /// </summary>
        public float GetRate(CommonClass memClass, float hpRatio01)
        {
            foreach (var entry in Entries)
            {
                if (entry.MemClass != memClass) continue;

                if (hpRatio01 > 0.6f) return entry.Rates.At100;
                if (hpRatio01 > 0.3f) return entry.Rates.At60;
                if (hpRatio01 > 0.03f) return entry.Rates.At30;
                return entry.Rates.At3;
            }

            return 0f;
        }
    }
}
