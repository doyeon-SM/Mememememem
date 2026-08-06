using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Capture;
using HDY.Forge;

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
public class WaypointInfo
{
    public string wayPointId;
    public bool isUnlocked;
}

[Serializable]
public class ChestInfo
{
    public string chestId;
    public bool isOpen;
}

[Serializable]
public class PlayerInfo
{
    public float maxHealth = 100f;
    public float maxHunger = 100f;
    public float currentHealth = 100f;
    public float currentHunger = 100f;
}

[Serializable]
public class MemFirstCapturedEntry
{
    public string memId;
    public long firstCapturedTimestamp; 
}

[Serializable]
public class Vector3Data
{
    public float x;
    public float y;
    public float z;
    public Vector3Data(Vector3 vector)
    {
        x = vector.x;
        y = vector.y;
        z = vector.z;
    }
    public Vector3 ToVector3() => new Vector3(x, y, z);
}

[Serializable]
public class ScenePlayerPosData
{
    public string sceneName;
    public Vector3Data lastPlayerPos;
    public bool hasSavedPlayerPos = false;
}

[Serializable]
public class SaveData
{
    public string lastSaveTime;
    public string lastPlayScene = "Main_World_3";

    [Header("씬별 플레이어 마지막 좌표 데이터")]
    public List<ScenePlayerPosData> playerPosDataList = new List<ScenePlayerPosData>();

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

    [Header("제작법 해금 데이터")]
    public List<bool> recipeUnlockedStates = new List<bool>();

    [Header("요리 제작법 해금 데이터")]
    public List<string> cookRecipeUnlockedStates = new List<string>();

    [Header("창고 및 인벤토리 실물 데이터")]
    public ContainerData playerInventoryData;
    public ContainerData warehouseStorageData;
    public ContainerData foodWarehouseStorageData;
    
    public ContainerData playerQuickSlotsData;
    public int selectedQuickSlotIndex;
    public int unlockedInventorySlotCount = 10;

    [Header("음식 소모 시뮬레이션 데이터")]
    public int maxSatiety;
    public int currentSatiety;
    public bool isWorkStoppedDueToStarvation;

    [Header("멤 창고 데이터")]
    public int unlockedPageCount = 2;
    public List<CapturedMemEntry> serializedCapturedMems = new List<CapturedMemEntry>();

    [Header("도감 최초 포획 시간 기록 데이터")]
    public List<MemFirstCapturedEntry> firstCapturedTimestamps = new List<MemFirstCapturedEntry>();

    [Header("배치된 시설 레이아웃 청사진 및 일꾼 마스터 데이터")]
    public List<PlacedBuildingData> placedBuildings = new List<PlacedBuildingData>();

    [Header("시간 및 일자 데이터")]
    public GameTimeSaveData timeData;

    [Header("웨이포인트 해금 데이터")]
    public List<WaypointInfo> waypointInfo = new List<WaypointInfo>();

    [Header("상자 개방 데이터")]
    public List<ChestInfo> chestInfo = new List<ChestInfo>();

    [Header("플레이어 스탯 데이터")]
    public PlayerInfo playerInfo = new PlayerInfo();

    [Header("대장간 도구 인스턴스 데이터")]
    public List<ForgeInstanceData> forgeInstanceDataList = new List<ForgeInstanceData>();
}