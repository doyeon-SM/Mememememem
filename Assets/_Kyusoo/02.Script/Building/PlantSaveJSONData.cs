using System.Collections.Generic;
using UnityEngine;

public class PlantJSONSaveData
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
}

[System.Serializable]
public class PlantSaveWrapper
{
    public string lastSaveTime;
    public List<PlantJSONSaveData> SaveDataList = new List<PlantJSONSaveData>();
}
