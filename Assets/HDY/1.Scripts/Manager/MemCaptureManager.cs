using System;
using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;
using MemSystem.Events;
using PikachuMem = MemSystem.Core.Mem;

namespace HDY.Capture
{
    /// <summary>
    /// 포획된 멤 한 마리를 식별하기 위한 저장 항목.
    /// KeyId는 GUID 문자열이며, 방출(삭제)되어도 재사용하지 않는다.
    /// 이후 저장 정렬이나 딕셔너리 탐색의 키로 사용된다.
    /// 현재는 KeyId, MemId, 탐험 스탯(ExplorationStat)만 저장한다.
    /// </summary>
    [Serializable]
    public class CapturedMemEntry
    {
        public string KeyId;
        public string MemId;
        public int ExplorationStat;
    }

    /// <summary>
    /// 포획에 성공한 멤 데이터를 메모리에 보관하는 매니저.
    /// [교통정리] Pikachu 팀의 MemEvents.OnMemCaptured 이벤트를 구독하여 MemSnapshot을 받아,
    /// KeyId(GUID)를 부여한 뒤 필요한 값(MemId, 탐험 스탯)만 CapturedMemEntry로 저장한다.
    /// 파일 저장 등 영속화 로직은 다음 단계에서 추가 예정.
    /// 씬에 배치되어 DontDestroyOnLoad로 유지되는 파괴불가 싱글톤.
    /// </summary>
    public class MemCaptureManager : MonoBehaviour
    {
        public static MemCaptureManager Instance { get; private set; }

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

        [Header("포획된 멤 목록 (런타임 메모리 보관)")]
        [SerializeField] private List<CapturedMemEntry> capturedMems = new List<CapturedMemEntry>();

        public IReadOnlyList<CapturedMemEntry> CapturedMems => capturedMems;

        /// <summary>목록에 새 멤이 저장될 때마다 발행. UI 등에서 이 이벤트를 구독해 갱신하면 된다.</summary>
        public event Action OnCapturedMemsChanged;

        [Header("KeyId -> 항목 딕셔너리 (추후 채워짐)")]
        private Dictionary<string, CapturedMemEntry> capturedMemDictionary = new Dictionary<string, CapturedMemEntry>();

        private void OnEnable()
        {
            MemEvents.OnMemCaptured += HandleMemCaptured;
        }

        private void OnDisable()
        {
            MemEvents.OnMemCaptured -= HandleMemCaptured;
        }

        private void HandleMemCaptured(PikachuMem mem, MemSnapshot snapshot)
        {
            var entry = new CapturedMemEntry
            {
                KeyId = Guid.NewGuid().ToString(),
                MemId = snapshot.memId,
                ExplorationStat = snapshot.explorationStat
            };

            capturedMems.Add(entry);
            Debug.Log($"[MemCaptureManager] 포획 데이터 저장: KeyId={entry.KeyId} / MemId={entry.MemId} / Exploration={entry.ExplorationStat}");

            OnCapturedMemsChanged?.Invoke();
        }
    }
}
