using System.Collections.Generic;
using UnityEngine;
using HDY.Item;
using HDY.Territory;
using HDY.Recipe;
using HDY.Upgrade;

namespace HDY.UI
{
    /// <summary>
    /// 여신상 UI 컨트롤러.
    /// RecipeUnlockManager / TerritoryExpansionManager / TerritoryData / ItemCatalogManager에서 데이터를
    /// 가져와, 요구 영지 레벨별로 (영지 확장 -> 레시피 순으로) 슬롯을 묶어서
    /// 줄(GoddessStatueUI_LevelRow)을 스크롤 영역(Content) 안에 만든다.
    ///
    /// [생명주기 = UIManager가 관리] 이 컴포넌트는 씬에 상시 배치된 오브젝트가 아니라, HUD의 "여신상"
    /// 버튼을 누를 때마다 UIManager가 프리팹을 Instantiate해서 만들고 닫힐 때 Destroy한다. 그래서
    /// recipeUnlockManager/territoryExpansionManager/territoryData처럼 다른 씬 오브젝트를 가리키는
    /// 참조들은 프리팹 에셋 자체에 저장될 수 없어(프리팹은 특정 씬의 오브젝트를 가리킬 수 없음) 전부
    /// Awake에서 씬을 훑어 자동으로 채운다(ShopUI.territoryData와 동일한 이유).
    ///
    /// [해금/확장 흐름] 슬롯을 선택하는 즉시 공용 업그레이드 팝업(UpgradePopupUI)을 띄운다. 팝업에서 실제
    /// 결제(골드+재료) 확인/차감을 전부 처리하고, 확인 버튼을 누르면 결과에 따라
    /// RecipeUnlockUpgrade/TerritoryExpansionUpgrade(IUpgradable 어댑터, 슬롯마다 즉석에서 new로 생성)의
    /// ApplyUpgrade()가 호출되어 실제 해금/확장이 반영된다.
    ///
    /// [팝업 닫힘 -> 선택 해제, 구독 타이밍 방어] UpgradePopupUI.OnPopupClosed를 구독해서, 확인 성공이든
    /// 취소든 팝업이 어떤 이유로 닫히든 선택 강조 표시를 꺼준다. Unity는 "모든 오브젝트의 Awake가 다른
    /// 오브젝트의 Start보다 먼저 실행됨"은 보장하지만, Awake와 다른 오브젝트의 OnEnable 사이에는 순서를
    /// 보장하지 않는다 - 그래서 OnEnable 시점에는 UpgradePopupUI.Instance가 아직 설정되기 전(그 컴포넌트의
    /// Awake가 아직 실행 안 됨)일 수 있다. 이를 방어하기 위해 OnEnable과 Start 양쪽에서 구독을 시도하고,
    /// 이미 구독했으면 다시 시도하지 않도록 subscribedToPopupClosed 플래그로 관리한다(Start는 모든 Awake가
    /// 끝난 뒤 호출되는 것이 보장되므로, 최초 활성화 시점에는 여기서 반드시 성공한다).
    ///
    /// [영지 확장 슬롯 - 레벨별 독립 노출 + 레벨별 고유 id] TerritoryExpansionManager.GetAllPendingEntries()로
    /// "아직 완료되지 않은 확장 단계 전부"를 가져와서, 각 단계가 요구하는 레벨의 줄에 슬롯을 하나씩 미리
    /// 노출한다. 즉 Lv.3 단계가 아직 안 끝났어도 Lv.4 줄의 grid 자체는 항상 만들어지고, 다만 "지금 실제로
    /// 진행 가능한 단계"(TerritoryExpansionManager.CanAttemptExpand = 순차 진행 규칙)가 아니면 그 슬롯은
    /// 잠금(비활성) 상태로만 보인다. 이렇게 여러 확장 슬롯이 동시에 존재할 수 있기 때문에, 슬롯 id는
    /// 고정 문자열 하나가 아니라 요구 레벨을 포함한 고유 id(BuildExpansionSlotId, 예: "__territory_expansion__:4")
    /// 를 슬롯마다 따로 부여한다 - 그렇지 않으면 여러 슬롯이 같은 id를 공유하게 되어, 하나를 선택했을 때
    /// RefreshSelection(ItemId 일치 여부로 강조 표시를 켬)이 같은 id를 가진 다른 슬롯까지 함께 강조해버린다.
    /// 슬롯 id -> 확장 단계 매핑은 expansionEntriesBySlotId에 BuildRows에서 채워두고, 클릭 시 그 매핑으로
    /// "정확히 그 슬롯이 가리키던" 단계를 찾는다(GetNextPendingEntry()에 기대지 않음 - 여러 슬롯이 동시에
    /// 떠 있을 수 있으므로 클릭한 슬롯과 항상 일치한다는 보장이 없다). 같은 레벨에 확장 단계가 두 개 이상
    /// 걸려있는 경우는 지원하지 않는다(줄 하나에 확장 슬롯은 최대 하나 - 먼저 나온 것만 표시하고 경고 로그를
    /// 남긴다). 레시피와 같은 슬롯 컴포넌트(GoddessRecipeSlotUI)를 재사용하되, 아이콘은
    /// territoryExpansionIcon(전용 아이콘, ItemData 기반 아님)을 사용한다.
    ///
    /// [스크롤] 마우스 휠 스크롤은 Unity의 ScrollRect가 기본으로 지원하므로 이 스크립트에서 별도 처리하지 않는다.
    /// [그리드 최대 2줄] 각 줄의 슬롯 배치는 GoddessStatueUI_LevelRow의 Grid Layout Group(Constraint를
    /// Fixed Row Count=2로 설정)이 알아서 처리하므로, 이 컨트롤러는 슬롯 개수만 넘겨주면 된다.
    /// </summary>
    public class GoddessStatueUI : MonoBehaviour
    {
        /// <summary>영지 확장 슬롯 id의 접두사. 실제 Item_ID와 겹치지 않도록 언더스코어로 감쌌다.
        /// 뒤에 요구 레벨을 붙여(BuildExpansionSlotId) 슬롯마다 고유한 id를 만든다 - 레벨별로 여러 확장
        /// 슬롯이 동시에 노출될 수 있어서, 전부 같은 id를 쓰면 하나를 선택했을 때 다른 확장 슬롯까지
        /// 함께 강조 표시되는 문제가 생긴다.</summary>
        private const string TerritoryExpansionSlotIdPrefix = "__territory_expansion__:";

        private static string BuildExpansionSlotId(int requestTerritoryLevel)
        {
            return TerritoryExpansionSlotIdPrefix + requestTerritoryLevel;
        }

        [Header("데이터 참조")]
        [SerializeField] private RecipeUnlockManager recipeUnlockManager;
        [SerializeField] private TerritoryExpansionManager territoryExpansionManager;
        [SerializeField] private TerritoryData territoryData;
        [SerializeField] private ItemCatalogManager itemCatalogManager; // 비어있으면 자동 탐색(싱글톤 -> 씬 검색)

        [Header("영지 확장 슬롯 아이콘 (ItemData 기반이 아닌 전용 아이콘)")]
        [SerializeField] private Sprite territoryExpansionIcon;

        [Header("레벨별 줄 배치 (ScrollRect의 Content를 rowsParent로 연결)")]
        [SerializeField] private Transform rowsParent;
        [SerializeField] private GoddessStatueUI_LevelRow rowPrefab;

        private readonly List<GoddessStatueUI_LevelRow> rows = new List<GoddessStatueUI_LevelRow>();

        /// <summary>슬롯 id(레벨별로 고유, BuildExpansionSlotId) -> 그 슬롯이 가리키는 확장 단계.
        /// BuildRows에서 채우고 ClearRows에서 비운다. 클릭된 슬롯이 정확히 어떤 단계인지 찾는 데 쓴다.</summary>
        private readonly Dictionary<string, TerritoryExpansionEntry> expansionEntriesBySlotId = new Dictionary<string, TerritoryExpansionEntry>();

        private string selectedItemId;

        // UpgradePopupUI.Instance(싱글톤)는 Awake 실행 순서에 따라 이 컴포넌트의 OnEnable 시점에 아직 없을
        // 수 있어서, 구독 성공 여부를 별도로 기억해두고 OnEnable/Start 양쪽에서 시도한다.
        private bool subscribedToPopupClosed;

        private void Awake()
        {
            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);

            // 아래 세 참조는 다른 씬 오브젝트를 가리키는 참조라 프리팹 에셋 자체에는 저장될 수 없다
            // (UIManager가 매번 새로 Instantiate하기 때문에 인스펙터 값이 항상 비어있다) - 그래서
            // ShopUI.territoryData와 동일하게 씬에서 자동으로 찾도록 폴백을 둔다.
            if (recipeUnlockManager == null) recipeUnlockManager = FindFirstObjectByType<RecipeUnlockManager>();
            if (territoryExpansionManager == null) territoryExpansionManager = FindFirstObjectByType<TerritoryExpansionManager>();
            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();

            if (recipeUnlockManager == null) Debug.LogWarning("[GoddessStatueUI] recipeUnlockManager가 비어있습니다.", this);
            if (territoryExpansionManager == null) Debug.LogWarning("[GoddessStatueUI] territoryExpansionManager가 비어있습니다. 영지 확장 슬롯이 표시되지 않습니다.", this);
            if (territoryData == null) Debug.LogWarning("[GoddessStatueUI] territoryData가 비어있습니다.", this);
            if (itemCatalogManager == null) Debug.LogWarning("[GoddessStatueUI] itemCatalogManager를 찾을 수 없습니다.", this);
            if (rowsParent == null) Debug.LogWarning("[GoddessStatueUI] rowsParent가 비어있습니다.", this);
            if (rowPrefab == null) Debug.LogWarning("[GoddessStatueUI] rowPrefab이 비어있습니다.", this);
        }

        private void OnEnable()
        {
            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);

            if (recipeUnlockManager != null) recipeUnlockManager.OnRecipeUnlocksChanged += HandleDataChanged;
            if (territoryExpansionManager != null) territoryExpansionManager.OnExpansionChanged += HandleDataChanged;

            TrySubscribeToPopupClosed();

            selectedItemId = null;
            BuildRows();
        }

        private void Start()
        {
            // OnEnable 시점에 UpgradePopupUI.Instance가 아직 없었을 경우를 대비한 재시도.
            // Start는 모든 오브젝트의 Awake가 끝난 뒤에 호출되는 것이 Unity에서 보장되므로 여기서는 반드시 성공한다.
            TrySubscribeToPopupClosed();
        }

        private void OnDisable()
        {
            if (recipeUnlockManager != null) recipeUnlockManager.OnRecipeUnlocksChanged -= HandleDataChanged;
            if (territoryExpansionManager != null) territoryExpansionManager.OnExpansionChanged -= HandleDataChanged;

            if (subscribedToPopupClosed && UpgradePopupUI.Instance != null)
            {
                UpgradePopupUI.Instance.OnPopupClosed -= HandlePopupClosed;
            }

            subscribedToPopupClosed = false;
        }

        /// <summary>이미 구독했으면 아무 것도 하지 않는다. UpgradePopupUI.Instance가 아직 없으면 조용히 넘어가고(다음 시도에서 재시도), 있으면 구독하고 플래그를 세운다.</summary>
        private void TrySubscribeToPopupClosed()
        {
            if (subscribedToPopupClosed) return;
            if (UpgradePopupUI.Instance == null) return;

            UpgradePopupUI.Instance.OnPopupClosed += HandlePopupClosed;
            subscribedToPopupClosed = true;
        }

        /// <summary>레시피 해금/영지 확장 상태가 바뀌었을 때(팝업 결제 성공 등) 호출. 선택을 풀고 줄 전체를 다시 그린다.</summary>
        private void HandleDataChanged()
        {
            selectedItemId = null;
            BuildRows();
        }

        /// <summary>
        /// 업그레이드 팝업이 어떤 이유로든(확인 성공/취소) 닫혔을 때 호출. 선택 강조 표시만 끈다(줄을 다시
        /// 그리지는 않음 - 데이터가 안 바뀌었을 수도 있어서). 성공 케이스는 HandleDataChanged가 이미 줄을
        /// 새로 그려서 선택도 같이 초기화하므로, 여기서 한 번 더 처리해도 안전하다(중복 무해).
        /// </summary>
        private void HandlePopupClosed()
        {
            selectedItemId = null;

            foreach (var row in rows)
            {
                row.RefreshSelection(null);
            }
        }

        /// <summary>
        /// RecipeUnlocks를 요구 영지 레벨별로 묶어서 줄(row)들을 새로 만든다. 기존 줄은 전부 지운다.
        /// 아직 완료되지 않은 영지 확장 단계는 전부(순서 무관) 각자가 요구하는 레벨의 줄에 슬롯을 하나씩
        /// 추가한다(그 레벨에 레시피가 하나도 없어도 줄 자체는 새로 만든다) - "지금 실제로 진행 가능한지"는
        /// CanAttemptExpand로만 판단해서 슬롯의 잠금(비활성) 여부에 반영한다. 슬롯 채우는 순서는 영지 확장
        /// -> 레시피 순. 확장 슬롯마다 BuildExpansionSlotId로 레벨별 고유 id를 부여하고, 그 id -> 단계
        /// 매핑을 expansionEntriesBySlotId에 기록해둔다(클릭 시 정확히 어떤 단계인지 찾기 위함).
        /// </summary>
        private void BuildRows()
        {
            ClearRows();

            if (recipeUnlockManager == null || rowsParent == null || rowPrefab == null) return;

            var groups = new SortedDictionary<int, List<RecipeUnlockEntry>>();
            foreach (var entry in recipeUnlockManager.RecipeUnlocks)
            {
                if (!groups.TryGetValue(entry.RequestTerritoryLevel, out var list))
                {
                    list = new List<RecipeUnlockEntry>();
                    groups[entry.RequestTerritoryLevel] = list;
                }

                list.Add(entry);
            }

            // 레벨별 독립 노출: 완료되지 않은 확장 단계는 전부 각자의 요구 레벨에 매칭해둔다.
            // (같은 레벨에 두 개 이상 걸려있으면 줄 하나에 슬롯 하나만 넣을 수 있는 구조상 첫 번째만 쓴다.)
            var pendingExpansionsByLevel = new Dictionary<int, TerritoryExpansionEntry>();
            if (territoryExpansionManager != null)
            {
                foreach (var entry in territoryExpansionManager.GetAllPendingEntries())
                {
                    if (pendingExpansionsByLevel.ContainsKey(entry.RequestTerritoryLevel))
                    {
                        Debug.LogWarning($"[GoddessStatueUI] 요구 레벨 {entry.RequestTerritoryLevel}에 확장 단계가 두 개 이상 등록되어 있습니다. 줄 하나에는 확장 슬롯을 하나만 표시할 수 있어 먼저 등록된 단계만 노출합니다.", this);
                        continue;
                    }

                    pendingExpansionsByLevel[entry.RequestTerritoryLevel] = entry;

                    if (!groups.ContainsKey(entry.RequestTerritoryLevel))
                    {
                        groups[entry.RequestTerritoryLevel] = new List<RecipeUnlockEntry>();
                    }
                }
            }

            int currentTerritoryLevel = territoryData != null ? territoryData.Level : 0;

            foreach (var pair in groups)
            {
                var row = Instantiate(rowPrefab, rowsParent);

                GoddessStatueUI_LevelRow.ExtraSlotInfo? extraSlot = null;
                if (pendingExpansionsByLevel.TryGetValue(pair.Key, out var expansionEntry))
                {
                    bool interactable = territoryExpansionManager.CanAttemptExpand(expansionEntry, currentTerritoryLevel);
                    string slotId = BuildExpansionSlotId(expansionEntry.RequestTerritoryLevel);
                    expansionEntriesBySlotId[slotId] = expansionEntry;
                    extraSlot = new GoddessStatueUI_LevelRow.ExtraSlotInfo(slotId, territoryExpansionIcon, false, interactable);
                }

                row.Setup(pair.Key, pair.Value, FindItemIcon, CanAttemptUnlock, extraSlot);
                row.OnSlotSelected += HandleSlotSelected;
                rows.Add(row);
            }
        }

        private void ClearRows()
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                row.OnSlotSelected -= HandleSlotSelected;
                Destroy(row.gameObject);
            }

            rows.Clear();
            expansionEntriesBySlotId.Clear();
        }

        private Sprite FindItemIcon(string itemId)
        {
            var data = recipeUnlockManager != null ? recipeUnlockManager.FindRecipeItemData(itemId) : null;
            return data != null ? data.ItemIcon : null;
        }

        private bool CanAttemptUnlock(RecipeUnlockEntry entry)
        {
            if (entry == null || territoryData == null || recipeUnlockManager == null) return false;
            return recipeUnlockManager.CanAttemptUnlock(entry.Item_ID, territoryData.Level);
        }

        /// <summary>
        /// 슬롯 선택 처리. 강조 표시를 갱신한 뒤, 선택된 것이 레시피인지 영지 확장 슬롯인지에 따라
        /// 알맞은 IUpgradable 어댑터를 즉석에서 만들어 공용 업그레이드 팝업을 띄운다.
        /// </summary>
        private void HandleSlotSelected(string itemId)
        {
            selectedItemId = itemId;

            foreach (var row in rows)
            {
                row.RefreshSelection(selectedItemId);
            }

            ShowUpgradePopupForSelection();
        }

        private void ShowUpgradePopupForSelection()
        {
            if (string.IsNullOrEmpty(selectedItemId)) return;

            // Instance가 아직 없을 수도 있는 타이밍 문제와는 별개로, 팝업을 실제로 여는 시점은 항상
            // 사용자의 클릭(플레이 중) 이후라 이 시점에는 모든 Awake가 끝난 지 오래이므로 안전하다.
            if (UpgradePopupUI.Instance == null)
            {
                Debug.LogWarning("[GoddessStatueUI] 씬에서 UpgradePopupUI를 찾을 수 없습니다.", this);
                return;
            }

            // 혹시 OnEnable/Start 양쪽에서 구독을 놓쳤다면(비정상적인 타이밍) 여기서 한 번 더 시도한다.
            TrySubscribeToPopupClosed();

            if (selectedItemId.StartsWith(TerritoryExpansionSlotIdPrefix))
            {
                // 슬롯 id에 요구 레벨이 포함되어 있어(BuildExpansionSlotId), BuildRows에서 만들어둔 매핑으로
                // "정확히 이 슬롯이 가리키던" 확장 단계를 찾는다 - 여러 확장 슬롯이 동시에 떠 있을 수 있으므로
                // GetNextPendingEntry()에 기대지 않는다.
                if (!expansionEntriesBySlotId.TryGetValue(selectedItemId, out var expansionEntry) || expansionEntry == null)
                {
                    Debug.LogWarning($"[GoddessStatueUI] 슬롯 id '{selectedItemId}'에 해당하는 영지 확장 단계를 찾을 수 없습니다.", this);
                    return;
                }

                var expansionUpgrade = new TerritoryExpansionUpgrade(expansionEntry, territoryExpansionManager, territoryData);
                UpgradePopupUI.Instance.Show(expansionUpgrade);
                return;
            }

            var recipeEntry = FindEntry(selectedItemId);
            if (recipeEntry == null)
            {
                Debug.LogWarning($"[GoddessStatueUI] Item_ID '{selectedItemId}'에 해당하는 레시피 항목을 찾을 수 없습니다.", this);
                return;
            }

            var recipeUpgrade = new RecipeUnlockUpgrade(recipeEntry, recipeUnlockManager, territoryData);
            UpgradePopupUI.Instance.Show(recipeUpgrade);
        }

        private RecipeUnlockEntry FindEntry(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || recipeUnlockManager == null) return null;

            foreach (var entry in recipeUnlockManager.RecipeUnlocks)
            {
                if (entry.Item_ID == itemId) return entry;
            }

            return null;
        }
    }
}
