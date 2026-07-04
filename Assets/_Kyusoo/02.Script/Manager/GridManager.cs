using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class GridManager : MonoBehaviour
{
    [Header("타일 생성 관련 정보: Prefab, 생성될 위치, Grid Layer")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Transform floorContainer;
    [SerializeField] private LayerMask gridLayerMask;

    [Header("시설 데이터 정보: SO, 프리뷰")]
    [SerializeField] private List<BuildingData> buildings = new List<BuildingData>();
    [SerializeField] private Material previewMaterial;

    [Header("타일 색상 정보: 배치 가능, 배치 불가")]
    [SerializeField] private Color buildableColor = new Color(0f, 0.5f, 1f, 0.4f);
    [SerializeField] private Color unbuildableColor = new Color(1f, 0f, 0f, 0.4f);

    // 현재 선택된 시설 데이터, 배치할 때 보이는 프리뷰
    private BuildingData selectedBuildingData;
    private GameObject currentPreviewInstance;

    // 프리뷰 최초 소환시 캐싱하여 프레임마다 호출하여 연산되지 않도록 처리
    private MeshRenderer[] previewRenderers;

    private int currentWidth;
    private int currentHeight;

    // tileGrid => 바닥 타일 좌표 / occupiedCells => 해당 좌표에 건물이 존재하는지 여부(빈 타일: false)
    private GameObject[,] tileGrid;
    private Vector3 raycastHitPoint;
    private bool[,] occupiedCells;

    // 타일에 배치된 시설 정보
    private GameObject[,] buildingObjectsGrid; 
    private BuildingData[,] buildingDataGrid;

    // 현재 마우스가 가리키는 시설의 시작점 좌표
    private int currentStartGridX;
    private int currentStartGridZ;

    // 시설의 회전 상태가 반영된 최종 크기
    private int currentTargetWidth;
    private int currentTargetHeight;
    
    // 시설 배치 가능 여부, 불가능할 때 DOTween 진동여부
    private bool canPlaceCurrent = false;
    private bool isShaking = false;

    // 기본모드, 배치모드에 따른 경계선 처리
    private Material defaultModeMaterial;
    private Material placeModeMaterial;
    private bool isPlacementMode = false;

    public int MouseGridX { get; private set; }
    public int MouseGridZ { get; private set; }
    public bool IsMouseOnGrid { get; private set; }

    // 이벤트 발행(UI 연결용)
    public static event Action<bool, List<BuildingData>> OnPlacementModeChanged;

    // Test전역변수
    private int count = 5;

    void Start()
    {
        defaultModeMaterial = CreateGridMaterial(false);
        placeModeMaterial = CreateGridMaterial(true);
        InitializeGrid(5, 5);
    }

    private void OnEnable()
    {
        PlacementUI.OnBuildingSelected += CreateBuildingPreview;
    }

    private void OnDisable()
    {
        PlacementUI.OnBuildingSelected -= CreateBuildingPreview;
    }

    void Update()
    {
        UpdateMouseGridPosition();

        if(currentPreviewInstance != null)
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                ClearPreview();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RotatePreview();
            }

            if (IsMouseOnGrid)
            {
                UpdatePreviewPosition();

                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    TryPlaceBuilding();
                }
            }
        }
        else if (isPlacementMode && IsMouseOnGrid)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                TryPickUpBuilding(MouseGridX, MouseGridZ);
            }
        }
    }

    /// <summary>
    /// 최초 영지 생성시 5x5 타일로 생성시키는 함수
    /// </summary>
    public void InitializeGrid(int width, int height)
    {
        currentWidth = width;
        currentHeight = height;
        tileGrid = new GameObject[currentWidth, currentHeight];
        occupiedCells = new bool[currentWidth, currentHeight];

        buildingObjectsGrid = new GameObject[currentWidth, currentHeight];
        buildingDataGrid = new BuildingData[currentWidth, currentHeight];

        for (int i = 0; i < currentWidth; i++)
        {
            for (int j = 0; j < currentHeight; j++)
            {
                tileGrid[i, j] = SpawnTile(i, j);
            }
        }
    }

    /// <summary>
    /// 5x5 영지를 업그레이드 하였을 때, 1씩 사이즈를 늘리는 확장용 함수
    /// </summary>
    public void ExpandGrid(int newWidth, int newHeight)
    {
        if (newWidth == currentWidth || newHeight == currentHeight) return;

        GameObject[,] newTileGrid = new GameObject[newWidth, newHeight];
        bool[,] newOccupiedCells = new bool[newWidth, newHeight];

        GameObject[,] newBuildingObjectsGrid = new GameObject[newWidth, newHeight];
        BuildingData[,] newBuildingDataGrid = new BuildingData[newWidth, newHeight];

        for (int i = 0; i < currentWidth; i++)
        {
            for (int j = 0; j < currentHeight; j++)
            {
                newTileGrid[i, j] = tileGrid[i, j];
                newOccupiedCells[i, j] = occupiedCells[i, j];
                newBuildingObjectsGrid[i, j] = buildingObjectsGrid[i, j];
                newBuildingDataGrid[i, j] = buildingDataGrid[i, j];
            }
        }

        // 새롭게 확장되는 외곽선 영역에만 타일 추가 스폰
        for (int i = 0; i < newWidth; i++)
        {
            for (int j = 0; j < newHeight; j++)
            {
                if (i >= currentWidth || j >= currentHeight)
                {
                    newTileGrid[i, j] = SpawnTile(i, j);
                }
            }
        }

        tileGrid = newTileGrid;
        occupiedCells = newOccupiedCells;
        buildingObjectsGrid = newBuildingObjectsGrid;
        buildingDataGrid = newBuildingDataGrid;
        currentWidth = newWidth;
        currentHeight = newHeight;
    }

    /// <summary>
    /// 특정 좌표에 Quad 타일을 올바른 오프셋으로 생성하는 서브 루틴
    /// </summary>
    private GameObject SpawnTile(int x, int z)
    {
        // Quad의 피벗이 중앙이므로 월드 좌표 (x + 0.5, z + 0.5)에 배치해야 
        // 0.0~1.0 영역이 완벽하게 1칸의 격자가 됩니다.
        Vector3 spawnPosition = new Vector3(x + 0.5f, 0f, z + 0.5f);


        GameObject newTile = Instantiate(tilePrefab, spawnPosition, Quaternion.Euler(90, 0, 0), floorContainer);
        newTile.name = $"Tile_({x},{z})";

        if (newTile.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
        {
            meshRenderer.material = isPlacementMode ? placeModeMaterial : defaultModeMaterial;
        }

        return newTile;
    }

    /// <summary>
    /// 버튼 연동을 통해 배치모드 전환처리
    /// </summary>
    public void ChangePlacementMode()
    {
        isPlacementMode = !isPlacementMode;

        OnPlacementModeChanged?.Invoke(isPlacementMode, buildings);

        if (!isPlacementMode)
        {
            ClearPreview();
        }

        if (tileGrid == null) return;

        Material targetMaterial = isPlacementMode ? placeModeMaterial : defaultModeMaterial;

        for (int i = 0; i < currentWidth; i++)
        {
            for (int j = 0; j < currentHeight; j++)
            {
                if (tileGrid[i, j] != null && tileGrid[i, j].TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
                {
                    meshRenderer.material = targetMaterial;
                }
            }
        }

        Debug.Log($"배치 모드 상태 변경: {isPlacementMode}");
    }

    /// <summary>
    /// 배치모드가 닫혔을 때 프리뷰, 선택 건물 데이터 초기화
    /// </summary>
    private void ClearPreview()
    {
        if (currentPreviewInstance != null)
        {
            currentPreviewInstance.transform.DOKill();
            Destroy(currentPreviewInstance);

            selectedBuildingData = null;
            previewRenderers = null;
            canPlaceCurrent = false;
            isShaking = false;
        }
    }

    /// <summary>
    /// 마우스의 레이캐스트 좌표를 정수형 Grid 좌표로 변환하는 핵심 로직
    /// </summary>
    private void UpdateMouseGridPosition()
    {
        if (Mouse.current == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        Ray ray = Camera.main.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, gridLayerMask))
        {
            raycastHitPoint = hit.point;

            MouseGridX = Mathf.FloorToInt(hit.point.x);
            MouseGridZ = Mathf.FloorToInt(hit.point.z);
            IsMouseOnGrid = true;
        }
        else
        {
            IsMouseOnGrid = false;
        }
    }

    /// <summary>
    /// 마우스 위치를 Update에서 계산
    /// 위치 기반 프리뷰를 격자에 맞춰 정렬시키고 해당 위치에 시설 배치가 가능한지 여부 체크 및 색상변경
    /// </summary>
    private void UpdatePreviewPosition()
    {
        if (selectedBuildingData == null || currentPreviewInstance == null) return;
        if (isShaking) return;

        int currentRotationIndex = Mathf.RoundToInt(currentPreviewInstance.transform.eulerAngles.y / 90f) % 4;
        bool isRotated = (currentRotationIndex == 1 || currentRotationIndex == 3);

        currentTargetWidth = isRotated ? selectedBuildingData.height : selectedBuildingData.width;
        currentTargetHeight = isRotated ? selectedBuildingData.width : selectedBuildingData.height;

        // 현재 마우스 위치를 기준으로 프리뷰의 시작 좌표를 계산
        currentStartGridX = Mathf.FloorToInt(raycastHitPoint.x - (currentTargetWidth / 2.0f));
        currentStartGridZ = Mathf.FloorToInt(raycastHitPoint.z - (currentTargetHeight / 2.0f));

        float offsetX = currentStartGridX + (currentTargetWidth / 2.0f);
        float offsetZ = currentStartGridZ + (currentTargetHeight / 2.0f);
        currentPreviewInstance.transform.position = new Vector3(offsetX, 0f, offsetZ);

        canPlaceCurrent = CheckPlacement(currentStartGridX, currentStartGridZ, currentTargetWidth, currentTargetHeight);
        UpdatePreviewVisual(canPlaceCurrent);
    }

    /// <summary>
    /// 건물이 영지의 외곽을 벗어나거나 다른 건물과 겹치는지를 검사하는 함수
    /// 이 함수를 통해 이후 UpdatePreviewPosition에서 프리뷰 색상을 변경.
    /// </summary>
    private bool CheckPlacement(int startX, int startZ, int width, int height)
    {

        for (int x = startX; x < startX + width; x++)
        {
            for (int z = startZ; z < startZ + height; z++)
            {
                if (x < 0 || x >= currentWidth || z < 0 || z >= currentHeight)
                {
                    return false;
                }

                if (occupiedCells[x, z] == true)
                {
                    return false;
                }
            }
        }
        return true; 
    }

    /// <summary>
    /// 임시. 초록색 타일
    /// 배치모드로 변경되면 초록색 타일 + 외곽선 처리하여 1x1 타일이 붙어있다는 시각 정보 제공
    /// </summary>
    private Material CreateGridMaterial(bool isPlacementMode)
    {
        Texture2D texture = new Texture2D(64, 64);
        texture.filterMode = FilterMode.Point;

        Color grassGreen = new Color(0.3f, 0.75f, 0.3f);
        Color borderColor = new Color(0.15f, 0.5f, 0.15f);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (isPlacementMode && (x < 2 || x > 61 || y < 2 || y > 61))
                {
                    texture.SetPixel(x, y, borderColor);
                }
                else
                {
                    texture.SetPixel(x, y, grassGreen);
                }
            }
        }
        texture.Apply();

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetTexture("_BaseMap", texture);

        return mat;
    }

    /// <summary>
    /// 마우스 좌클릭시 배치 가능한지 확인 후 시설 실제 생성 및 점유 업데이트
    /// 불가능한 위치에 배치 시도시 DOTween 진동효과 처리
    /// </summary>
    private void TryPlaceBuilding()
    {
        Debug.Log($"[Click 검증] 배치가능여부: {canPlaceCurrent} | SO데이터존재: {selectedBuildingData != null} | 프리뷰존재: {currentPreviewInstance != null}");

        if (!canPlaceCurrent || selectedBuildingData == null || currentPreviewInstance == null)
        {
            if (!isShaking && currentPreviewInstance != null)
            {
                isShaking = true;

                currentPreviewInstance.transform.DOKill();

                currentPreviewInstance.transform.DOShakePosition(0.25f, new Vector3(0.25f, 0f, 0.25f), 40, 90, false, true)
                    .OnComplete(() =>
                    {
                        isShaking = false;
                    });
            }
            return;
        }

        GameObject realBuilding = Instantiate(
            selectedBuildingData.buildingPrefab,
            currentPreviewInstance.transform.position,
            currentPreviewInstance.transform.rotation,
            floorContainer
        );

        if (realBuilding.TryGetComponent<BuildingRuntime>(out BuildingRuntime buildingRuntime))
        {
            buildingRuntime.enabled = true;
            buildingRuntime.Initialize(selectedBuildingData, currentStartGridX, currentStartGridZ);
        }

        for (int i = currentStartGridX; i < currentStartGridX + currentTargetWidth; i++)
        {
            for (int j = currentStartGridZ; j < currentStartGridZ + currentTargetHeight; j++)
            {
                occupiedCells[i, j] = true;
                buildingObjectsGrid[i, j] = realBuilding;
                buildingDataGrid[i, j] = selectedBuildingData;
            }
        }

        Debug.Log($"[Build] {selectedBuildingData.buildingName} 건설 성공!");
    }

    /// <summary>
    /// 이미 설치된 시설에 좌클릭시 배치된 정보 제거, 프리뷰로 전환처리
    /// </summary>
    private void TryPickUpBuilding(int x, int z)
    {
        if (x < 0 || x >= currentWidth || z < 0 || z >= currentHeight) return;
        if (!occupiedCells[x, z] || buildingObjectsGrid[x, z] == null) return;

        GameObject targetBuilding = buildingObjectsGrid[x, z];
        BuildingData retrievedData = buildingDataGrid[x, z];

        Quaternion targetRotation = targetBuilding.transform.rotation;

        for (int i = 0; i < currentWidth; i++)
        {
            for (int j = 0; j < currentHeight; j++)
            {
                if (buildingObjectsGrid[i, j] == targetBuilding)
                {
                    occupiedCells[i, j] = false;
                    buildingObjectsGrid[i, j] = null;
                    buildingDataGrid[i, j] = null;
                }
            }
        }

        int buildingIndex = buildings.IndexOf(retrievedData);
        if (buildingIndex >= 0)
        {
            CreateBuildingPreview(buildingIndex);

            if (currentPreviewInstance != null)
            {
                currentPreviewInstance.transform.rotation = targetRotation;
            }

            Debug.Log($"[PickUp] {retrievedData.buildingName} 정보를 회수하여 재배치 모드로 전환합니다.");
        }

        Destroy(targetBuilding);
    }

    /// <summary>
    /// 선택한 시설 이미지 클릭시 구독하여 마우스에 3D프리뷰를 생성처리
    /// </summary>
    private void CreateBuildingPreview(int buildingIndex)
    {
        if (buildingIndex < 0 || buildingIndex >= buildings.Count) return;

        ClearPreview();
        selectedBuildingData = buildings[buildingIndex];

        if(selectedBuildingData.buildingPrefab != null)
        {
            currentPreviewInstance = Instantiate(selectedBuildingData.buildingPrefab);
            if (currentPreviewInstance.TryGetComponent<BuildingRuntime>(out BuildingRuntime buildingRuntime))
            {
                buildingRuntime.enabled = false;
            }

            Collider[] colliders = currentPreviewInstance.GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders)
            {
                col.enabled = false;
            }

            previewRenderers = currentPreviewInstance.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in previewRenderers)
            {
                renderer.material = previewMaterial;
            }
        }
    }

    /// <summary>
    /// 프리뷰 메쉬의 색상을 파란색/빨간색으로 변경하는 함수
    /// </summary>
    private void UpdatePreviewVisual(bool canPlace)
    {
        if (previewRenderers == null) return;

        Color targetColor = canPlace ? buildableColor : unbuildableColor;

        foreach (MeshRenderer renderer in previewRenderers)
        {
            if (renderer != null)
            {
                renderer.material.SetColor("_BaseColor", targetColor);
            }
        }
    }

    private void RotatePreview()
    {
        if (currentPreviewInstance == null) return;

        float currentY = currentPreviewInstance.transform.eulerAngles.y;

        float rotateY = (currentY > 45f) ? 0f : 90f;

        currentPreviewInstance.transform.rotation = Quaternion.Euler(0f, rotateY, 0f);

        UpdatePreviewPosition();
    }

    /// <summary>
    /// 확장에 대한 테스트함수
    /// </summary>
    [ContextMenu("Function: Expand to Test")]
    public void TestExpand()
    {
        count++;
        ExpandGrid(count, count);
    }

}