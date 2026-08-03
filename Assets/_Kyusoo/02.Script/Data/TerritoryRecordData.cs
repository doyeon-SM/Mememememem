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

        // 1. 레시피 해금 이벤트 (골드/경험치 소모)
        liveRecipeManager = FindFirstObjectByType<RecipeUnlockManager>();
        if (liveRecipeManager != null)
        {
            liveRecipeManager.OnRecipeUnlocksChanged += OnTerritoryDataChangedHandler;
        }

        // 2. 영토 확장 이벤트 (골드/재료 소모)
        liveExpansionManager = FindFirstObjectByType<TerritoryExpansionManager>();
        if (liveExpansionManager != null)
        {
            liveExpansionManager.OnExpansionChanged += OnTerritoryDataChangedHandler;
        }

        // 3. 상점 거래 이벤트 (구매/판매 골드 수급 및 지출 감지)
        liveShopStockManager = ShopStockManager.Resolve(null);
        if (liveShopStockManager != null)
        {
            liveShopStockManager.OnStockChanged += OnShopStockChangedHandler;
        }

        // 4. 그리드 건물 배치/철거 이벤트 (만족도 변경 감지)
        GridManager.OnGridDataChanged += OnTerritoryDataChangedHandler;
    }

    private void UnsubscribeManagers()
    {
        if (liveRecipeManager != null)
        {
            liveRecipeManager.OnRecipeUnlocksChanged -= OnTerritoryDataChangedHandler;
        }
        if (liveExpansionManager != null)
        {
            liveExpansionManager.OnExpansionChanged -= OnTerritoryDataChangedHandler;
        }
        if (liveShopStockManager != null)
        {
            liveShopStockManager.OnStockChanged -= OnShopStockChangedHandler;
        }

        GridManager.OnGridDataChanged -= OnTerritoryDataChangedHandler;

        liveTerritoryData = null;
        liveRecipeManager = null;
        liveExpansionManager = null;
        liveShopStockManager = null;
    }

    private void OnShopStockChangedHandler(ShopItemData item) => OnTerritoryDataChangedHandler();

    private void OnTerritoryDataChangedHandler()
    {
        if (RecordManager.Instance != null && !isApplyingData && !RecordManager.IsLoadingData)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
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
        saveData.recipeUnlockedStates = new List<bool>();

        saveData.foodWarehouseStorageData = new ContainerData { width = 1, height = 0 };
    }

    public void SaveData(string saveFilePath)
    {
        if (liveTerritoryData == null) RefreshManagersReference();

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        currentData.isBlueprintGiven = isBlueprintGivenCache;

        // 배치된 모든 시설의 총 만족도를 계산해 TerritoryData private 필드에 주입
        var gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null && liveTerritoryData != null)
        {
            int calculatedSatisfaction = gridManager.GetTotalSatisfactionFromGrid();
            RecordManager.Instance.SetPrivateFieldSafely(liveTerritoryData, "satisfaction", calculatedSatisfaction);
        }

        // 1. 영지 기초 정보 (골드, 만족도 반영)
        if (liveTerritoryData != null)
        {
            currentData.territoryLevel = liveTerritoryData.Level;
            currentData.currentExp = liveTerritoryData.CurrentExp;
            currentData.requiredExp = liveTerritoryData.RequiredExp;
            currentData.gold = liveTerritoryData.Gold;
            currentData.satisfaction = liveTerritoryData.Satisfaction;

            currentData.foodWarehouseStorageData = new ContainerData { width = 1, height = liveTerritoryData.FoodStorage.Count };
            currentData.foodWarehouseStorageData.slots.Clear();
            foreach (var food in liveTerritoryData.FoodStorage)
            {
                currentData.foodWarehouseStorageData.slots.Add(new ItemStackData { itemId = food.Item_ID, amount = food.Quantity });
            }
        }

        // 2. 레시피 도감 정보
        if (liveRecipeManager != null)
        {
            currentData.recipeUnlockedStates.Clear();
            foreach (var entry in liveRecipeManager.RecipeUnlocks)
            {
                currentData.recipeUnlockedStates.Add(entry.IsUnlocked);
            }
        }

        // 3. 영토 확장 정보
        if (liveExpansionManager != null)
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
            // 🌟 1. 재정(골드, 레벨, 경험치, 만족도) 및 밥통 복구 - 탐험 씬/영지 씬 상관없이 무조건 동기화!
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

                FieldInfo foodField = typeof(TerritoryData).GetField("foodStorage", BindingFlags.NonPublic | BindingFlags.Instance);
                if (foodField != null && saveData.foodWarehouseStorageData != null)
                {
                    List<FoodStorageEntry> internalFoodList = foodField.GetValue(liveTerritoryData) as List<FoodStorageEntry>;
                    if (internalFoodList != null)
                    {
                        internalFoodList.Clear();
                        foreach (var slot in saveData.foodWarehouseStorageData.slots)
                        {
                            if (!string.IsNullOrEmpty(slot.itemId) && slot.amount > 0)
                                internalFoodList.Add(new FoodStorageEntry { Item_ID = slot.itemId, Quantity = slot.amount });
                        }
                    }
                }

                FieldInfo levelEventField = typeof(TerritoryData).GetField("OnLevelChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                if (levelEventField != null)
                {
                    MulticastDelegate levelEvent = levelEventField.GetValue(liveTerritoryData) as MulticastDelegate;
                    levelEvent?.DynamicInvoke(liveTerritoryData.Level);
                }
            }

            // 탐험 씬(Exploration)일 경우, 영지 씬 전용 요소(레시피 UI/그리드 생성)만 스킵
            if (sceneType == SceneType.Exploration)
            {
                return;
            }

            // 2. 레시피 도감 복구
            if (liveRecipeManager != null && saveData.recipeUnlockedStates != null)
            {
                FieldInfo managerRecipeField = typeof(RecipeUnlockManager).GetField("recipeUnlocks", BindingFlags.NonPublic | BindingFlags.Instance);
                if (managerRecipeField != null)
                {
                    List<RecipeUnlockEntry> managerRecipes = managerRecipeField.GetValue(liveRecipeManager) as List<RecipeUnlockEntry>;
                    if (managerRecipes != null)
                    {
                        int limit = Mathf.Min(managerRecipes.Count, saveData.recipeUnlockedStates.Count);
                        for (int i = 0; i < limit; i++)
                        {
                            managerRecipes[i].IsUnlocked = saveData.recipeUnlockedStates[i];
                        }
                    }
                }

                FieldInfo eventField = typeof(RecipeUnlockManager).GetField("OnRecipeUnlocksChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                if (eventField != null)
                {
                    MulticastDelegate ev = eventField.GetValue(liveRecipeManager) as MulticastDelegate;
                    ev?.DynamicInvoke();
                }
            }

            // 3. 영토 및 물리 타일 스케일 복구
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

                FieldInfo expEventField = typeof(TerritoryExpansionManager).GetField("OnExpansionChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                if (expEventField != null)
                {
                    MulticastDelegate ev = expEventField.GetValue(liveExpansionManager) as MulticastDelegate;
                    ev?.DynamicInvoke();
                }
            }

            // 4. 청사진 최초 지급 동기화
            if (!isBlueprintGivenCache)
            {
                if (saveData.playerInventoryData != null && saveData.playerInventoryData.slots != null)
                {
                    var emptySlot = saveData.playerInventoryData.slots.Find(s => string.IsNullOrEmpty(s.itemId));
                    if (emptySlot != null)
                    {
                        emptySlot.itemId = "blueprint_production_stand";
                        emptySlot.amount = 1;
                    }
                    else
                    {
                        saveData.playerInventoryData.slots.Add(new ItemStackData { itemId = "blueprint_production_stand", amount = 1 });
                    }
                }

                KMS.InventoryDuped.PlayerInventory playerInv = FindFirstObjectByType<KMS.InventoryDuped.PlayerInventory>();
                if (playerInv != null)
                {
                    playerInv.AddItem("blueprint_production_stand", 1);
                }

                isBlueprintGivenCache = true;
                saveData.isBlueprintGiven = true;

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