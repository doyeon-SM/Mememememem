using System.Collections.Generic;
using UnityEngine;

public class BuildRecordManager : MonoBehaviour
{
    // 시설 배치 정보 백업용 롤백 스택
    private Stack<List<BuildingSnapshot>> rollbackStack = new Stack<List<BuildingSnapshot>>();

    /// <summary>
    /// 배치모드 전환시 현재 상태를 rollbackStack에 저장.
    /// </summary>
    public void SaveRollbackData(GameObject[,] objectsGrid, BuildingData[,] dataGrid, int width, int height)
    {
        rollbackStack.Clear();

        List<BuildingSnapshot> rollbackData = SaveSnapshot(objectsGrid, dataGrid, width, height);
        rollbackStack.Push(rollbackData);
    }

    /// <summary>
    /// 저장 버튼 클릭시 스택 비우기(배치모드 전환시 이미 이전 저장된 상태를 가져올거기 때문에
    /// </summary>
    public void ClearRecordOnSave()
    {
        rollbackStack.Clear();
    }

    /// <summary>
    /// 취소 버튼 클릭시 배치로 인해 변경된 모든 사항을 롤백처리.
    /// </summary>
    public List<BuildingSnapshot> Rollback()
    {
        if (rollbackStack.Count > 0)
        {
            return rollbackStack.Pop();
        }

        Debug.LogWarning("[Record] 롤백 스택이 비어있어 복원에 실패.");
        return null;
    }

    /// <summary> 
    /// 타일에 배치된 건물 정보를 SnapShot으로 저장 
    /// 특정 건물이 배치되었을 때 정보(0,0 ~ 2,2)까지의 타일에 중복된 정보가 존재하기에 이를 하나로 파악하고 SnapShot에 기록
    /// </summary>
    private List<BuildingSnapshot> SaveSnapshot(GameObject[,] objectsGrid, BuildingData[,] dataGrid, int width, int height)
    {
        List<BuildingSnapshot> snapshot = new List<BuildingSnapshot>();
        HashSet<GameObject> checkedBuildingData = new HashSet<GameObject>();

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                GameObject buildingData = objectsGrid[x, z];
                BuildingData data = dataGrid[x, z];

                if (buildingData != null && !checkedBuildingData.Contains(buildingData))
                {
                    checkedBuildingData.Add(buildingData);

                    if (buildingData.TryGetComponent<BuildingRuntime>(out BuildingRuntime runtime))
                    {
                        BuildingSnapshot snap = new BuildingSnapshot
                        {
                            data = data,
                            startX = runtime.gridX, 
                            startZ = runtime.gridZ, 
                            rotation = buildingData.transform.rotation
                        };
                        snapshot.Add(snap);
                    }
                }
            }
        }
        return snapshot;
    }
}