using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HDY.Territory;
using HDY.Recipe;
using HDY.Shop;

public class TerritoryRecordData : MonoBehaviour, IRecord
{
    private TerritoryData liveTerritoryData;
    private RecipeUnlockManager liveRecipeManager;
    private TerritoryExpansionManager liveExpansionManager;
    private ShopStockManager liveShopStockManager;
    private bool isApplyingData = false;
    private bool isBlueprintGivenCache = false;

    private void OnEnable()
    {
        RefreshManagersReference();
    }

    private void OnDisable()
    {
        UnsubscribeManagers();
    }

    private void RefreshManagersReference()
    {
        UnsubscribeManagers();
        liveTerritoryData = FindFirstObjectByType<TerritoryData>();

        // [멤] 중요행동 - 영지 레벨업은 여신상 해금 조건과 직결되는 큰 진척이라 즉시 저장한다.
        if (liveTerritoryData != null)
        {
            liveTerritoryData.OnLevelChanged += OnTerritoryLevelChangedHandler;
        }

        liveRecipeManager = FindFirstObjectByType<RecipeUnlockManager>();
        if (liveRecipeManager != null)
        {
            liveRecipeManager.OnRecipeUnlocksChanged += OnRecipeUnlockChangedHandler;
        }

        liveExpansionManager = FindFirstObjectByType<TerritoryExpansionManager>();
        if (liveExpansionManager != null)
        {
            liveExpansionManager.OnExpansionChanged += OnExpansionChangedHandler;
        }

        liveShopStockManager = ShopStockManager.Resolve(null);
        if (liveShopStockManager != null)
        {
            liveShopStockManager.OnStockChanged += OnShopStockChangedHandler;
        }

        GridManager.OnGridDataChanged += OnGridStructureChangedHandler;
    }

    private void UnsubscribeManagers()
    {
        if (liveTerritoryData != null)
        {
            liveTerritoryData.OnLevelChanged -= OnTerritoryLevelChangedHandler;
        }
        if (liveRecipeManager != null)
        {
            liveRecipeManager.OnRecipeUnlocksChanged -= OnRecipeUnlockChangedHandler;
        }
        if (liveExpansionManager != null)
        {
            liveExpansionManager.OnExpansionChanged -= OnExpansionChangedHandler;
        }
        if (liveShopStockManager != null)
        {
            liveShopStockManager.OnStockChanged -= OnShopStockChangedHandler;
        }
        GridManager.OnGridDataChanged -= OnGridStructureChangedHandler;
        liveTerritoryData = null;
        liveRecipeManager = null;
        liveExpansionManager = null;
        liveShopStockManager = null;
    }

    private void OnShopStockChangedHandler(ShopItemData item) => OnTerritoryDataChangedHandler();

    /// <summary>[멤] 공통 가드. 로딩 중·씬 언로드 중·복원 중에는 어떤 저장도 하지 않는다.</summary>
    private bool CanRecordNow()
    {
        return RecordManager.Instance != null
            && !isApplyingData
            && !RecordManager.IsLoadingData
            && !RecordManager.IsSceneUnloading;
    }

    /// <summary>
    /// [멤] 저장 빈도 감축 - 상점 재고처럼 수시로 바뀌는 값은 변경 표시만 한다.
    /// </summary>
    private void OnTerritoryDataChangedHandler()
    {
        if (!CanRecordNow()) return;
        RecordManager.NotifyDataChanged();
    }

    /// <summary>[멤] 중요행동 - 여신상 제작법 해금.</summary>
    private void OnRecipeUnlockChangedHandler()
    {
        if (!CanRecordNow()) return;
        RecordManager.NotifyCriticalAction(RecordManager.SaveReason.RecipeUnlock);
    }

    /// <summary>[멤] 중요행동 - 영지 레벨업.</summary>
    private void OnTerritoryLevelChangedHandler(int newLevel)
    {
        if (!CanRecordNow()) return;
        RecordManager.NotifyCriticalAction(RecordManager.SaveReason.TerritoryLevelUp);
    }

    /// <summary>[멤] 중요행동 - 영지 타일 확장.</summary>
    private void OnExpansionChangedHandler()
    {
        if (!CanRecordNow()) return;
        RecordManager.NotifyCriticalAction(RecordManager.SaveReason.TerritoryLevelUp);
    }

    /// <summary>[멤] 중요행동 - 시설 신축/철거로 영지 레이아웃이 바뀜 때.</summary>
    private void OnGridStructureChangedHandler()
    {
        if (!CanRecordNow()) return;
        RecordManager.NotifyCriticalAction(RecordManager.SaveReason.FacilityChanged);
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.territoryLevel = 1;
        saveData.currentExp = 0;
        saveData.requiredExp = 100;
        saveData.gold = 0;
        saveData.satisfaction = 0;
        saveData.isBlueprintGiven = false;
        saveData.currentGridSize = 10;
        saveData.expansionExpandedStates = new List<bool>();
        // [HDY 요청 - 여신상 저장 버그 수정] recipeUnlockedStates(List<bool>) -> unlockedRecipeItemIds(List<string>)
        saveData.unlockedRecipeItemIds = new List<string>();
    }

public void SaveData(string saveFilePath)
    {
        if (RecordManager.IsLoadingData || RecordManager.IsSceneUnloading) return;
        if (liveTerritoryData == null) RefreshManagersReference();

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        currentData.isBlueprintGiven = isBlueprintGivenCache;

        var gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null && liveTerritoryData != null)
        {
            int calculatedSatisfaction = gridManager.GetTotalSatisfactionFromGrid();
            RecordManager.Instance.SetPrivateFieldSafely(liveTerritoryData, "satisfaction", calculatedSatisfaction);
        }

        // 1. 영지 스탯 데이터
        if (liveTerritoryData != null)
        {
            currentData.territoryLevel = liveTerritoryData.Level;
            currentData.currentExp = liveTerritoryData.CurrentExp;
            currentData.requiredExp = liveTerritoryData.RequiredExp;
            currentData.gold = liveTerritoryData.Gold;
            currentData.satisfaction = liveTerritoryData.Satisfaction;
            // 🌟 [수정] ConsumeFoodRecordData가 사용하는 foodWarehouseStorageData 덮어쓰기 구문 완전히 제거!
        }

        // [HDY 요청 - 저장 시스템 버그 수정: 영지 이동 시 여신상 해금 초기화] RecipeUnlockManager와
        // TerritoryExpansionManager는 Title 씬에서 한 번 생성된 뒤 DontDestroyOnLoad로 세션 내내 유지된다.
        // ApplyData()의 레시피/확장 복원은 영지 씬에서만 실행되므로(아래 참고), 이번 세션에 영지를 아직
        // 한 번도 방문하지 않았다면 이 두 매니저는 CSV 기본값(전부 잠김/미확장) 그대로다. 그런데 씬 전환
        // 때마다(LoadingManager) SaveAllData()가 무조건 호출되기 때문에, liveRecipeManager/
        // liveExpansionManager가 살아있다는 것만으로는 "이 값이 신뢰할 수 있는 값"이라는 보장이 되지
        // 않는다 - 탐험 중(영지 미방문 상태)에 저장이 한 번이라도 실행되면 아직 복원되지 않은 기본값이
        // 그대로 저장 파일을 덮어써서 진짜 해금 데이터가 사라진다. 그래서 "현재 활성 씬이 실제로 영지
        // 씬일 때만" 이 두 항목을 쓰도록 제한한다 - RecordManager가 씬을 분류하는 방식과 동일한 기준이다.
        bool isInTerritoryScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower().Contains("territory");

        // 2. 레시피(여신상) 해금
        // [HDY 요청 - 여신상 저장 버그 수정] 예전에는 IsUnlocked를 리스트 순서(인덱스) 그대로 bool
        // 배열에 담아 저장했다. RecipeUnlockManager의 해금 목록이 RecipeUnlocks.csv를 매번 파싱해서
        // 만드는 방식으로 바뀌면서, 시트에 행이 추가/삭제/순서변경될 때마다 인덱스가 밀려 저장된
        // true/false가 엉뚱한 레시피에 적용되는 문제가 있었다. Item_ID만 저장하도록 바꿔서
        // 시트 순서가 바뀌어도 항상 올바른 레시피에 매칭되게 한다(요리 레시피 저장 방식과 동일).
        if (liveRecipeManager != null && isInTerritoryScene)
        {
            currentData.unlockedRecipeItemIds.Clear();
            foreach (var entry in liveRecipeManager.RecipeUnlocks)
            {
                if (entry != null && entry.IsUnlocked && !string.IsNullOrEmpty(entry.Item_ID))
                {
                    currentData.unlockedRecipeItemIds.Add(entry.Item_ID);
                }
            }
        }

        // 3. 영지 확장
        if (liveExpansionManager != null && isInTerritoryScene)
        {
            FieldInfo sizeField = typeof(TerritoryExpansionManager).GetField("currentGridSize", BindingFlags.NonPublic | BindingFlags.Instance);
            if (sizeField != null)
            {
                currentData.currentGridSize = (int)sizeField.GetValue(liveExpansionManager);
            }
            currentData.expansionExpandedStates.Clear();
            foreach (var step in liveExpansionManager.ExpansionSteps)
            {
                currentData.expansionExpandedStates.Add(step.IsExpanded);
            }
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        isBlueprintGivenCache = saveData.isBlueprintGiven;
        RefreshManagersReference();
        isApplyingData = true;

        try
        {
            if (liveTerritoryData != null)
            {
                RecordManager.Instance.SetPrivateFieldSafely(liveTerritoryData, "level", saveData.territoryLevel);
                RecordManager.Instance.SetPrivateFieldSafely(liveTerritoryData, "currentExp", saveData.currentExp);
                RecordManager.Instance.SetPrivateFieldSafely(liveTerritoryData, "gold", saveData.gold);
                RecordManager.Instance.SetPrivateFieldSafely(liveTerritoryData, "satisfaction", saveData.satisfaction);

                FieldInfo reqExpField = typeof(TerritoryData).GetField("requiredExp", BindingFlags.NonPublic | BindingFlags.Instance);
                if (reqExpField != null)
                {
                    List<int> reqList = reqExpField.GetValue(liveTerritoryData) as List<int>;
                    if (reqList != null && saveData.territoryLevel <= reqList.Count)
                    {
                        reqList[saveData.territoryLevel - 1] = saveData.requiredExp;
                    }
                }

                FieldInfo levelEventField = typeof(TerritoryData).GetField("OnLevelChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                if (levelEventField != null)
                {
                    MulticastDelegate levelEvent = levelEventField.GetValue(liveTerritoryData) as MulticastDelegate;
                    levelEvent?.DynamicInvoke(liveTerritoryData.Level);
                }
            }

            if (sceneType == SceneType.Exploration) return;

            // [HDY 요청 - 여신상 저장 버그 수정] Item_ID 집합으로 변환한 뒤, 현재 recipeUnlocks를
            // 순회하며 각 항목의 Item_ID가 그 집합에 있는지로 IsUnlocked를 결정한다. 인덱스에 의존하지
            // 않으므로 RecipeUnlocks.csv의 행 순서/개수가 바뀌어도 항상 올바른 레시피에 매칭된다.
            // (필드명이 recipeUnlockedStates -> unlockedRecipeItemIds로 바뀌었기 때문에, 이 변경 이전에
            // 저장된 세이브 파일은 saveData.unlockedRecipeItemIds가 비어있게 되어 여신상 해금 상태가
            // 한 번 초기화된다 - 확인 후 진행하기로 함.)
            if (liveRecipeManager != null)
            {
                FieldInfo managerRecipeField = typeof(RecipeUnlockManager).GetField("recipeUnlocks", BindingFlags.NonPublic | BindingFlags.Instance);
                if (managerRecipeField != null)
                {
                    List<RecipeUnlockEntry> managerRecipes = managerRecipeField.GetValue(liveRecipeManager) as List<RecipeUnlockEntry>;
                    if (managerRecipes != null)
                    {
                        HashSet<string> unlockedIds = saveData.unlockedRecipeItemIds != null
                            ? new HashSet<string>(saveData.unlockedRecipeItemIds)
                            : new HashSet<string>();

                        foreach (var entry in managerRecipes)
                        {
                            if (entry == null) continue;
                            entry.IsUnlocked = !string.IsNullOrEmpty(entry.Item_ID) && unlockedIds.Contains(entry.Item_ID);
                        }
                    }
                }
            }

            if (liveExpansionManager != null)
            {
                RecordManager.Instance.SetPrivateFieldSafely(liveExpansionManager, "currentGridSize", saveData.currentGridSize);
                FieldInfo expStepsField = typeof(TerritoryExpansionManager).GetField("expansionSteps", BindingFlags.NonPublic | BindingFlags.Instance);
                if (expStepsField != null && saveData.expansionExpandedStates != null)
                {
                    List<TerritoryExpansionEntry> steps = expStepsField.GetValue(liveExpansionManager) as List<TerritoryExpansionEntry>;
                    if (steps != null)
                    {
                        int limit = Mathf.Min(steps.Count, saveData.expansionExpandedStates.Count);
                        for (int i = 0; i < limit; i++)
                        {
                            steps[i].IsExpanded = saveData.expansionExpandedStates[i];
                        }
                    }
                }
                GridManager actualGrid = FindFirstObjectByType<GridManager>();
                actualGrid?.InitializeGrid(saveData.currentGridSize, saveData.currentGridSize);
            }

            if (!isBlueprintGivenCache)
            {
                KMS.InventoryDuped.PlayerInventory playerInv = FindFirstObjectByType<KMS.InventoryDuped.PlayerInventory>();
                //if (playerInv != null)
                //{
                //    playerInv.AddItem("blueprint_production_stand", 1);
                //}
                //isBlueprintGivenCache = true;
                //saveData.isBlueprintGiven = true;
                string path = RecordManager.Instance.SaveFilePath;
                SaveData rawDiskData = RecordManager.Instance.ReadRawSaveFileOnly();
                if (rawDiskData == null) rawDiskData = saveData;
                rawDiskData.isBlueprintGiven = true;
                File.WriteAllText(path, JsonUtility.ToJson(rawDiskData, true));
            }
        }
        finally
        {
            isApplyingData = false;
        }
    }
}
