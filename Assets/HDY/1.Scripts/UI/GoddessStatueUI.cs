using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Item;
using HDY.Territory;
using HDY.Recipe;

namespace HDY.UI
{
    /// <summary>
    /// 여신상 UI 컨트롤러.
    /// RecipeUnlockManager / TerritoryData / ItemCatalogManager에서 데이터를 가져와, 요구 영지 레벨별로
    /// 레시피를 묶어서 줄(GoddessStatueUI_LevelRow)을 스크롤 영역(Content) 안에 만들고, 슬롯 선택 및
    /// 해금하기 버튼(가장 아래, 항상 하나) 동작을 담당한다.
    ///
    /// [스크롤] 마우스 휠 스크롤은 Unity의 ScrollRect가 기본으로 지원하므로 이 스크립트에서 별도 처리하지 않는다.
    /// (Mem 창고의 페이지 그리드와 달리, 여기는 실제 ScrollRect + Content를 사용하는 리스트형 UI이기 때문)
    ///
    /// [그리드 최대 2줄] 각 줄의 슬롯 배치는 GoddessStatueUI_LevelRow의 Grid Layout Group(Constraint를
    /// Fixed Row Count=2로 설정)이 알아서 처리하므로, 이 컨트롤러는 슬롯 개수만 넘겨주면 된다.
    ///
    /// [선택 및 구매 흐름] 슬롯 클릭 -> selectedItemId 갱신 -> 모든 줄의 강조 표시 갱신 + 해금하기 버튼
    /// 텍스트/활성화 갱신 -> 해금하기 버튼 클릭 -> RecipeUnlockManager.TryPurchase 호출 -> 성공 여부와 관계없이
    /// 줄 전체를 다시 그려서(BuildRows) 최신 해금 상태를 반영한다.
    /// </summary>
    public class GoddessStatueUI : MonoBehaviour
    {
        [Header("데이터 참조")]
        [SerializeField] private RecipeUnlockManager recipeUnlockManager;
        [SerializeField] private TerritoryData territoryData;
        [SerializeField] private ItemCatalogManager itemCatalogManager; // 비어있으면 자동 탐색(싱글톤 -> 씬 검색)

        [Header("레벨별 줄 배치 (ScrollRect의 Content를 rowsParent로 연결)")]
        [SerializeField] private Transform rowsParent;
        [SerializeField] private GoddessStatueUI_LevelRow rowPrefab;

        [Header("해금하기 버튼 (여신상 UI 가장 아래, 항상 하나)")]
        [SerializeField] private Button unlockButton;
        [SerializeField] private TMP_Text unlockButtonText;

        private readonly List<GoddessStatueUI_LevelRow> rows = new List<GoddessStatueUI_LevelRow>();
        private string selectedItemId;

        private void Awake()
        {
            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);

            if (recipeUnlockManager == null) Debug.LogWarning("[GoddessStatueUI] recipeUnlockManager가 비어있습니다.", this);
            if (territoryData == null) Debug.LogWarning("[GoddessStatueUI] territoryData가 비어있습니다.", this);
            if (itemCatalogManager == null) Debug.LogWarning("[GoddessStatueUI] itemCatalogManager를 찾을 수 없습니다.", this);
            if (rowsParent == null) Debug.LogWarning("[GoddessStatueUI] rowsParent가 비어있습니다.", this);
            if (rowPrefab == null) Debug.LogWarning("[GoddessStatueUI] rowPrefab이 비어있습니다.", this);

            if (unlockButton != null)
            {
                unlockButton.onClick.AddListener(HandleUnlockButtonClicked);
            }
        }

        private void OnEnable()
        {
            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);

            selectedItemId = null;
            BuildRows();
            RefreshUnlockButton();
        }

        /// <summary>RecipeUnlocks를 요구 영지 레벨별로 묶어서 줄(row)들을 새로 만든다. 기존 줄은 전부 지운다.</summary>
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

            foreach (var pair in groups)
            {
                var row = Instantiate(rowPrefab, rowsParent);
                row.Setup(pair.Key, pair.Value, FindItemIcon, CanAttemptUnlock);
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

        private void HandleSlotSelected(string itemId)
        {
            selectedItemId = itemId;

            foreach (var row in rows)
            {
                row.RefreshSelection(selectedItemId);
            }

            RefreshUnlockButton();
        }

        /// <summary>
        /// 선택 상태에 맞게 해금하기 버튼을 갱신한다.
        /// 선택된 레시피가 없으면 interactable=false로만 만들고, 버튼의 Disabled Color(반투명)는
        /// 인스펙터에서 Selectable 설정으로 처리한다(코드에서 별도로 알파값을 조작하지 않음).
        /// </summary>
        private void RefreshUnlockButton()
        {
            var entry = FindEntry(selectedItemId);
            bool hasSelection = entry != null;

            if (unlockButton != null)
            {
                unlockButton.interactable = hasSelection;
            }

            if (unlockButtonText != null)
            {
                unlockButtonText.text = hasSelection ? $"{entry.RequestGold}골드\n해금하기" : "해금하기";
            }
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

        private void HandleUnlockButtonClicked()
        {
            if (string.IsNullOrEmpty(selectedItemId) || recipeUnlockManager == null || territoryData == null) return;

            bool success = recipeUnlockManager.TryPurchase(selectedItemId, territoryData);
            Debug.Log($"[GoddessStatueUI] 해금 시도: Item_ID={selectedItemId}, 성공={success}");

            selectedItemId = null;
            BuildRows(); // 잠금 상태/상호작용 가능 여부가 바뀌었으니 줄 전체를 다시 그림
            RefreshUnlockButton();
        }
    }
}
