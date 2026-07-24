using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FacilityData
{
    [Header("시설ID. 이 아이디로 시설별 데이터 구분")]
    public string Building_ID;

    [Header("동작 여부, 제작용 아이템id, 목표 수량, 미완성된 남은 수량")]
    public bool isActive;
    public string currentCraftingItemId;
    public int targetQuantity;
    public int remainingQuantity;

    public float currentProgressTime;
    public int currentStorageCount;

    [Header("배치된 멤 정보 리스트")]
    public List<string> DeployedMemIDs = new List<string>();

    [Header("목장 저장 데이터")]
    public List<RanchSlotSaveData> ranchSlots = new List<RanchSlotSaveData>();
}

[System.Serializable]
public class RanchSlotSaveData
{
    public int slotIndex;
    public bool isUnlocked;
    public string deployedMemKeyId;
    public string craftingItemId;
    public bool isProducing;
    public float currentProgressTime;
    public int currentStorageCount;
}

[System.Serializable]
public class PlantSaveWrapper
{
    public string lastSaveTime;
    public List<FacilityData> SaveDataList = new List<FacilityData>();
}