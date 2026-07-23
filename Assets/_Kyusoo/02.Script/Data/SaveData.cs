using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Capture;

[Serializable]
public class ItemStackData
{
    public string itemId;
    public int amount;
}

[Serializable]
public class ContainerData
{
    public int width;
    public int height;
    public List<ItemStackData> slots = new List<ItemStackData>();
}

[Serializable]
public class PlacedBuildingData
{
    public string buildingName;
    public int gridX;
    public int gridZ;
    public float rotationY;
    public FacilityData runtimeData;
}

[Serializable]
public class GameTimeSaveData
{
    public float elapsedTime;          
    public string lastSaveRealTimeKst; 
}

[Serializable]
public class SaveData
{
    public string lastSaveTime;

    [Header("영지 기초 성장 데이터")]
    public int territoryLevel = 1;
    public int currentExp = 0;
    public int requiredExp = 100;
    public int gold = 0;
    public int satisfaction = 0;
    public bool isBlueprintGiven = false;

    [Header("영지 타일 확장 데이터")]
    public int currentGridSize = 5;
    public List<bool> expansionExpandedStates = new List<bool>();

    // 🌟 [정식 추가]: 레시피 도감 해금 상태를 저장할 전용 리스트 규격 바인딩
    [Header("제작법 해금 데이터")]
    public List<bool> recipeUnlockedStates = new List<bool>();

    [Header("창고 및 인벤토리 실물 데이터")]
    public ContainerData playerInventoryData;
    public ContainerData warehouseStorageData;
    public ContainerData foodWarehouseStorageData;

    // 퀵슬롯 영구 보존 규격 바인딩
    public ContainerData playerQuickSlotsData;
    public int selectedQuickSlotIndex;

    [Header("음식 소모 시뮬레이션 데이터")]
    public int maxSatiety;
    public int currentSatiety;
    public bool isWorkStoppedDueToStarvation;

    [Header("멤 창고 데이터")]
    public int unlockedPageCount = 2;
    public List<CapturedMemEntry> serializedCapturedMems = new List<CapturedMemEntry>();

    [Header("배치된 시설 레이아웃 청사진 및 일꾼 마스터 데이터")]
    public List<PlacedBuildingData> placedBuildings = new List<PlacedBuildingData>();

    [Header("시간 및 일자 데이터")]
    public GameTimeSaveData timeData;
}