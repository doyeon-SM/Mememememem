using System.Collections.Generic;
using UnityEngine;

namespace HDY.Mem
{
    /// <summary>
    /// 머 데이터(MemData)를 보관하는 매니저.
    /// Mem_ID를 키로 하는 딕셔너리 탐색을 전제로 함.
    /// 현재 단계에서는 데이터 컨테이너 역할만 하며, 로드/탐색 등의 기능은 추후 추가 예정.
    /// </summary>
    public class MemCatalogManager : MonoBehaviour
    {
        [Header("머 데이터 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<MemData> memDataList = new List<MemData>();

        [Header("Mem_ID -> MemData 딕셔너리 (추후 채워짐)")]
        private Dictionary<string, MemData> memDictionary = new Dictionary<string, MemData>();
    }
}
