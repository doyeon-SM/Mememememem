using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;
using HDY.Capture;
using HDY.Mem;

namespace HDY.UI
{
    /// <summary>
    /// 멤 창고 UI 컨트롤러.
    /// MemCaptureManager / MemCatalogManager에서 데이터를 가져와 MemStorageUI_Grid와 MemStorageUI_Info에 전달하는 역할만 한다.
    /// 실제 그리드 표시는 MemStorageUI_Grid, 정보 패널 표시는 MemStorageUI_Info가 담당한다.
    /// 그리드에서 드래그앤드롭으로 슬롯 위치 교체가 요청되면, 이 컨트롤러가 MemCaptureManager에 실제 데이터 반영을 지시한다.
    ///
    /// [씬 이동 대응] MemCaptureManager/MemCatalogManager는 파괴불가 싱글톤이라 씬을 이동해도 유지되지만,
    /// 이 컴포넌트는 씬에 배치된 오브젝트라서 씬이 다시 로드되면 인스펙터 참조가 끊길 수 있다.
    /// (씬 파일에 같이 저장된 매니저 오브젝트는 재로드 시 새로 생기지만, 싱글톤 중복 검사로 즉시 파괴되기 때문)
    /// Awake/OnEnable에서 참조가 끊겨 있으면(null) 싱글톤 Instance로 자동 재할당한다.
    /// </summary>
    public class MemStorageUI : MonoBehaviour
    {
        [Header("데이터 참조")]
        [SerializeField] private MemCaptureManager captureManager;
        [SerializeField] private MemCatalogManager catalogManager;

        [Header("하위 UI (그리드 / 정보 패널)")]
        [SerializeField] private MemStorageUI_Grid grid;
        [SerializeField] private MemStorageUI_Info info;

        private void Awake()
        {
            // 씬 재로드 등으로 인스펙터 참조가 끊겼으면 파괴불가 싱글톤에서 다시 가져온다.
            if (captureManager == null) captureManager = MemCaptureManager.Instance;
            if (catalogManager == null) catalogManager = MemCatalogManager.Instance;

            if (captureManager == null) Debug.LogWarning("[MemStorageUI] captureManager가 비어있습니다. 포획 데이터를 읽어올 수 없습니다.", this);
            if (catalogManager == null) Debug.LogWarning("[MemStorageUI] catalogManager가 비어있습니다. 멤 SO 정보를 찾을 수 없습니다.", this);
            if (grid == null) Debug.LogWarning("[MemStorageUI] grid가 비어있습니다.", this);
            if (info == null) Debug.LogWarning("[MemStorageUI] info가 비어있습니다.", this);

            if (grid != null)
            {
                grid.OnSlotClicked += HandleSlotClicked;
                grid.OnSwapRequested += HandleSwapRequested;
            }
        }

        private void OnEnable()
        {
            // Awake 이후에도 혹시 끊겨 있다면(실행 순서 문제 등) 한 번 더 보정.
            if (captureManager == null) captureManager = MemCaptureManager.Instance;
            if (catalogManager == null) catalogManager = MemCatalogManager.Instance;

            if (captureManager != null)
            {
                captureManager.OnCapturedMemsChanged += HandleCapturedMemsChanged;
            }

            if (grid != null && captureManager != null)
            {
                grid.ShowInitial(captureManager.CapturedMems, FindMemData);
            }
        }

        private void OnDisable()
        {
            if (captureManager != null)
            {
                captureManager.OnCapturedMemsChanged -= HandleCapturedMemsChanged;
            }

            if (grid != null)
            {
                grid.OnSlotClicked -= HandleSlotClicked;
                grid.OnSwapRequested -= HandleSwapRequested;
            }
        }

        /// <summary>새로 멤이 포획되거나 슬롯 위치가 바뀌는 등 데이터가 바뀔 때마다 호출된다.</summary>
        private void HandleCapturedMemsChanged()
        {
            Debug.Log("[MemStorageUI] OnCapturedMemsChanged 수신 -> 그리드 갱신 시도");

            if (grid != null && captureManager != null)
            {
                grid.NotifyDataChanged(captureManager.CapturedMems, FindMemData);
            }
        }

        private void HandleSlotClicked(CapturedMemEntry entry, MemData data)
        {
            Debug.Log($"[MemStorageUI] 슬롯 클릭 처리: MemId={entry.MemId}, Exploration={entry.ExplorationStat}");
            if (info != null)
            {
                info.ShowInfo(entry, data);
            }
        }

        /// <summary>그리드에서 드래그앤드롭으로 슬롯 위치 교체가 요청되었을 때 호출. 실제 데이터(MemCaptureManager)에 반영한다.</summary>
        private void HandleSwapRequested(int indexA, int indexB)
        {
            Debug.Log($"[MemStorageUI] 슬롯 교체 요청 수신: index {indexA} <-> {indexB}");

            if (captureManager == null)
            {
                Debug.LogWarning("[MemStorageUI] captureManager가 비어있어 슬롯 교체를 처리할 수 없습니다.", this);
                return;
            }

            captureManager.SwapEntries(indexA, indexB);
        }

        /// <summary>MemCatalogManager에 등록된 SO 목록에서 memId가 일치하는 MemData를 찾는다.</summary>
        private MemData FindMemData(string memId)
        {
            if (catalogManager == null) return null;
            return catalogManager.MemDataList.FirstOrDefaultByMemId(memId);
        }
    }

    /// <summary>MemCatalogManager의 SO 목록에서 memId로 탐색하기 위한 확장 메서드.</summary>
    internal static class MemDataListExtensions
    {
        public static MemData FirstOrDefaultByMemId(this IReadOnlyList<MemData> list, string memId)
        {
            if (list == null || string.IsNullOrEmpty(memId)) return null;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].memId == memId)
                {
                    return list[i];
                }
            }

            return null;
        }
    }
}
