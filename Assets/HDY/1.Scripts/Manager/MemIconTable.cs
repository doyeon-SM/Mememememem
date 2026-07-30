using System.Collections.Generic;
using UnityEngine;

namespace HDY.Mem
{
    /// <summary>
    /// Mem_ID -> 해상도별 아이콘(Sprite) 매핑 전용 SO.
    ///
    /// [HDY 요청 - 에디터 사전 굽기 전환] 에디터 전용 도구(MemIconBaker)가 MemAppearanceTable의
    /// 3D 모델을 미리 촬영해서 이 테이블을 채운다. 런타임에는 카메라 촬영이 전혀 일어나지 않고
    /// 이 테이블에서 조회만 한다.
    ///
    /// [해상도 3종] 64=시설/창고 슬롯용, 128=도감 슬롯용, 512=멤 정보 큰 아이콘용.
    ///
    /// [폴백] 아직 굽지 않은(엔트리가 없거나 특정 해상도 슬롯이 비어있는) 멤은 fallbackMemId
    /// (기본값 Mem_Rare_01)의 아이콘을 대신 반환한다. 인스펙터에서 다른 memId로 바꿀 수 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "MemIconTable", menuName = "HDY/Mem/Mem Icon Table", order = 2)]
    public class MemIconTable : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string Mem_ID;
            public Sprite Icon64;
            public Sprite Icon128;
            public Sprite Icon512;
        }

        [Header("미굽음 폴백 - 이 Mem_ID의 아이콘을 대신 사용")]
        [SerializeField] private string fallbackMemId = "Mem_Rare_01";

        [Header("Mem_ID -> 해상도별 아이콘 목록 (MemIconBaker가 채움)")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> lookup;

        public string FallbackMemId => fallbackMemId;

        private void BuildLookupIfNeeded()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, Entry>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Mem_ID)) continue;

                if (!lookup.ContainsKey(entry.Mem_ID))
                {
                    lookup.Add(entry.Mem_ID, entry);
                }
                else
                {
                    Debug.LogWarning($"[MemIconTable] Mem_ID가 중복되었습니다: {entry.Mem_ID} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>시설/창고 슬롯용 64px 아이콘을 조회한다. 미굽음이면 fallbackMemId의 아이콘으로 대체.</summary>
        public Sprite GetIcon64(string memId)
        {
            return ResolveSprite(memId, useFallback: true, wantIcon64: true, wantIcon128: false, wantIcon512: false);
        }

        /// <summary>도감 슬롯용 128px 아이콘을 조회한다. 미굽음이면 fallbackMemId의 아이콘으로 대체.</summary>
        public Sprite GetIcon128(string memId)
        {
            return ResolveSprite(memId, useFallback: true, wantIcon64: false, wantIcon128: true, wantIcon512: false);
        }

        /// <summary>멤 정보 큰 아이콘용 512px 아이콘을 조회한다. 미굽음이면 fallbackMemId의 아이콘으로 대체.</summary>
        public Sprite GetIcon512(string memId)
        {
            return ResolveSprite(memId, useFallback: true, wantIcon64: false, wantIcon128: false, wantIcon512: true);
        }

        private Sprite ResolveSprite(string memId, bool useFallback, bool wantIcon64, bool wantIcon128, bool wantIcon512)
        {
            BuildLookupIfNeeded();

            if (!string.IsNullOrEmpty(memId) && lookup.TryGetValue(memId, out var entry))
            {
                var sprite = wantIcon64 ? entry.Icon64 : (wantIcon128 ? entry.Icon128 : entry.Icon512);
                if (sprite != null) return sprite;
            }

            if (useFallback && !string.IsNullOrEmpty(fallbackMemId) &&
                !string.Equals(fallbackMemId, memId) &&
                lookup.TryGetValue(fallbackMemId, out var fallbackEntry))
            {
                return wantIcon64 ? fallbackEntry.Icon64 : (wantIcon128 ? fallbackEntry.Icon128 : fallbackEntry.Icon512);
            }

            return null;
        }

        /// <summary>해당 memId가 폴백이 아니라 실제로 구워진 전용 아이콘을 갖고 있는지 확인.</summary>
        public bool HasDedicatedIcon(string memId)
        {
            if (string.IsNullOrEmpty(memId)) return false;

            BuildLookupIfNeeded();

            return lookup.TryGetValue(memId, out var entry) &&
                   (entry.Icon64 != null || entry.Icon128 != null || entry.Icon512 != null);
        }

#if UNITY_EDITOR
        /// <summary>[MemIconBaker 전용] 항목 전체를 덮어쓴다.</summary>
        public void EditorSetEntries(List<Entry> newEntries)
        {
            entries = newEntries;
            lookup = null;
        }

        /// <summary>
        /// [MemIconBaker 전용 - 부분 재굽기] 특정 memId의 항목 하나만 갱신한다(없으면 추가, 있으면 덮어쓰기).
        /// 기존 리스트 수정 = 덮어쓰기 요구사항을 이 메서드로 처리한다.
        /// </summary>
        public void EditorUpsertEntry(Entry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Mem_ID == entry.Mem_ID)
                {
                    entries[i] = entry;
                    lookup = null;
                    return;
                }
            }

            entries.Add(entry);
            lookup = null;
        }

        /// <summary>[MemIconBaker 전용] 폴백 memId를 인스펙터 밖에서 코드로 바꾸고 싶을 때 사용.</summary>
        public void EditorSetFallbackMemId(string memId)
        {
            fallbackMemId = memId;
        }
#endif
    }
}
