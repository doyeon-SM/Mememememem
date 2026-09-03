using System;
using System.Collections.Generic;
using UnityEngine;

namespace KMS.Equipment
{
    /// <summary>
    /// [멤] 장비 개체(방어구 강화/연마, 장신구 특수옵션)의 런타임 상태를 InstanceId 기준으로 보관하는 매니저.
    /// 씬에 배치해 DontDestroyOnLoad로 유지하는 싱글톤이며, HDY.Forge.ForgeInstanceRegistry와 완전히 같은
    /// 패턴이다(도구용과 장비용을 한 레지스트리에 섞지 않는다 - 사용자 확정 사양).
    ///
    /// 합성 ID("{BaseItemId}@{InstanceId}") 규칙만은 도구와 공유해야 하므로 ForgeInstanceRegistry의
    /// static 헬퍼(BuildCompositeId/TryParseCompositeId/IsCompositeId)를 그대로 재사용한다.
    /// </summary>
    public class EquipmentInstanceRegistry : MonoBehaviour
    {
        public static EquipmentInstanceRegistry Instance { get; private set; }

        private readonly Dictionary<string, EquipmentInstanceData> instances = new Dictionary<string, EquipmentInstanceData>();

        [Header("디버그용 - 현재 등록된 장비 개체 확인 (인스펙터 표시 전용, 직접 편집 비권장)")]
        [SerializeField] private List<EquipmentInstanceData> instanceListView = new List<EquipmentInstanceData>();

        /// <summary>장비 개체 목록이 바뀔 때(생성/제거/복원/옵션 변경) 발행된다 - 저장 트리거용.</summary>
        public static event Action OnEquipmentInstanceDataChanged;

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

        /// <summary>인스펙터 확인용 - 현재 등록된 모든 장비 개체(읽기 전용 뷰).</summary>
        public IReadOnlyList<EquipmentInstanceData> AllInstances => instanceListView;

        /// <summary>
        /// 새 장비 개체를 만든다. 지연 생성 원칙에 따라 "강화/연마/특수옵션이 실제로 생기는 순간"에만
        /// 호출해야 한다 - 아무 상태도 없는 장비는 순수 Item_ID 그대로 두는 편이 세이브가 가볍다.
        /// </summary>
        public EquipmentInstanceData CreateInstance(string baseItemId, EquipSlotType equipSlot)
        {
            var data = new EquipmentInstanceData
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                BaseItemId = baseItemId,
                EquipSlot = equipSlot,
                EnhanceLevel = 0,
            };

            instances[data.InstanceId] = data;
            instanceListView.Add(data);
            NotifyChanged();

            return data;
        }

        /// <summary>InstanceId로 개체 데이터를 찾는다. 없으면 null.</summary>
        public EquipmentInstanceData GetInstance(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            return instances.TryGetValue(instanceId, out var data) ? data : null;
        }

        /// <summary>합성 ID로 개체 데이터를 바로 찾는다. 합성 ID가 아니거나 등록되지 않았으면 null.</summary>
        public EquipmentInstanceData GetInstanceByCompositeId(string compositeId)
        {
            if (!HDY.Forge.ForgeInstanceRegistry.TryParseCompositeId(compositeId, out _, out var instanceId)) return null;
            return GetInstance(instanceId);
        }

        /// <summary>개체를 완전히 제거한다(전승 재료 소멸, 아이템 파괴 등).</summary>
        public void RemoveInstance(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return;

            if (instances.TryGetValue(instanceId, out var data))
            {
                instances.Remove(instanceId);
                instanceListView.Remove(data);
                NotifyChanged();
            }
        }

        /// <summary>개체의 옵션/강화 상태를 바꾼 뒤 호출해 저장을 트리거한다.</summary>
        public void NotifyChanged()
        {
            OnEquipmentInstanceDataChanged?.Invoke();
        }

        /// <summary>세이브 파일에서 불러온 개체 목록을 메모리 딕셔너리 및 인스펙터 리스트에 재복원한다.</summary>
        public void RestoreInstances(List<EquipmentInstanceData> restoredList)
        {
            instances.Clear();
            instanceListView.Clear();

            if (restoredList == null) return;

            foreach (var data in restoredList)
            {
                if (data == null || string.IsNullOrEmpty(data.InstanceId)) continue;

                if (data.RefinementOptions == null) data.RefinementOptions = new List<EquipmentOptionData>();
                if (data.SpecialOptions == null) data.SpecialOptions = new List<EquipmentOptionData>();

                instances[data.InstanceId] = data;
                instanceListView.Add(data);
            }
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 참조가 비어있을 때 쓰는 공용 폴백 탐색(ItemCatalogManager.Resolve와 동일한 패턴).
        /// 장비 개체가 아직 하나도 없는 프로젝트 상태에서도 동작해야 하므로, 못 찾아도 경고만 남기고 null을 돌려준다.
        /// </summary>
        public static EquipmentInstanceRegistry Resolve(EquipmentInstanceRegistry existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            return FindFirstObjectByType<EquipmentInstanceRegistry>();
        }
    }
}
