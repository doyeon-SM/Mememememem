using System.Collections.Generic;
using HDY.Item;
using UnityEngine;

namespace KMS.Equipment
{
    /// <summary>
    /// [멤] 장비 개체(합성 ID)를 실제 ItemData로 변환해 ItemCatalogManager에 제공하는 브릿지.
    /// HDY.Forge.ForgeInstanceItemDataProvider와 정확히 대응되는 장비판이다 - 템플릿 EquipmentItemData를
    /// 복제해 개체 상태(강화 레벨 등)가 반영된 런타임 전용 인스턴스를 만들고 캐싱한다.
    ///
    /// 이렇게 해야 인벤토리/툴팁/장착 코드가 지금처럼 ItemCatalogManager.FindItemData(itemId)만 호출해도
    /// 합성 ID든 순수 ID든 똑같이 EquipmentItemData를 받게 된다.
    /// </summary>
    public class EquipmentInstanceItemDataProvider : MonoBehaviour
    {
        public static EquipmentInstanceItemDataProvider Instance { get; private set; }

        [Header("참조")]
        [SerializeField] private ItemCatalogManager catalogManager;
        [SerializeField] private EquipmentInstanceRegistry instanceRegistry;

        private readonly Dictionary<string, EquipmentItemData> runtimeItemCache = new Dictionary<string, EquipmentItemData>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            instanceRegistry = EquipmentInstanceRegistry.Resolve(instanceRegistry);
        }

        /// <summary>합성 ID로 런타임 ItemData를 찾는다. 캐시에 있으면 그대로 반환하고, 없으면 새로 만들어 캐싱한다.</summary>
        public ItemData ResolveRuntimeItemData(string compositeId)
        {
            if (runtimeItemCache.TryGetValue(compositeId, out var cached) && cached != null)
            {
                return cached;
            }

            return RebuildRuntimeItemData(compositeId);
        }

        /// <summary>
        /// 강화/연마/전승으로 상태가 바뀐 뒤 호출한다. 같은 오브젝트 참조를 유지한 채 값만 갱신하므로,
        /// 이미 그 참조를 들고 있는 UI에도 자동으로 반영된다.
        /// </summary>
        public ItemData RefreshRuntimeItemData(string compositeId)
        {
            return RebuildRuntimeItemData(compositeId);
        }

        /// <summary>개체가 소멸/제거될 때(전승 재료 소멸 등) 캐시도 함께 정리한다.</summary>
        public void ClearCache(string compositeId)
        {
            if (string.IsNullOrEmpty(compositeId)) return;

            if (runtimeItemCache.TryGetValue(compositeId, out var cached) && cached != null)
            {
                Destroy(cached);
            }

            runtimeItemCache.Remove(compositeId);
        }

        private ItemData RebuildRuntimeItemData(string compositeId)
        {
            if (!HDY.Forge.ForgeInstanceRegistry.TryParseCompositeId(compositeId, out _, out var instanceId))
            {
                return null;
            }

            instanceRegistry = EquipmentInstanceRegistry.Resolve(instanceRegistry);
            var instance = instanceRegistry != null ? instanceRegistry.GetInstance(instanceId) : null;
            if (instance == null) return null;

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            var template = catalogManager != null ? catalogManager.FindItemData(instance.BaseItemId) as EquipmentItemData : null;
            if (template == null)
            {
                Debug.LogWarning($"[EquipmentInstanceItemDataProvider] 템플릿 EquipmentItemData를 찾을 수 없습니다: {instance.BaseItemId}");
                return null;
            }

            if (!runtimeItemCache.TryGetValue(compositeId, out var runtimeData) || runtimeData == null)
            {
                runtimeData = ScriptableObject.CreateInstance<EquipmentItemData>();
                runtimeItemCache[compositeId] = runtimeData;
            }

            runtimeData.Item_ID = compositeId;
            runtimeData.ItemName = instance.EnhanceLevel > 0
                ? $"{template.ItemName} +{instance.EnhanceLevel}"
                : template.ItemName;
            runtimeData.ItemIcon = template.ItemIcon;
            runtimeData.ItemClass = template.ItemClass;
            runtimeData.Value = template.Value;
            runtimeData.MaxStack = 1; // [멤] 장비는 개체 구분을 위해 항상 1칸 1개다.
            runtimeData.Category = template.Category;
            runtimeData.UseAction = template.UseAction;
            runtimeData.ObjectType = template.ObjectType;

            // [멤] 장비 전용 값은 템플릿 그대로 복사한다. 강화/연마로 인한 수치 보정은 로직이 만들어지는
            // 시점에 여기서 EnhanceLevel/RefinementOptions를 반영하도록 확장하면 된다.
            runtimeData.EquipSlot = template.EquipSlot;
            runtimeData.DamageType = template.DamageType;
            runtimeData.HealthBonus = template.HealthBonus;
            runtimeData.PrimaryStatValue = template.PrimaryStatValue;
            runtimeData.SecondaryStatValue = template.SecondaryStatValue;
            runtimeData.BaseOptionStatType = template.BaseOptionStatType;
            runtimeData.BaseOptionValue = template.BaseOptionValue;

            return runtimeData;
        }

        /// <summary>다른 스크립트가 들고 있는 참조가 비어있을 때 쓰는 공용 폴백 탐색.</summary>
        public static EquipmentInstanceItemDataProvider Resolve(EquipmentInstanceItemDataProvider existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            return FindFirstObjectByType<EquipmentInstanceItemDataProvider>();
        }
    }
}
