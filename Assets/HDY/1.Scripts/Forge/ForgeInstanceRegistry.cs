using System;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.Forge
{
    /// <summary>
    /// 강화가 시도된 도구 개체들의 런타임 상태(강화레벨/과열수치 등)를 InstanceId 기준으로 보관하는 매니저.
    /// 씬에 배치되어 DontDestroyOnLoad로 유지되는 싱글톤 (ItemCatalogManager와 동일한 패턴).
    /// [임시 조치] 저장/불러오기 연동은 추후 논의 예정 - 현재는 세션 중 메모리에만 유지한다.
    ///
    /// [인스펙터 노출] instances(Dictionary)는 Unity가 기본적으로 직렬화/표시하지 못하므로, 같은 객체
    /// 참조를 그대로 담는 instanceListView(List)를 별도로 유지해 인스펙터에서 확인할 수 있게 한다.
    /// 두 컬렉션은 같은 ForgeInstanceData 객체를 가리키므로 EnhanceLevel 등 필드값은 항상 동기화되어
    /// 보인다(값을 바꾸는 게 아니라 항목 자체를 추가/삭제할 때만 두 곳 모두 갱신해주면 됨).
    /// 다만 플레이 모드에서 이 리스트를 인스펙터로 직접 편집(항목 삭제/재정렬)하면 딕셔너리와 어긋날 수
    /// 있으니, 디버깅 확인용으로만 쓰고 편집은 권장하지 않는다(값 필드 수정 정도는 안전함).
    /// </summary>
    public class ForgeInstanceRegistry : MonoBehaviour
    {
        public static ForgeInstanceRegistry Instance { get; private set; }

        private const char CompositeIdSeparator = '@';

        private readonly Dictionary<string, ForgeInstanceData> instances = new Dictionary<string, ForgeInstanceData>();

        [Header("디버그용 - 현재 등록된 강화 개체 확인 (인스펙터 표시 전용, 직접 편집 비권장)")]
        [SerializeField] private List<ForgeInstanceData> instanceListView = new List<ForgeInstanceData>();

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

        /// <summary>
        /// 새 강화 개체를 만든다. 강화 성공 여부와 무관하게 "첫 시도"가 이루어지는 시점에 호출해야 한다
        /// (실패해도 모루 과열 수치를 이 개체에 누적해야 하기 때문).
        /// </summary>
        public ForgeInstanceData CreateInstance(string baseItemId, ForgeToolType toolType, int tierIndex)
        {
            var data = new ForgeInstanceData
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                BaseItemId = baseItemId,
                ToolType = toolType,
                TierIndex = tierIndex,
                EnhanceLevel = 0,
                OverheatPercent = 0f
            };

            instances[data.InstanceId] = data;
            instanceListView.Add(data);

            return data;
        }

        /// <summary>InstanceId로 인스턴스 데이터를 찾는다. 없으면 null.</summary>
        public ForgeInstanceData GetInstance(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            return instances.TryGetValue(instanceId, out var data) ? data : null;
        }

        /// <summary>인스펙터 확인용 - 현재 등록된 모든 강화 개체(읽기 전용 뷰).</summary>
        public IReadOnlyList<ForgeInstanceData> AllInstances => instanceListView;

        /// <summary>인스턴스를 완전히 제거한다(예: 아이템 파괴/디버그용). 일반적인 강화 실패로는 호출하지 않는다.</summary>
        public void RemoveInstance(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return;

            if (instances.TryGetValue(instanceId, out var data))
            {
                instances.Remove(instanceId);
                instanceListView.Remove(data);
            }
        }

        /// <summary>"{BaseItemId}@{InstanceId}" 형태의 합성 ID를 만든다.</summary>
        public static string BuildCompositeId(string baseItemId, string instanceId)
        {
            return $"{baseItemId}{CompositeIdSeparator}{instanceId}";
        }

        /// <summary>주어진 itemId가 합성 ID(강화 개체) 형태인지 확인한다.</summary>
        public static bool IsCompositeId(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && itemId.IndexOf(CompositeIdSeparator) >= 0;
        }

        /// <summary>합성 ID를 BaseItemId와 InstanceId로 분리한다. 합성 ID가 아니면 false.</summary>
        public static bool TryParseCompositeId(string itemId, out string baseItemId, out string instanceId)
        {
            baseItemId = null;
            instanceId = null;

            if (!IsCompositeId(itemId)) return false;

            int separatorIndex = itemId.IndexOf(CompositeIdSeparator);
            baseItemId = itemId.Substring(0, separatorIndex);
            instanceId = itemId.Substring(separatorIndex + 1);

            return !string.IsNullOrEmpty(baseItemId) && !string.IsNullOrEmpty(instanceId);
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 참조가 비어있을 때 쓰는 공용 폴백 탐색.
        /// (ItemCatalogManager.Resolve와 동일한 패턴)
        /// </summary>
        public static ForgeInstanceRegistry Resolve(ForgeInstanceRegistry existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<ForgeInstanceRegistry>();
            if (found == null)
            {
                Debug.LogWarning("[ForgeInstanceRegistry] 씬에서 ForgeInstanceRegistry를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
