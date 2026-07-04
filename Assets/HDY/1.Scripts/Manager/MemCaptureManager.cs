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
    /// </summary>
    [Serializable]
    public class CapturedMemEntry
    {
        public string KeyId;
        public MemSnapshot Snapshot;
    }

    /// <summary>
    /// 포획에 성공한 멤 데이터를 메모리에 보관하는 매니저.
    /// [교통정리] Pikachu 팀의 MemEvents.OnMemCaptured 이벤트를 구독하여 MemSnapshot을 받아,
    /// KeyId(GUID)를 부여한 뒤 CapturedMemEntry로 저장한다.
    /// 파일 저장 등 영속화 로직은 다음 단계에서 추가 예정.
    /// </summary>
    public class MemCaptureManager : MonoBehaviour
    {
        [Header("포획된 멤 목록 (런타임 메모리 보관)")]
        [SerializeField] private List<CapturedMemEntry> capturedMems = new List<CapturedMemEntry>();

        public IReadOnlyList<CapturedMemEntry> CapturedMems => capturedMems;

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
                Snapshot = snapshot
            };

            capturedMems.Add(entry);
            Debug.Log($"[MemCaptureManager] 포획 데이터 저장: KeyId={entry.KeyId} / {snapshot}");
        }
    }
}
