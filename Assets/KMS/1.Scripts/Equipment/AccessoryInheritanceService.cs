using System.Collections.Generic;
using HDY.Item;
using UnityEngine;

namespace KMS.Equipment
{
    /// <summary>전승 시도가 거부됐을 때의 사유(HDY.Forge.InheritanceFailReason과 같은 형태).</summary>
    public enum AccessoryInheritanceFailReason
    {
        None,
        NotAccessory,
        ItemIdMismatch,
        SameInstance,
        NoSpecialOptionToInherit,
        MissingDependency,
    }

    /// <summary>전승 시도 결과. 조건만 맞으면 무조건 성공한다(확률 없음).</summary>
    public readonly struct AccessoryInheritanceOutcome
    {
        public readonly bool Attempted;
        public readonly AccessoryInheritanceFailReason FailReason;

        /// <summary>성공 시 베이스 장신구의 새 itemId(합성 ID). 호출자가 인벤토리 슬롯의 itemId를 이 값으로 바꿔줘야 한다.</summary>
        public readonly string ResultItemId;

        public AccessoryInheritanceOutcome(bool attempted, AccessoryInheritanceFailReason failReason, string resultItemId)
        {
            Attempted = attempted;
            FailReason = failReason;
            ResultItemId = resultItemId;
        }

        public static AccessoryInheritanceOutcome Rejected(AccessoryInheritanceFailReason reason)
        {
            return new AccessoryInheritanceOutcome(false, reason, null);
        }
    }

    /// <summary>
    /// [멤] 장신구 전승 합성. 확정된 규칙은 다음과 같다:
    /// - 같은 Item_ID끼리만 전승할 수 있다(부위만 같은 다른 장신구는 불가).
    /// - 베이스 장신구의 기본옵션은 그대로 유지된다(기본옵션은 아이템 종류가 결정하므로 애초에 건드릴 게 없다).
    /// - 재료 장신구의 특수옵션만 베이스로 옮겨지고, 재료는 소멸한다.
    ///
    /// [멤] 이 서비스는 "개체 데이터" 수준의 작업만 한다 - 재료 아이템을 인벤토리에서 실제로 지우고 베이스 슬롯의
    /// itemId를 결과값으로 바꾸는 것은 호출자(장비/전승 UI, 다음 단계에서 제작)의 몫이다. 인벤토리 슬롯을
    /// 어떻게 다룰지는 UI 흐름에 따라 달라지기 때문에 여기서 임의로 결정하지 않는다.
    /// 두 인자가 서로 다른 스택인지도 호출자가 보장해야 한다(순수 Item_ID 두 개는 문자열만으로 구분할 수 없다).
    /// </summary>
    public class AccessoryInheritanceService : MonoBehaviour
    {
        public static AccessoryInheritanceService Instance { get; private set; }

        [Header("참조")]
        [SerializeField] private ItemCatalogManager catalogManager;
        [SerializeField] private EquipmentInstanceRegistry instanceRegistry;
        [SerializeField] private EquipmentInstanceItemDataProvider itemDataProvider;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            instanceRegistry = EquipmentInstanceRegistry.Resolve(instanceRegistry);
            itemDataProvider = EquipmentInstanceItemDataProvider.Resolve(itemDataProvider);
        }

        /// <summary>
        /// 실행하지 않고 전승 가능 여부만 확인한다(버튼 활성/비활성 표시용).
        /// </summary>
        public AccessoryInheritanceFailReason CheckInheritable(string baseItemId, string materialItemId)
        {
            ResolveReferences();

            if (catalogManager == null || instanceRegistry == null) return AccessoryInheritanceFailReason.MissingDependency;

            var baseData = catalogManager.FindItemData(baseItemId) as EquipmentItemData;
            var materialData = catalogManager.FindItemData(materialItemId) as EquipmentItemData;
            if (baseData == null || materialData == null) return AccessoryInheritanceFailReason.NotAccessory;
            if (baseData.Category != ItemCategory.Accessory || materialData.Category != ItemCategory.Accessory)
            {
                return AccessoryInheritanceFailReason.NotAccessory;
            }

            // [멤] 확정 사양: "같은 Item_ID끼리만". 합성 ID일 수 있으므로 베이스 템플릿 ID로 비교한다.
            if (GetTemplateItemId(baseItemId) != GetTemplateItemId(materialItemId))
            {
                return AccessoryInheritanceFailReason.ItemIdMismatch;
            }

            // 같은 개체(같은 합성 ID)를 재료로 쓸 수는 없다. 순수 Item_ID 두 개는 문자열이 같아 구분되지 않으므로
            // 호출자가 서로 다른 스택임을 보장해야 하고, 여기서는 개체가 명확히 같은 경우만 걸러낸다.
            if (HDY.Forge.ForgeInstanceRegistry.IsCompositeId(baseItemId) && baseItemId == materialItemId)
            {
                return AccessoryInheritanceFailReason.SameInstance;
            }

            var materialInstance = instanceRegistry.GetInstanceByCompositeId(materialItemId);
            bool materialHasSpecialOption = materialInstance != null
                && materialInstance.SpecialOptions != null
                && materialInstance.SpecialOptions.Count > 0;

            // [멤] 옮길 특수옵션이 없는데 재료만 사라지는 것을 막는다. 재료가 순수 Item_ID(개체 없음)면
            // 애초에 특수옵션이 존재할 수 없으므로 이 분기에 걸린다.
            if (!materialHasSpecialOption) return AccessoryInheritanceFailReason.NoSpecialOptionToInherit;

            return AccessoryInheritanceFailReason.None;
        }

        /// <summary>
        /// 전승을 실행한다. 성공하면 재료의 특수옵션이 베이스로 복사되고, 재료의 개체 데이터는 제거된다
        /// (인벤토리에서 재료 아이템을 지우는 것은 호출자 몫 - 클래스 주석 참고).
        /// 성공 시 Outcome.ResultItemId가 베이스의 새 itemId(합성 ID)다.
        /// </summary>
        public AccessoryInheritanceOutcome TryInherit(string baseItemId, string materialItemId)
        {
            AccessoryInheritanceFailReason reason = CheckInheritable(baseItemId, materialItemId);
            if (reason != AccessoryInheritanceFailReason.None)
            {
                return AccessoryInheritanceOutcome.Rejected(reason);
            }

            var materialInstance = instanceRegistry.GetInstanceByCompositeId(materialItemId);
            List<EquipmentOptionData> inheritedOptions = materialInstance.CloneSpecialOptions();

            // [멤] 베이스가 아직 순수 Item_ID면 이 시점에 개체를 만든다(지연 생성 원칙).
            EquipmentInstanceData baseInstance = EnsureInstance(baseItemId);
            if (baseInstance == null)
            {
                return AccessoryInheritanceOutcome.Rejected(AccessoryInheritanceFailReason.MissingDependency);
            }

            // 특수옵션은 "덮어쓰기"다 - 기본옵션은 그대로 유지되고 특수옵션 자리만 재료 것으로 바뀐다.
            // 지금은 자리가 1개(MaxSpecialOptionCount)이며, 이 상수만 늘리면 그대로 확장된다.
            if (inheritedOptions.Count > EquipmentInstanceData.MaxSpecialOptionCount)
            {
                inheritedOptions.RemoveRange(
                    EquipmentInstanceData.MaxSpecialOptionCount,
                    inheritedOptions.Count - EquipmentInstanceData.MaxSpecialOptionCount);
            }

            baseInstance.SpecialOptions = inheritedOptions;

            // 재료 소멸 - 개체 데이터와 런타임 ItemData 캐시를 모두 정리한다.
            string materialCompositeId = materialInstance.BuildCompositeId();
            instanceRegistry.RemoveInstance(materialInstance.InstanceId);
            if (itemDataProvider != null) itemDataProvider.ClearCache(materialCompositeId);

            string resultItemId = baseInstance.BuildCompositeId();
            if (itemDataProvider != null) itemDataProvider.RefreshRuntimeItemData(resultItemId);
            instanceRegistry.NotifyChanged();

            return new AccessoryInheritanceOutcome(true, AccessoryInheritanceFailReason.None, resultItemId);
        }

        /// <summary>
        /// itemId에 해당하는 개체 데이터를 돌려주고, 아직 없으면(순수 Item_ID) 새로 만든다.
        /// 강화/연마/특수옵션이 실제로 생기는 시점에만 개체를 만드는 지연 생성 원칙의 진입점이다.
        /// </summary>
        public EquipmentInstanceData EnsureInstance(string itemId)
        {
            ResolveReferences();
            if (instanceRegistry == null || catalogManager == null) return null;

            var existing = instanceRegistry.GetInstanceByCompositeId(itemId);
            if (existing != null) return existing;

            var data = catalogManager.FindItemData(itemId) as EquipmentItemData;
            if (data == null) return null;

            return instanceRegistry.CreateInstance(GetTemplateItemId(itemId), data.EquipSlot);
        }

        /// <summary>합성 ID면 '@' 앞의 템플릿 Item_ID를, 순수 ID면 그대로 돌려준다.</summary>
        public static string GetTemplateItemId(string itemId)
        {
            return HDY.Forge.ForgeInstanceRegistry.TryParseCompositeId(itemId, out var baseItemId, out _)
                ? baseItemId
                : itemId;
        }

        public static AccessoryInheritanceService Resolve(AccessoryInheritanceService existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            return FindFirstObjectByType<AccessoryInheritanceService>();
        }
    }
}
