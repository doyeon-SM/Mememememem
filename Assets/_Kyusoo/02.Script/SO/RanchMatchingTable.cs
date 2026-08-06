using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RanchMatchingEntry
{
    public string Mem_ID;

    public string Item_ID;

    [Tooltip("해당 아이템 1개를 생산하는 데 걸리는 기본 시간(초)")]
    public float BaseProductionTime = 30f;
}

/// <summary>
/// 목장에 배치된 멤 ID에 따라 생산되는 아이템 ID와 기본 생산 시간을 매핑하는 데이터 에셋입니다.
/// </summary>
[CreateAssetMenu(fileName = "RanchMatchingTable", menuName = "KKS/Building/Ranch Matching Table")]
public class RanchMatchingTable : ScriptableObject
{
    [Tooltip("멤 ID별 아이템 및 생산 시간 매핑 목록")]
    public List<RanchMatchingEntry> ProduceEntries = new List<RanchMatchingEntry>();

    /// <summary>
    /// 멤 ID로 매핑 데이터를 조회합니다.
    /// </summary>
    public RanchMatchingEntry FindEntryByMemId(string memId)
    {
        if (string.IsNullOrEmpty(memId) || ProduceEntries == null) return null;
        return ProduceEntries.Find(e => e != null && e.Mem_ID == memId);
    }
}
