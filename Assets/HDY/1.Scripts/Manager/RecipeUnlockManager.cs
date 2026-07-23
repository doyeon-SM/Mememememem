using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using HDY.Item;
using HDY.Territory;

namespace HDY.Recipe
{
    /// <summary>
    /// 제작법 하나(Item_ID)와 해금 조건/여부를 짝지어 보관하는 항목.
    /// Item_ID는 ItemCatalogManager에 등록된 ItemData.Item_ID와 매칭된다(SO를 직접 들고 있지 않음).
    /// RequestTerritoryLevel: 이 레시피를 해금하기 위해 필요한 영지 레벨(기본값 1).
    /// RequestGold: 여신상 UI에서 이 레시피를 구매할 때 필요한 골드.
    /// MaterialCosts: 골드와 함께 필요한 재료(공용 업그레이드 팝업에 표시/차감됨).
    /// RewardExp: 이 레시피 해금에 성공했을 때 획득하는 영지 경험치(TerritoryData.CurrentExp).
    /// IsUnlocked: 실제 해금 여부(두 조건을 모두 만족해야 true가 됨).
    ///
    /// [HDY 요청 - 시트 마이그레이션] Item_ID/RequestTerritoryLevel/RequestGold/MaterialCosts/RewardExp는
    /// recipeUnlockSheet에서 파싱해 채우는 "정적 설정값"이고, IsUnlocked만 파싱 시 항상 false로 시작하는
    /// "런타임 상태"다 - 세이브 로드나 Unlock()/ApplyUnlock() 호출로만 바뀐다. _Kyusoo의
    /// TerritoryRecordData.cs가 리플렉션으로 RecipeUnlockManager의 private 필드 "recipeUnlocks"를
    /// List&lt;RecipeUnlockEntry&gt;로 직접 캐스팅해서 IsUnlocked를 "리스트의 인덱스 순서"로 저장/복원하기
    /// 때문에, 이 클래스 자체(필드 구성)와 리스트 순서를 함부로 바꾸면 안 된다 - 시트의 줄 순서가 곧
    /// 세이브 데이터의 순서와 대응된다.
    /// </summary>
    [Serializable]
    public class RecipeUnlockEntry
    {
        [Tooltip("ItemCatalogManager에 등록된 ItemData.Item_ID와 매칭되는 값")]
        public string Item_ID;

        [Tooltip("이 레시피를 해금하기 위해 필요한 영지 레벨")]
        public int RequestTerritoryLevel = 1;

        [Tooltip("여신상 UI에서 이 레시피를 구매할 때 필요한 골드")]
        public int RequestGold;

        [Tooltip("골드와 함께 필요한 재료 (공용 업그레이드 팝업에 표시/차감)")]
        public List<Recipe_Requset_Item_Data> MaterialCosts = new List<Recipe_Requset_Item_Data>();

        [Tooltip("이 레시피 해금에 성공하면 획득하는 영지 경험치")]
        public int RewardExp = 0;

        public bool IsUnlocked;
    }

    /// <summary>
    /// 영지에서 사용 가능한 제작법의 해금 여부를 보관하는 매니저.
    /// 제작법은 ItemData(SO)를 직접 참조하지 않고 Item_ID(string)로 관리하며, 실제 아이템 정보가 필요하면
    /// ItemCatalogManager에서 탐색한다.
    ///
    /// [해금 조건] 레시피 해금은 두 조건을 모두 만족해야 한다: (1) 영지 레벨이 RequestTerritoryLevel 이상
    /// (영지 레벨은 TerritoryData.Level), (2) 여신상 UI에서 RequestGold + MaterialCosts를 지불하고 구매.
    ///
    /// [결제 위치 변경] 예전에는 TryPurchase가 레벨 확인+골드 차감+해금까지 한 번에 처리했지만, 이제 여신상
    /// UI는 공용 업그레이드 팝업(UpgradePopupUI)을 통해 결제(골드+재료)를 진행한다 - 팝업이 결제를 마친 뒤에만
    /// ApplyUnlock을 호출해준다. TryPurchase는 결제 로직 자체는 그대로 남겨뒀지만(다른 곳에서 쓸 수도 있어
    /// 삭제하지 않음), 여신상 UI의 새 흐름에서는 더 이상 호출되지 않는다.
    ///
    /// [영지 경험치 보상] 해금에 성공하면(ApplyUnlock/TryPurchase/Unlock 모두 포함) RecipeUnlockEntry.RewardExp만큼
    /// TerritoryData.AddExp가 호출된다. territoryData 참조는 인스펙터에 비어있으면 자동 탐색(FindFirstObjectByType)
    /// 한다. 이미 해금된 항목을 다시 해금 처리해도(재호출) 경험치가 중복 지급되지 않도록 가드한다.
    ///
    /// [저장/불러오기 대응] TerritoryData와 함께 저장/불러오기 시스템이 이 매니저의 생명주기를 직접 관리할
    /// 예정이라, 더 이상 DontDestroyOnLoad 싱글톤을 쓰지 않는다(일반 컴포넌트).
    ///
    /// [HDY 요청 - 시트 마이그레이션] recipeUnlocks는 더 이상 Inspector에서 직접 드래그/입력하지 않고,
    /// Awake 시 recipeUnlockSheet(TextAsset, 탭 구분)를 파싱해서 채운다. IsUnlocked는 시트에 없는 컬럼이라
    /// 파싱 직후에는 항상 false다(실제 해금 여부는 세이브 로드나 플레이 중 Unlock()/ApplyUnlock() 호출로
    /// 채워짐). recipeUnlocks 필드의 이름과 타입(List&lt;RecipeUnlockEntry&gt;)은 _Kyusoo의
    /// TerritoryRecordData.cs가 리플렉션으로 직접 참조하고 있어 절대 바꾸면 안 된다.
    /// </summary>
    public class RecipeUnlockManager : MonoBehaviour
    {
        [Header("아이템 카탈로그 참조 (Item_ID -> ItemData 탐색용, 비어있으면 자동 탐색)")]
        [SerializeField] private ItemCatalogManager itemCatalogManager;

        [Header("영지 데이터 참조 (경험치 지급용, 비어있으면 자동 탐색)")]
        [SerializeField] private TerritoryData territoryData;

        [Header("제작법 해금 시트 (탭 구분 텍스트: Item_ID, RequestTerritoryLevel, RequestGold, MaterialCosts, RewardExp)")]
        [SerializeField] private TextAsset recipeUnlockSheet;

        private void Awake()
        {
            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);

            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData == null) Debug.LogWarning("[RecipeUnlockManager] territoryData를 찾을 수 없습니다.", this);

            BuildRecipeUnlocks();
        }

        // [HDY 요청 - 시트 마이그레이션] 필드 이름 "recipeUnlocks"와 타입은 _Kyusoo의 TerritoryRecordData.cs가
        // 리플렉션으로 직접 참조하므로 절대 바꾸지 않는다. 세이브 데이터가 이 리스트의 "인덱스 순서"로
        // IsUnlocked를 저장/복원하기 때문에, recipeUnlockSheet의 줄 순서도 함부로 바꾸면 안 된다.
        private List<RecipeUnlockEntry> recipeUnlocks = new List<RecipeUnlockEntry>();

        public IReadOnlyList<RecipeUnlockEntry> RecipeUnlocks => recipeUnlocks;

        /// <summary>레시피 해금 상태가 바뀔 때마다(해금/강제 잠금 등) 발행. 여신상 UI가 구독해서 다시 그린다.</summary>
        public event Action OnRecipeUnlocksChanged;

        /// <summary>
        /// 시트를 파싱해 행마다 RecipeUnlockEntry를 만들어 recipeUnlocks에 채운다. IsUnlocked는 시트에
        /// 없으므로 항상 false로 시작한다(세이브 로드나 Unlock()/ApplyUnlock()이 이후에 채운다).
        /// 줄 순서를 그대로 유지해야 세이브 데이터의 인덱스와 어긋나지 않는다.
        /// </summary>
        private void BuildRecipeUnlocks()
        {
            recipeUnlocks.Clear();

            if (recipeUnlockSheet == null)
            {
                Debug.LogWarning("[RecipeUnlockManager] recipeUnlockSheet가 비어있습니다.");
                return;
            }

            var lines = recipeUnlockSheet.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더라 건너뜀
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split('\t');
                if (cols.Length < 5)
                {
                    Debug.LogWarning($"[RecipeUnlockManager] 시트 {i + 1}번째 줄 컬럼 수가 부족합니다: {line}");
                    continue;
                }

                var entry = new RecipeUnlockEntry
                {
                    Item_ID = cols[0].Trim(),
                    RequestTerritoryLevel = ParseInt(cols[1]),
                    RequestGold = ParseInt(cols[2]),
                    MaterialCosts = ParseMaterials(cols[3]),
                    RewardExp = ParseInt(cols[4]),
                    IsUnlocked = false
                };

                if (string.IsNullOrEmpty(entry.Item_ID)) continue;

                recipeUnlocks.Add(entry);
            }
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        /// <summary>"item_wood:30;item_baseblueprint:1" 형식을 파싱한다. 빈 문자열이면 빈 리스트를 반환한다.</summary>
        private static List<Recipe_Requset_Item_Data> ParseMaterials(string raw)
        {
            var materials = new List<Recipe_Requset_Item_Data>();
            if (string.IsNullOrWhiteSpace(raw)) return materials;

            var entries = raw.Split(';');
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                var itemId = parts[0].Trim();
                if (string.IsNullOrEmpty(itemId)) continue;

                if (int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                {
                    materials.Add(new Recipe_Requset_Item_Data { Item_ID = itemId, Amount = amount });
                }
            }

            return materials;
        }

        /// <summary>해당 제작법(Item_ID)의 해금 여부를 반환한다. 목록에 없으면 false.</summary>
        public bool IsUnlocked(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            return entry != null && entry.IsUnlocked;
        }

        /// <summary>
        /// 레시피 해금에 성공했을 때 RewardExp만큼 영지 경험치를 지급한다.
        /// territoryData가 비어있으면(자동 탐색도 실패) 경고만 남기고 아무 것도 하지 않는다.
        /// </summary>
        private void GrantUnlockExp(RecipeUnlockEntry entry)
        {
            if (entry == null || entry.RewardExp <= 0) return;

            if (territoryData == null) territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData == null)
            {
                Debug.LogWarning("[RecipeUnlockManager] territoryData를 찾을 수 없어 경험치를 지급하지 못했습니다.", this);
                return;
            }

            territoryData.AddExp(entry.RewardExp);
        }

        /// <summary>해당 제작법(Item_ID)을 강제로 해금 처리한다(디버그/치트용). 목록에 없으면 새 항목을 추가해 해금 처리.
        /// 치트 해금도 RewardExp만큼 영지 경험치를 지급한다(이미 해금된 항목이면 중복 지급하지 않음).</summary>
        public void Unlock(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            bool alreadyUnlocked = entry != null && entry.IsUnlocked;

            if (entry != null)
            {
                entry.IsUnlocked = true;
            }
            else
            {
                entry = new RecipeUnlockEntry { Item_ID = itemId, RequestTerritoryLevel = 1, IsUnlocked = true };
                recipeUnlocks.Add(entry);
            }

            if (!alreadyUnlocked)
            {
                GrantUnlockExp(entry);
            }

            Debug.Log($"[RecipeUnlockManager] 레시피 강제 해금: Item_ID={itemId}");
            OnRecipeUnlocksChanged?.Invoke();
        }

        /// <summary>해당 제작법(Item_ID)을 다시 잠금 처리한다. 목록에 없으면 아무 동작도 하지 않는다.</summary>
        public void Lock(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            if (entry != null)
            {
                entry.IsUnlocked = false;
                OnRecipeUnlocksChanged?.Invoke();
            }
        }

        /// <summary>해당 제작법(Item_ID)의 요구 영지 레벨을 반환한다. 목록에 없으면 기본값 1.</summary>
        public int GetRequiredTerritoryLevel(string itemId)
        {
            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            return entry != null ? entry.RequestTerritoryLevel : 1;
        }

        /// <summary>
        /// 해당 레시피가 지금 해금을 "시도"할 수 있는 상태인지 확인한다 - 영지 레벨 조건을 만족하고
        /// 아직 해금되지 않은 상태여야 true. 여신상 UI에서 슬롯의 상호작용 가능 여부를 결정하는 데 쓰인다.
        /// </summary>
        public bool CanAttemptUnlock(string itemId, int currentTerritoryLevel)
        {
            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            if (entry == null || entry.IsUnlocked) return false;

            return currentTerritoryLevel >= entry.RequestTerritoryLevel;
        }

        /// <summary>
        /// [공용 업그레이드 팝업 경유 흐름에서 사용] 결제(골드+재료)가 이미 끝난 뒤 호출된다.
        /// 순수하게 해금 상태만 반영한다 - 조건 검사나 결제는 팝업(UpgradePopupUI)과
        /// RecipeUnlockUpgrade(IUpgradable 어댑터)가 이미 마친 상태에서 호출되므로 여기서 다시 검사하지 않는다.
        /// 이미 해금된 항목이면 아무 것도 하지 않는다(재호출 시 경험치 중복 지급 방지).
        /// </summary>
        public void ApplyUnlock(string itemId)
        {
            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            if (entry == null || entry.IsUnlocked) return;

            entry.IsUnlocked = true;
            Debug.Log($"[RecipeUnlockManager] 레시피 해금 완료(팝업 결제 후 적용): Item_ID={itemId}");

            GrantUnlockExp(entry);

            OnRecipeUnlocksChanged?.Invoke();
        }

        /// <summary>
        /// [구 버전 호환용, 새 흐름에서는 호출되지 않음] 영지 레벨 조건과 골드를 모두 확인해서 실제
        /// 구매(해금)를 시도한다. 조건을 만족하지 못하거나(레벨 부족/이미 해금됨) 골드가 부족하면 아무 것도
        /// 바뀌지 않고 false를 반환한다. 성공하면 골드가 차감되고 IsUnlocked가 true로 바뀌며 RewardExp만큼
        /// 영지 경험치를 획득한다.
        /// 재료(MaterialCosts)는 확인/차감하지 않는다 - 재료까지 반영하려면 공용 업그레이드 팝업 경유 흐름
        /// (RecipeUnlockUpgrade + ApplyUnlock)을 사용해야 한다.
        /// </summary>
        public bool TryPurchase(string itemId, TerritoryData territoryData)
        {
            if (territoryData == null) return false;

            var entry = recipeUnlocks.Find(e => e.Item_ID == itemId);
            if (entry == null || entry.IsUnlocked) return false;
            if (territoryData.Level < entry.RequestTerritoryLevel) return false;
            if (!territoryData.TrySpendGold(entry.RequestGold)) return false;

            entry.IsUnlocked = true;
            Debug.Log($"[RecipeUnlockManager] 레시피 구매 완료: Item_ID={itemId}, 지불 골드={entry.RequestGold}");

            territoryData.AddExp(entry.RewardExp);

            OnRecipeUnlocksChanged?.Invoke();
            return true;
        }

        /// <summary>Item_ID로 실제 ItemData(SO)를 카탈로그에서 찾는다. 여신상 UI에서 아이콘 표시 등에 사용.</summary>
        public ItemData FindRecipeItemData(string itemId)
        {
            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);
            return itemCatalogManager != null ? itemCatalogManager.FindItemData(itemId) : null;
        }
    }
}
