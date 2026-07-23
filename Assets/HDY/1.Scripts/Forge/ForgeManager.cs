using System.Collections.Generic;
using HDY.Item;
using HDY.Territory;
using HDY.Upgrade;
using KMS.InventoryDuped;
using UnityEngine;

namespace HDY.Forge
{
    /// <summary>대장간 시도가 거부/실패했을 때의 사유. ForgeUI가 안내 메시지·버튼 활성 여부를 결정할 때 사용한다.</summary>
    public enum ForgeFailReason
    {
        None,
        NotForgeableTool,
        ActionNotAllowedForTool,
        MaxEnhanceLevelReached,
        PromotionRequiresMaxEnhanceLevel,
        NoNextTierDefined,
        NotEnoughMaterial,
        NotEnoughGold,
        MissingDependency
    }

    /// <summary>강화/승급 시도 한 번의 결과. ForgeUI가 결과 팝업을 그릴 때 사용한다.</summary>
    public readonly struct ForgeAttemptOutcome
    {
        /// <summary>실제로 시도(재료 소비 포함)가 이루어졌는지. false면 사전 조건 미충족으로 아예 시도되지 않은 것.</summary>
        public readonly bool Attempted;
        public readonly ForgeAttemptResult Result;
        public readonly ForgeFailReason FailReason;
        public readonly int EnhanceLevel;
        public readonly int TierIndex;
        public readonly float OverheatPercent;
        public readonly bool WasGuaranteed;
        public readonly bool ReachedMaxEnhanceLevel;

        public ForgeAttemptOutcome(bool attempted, ForgeAttemptResult result, ForgeFailReason failReason,
            int enhanceLevel, int tierIndex, float overheatPercent, bool wasGuaranteed, bool reachedMaxEnhanceLevel)
        {
            Attempted = attempted;
            Result = result;
            FailReason = failReason;
            EnhanceLevel = enhanceLevel;
            TierIndex = tierIndex;
            OverheatPercent = overheatPercent;
            WasGuaranteed = wasGuaranteed;
            ReachedMaxEnhanceLevel = reachedMaxEnhanceLevel;
        }

        public static ForgeAttemptOutcome Rejected(ForgeFailReason reason)
        {
            return new ForgeAttemptOutcome(false, ForgeAttemptResult.Failure, reason, 0, 0, 0f, false, false);
        }
    }

    /// <summary>아이템 하나를 대장간 관점에서 설명하는 정보. ForgeUI가 목록 필터링/정렬/버튼 라벨 결정에 사용한다.</summary>
    public readonly struct ForgeItemDescriptor
    {
        public readonly bool IsForgeable;
        public readonly ForgeToolType ToolType;
        public readonly int TierIndex;
        public readonly int EnhanceLevel;
        public readonly bool CanEnhance;
        public readonly bool CanPromote;
        public readonly bool EligibleForPromotionNow;

        public ForgeItemDescriptor(bool isForgeable, ForgeToolType toolType, int tierIndex, int enhanceLevel,
            bool canEnhance, bool canPromote, bool eligibleForPromotionNow)
        {
            IsForgeable = isForgeable;
            ToolType = toolType;
            TierIndex = tierIndex;
            EnhanceLevel = enhanceLevel;
            CanEnhance = canEnhance;
            CanPromote = canPromote;
            EligibleForPromotionNow = eligibleForPromotionNow;
        }

        public static readonly ForgeItemDescriptor NotForgeable = new ForgeItemDescriptor(false, default, -1, 0, false, false, false);
    }

    /// <summary>버튼을 누르기 전에 UI가 미리 보여줄 비용/확률 정보. 실행하지 않고 조회만 한다(재료 소비/판정 없음).</summary>
    public readonly struct ForgePreview
    {
        public readonly ForgeFailReason BlockReason;
        public readonly string MaterialItemId;
        public readonly int MaterialCost;
        public readonly int MaterialOwned;
        public readonly int GoldCost;
        public readonly int GoldOwned;
        public readonly float SuccessRate;
        public readonly bool IsGuaranteed;
        public readonly float OverheatPercent;

        public ForgePreview(ForgeFailReason blockReason, string materialItemId, int materialCost, int materialOwned,
            int goldCost, int goldOwned, float successRate, bool isGuaranteed, float overheatPercent)
        {
            BlockReason = blockReason;
            MaterialItemId = materialItemId;
            MaterialCost = materialCost;
            MaterialOwned = materialOwned;
            GoldCost = goldCost;
            GoldOwned = goldOwned;
            SuccessRate = successRate;
            IsGuaranteed = isGuaranteed;
            OverheatPercent = overheatPercent;
        }

        /// <summary>사전 조건은 통과했지만(도구 종류/레벨 등) 재료·골드가 부족한지.</summary>
        public bool HasEnoughToAttempt => BlockReason == ForgeFailReason.None
            && MaterialOwned >= MaterialCost && GoldOwned >= GoldCost;

        public static ForgePreview Blocked(ForgeFailReason reason)
        {
            return new ForgePreview(reason, null, 0, 0, 0, 0, 0f, false, 0f);
        }
    }

    /// <summary>
    /// 강화/승급 시도를 실제로 처리하는 매니저. 재료·골드 소비, 확률 판정, 모루 과열 수치 관리,
    /// 강화 개체(ForgeInstanceData) 생성/갱신을 담당한다.
    /// ForgeUI는 강화칸에 참조된 도구 1개(ItemStack, 실제로는 인벤토리/창고에 그대로 있는 원본 참조)를
    /// 넘겨 이 매니저를 호출하기만 하면 된다 - 슬롯을 옮기지 않고 그 자리에서 itemId만 갱신한다.
    /// </summary>
    public class ForgeManager : MonoBehaviour
    {
        public static ForgeManager Instance { get; private set; }

        [Header("참조")]
        [SerializeField] private ForgeTierData tierData;
        [SerializeField] private ForgeEnhancementTable enhancementTable;
        [SerializeField] private List<ForgeToolTypeData> toolTypeDataList = new List<ForgeToolTypeData>();
        [SerializeField] private ForgeInstanceRegistry instanceRegistry;
        [SerializeField] private ForgeInstanceItemDataProvider itemDataProvider;
        [SerializeField] private ItemCatalogManager catalogManager;
        [SerializeField] private TerritoryData territoryData;

        [Tooltip("IMaterialInventory를 구현한 컴포넌트를 연결. 비워두면 Awake에서 씬을 훑어 자동으로 찾는다 " +
                 "(UpgradePopupUI와 동일한 패턴 - CombinedMaterialInventory가 인벤토리+창고를 합산해서 확인/차감해준다).")]
        [SerializeField] private MonoBehaviour materialInventorySource;
        private IMaterialInventory MaterialInventory => materialInventorySource as IMaterialInventory;

        [Tooltip("과열 수치 비교 시 부동소수점 오차를 흡수하기 위한 여유값 (예: 33.3% x 3 = 99.9%를 100%로 취급)")]
        [SerializeField] private float overheatEpsilon = 0.01f;

        private Dictionary<ForgeToolType, ForgeToolTypeData> toolTypeLookup;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            instanceRegistry = ForgeInstanceRegistry.Resolve(instanceRegistry);
            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            territoryData = TerritoryData.Resolve(territoryData);

            if (itemDataProvider == null)
            {
                itemDataProvider = ForgeInstanceItemDataProvider.Instance;
            }

            if (materialInventorySource == null)
            {
                materialInventorySource = FindMaterialInventorySource();
            }
            else if (MaterialInventory == null)
            {
                Debug.LogWarning("[ForgeManager] materialInventorySource가 IMaterialInventory를 구현하지 않습니다.", this);
            }

            BuildToolTypeLookup();
        }

        /// <summary>materialInventorySource가 비어있을 때, 씬에서 IMaterialInventory 구현체를 아무거나 찾아 연결한다.</summary>
        private MonoBehaviour FindMaterialInventorySource()
        {
            var candidates = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            foreach (var candidate in candidates)
            {
                if (candidate is IMaterialInventory)
                {
                    return candidate;
                }
            }

            Debug.LogWarning("[ForgeManager] 씬에서 IMaterialInventory 구현체를 찾지 못했습니다. 재료 조건 검사를 건너뜁니다.");
            return null;
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
        /// 임의의 템플릿 Item_ID(예: tool_shabby_axe)가 어떤 도구 종류/티어인지 찾는다.
        /// 몽둥이처럼 대장간 설정 자체가 없는 아이템이면 false.
        /// </summary>
        private bool ResolveToolTypeAndTier(string templateItemId, out ForgeToolTypeData toolTypeData, out int tierIndex)
        {
            toolTypeData = null;
            tierIndex = -1;

            if (string.IsNullOrEmpty(templateItemId)) return false;

            foreach (var data in toolTypeDataList)
            {
                if (data == null) continue;

                int foundTier = data.FindTierIndex(templateItemId);
                if (foundTier >= 0)
                {
                    toolTypeData = data;
                    tierIndex = foundTier;
                    return true;
                }
            }

            return false;
        }

        /// <summary>ForgeUI가 인벤토리/창고를 스캔할 때, 이 아이템이 대장간 대상인지 확인하는 공개 API.</summary>
        public bool IsForgeableItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            if (ForgeInstanceRegistry.TryParseCompositeId(itemId, out _, out var instanceId))
            {
                var instance = instanceRegistry != null ? instanceRegistry.GetInstance(instanceId) : null;
                return instance != null && GetToolTypeData(instance.ToolType) != null;
            }

            return ResolveToolTypeAndTier(itemId, out _, out _);
        }

        /// <summary>
        /// 읽기 전용 조회(등록/생성 없음). 아직 강화 시도가 없었던 일반 아이템은 "0강" 상태로 간주해 설명한다.
        /// ForgeUI가 하단 목록을 정렬/필터링할 때 사용한다.
        /// </summary>
        public ForgeItemDescriptor Describe(ItemStack stack)
        {
            if (!TryPeekState(stack, out var toolTypeData, out int tierIndex, out int enhanceLevel, out _, out _))
            {
                return ForgeItemDescriptor.NotForgeable;
            }

            bool eligibleForPromotionNow = toolTypeData.CanPromote
                && tierData.HasNextTier(tierIndex)
                && (!toolTypeData.CanEnhance || enhanceLevel >= 10);

            return new ForgeItemDescriptor(true, toolTypeData.ToolType, tierIndex, enhanceLevel,
                toolTypeData.CanEnhance, toolTypeData.CanPromote, eligibleForPromotionNow);
        }

        /// <summary>버튼을 누르기 전 미리보기. 재료/골드를 소비하지 않고, 새 강화 개체도 등록하지 않는다.</summary>
        public ForgePreview GetPreview(ItemStack stack, ForgeActionType action)
        {
            if (!ValidateDependencies()) return ForgePreview.Blocked(ForgeFailReason.MissingDependency);

            if (!TryPeekState(stack, out var toolTypeData, out int tierIndex, out int enhanceLevel, out float overheat, out _))
            {
                return ForgePreview.Blocked(ForgeFailReason.NotForgeableTool);
            }

            var tier = tierData.GetTier(tierIndex);
            if (tier == null) return ForgePreview.Blocked(ForgeFailReason.MissingDependency);

            int goldOwned = territoryData != null ? territoryData.Gold : 0;
            var materials = MaterialInventory;

            if (action == ForgeActionType.Enhance)
            {
                if (!toolTypeData.CanEnhance) return ForgePreview.Blocked(ForgeFailReason.ActionNotAllowedForTool);
                if (enhanceLevel >= 10) return ForgePreview.Blocked(ForgeFailReason.MaxEnhanceLevelReached);

                var bracket = enhancementTable.GetBracket(enhanceLevel + 1);
                if (bracket == null) return ForgePreview.Blocked(ForgeFailReason.MissingDependency);

                int owned = materials != null ? materials.GetAmount(tier.TierMaterialItemId) : 0;
                bool guaranteed = overheat >= enhancementTable.OverheatGuaranteedThreshold - overheatEpsilon;

                return new ForgePreview(ForgeFailReason.None, tier.TierMaterialItemId, bracket.MaterialCost, owned,
                    bracket.GoldCost, goldOwned, bracket.SuccessRate, guaranteed, overheat);
            }
            else
            {
                if (!toolTypeData.CanPromote) return ForgePreview.Blocked(ForgeFailReason.ActionNotAllowedForTool);
                if (toolTypeData.CanEnhance && enhanceLevel < 10) return ForgePreview.Blocked(ForgeFailReason.PromotionRequiresMaxEnhanceLevel);
                if (!tierData.HasNextTier(tierIndex)) return ForgePreview.Blocked(ForgeFailReason.NoNextTierDefined);

                int owned = materials != null ? materials.GetAmount(tier.TierMaterialItemId) : 0;
                bool guaranteed = overheat >= enhancementTable.OverheatGuaranteedThreshold - overheatEpsilon;

                return new ForgePreview(ForgeFailReason.None, tier.TierMaterialItemId, enhancementTable.PromotionMaterialCost,
                    owned, enhancementTable.PromotionGoldCost, goldOwned, enhancementTable.PromotionSuccessRate, guaranteed, overheat);
            }
        }

        /// <summary>강화(레벨업)를 한 번 시도한다. 성공/실패와 무관하게 재료·골드는 소모된다.</summary>
        public ForgeAttemptOutcome TryEnhance(ItemStack stack)
        {
            if (!ValidateDependencies()) return ForgeAttemptOutcome.Rejected(ForgeFailReason.MissingDependency);

            if (!TryGetOrCreateInstance(stack, out var instance, out var toolTypeData))
            {
                return ForgeAttemptOutcome.Rejected(ForgeFailReason.NotForgeableTool);
            }

            if (!toolTypeData.CanEnhance)
            {
                return ForgeAttemptOutcome.Rejected(ForgeFailReason.ActionNotAllowedForTool);
            }

            if (instance.EnhanceLevel >= 10)
            {
                return ForgeAttemptOutcome.Rejected(ForgeFailReason.MaxEnhanceLevelReached);
            }

            var tier = tierData.GetTier(instance.TierIndex);
            var bracket = enhancementTable.GetBracket(instance.EnhanceLevel + 1);

            if (tier == null || bracket == null)
            {
                return ForgeAttemptOutcome.Rejected(ForgeFailReason.MissingDependency);
            }

            if (!HasEnoughMaterialAndGold(tier.TierMaterialItemId, bracket.MaterialCost, bracket.GoldCost, out var shortageReason))
            {
                return ForgeAttemptOutcome.Rejected(shortageReason);
            }

            ConsumeMaterialAndGold(tier.TierMaterialItemId, bracket.MaterialCost, bracket.GoldCost);

            bool guaranteed = instance.OverheatPercent >= enhancementTable.OverheatGuaranteedThreshold - overheatEpsilon;
            bool success = guaranteed || Random.value < bracket.SuccessRate;

            if (success)
            {
                instance.EnhanceLevel += 1;
                instance.OverheatPercent = 0f;
            }
            else
            {
                instance.OverheatPercent = Mathf.Min(1f, instance.OverheatPercent + bracket.FailureOverheatCharge);
            }

            ApplyInstanceToSlot(stack, instance);

            return new ForgeAttemptOutcome(
                true,
                success ? ForgeAttemptResult.Success : ForgeAttemptResult.Failure,
                ForgeFailReason.None,
                instance.EnhanceLevel,
                instance.TierIndex,
                instance.OverheatPercent,
                guaranteed,
                instance.EnhanceLevel >= 10);
        }

        /// <summary>승급을 한 번 시도한다. 성공 시 아이템 자체가 다음 티어 아이템으로 교체된다.</summary>
        public ForgeAttemptOutcome TryPromote(ItemStack stack)
        {
            if (!ValidateDependencies()) return ForgeAttemptOutcome.Rejected(ForgeFailReason.MissingDependency);

            if (!TryGetOrCreateInstance(stack, out var instance, out var toolTypeData))
            {
                return ForgeAttemptOutcome.Rejected(ForgeFailReason.NotForgeableTool);
            }

            if (!toolTypeData.CanPromote)
            {
                return ForgeAttemptOutcome.Rejected(ForgeFailReason.ActionNotAllowedForTool);
            }

            if (toolTypeData.CanEnhance && instance.EnhanceLevel < 10)
            {
                return ForgeAttemptOutcome.Rejected(ForgeFailReason.PromotionRequiresMaxEnhanceLevel);
            }

            if (!tierData.HasNextTier(instance.TierIndex))
            {
                return ForgeAttemptOutcome.Rejected(ForgeFailReason.NoNextTierDefined);
            }

            var currentTier = tierData.GetTier(instance.TierIndex);
            string materialId = currentTier != null ? currentTier.TierMaterialItemId : null;
            int materialCost = enhancementTable.PromotionMaterialCost;
            int goldCost = enhancementTable.PromotionGoldCost;

            if (!HasEnoughMaterialAndGold(materialId, materialCost, goldCost, out var shortageReason))
            {
                return ForgeAttemptOutcome.Rejected(shortageReason);
            }

            ConsumeMaterialAndGold(materialId, materialCost, goldCost);

            bool guaranteed = instance.OverheatPercent >= enhancementTable.OverheatGuaranteedThreshold - overheatEpsilon;
            bool success = guaranteed || Random.value < enhancementTable.PromotionSuccessRate;

            if (success)
            {
                int nextTierIndex = instance.TierIndex + 1;
                string nextItemId = toolTypeData.GetItemId(nextTierIndex);

                if (string.IsNullOrEmpty(nextItemId))
                {
                    Debug.LogWarning($"[ForgeManager] {toolTypeData.ToolType} 티어 {nextTierIndex}에 매핑된 Item_ID가 없습니다. " +
                                      "ForgeToolTypeData의 TierItems에 해당 티어 항목을 추가해야 합니다.");
                    return ForgeAttemptOutcome.Rejected(ForgeFailReason.NoNextTierDefined);
                }

                instance.TierIndex = nextTierIndex;
                instance.BaseItemId = nextItemId;
                instance.EnhanceLevel = 0;
                instance.OverheatPercent = 0f;
            }
            else
            {
                instance.OverheatPercent = Mathf.Min(1f, instance.OverheatPercent + enhancementTable.PromotionOverheatCharge);
            }

            ApplyInstanceToSlot(stack, instance);

            return new ForgeAttemptOutcome(
                true,
                success ? ForgeAttemptResult.Success : ForgeAttemptResult.Failure,
                ForgeFailReason.None,
                instance.EnhanceLevel,
                instance.TierIndex,
                instance.OverheatPercent,
                guaranteed,
                false);
        }

        /// <summary>읽기 전용 상태 조회(등록/생성 없음). 합성 ID면 실제 인스턴스를, 아니면 "0강" 가상 상태를 반환한다.</summary>
        private bool TryPeekState(ItemStack stack, out ForgeToolTypeData toolTypeData, out int tierIndex,
            out int enhanceLevel, out float overheatPercent, out bool hasRealInstance)
        {
            toolTypeData = null;
            tierIndex = -1;
            enhanceLevel = 0;
            overheatPercent = 0f;
            hasRealInstance = false;

            if (stack == null || stack.IsEmpty) return false;

            if (ForgeInstanceRegistry.TryParseCompositeId(stack.itemId, out _, out var instanceId))
            {
                var instance = instanceRegistry != null ? instanceRegistry.GetInstance(instanceId) : null;
                if (instance == null) return false;

                toolTypeData = GetToolTypeData(instance.ToolType);
                if (toolTypeData == null) return false;

                tierIndex = instance.TierIndex;
                enhanceLevel = instance.EnhanceLevel;
                overheatPercent = instance.OverheatPercent;
                hasRealInstance = true;
                return true;
            }

            if (!ResolveToolTypeAndTier(stack.itemId, out toolTypeData, out tierIndex)) return false;

            return true;
        }

        /// <summary>대상 슬롯의 강화 개체를 가져온다. 첫 시도라면 새로 등록한다(실제 시도 실행 경로 전용).</summary>
        private bool TryGetOrCreateInstance(ItemStack stack, out ForgeInstanceData instance, out ForgeToolTypeData toolTypeData)
        {
            instance = null;
            toolTypeData = null;

            if (stack == null || stack.IsEmpty) return false;

            if (ForgeInstanceRegistry.TryParseCompositeId(stack.itemId, out _, out var instanceId))
            {
                instance = instanceRegistry != null ? instanceRegistry.GetInstance(instanceId) : null;
                if (instance == null) return false;

                toolTypeData = GetToolTypeData(instance.ToolType);
                return toolTypeData != null;
            }

            if (!ResolveToolTypeAndTier(stack.itemId, out toolTypeData, out int tierIndex))
            {
                return false;
            }

            instance = instanceRegistry.CreateInstance(stack.itemId, toolTypeData.ToolType, tierIndex);
            return true;
        }

        private void ApplyInstanceToSlot(ItemStack stack, ForgeInstanceData instance)
        {
            stack.itemId = instance.BuildCompositeId();

            if (itemDataProvider != null)
            {
                itemDataProvider.RefreshRuntimeItemData(stack.itemId);
            }
        }

        private bool HasEnoughMaterialAndGold(string materialItemId, int materialCost, int goldCost, out ForgeFailReason shortageReason)
        {
            shortageReason = ForgeFailReason.None;
            var materials = MaterialInventory;

            if (!string.IsNullOrEmpty(materialItemId) && materialCost > 0
                && (materials == null || !materials.HasEnough(materialItemId, materialCost)))
            {
                shortageReason = ForgeFailReason.NotEnoughMaterial;
                return false;
            }

            if (goldCost > 0 && (territoryData == null || territoryData.Gold < goldCost))
            {
                shortageReason = ForgeFailReason.NotEnoughGold;
                return false;
            }

            return true;
        }

        private void ConsumeMaterialAndGold(string materialItemId, int materialCost, int goldCost)
        {
            var materials = MaterialInventory;

            if (!string.IsNullOrEmpty(materialItemId) && materialCost > 0 && materials != null)
            {
                materials.Consume(materialItemId, materialCost);
            }

            if (goldCost > 0 && territoryData != null)
            {
                territoryData.TrySpendGold(goldCost);
            }
        }

        private bool ValidateDependencies()
        {
            return tierData != null && enhancementTable != null && instanceRegistry != null && catalogManager != null;
        }
    }
}
