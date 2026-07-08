using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;

namespace HDY.Mem
{
    /// <summary>
    /// 멤 데이터(MemData)를 보관하는 매니저.
    /// [교통정리] 멤 데이터 정의는 Pikachu 팀의 MemSystem.Data.MemData를 그대로 사용한다.
    /// Mem_ID(memId)를 키로 하는 딕셔너리 탐색을 전제로 함.
    /// 씬에 배치되어 DontDestroyOnLoad로 유지되는 파괴불가 싱글톤.
    /// [ItemCatalogManager와 동일한 패턴] FindMemData/Resolve 구조를 그대로 맞췄다 - 다른 스크립트가
    /// 이미 ItemCatalogManager를 다루는 방식과 동일하게 MemCatalogManager도 다룰 수 있도록 하기 위함.
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

            BuildDictionary();
        }

        [Header("멤 데이터 목록 (인스펙터에서 등록)")]
        [SerializeField] private List<MemData> memDataList = new List<MemData>();

        public IReadOnlyList<MemData> MemDataList => memDataList;

        [Header("memId -> MemData 딕셔너리")]
        private Dictionary<string, MemData> memDictionary = new Dictionary<string, MemData>();

        /// <summary>memDataList를 memId 기준으로 딕셔너리에 채운다. memId가 중복되면 먼저 등록된 항목을 유지한다.</summary>
        private void BuildDictionary()
        {
            memDictionary.Clear();

            foreach (var data in memDataList)
            {
                if (data == null || string.IsNullOrEmpty(data.memId)) continue;

                if (!memDictionary.ContainsKey(data.memId))
                {
                    memDictionary.Add(data.memId, data);
                }
                else
                {
                    Debug.LogWarning($"[MemCatalogManager] memId가 중복되었습니다: {data.memId} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>memId로 MemData를 찾는다. 목록에 없으면 null.</summary>
        public MemData FindMemData(string memId)
        {
            if (string.IsNullOrEmpty(memId)) return null;
            return memDictionary.TryGetValue(memId, out var data) ? data : null;
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 MemCatalogManager 참조가 비어있을 때 공용으로 쓰는 폴백 탐색.
        /// 1) 이미 참조가 있으면 그대로 반환, 2) 없으면 싱글톤(Instance), 3) 그래도 없으면 씬 전체에서 검색.
        /// (ItemCatalogManager.Resolve와 동일한 패턴)
        /// </summary>
        public static MemCatalogManager Resolve(MemCatalogManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<MemCatalogManager>();
            if (found == null)
            {
                Debug.LogWarning("[MemCatalogManager] 씬에서 MemCatalogManager를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
