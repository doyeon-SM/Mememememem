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
    /// 강화/승급/연마/전승 시도를 실제로 처리하는 매니저. 재료·골드 소비, 확률 판정, 모루 과열 수치 관리,
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

        [Header("연마 설정")]
        [Tooltip("연마(Refinement)/전승(Inheritance) 관련 확률·비용 테이블. 비워두면 연마 관련 기능은 전부 비활성화된다.")]
        [SerializeField] private RefinementConfig refinementConfig;

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

                // [연마 시스템] 승급(티어업)은 연마칸/옵션에 영향을 주지 않는다 - instance.RefinementSlots는 그대로 유지된다.
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

        // ==================== 연마(Refinement) ====================

        /// <summary>
        /// 인벤토리/창고에 새로 들어온 도구가 아직 강화 개체로 등록되지 않았다면(합성 ID가 아니라면)
        /// 즉시 인스턴스를 만들고 연마 슬롯을 채운다. ForgeRefinementAutoAssigner가 인벤토리 변경 이벤트에서
        /// 호출해서, 도구가 "제작 즉시" 연마 슬롯을 가진 것처럼 동작하게 만든다.
        /// 이미 합성 ID인데 연마 슬롯만 비어있는 경우(예: 이전 버전 데이터)도 마찬가지로 채워준다.
        /// 실제로 뭔가 변경되었으면 true.
        /// </summary>
        public bool TryEnsureRefinementInstance(ItemStack stack)
        {
            if (!ValidateDependencies() || refinementConfig == null) return false;
            if (stack == null || stack.IsEmpty) return false;

            return TryGetInstanceForRefinement(stack, out _, out _);
        }

        /// <summary>
        /// 읽기 전용 조회(등록 없음, 실행 아님). 슬롯이 아직 없다면 방어적으로 채워서 돌려준다.
        /// ForgeUI_RefinementPanel/ForgeUI_InheritancePanel이 슬롯 표시에 사용한다.
        /// </summary>
        public bool TryPeekRefinementSlots(ItemStack stack, out ForgeRefinementSlotData[] slots)
        {
            slots = null;

            if (!ValidateDependencies() || refinementConfig == null) return false;
            if (!TryGetInstanceForRefinement(stack, out var instance, out _)) return false;

            slots = instance.RefinementSlots;
            return true;
        }

        /// <summary>도구 제작(첫 등록) 시 슬롯개수(1~5)를 굴리고, 각 칸을 Rare 등급 랜덤 옵션으로 채운다.</summary>
        private void AssignInitialRefinementSlots(ForgeInstanceData instance)
        {
            if (refinementConfig == null || instance == null) return;

            int slotCount = refinementConfig.RollSlotCount();
            var slots = new ForgeRefinementSlotData[slotCount];
            var optionTable = refinementConfig.GetOptionTable();

            for (int i = 0; i < slotCount; i++)
            {
                string optionType = null;
                string displayName = null;
                float value = 0f;
                optionTable?.TryPickRandom(CommonClass.Rare, out optionType, out displayName, out value);
                slots[i] = new ForgeRefinementSlotData(CommonClass.Rare, optionType, displayName, value);
            }

            instance.RefinementSlots = slots;
        }

        /// <summary>
        /// 강화 시스템과 동일한 인스턴스를 재사용하되, 연마 슬롯이 비어있으면 방어적으로 채워준다.
        ///
        /// [버그 수정] 이 stack이 아직 합성 ID가 아니었다면(일반 아이템), TryGetOrCreateInstance는 매번
        /// "새" ForgeInstanceData를 만들어 레지스트리에 등록만 하고 stack.itemId는 그대로 둔다. 그래서 이
        /// 메서드가 커밋(ApplyInstanceToSlot) 없이 끝나면, 같은 아이템을 다시 조회할 때마다(예: UI가 매
        /// 프레임/매 갱신마다 TryPeekRefinementSlots를 부르는 경우) 매번 새 인스턴스가 또 만들어지고 이전
        /// 인스턴스는 레지스트리에 고아로 남아 계속 쌓이는 문제("연마 인스턴스 복제")가 있었다.
        /// 새로 만든 인스턴스는 이 메서드 안에서 즉시 합성 ID로 커밋해서, 다음 조회부터는 항상 같은
        /// 인스턴스를 재사용하도록 고정한다 - 그래서 이 메서드는 이름과 달리 "완전한 읽기 전용"은 아니고,
        /// 최초 1회에 한해 상태를 확정짓는(materialize) 부수효과를 갖는다.
        /// </summary>
        private bool TryGetInstanceForRefinement(ItemStack stack, out ForgeInstanceData instance, out ForgeToolTypeData toolTypeData)
        {
            instance = null;
            toolTypeData = null;

            if (stack == null || stack.IsEmpty) return false;

            bool alreadyComposite = ForgeInstanceRegistry.IsCompositeId(stack.itemId);

            if (!TryGetOrCreateInstance(stack, out instance, out toolTypeData))
            {
                return false;
            }

            if (instance.RefinementSlots == null || instance.RefinementSlots.Length == 0)
            {
                AssignInitialRefinementSlots(instance);
            }

            if (!alreadyComposite)
            {
                ApplyInstanceToSlot(stack, instance);
            }

            return true;
        }

        /// <summary>도구 전체 슬롯 중 최고 등급을 찾는다. 슬롯이 없으면 Rare.</summary>
        private static CommonClass GetHighestGrade(ForgeRefinementSlotData[] slots)
        {
            var highest = CommonClass.Rare;
            if (slots == null) return highest;

            foreach (var slot in slots)
            {
                if (slot != null && (int)slot.Grade > (int)highest)
                {
                    highest = slot.Grade;
                }
            }

            return highest;
        }

        /// <summary>기본비용(최고등급 기준) + 잠근 슬롯들의 잠금비용 합산.</summary>
        private int CalculateRefinementStoneCost(ForgeRefinementSlotData[] slots, bool[] lockedSlotIndices)
        {
            int total = refinementConfig.GetBaseCost(GetHighestGrade(slots));

            if (slots != null && lockedSlotIndices != null)
            {
                for (int i = 0; i < slots.Length && i < lockedSlotIndices.Length; i++)
                {
                    if (lockedSlotIndices[i] && slots[i] != null)
                    {
                        total += refinementConfig.GetLockCost(slots[i].Grade);
                    }
                }
            }

            return total;
        }

        /// <summary>
        /// 연마 실행 버튼을 누르기 전 미리보기. lockedSlotIndices는 슬롯 배열과 같은 길이여야 하며,
        /// true인 인덱스는 "이번 연마에서 잠가서 보호할 칸"을 의미한다(null이면 전부 재판정 대상).
        /// </summary>
        public RefinementPreview GetRefinementPreview(ItemStack stack, bool[] lockedSlotIndices)
        {
            if (!ValidateDependencies() || refinementConfig == null)
            {
                return RefinementPreview.Blocked(RefinementFailReason.MissingDependency);
            }

            if (!TryGetInstanceForRefinement(stack, out var instance, out _))
            {
                return RefinementPreview.Blocked(RefinementFailReason.NotForgeableTool);
            }

            if (instance.RefinementSlots == null || instance.RefinementSlots.Length == 0)
            {
                return RefinementPreview.Blocked(RefinementFailReason.NoInstanceData);
            }

            int stoneCost = CalculateRefinementStoneCost(instance.RefinementSlots, lockedSlotIndices);
            int goldCost = refinementConfig.FixedGoldCost;

            var materials = MaterialInventory;
            int stoneOwned = materials != null ? materials.GetAmount(refinementConfig.RefinementMaterialItemId) : 0;
            int goldOwned = territoryData != null ? territoryData.Gold : 0;

            return new RefinementPreview(RefinementFailReason.None, refinementConfig.RefinementMaterialItemId,
                stoneCost, stoneOwned, goldCost, goldOwned);
        }

        /// <summary>
        /// 연마를 한 번 시도한다. lockedSlotIndices에서 true인 칸은 건드리지 않고, 나머지 칸은 전부
        /// (등급변화확률 판정 → 결정된 등급 내 종류/수치 재판정) 순서로 재판정된다. 실패라는 결과는 없다 -
        /// 재료/골드만 충분하면 항상 무언가 바뀐다(등급이 그대로여도 종류/수치는 재판정됨).
        /// </summary>
        public RefinementOutcome TryRefine(ItemStack stack, bool[] lockedSlotIndices)
        {
            if (!ValidateDependencies() || refinementConfig == null)
            {
                return RefinementOutcome.Rejected(RefinementFailReason.MissingDependency);
            }

            if (!TryGetInstanceForRefinement(stack, out var instance, out _))
            {
                return RefinementOutcome.Rejected(RefinementFailReason.NotForgeableTool);
            }

            if (instance.RefinementSlots == null || instance.RefinementSlots.Length == 0)
            {
                return RefinementOutcome.Rejected(RefinementFailReason.NoInstanceData);
            }

            int stoneCost = CalculateRefinementStoneCost(instance.RefinementSlots, lockedSlotIndices);
            int goldCost = refinementConfig.FixedGoldCost;

            if (!HasEnoughMaterialAndGold(refinementConfig.RefinementMaterialItemId, stoneCost, goldCost, out var shortageReason))
            {
                var refinementReason = shortageReason == ForgeFailReason.NotEnoughGold
                    ? RefinementFailReason.NotEnoughGold
                    : RefinementFailReason.NotEnoughMaterial;
                return RefinementOutcome.Rejected(refinementReason);
            }

            ConsumeMaterialAndGold(refinementConfig.RefinementMaterialItemId, stoneCost, goldCost);

            var optionTable = refinementConfig.GetOptionTable();

            for (int i = 0; i < instance.RefinementSlots.Length; i++)
            {
                bool isLocked = lockedSlotIndices != null && i < lockedSlotIndices.Length && lockedSlotIndices[i];
                if (isLocked) continue;

                var slot = instance.RefinementSlots[i];
                if (slot == null) continue;

                // 1단계: 등급변화확률로 등급 결정 (최고등급이면 스킵, 항상 유지)
                if (!refinementConfig.IsMaxGrade(slot.Grade) && refinementConfig.RollGradeUp(slot.Grade))
                {
                    slot.Grade = refinementConfig.GetNextGrade(slot.Grade);
                }

                // 2단계: 결정된 등급 내에서 종류+수치 재판정 (등급이 그대로여도 항상 재판정됨)
                if (optionTable != null && optionTable.TryPickRandom(slot.Grade, out var optionType, out var displayName, out var value))
                {
                    slot.OptionType = optionType;
                    slot.DisplayName = displayName;
                    slot.Value = value;
                }
            }

            if (itemDataProvider != null)
            {
                itemDataProvider.RefreshRuntimeItemData(stack.itemId);
            }

            return new RefinementOutcome(true, RefinementFailReason.None, instance.RefinementSlots);
        }

        // ==================== 전승(Inheritance) ====================

        /// <summary>
        /// 재료 도구의 연마칸 수·옵션을 받는 도구에 그대로 옮긴다(받는 도구의 기존 연마 옵션은 버려짐).
        /// 도구 종류만 같으면 티어는 달라도 무관하다. 무조건 성공하며 추가 비용은 없다. 재료 도구는 소멸한다.
        /// </summary>
        public InheritanceOutcome TryInherit(ItemStack materialStack, ItemStack targetStack)
        {
            if (!ValidateDependencies())
            {
                return InheritanceOutcome.Rejected(InheritanceFailReason.MissingDependency);
            }

            if (materialStack == null || targetStack == null || materialStack.IsEmpty || targetStack.IsEmpty)
            {
                return InheritanceOutcome.Rejected(InheritanceFailReason.NotForgeableTool);
            }

            if (ReferenceEquals(materialStack, targetStack))
            {
                return InheritanceOutcome.Rejected(InheritanceFailReason.SameStack);
            }

            if (!TryGetInstanceForRefinement(materialStack, out var materialInstance, out _))
            {
                return InheritanceOutcome.Rejected(InheritanceFailReason.NotForgeableTool);
            }

            if (!TryGetInstanceForRefinement(targetStack, out var targetInstance, out _))
            {
                return InheritanceOutcome.Rejected(InheritanceFailReason.NotForgeableTool);
            }

            if (materialInstance.ToolType != targetInstance.ToolType)
            {
                return InheritanceOutcome.Rejected(InheritanceFailReason.ToolTypeMismatch);
            }

            var sourceSlots = materialInstance.RefinementSlots ?? System.Array.Empty<ForgeRefinementSlotData>();
            var copiedSlots = new ForgeRefinementSlotData[sourceSlots.Length];

            for (int i = 0; i < sourceSlots.Length; i++)
            {
                copiedSlots[i] = sourceSlots[i]?.Clone();
            }

            targetInstance.RefinementSlots = copiedSlots;

            // 재료 도구는 소멸한다.
            if (instanceRegistry != null) instanceRegistry.RemoveInstance(materialInstance.InstanceId);
            if (itemDataProvider != null) itemDataProvider.ClearCache(materialStack.itemId);
            materialStack.Clear();

            if (itemDataProvider != null)
            {
                itemDataProvider.RefreshRuntimeItemData(targetStack.itemId);
            }

            return InheritanceOutcome.Success;
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
