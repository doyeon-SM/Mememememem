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

        liveRecipeManager = FindFirstObjectByType<RecipeUnlockManager>();
        if (liveRecipeManager != null)
        {
            liveRecipeManager.OnRecipeUnlocksChanged += OnTerritoryDataChangedHandler;
        }

        liveExpansionManager = FindFirstObjectByType<TerritoryExpansionManager>();
        if (liveExpansionManager != null)
        {
            liveExpansionManager.OnExpansionChanged += OnTerritoryDataChangedHandler;
        }

        liveShopStockManager = ShopStockManager.Resolve(null);
        if (liveShopStockManager != null)
        {
            liveShopStockManager.OnStockChanged += OnShopStockChangedHandler;
        }

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
        if (RecordManager.Instance != null && !isApplyingData && !RecordManager.IsLoadingData && !RecordManager.IsSceneUnloading)
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

        // 2. 레시피 해금
        if (liveRecipeManager != null)
        {
            currentData.recipeUnlockedStates.Clear();
            foreach (var entry in liveRecipeManager.RecipeUnlocks)
            {
                currentData.recipeUnlockedStates.Add(entry.IsUnlocked);
            }
        }

        // 3. 영지 확장
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