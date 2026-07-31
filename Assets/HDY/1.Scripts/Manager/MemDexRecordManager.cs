using System;
using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;
using MemSystem.Events;
using PikachuMem = MemSystem.Core.Mem;

namespace HDY.Capture
{
    /// <summary>
    /// 멤 종(memId)별 "최초 포획" 기록 하나. 그 종을 처음 포획한 시각만 담는다.
    /// [저장/불러오기 연동] 세이브 파일에 그대로 직렬화되는 형태라 [Serializable]이다 - CapturedMemEntry가
    /// SaveData.serializedCapturedMems에 그대로 저장되는 것과 동일한 방식.
    /// </summary>
    [Serializable]
    public class MemDexRecord
    {
        public string MemId;

        /// <summary>최초 포획 시각 (UTC Unix timestamp, 초 단위). MemSnapshot.capturedTimestamp와 동일한 형식.</summary>
        public long FirstCapturedTimestamp;
    }

    /// <summary>
    /// 멤 도감의 "최초 포획" 기록을 관리하는 매니저.
    ///
    /// [MemCaptureManager와 다른 점 - 왜 따로 만들었나] MemCaptureManager는 "지금 창고에 들어있는 개체
    /// 목록"만 관리한다 - 슬롯을 드래그로 옮기거나 창고가 가득 차서 방생되면 그 기록 자체가 사라지거나
    /// 바뀌므로, "이 종을 한 번이라도 포획한 적이 있는가"라는 영구적인 도감 발견 여부를 판단하는 용도로는
    /// 쓸 수 없다. 그래서 이 매니저는 MemCaptureManager를 거치지 않고 Pikachu 팀의 MemEvents.OnMemCaptured를
    /// 직접 구독한다 - MemEvents.cs 주석에도 "[도감 시스템] OnMemCaptured: 최초 포획 시 도감 등록 처리"라고
    /// 이미 명시되어 있던 부분이다. 이렇게 하면 창고가 가득 차서 그 개체가 곧바로 방생되더라도(포획 자체는
    /// 성공했으므로) 도감에는 정상적으로 "발견됨"으로 기록된다 - 창고 저장 성공 여부와 도감 발견 여부는
    /// 서로 별개의 문제이기 때문이다.
    ///
    /// [기록 시점] 같은 종을 이미 발견한 뒤에 또 포획해도 최초 기록은 덮어쓰지 않는다(가장 처음 값 유지).
    /// OnFirstCaptureRecorded 이벤트도 정말 "처음 발견되는 순간"에만 발행되고, 이미 발견된 종을 다시
    /// 포획할 때는 발행되지 않는다.
    ///
    /// [씬 배치 - HDY 요청] MemCatalogManager/MemCaptureManager와 동일하게 씬에 미리 배치해두는 파괴불가
    /// 싱글톤이다(자동 생성하지 않음 - Kyusoo의 저장/불러오기 데이터 배치 규칙에 맞춰 MemCatalogManager와
    /// 같은 Managers 오브젝트에 함께 둘 예정).
    ///
    /// [HDY 요청 - 저장/불러오기 연동] Kyusoo 쪽 저장 시스템(RecordManager/IRecord 패턴, 예:
    /// MemCaptureManager와 짝을 이루는 MemRecordData.cs 참고)이 이 매니저와 연동할 수 있도록 아래 3가지를
    /// 공개한다:
    /// 1) OnFirstCaptureRecorded 이벤트 - MemCaptureManager.OnCapturedMemsChanged와 동일한 역할(데이터가
    ///    바뀌는 시점을 알려주는 훅)을 한다. 이 이벤트를 구독해서 실시간 저장(SaveData 호출)을 트리거하면 된다.
    /// 2) Records(읽기 전용 프로퍼티) - 지금까지 쌓인 기록 전체를 그대로 읽어서 SaveData의 새 필드(예:
    ///    serializedMemDexRecords 같은 리스트)에 복사해 저장할 때 쓴다. MemCaptureManager.CapturedMems와
    ///    동일한 역할.
    /// 3) LoadRecords(loadedRecords) - 불러온 세이브 데이터를 이 매니저에 그대로 주입한다. MemRecordData가
    ///    MemCaptureManager의 private capturedMems 필드에 리플렉션으로 직접 접근하던 것과 달리, 이 매니저는
    ///    처음부터 공개 메서드로 제공해서 리플렉션 없이 안전하게 불러오기를 연결할 수 있게 했다(records와
    ///    조회용 딕셔너리를 이 메서드 하나가 한 번에 정합성 있게 교체해준다).
    /// 저장 시점 가드(RecordManager.IsLoadingData 확인 등)는 Kyusoo 쪽 IRecord 구현체(예: MemDexRecordData)의
    /// 책임이다 - 이 매니저는 순수하게 데이터 보관 + 이벤트 발행만 담당한다.
    /// </summary>
    public class MemDexRecordManager : MonoBehaviour
    {
        public static MemDexRecordManager Instance { get; private set; }

        [Header("최초 포획 기록 (memId -> 기록, 저장/불러오기 연동 대상)")]
        [SerializeField] private List<MemDexRecord> records = new List<MemDexRecord>();

        private readonly Dictionary<string, MemDexRecord> recordLookup = new Dictionary<string, MemDexRecord>();

        /// <summary>
        /// [저장/불러오기 연동용] 지금까지 쌓인 최초 포획 기록 전체. 저장 시스템이 이 값을 그대로
        /// SaveData 쪽 리스트로 복사해서 저장하면 된다(MemCaptureManager.CapturedMems와 동일한 역할).
        /// </summary>
        public IReadOnlyList<MemDexRecord> Records => records;

        /// <summary>
        /// 어떤 멤 종이 최초로 포획되어 도감에 새로 기록되는 순간 발행된다. (memId, 최초 포획 시각(UTC Unix, 초))
        /// 이미 발견된 종을 다시 포획할 때는 발행되지 않는다 - 정말 "처음" 발견될 때만 한 번 발행된다.
        /// [저장/불러오기 연동용] MemCaptureManager.OnCapturedMemsChanged와 동일하게, 이 이벤트를 구독해서
        /// 실시간 저장을 트리거할 수 있다.
        /// </summary>
        public event Action<string, long> OnFirstCaptureRecorded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MemDexRecordManager] 씬에 MemDexRecordManager가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            RebuildLookup();
        }

        private void OnEnable()
        {
            MemEvents.OnMemCaptured += HandleMemCaptured;
        }

        private void OnDisable()
        {
            MemEvents.OnMemCaptured -= HandleMemCaptured;
        }

        /// <summary>인스펙터(또는 LoadRecords)에 채워진 records 리스트로부터 조회용 딕셔너리를 다시 만든다.</summary>
        private void RebuildLookup()
        {
            recordLookup.Clear();

            foreach (var record in records)
            {
                if (record == null || string.IsNullOrEmpty(record.MemId)) continue;

                if (!recordLookup.ContainsKey(record.MemId))
                {
                    recordLookup.Add(record.MemId, record);
                }
                else
                {
                    Debug.LogWarning($"[MemDexRecordManager] memId가 중복된 기록이 있습니다: {record.MemId} (먼저 등록된 항목을 유지합니다)", this);
                }
            }
        }

        /// <summary>
        /// Pikachu 팀의 포획 성공 이벤트를 직접 구독한다. MemCaptureManager의 창고 저장 성공/실패와
        /// 무관하게, "포획 자체가 성공했다"는 사실만으로 도감에 기록한다.
        /// </summary>
        private void HandleMemCaptured(PikachuMem mem, MemSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.memId)) return;

            if (recordLookup.ContainsKey(snapshot.memId)) return; // 이미 발견된 종 - 최초 기록은 덮어쓰지 않는다.

            var record = new MemDexRecord
            {
                MemId = snapshot.memId,
                FirstCapturedTimestamp = snapshot.capturedTimestamp
            };

            records.Add(record);
            recordLookup.Add(snapshot.memId, record);

            Debug.Log($"[MemDexRecordManager] 최초 포획 기록: MemId={snapshot.memId}, Timestamp={snapshot.capturedTimestamp}");

            OnFirstCaptureRecorded?.Invoke(snapshot.memId, snapshot.capturedTimestamp);
        }

        /// <summary>이 종(memId)을 한 번이라도 포획한 적이 있는지(도감에 발견되었는지) 여부.</summary>
        public bool IsDiscovered(string memId)
        {
            if (string.IsNullOrEmpty(memId)) return false;
            return recordLookup.ContainsKey(memId);
        }

        /// <summary>이 종(memId)의 최초 포획 기록을 찾는다. 발견되지 않았으면 false.</summary>
        public bool TryGetFirstCaptureInfo(string memId, out MemDexRecord record)
        {
            if (string.IsNullOrEmpty(memId))
            {
                record = null;
                return false;
            }

            return recordLookup.TryGetValue(memId, out record);
        }

        /// <summary>
        /// [저장/불러오기 연동용] 세이브 파일에서 불러온 기록 목록을 이 매니저에 그대로 주입한다.
        /// records와 조회용 딕셔너리(recordLookup)를 한 번에 정합성 있게 교체하므로, 저장/불러오기
        /// 시스템은 MemCaptureManager 때처럼 private 필드에 리플렉션으로 직접 접근할 필요가 없다 -
        /// 씬 로드 후(불러오기 시점) 이 메서드 한 번만 호출하면 된다. 중복 memId는 먼저 온 항목을 유지한다.
        /// </summary>
        public void LoadRecords(IEnumerable<MemDexRecord> loadedRecords)
        {
            records.Clear();
            recordLookup.Clear();

            if (loadedRecords != null)
            {
                foreach (var record in loadedRecords)
                {
                    if (record == null || string.IsNullOrEmpty(record.MemId)) continue;
                    if (recordLookup.ContainsKey(record.MemId)) continue;

                    records.Add(record);
                    recordLookup.Add(record.MemId, record);
                }
            }

            Debug.Log($"[MemDexRecordManager] 저장된 최초 포획 기록 불러오기 완료: {records.Count}개");
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 MemDexRecordManager 참조가 비어있을 때 공용으로 쓰는 폴백 탐색.
        /// 1) 이미 참조가 있으면 그대로 반환, 2) 없으면 싱글톤(Instance), 3) 그래도 없으면 씬 전체에서 검색.
        /// (MemCatalogManager.Resolve와 동일한 패턴)
        /// </summary>
        public static MemDexRecordManager Resolve(MemDexRecordManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<MemDexRecordManager>();
            if (found == null)
            {
                Debug.LogWarning("[MemDexRecordManager] 씬에서 MemDexRecordManager를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
