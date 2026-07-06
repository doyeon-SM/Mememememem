using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;

namespace HDY.Mem
{
    /// <summary>
    /// 멤 데이터(MemData)를 보관하는 매니저.
    /// [교통정리] 멤 데이터 정의는 Pikachu 팀의 MemSystem.Data.MemData를 그대로 사용한다.
    /// Mem_ID(memId)를 키로 하는 딕셔너리 탐색을 전제로 함.
    /// 현재 단계에서는 데이터 컨테이너 역할만 하며, 로드/탐색 등의 기능은 추후 추가 예정.
    /// 씬에 배치되어 DontDestroyOnLoad로 유지되는 파괴불가 싱글톤.
    /// </summary>
    public class MemCatalogManager : MonoBehaviour
    {
        public static MemCatalogManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        [Header("멤 데이터 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<MemData> memDataList = new List<MemData>();

        public IReadOnlyList<MemData> MemDataList => memDataList;

        [Header("memId -> MemData 딕셔너리 (추후 채워짐)")]
        private Dictionary<string, MemData> memDictionary = new Dictionary<string, MemData>();
    }
}
