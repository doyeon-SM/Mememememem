using System.Collections.Generic;
using HDY.Item;
using UnityEngine;

namespace HDY.Forge
{
    /// <summary>
    /// 강화 개체(합성 ID)를 실제 ItemData로 변환해 ItemCatalogManager에 제공하는 브릿지.
    /// 원본 티어 템플릿 ItemData를 복제해 강화 보너스가 반영된 런타임 전용 ItemData를 만들고 캐싱한다.
    ///
    /// 이렇게 하면 WorldObject/PlayerHarvestController 등 GH·KMS 팀 코드는 지금처럼
    /// ItemData.Value(또는 ItemClass 등)만 읽어도 강화 보너스가 이미 반영된 값을 받게 되어
    /// 그쪽 코드를 수정할 필요가 없다 (강화수치 자체의 최종 반영/조합은 GH·KMS 팀 담당 로직에서
    /// 그대로 tool.Value를 사용하면 된다는 전제로 설계됨).
    /// </summary>
    public class ForgeInstanceItemDataProvider : MonoBehaviour
    {
        public static ForgeInstanceItemDataProvider Instance { get; private set; }

        [Header("참조")]
        [SerializeField] private ItemCatalogManager catalogManager;
        [SerializeField] private ForgeTierData tierData;
        [SerializeField] private ForgeInstanceRegistry instanceRegistry;
        [SerializeField] private List<ForgeToolTypeData> toolTypeDataList = new List<ForgeToolTypeData>();

        private Dictionary<ForgeToolType, ForgeToolTypeData> toolTypeLookup;
        private readonly Dictionary<string, ItemData> runtimeItemCache = new Dictionary<string, ItemData>();

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
            instanceRegistry = ForgeInstanceRegistry.Resolve(instanceRegistry);

            BuildToolTypeLookup();
        }

        private void BuildToolTypeLookup()
        {
            toolTypeLookup = new Dictionary<ForgeToolType, ForgeToolTypeData>();

            foreach (var data in toolTypeDataList)
            {
                if (data == null) continue;
                if (!toolTypeLookup.ContainsKey(data.ToolType))
                {
                    toolTypeLookup.Add(data.ToolType, data);
                }
            }
        }

        private ForgeToolTypeData GetToolTypeData(ForgeToolType toolType)
        {
            if (toolTypeLookup == null) BuildToolTypeLookup();
            return toolTypeLookup.TryGetValue(toolType, out var data) ? data : null;
        }

        /// <summary>
        /// 합성 ID로 런타임 ItemData를 찾는다. 캐시에 있으면 그대로 반환하고,
        /// 없으면 새로 만들어 캐싱한다. ItemCatalogManager.FindItemData에서 호출된다.
        /// </summary>
        public ItemData ResolveRuntimeItemData(string compositeId)
        {
            if (runtimeItemCache.TryGetValue(compositeId, out var cached) && cached != null)
            {
                return cached;
            }

            return RebuildRuntimeItemData(compositeId);
        }

        /// <summary>
        /// 강화/승급으로 상태가 바뀐 뒤 호출한다. 캐시된 런타임 ItemData를 최신 상태로 다시 계산한다.
        /// 같은 오브젝트 참조를 그대로 유지한 채 값만 갱신하므로, 이미 그 참조를 들고 있는 UI 등에도
        /// 자동으로 반영된다.
        /// </summary>
        public ItemData RefreshRuntimeItemData(string compositeId)
        {
            return RebuildRuntimeItemData(compositeId);
        }

        /// <summary>인스턴스가 소멸/제거될 때 캐시도 함께 정리한다.</summary>
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
            if (!ForgeInstanceRegistry.TryParseCompositeId(compositeId, out _, out var instanceId))
            {
                return null;
            }

            var instance = instanceRegistry != null ? instanceRegistry.GetInstance(instanceId) : null;
            if (instance == null) return null;

            var template = catalogManager != null ? catalogManager.FindItemData(instance.BaseItemId) : null;
            if (template == null)
            {
                Debug.LogWarning($"[ForgeInstanceItemDataProvider] 템플릿 ItemData를 찾을 수 없습니다: {instance.BaseItemId}");
                return null;
            }

            var tier = tierData != null ? tierData.GetTier(instance.TierIndex) : null;
            var toolTypeConfig = GetToolTypeData(instance.ToolType);
            bool scalesWithTier = tier != null && (toolTypeConfig == null || toolTypeConfig.DamageScalesWithTier);

            int computedValue = scalesWithTier
                ? tier.BaseDamage + tier.DamagePerEnhanceLevel * instance.EnhanceLevel
                : template.Value;

            if (!runtimeItemCache.TryGetValue(compositeId, out var runtimeData) || runtimeData == null)
            {
                runtimeData = ScriptableObject.CreateInstance<ItemData>();
                runtimeItemCache[compositeId] = runtimeData;
            }

            runtimeData.Item_ID = compositeId;
            runtimeData.ItemName = instance.EnhanceLevel > 0
                ? $"{template.ItemName} +{instance.EnhanceLevel}"
                : template.ItemName;
            runtimeData.ItemIcon = template.ItemIcon;
            runtimeData.ItemClass = template.ItemClass;
            runtimeData.Value = computedValue;
            runtimeData.MaxStack = 1;
            runtimeData.Category = template.Category;
            runtimeData.UseAction = template.UseAction;
            runtimeData.ObjectType = template.ObjectType;

            return runtimeData;
        }
    }
}
