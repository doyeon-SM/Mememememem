using DG.Tweening;
using HDY.Capture;
using HDY.Item;
using HDY.Mem;
using KMS.InventoryDuped;
using MemSystem.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GridManager : MonoBehaviour
{
    [Header("타일 생성 관련 정보: Prefab, 생성될 위치, Grid Layer")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Transform floorContainer;
    [SerializeField] private LayerMask gridLayerMask;

    [Header("시설 데이터 정보: SO, 프리뷰")]
    [SerializeField] private List<BuildingData> buildings = new List<BuildingData>();
    [SerializeField] private Material previewMaterial;

    [Tooltip("URP/Unlit 기반의 Surface Type: Transparent로 설정된 머티리얼 에셋을 여기에 넣어주세요.")]
    [SerializeField] private Material gridMaterialPrefab;

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

    // 시설 배치 기록 관리용 참조
    private BuildRecordManager buildRecordManager;

    // 인벤토리의 설계도 상태에 따라 필터링된 실시간 배치 가능 건물 리스트 캐시
    private List<BuildingData> currentAvailableBuildings = new List<BuildingData>();

    // 배치 모드 진행 도중 실시간으로 증감된 설계도 내역을 기억하여 롤백 시 인벤토리를 완벽하게 원복합니다.
    private List<ItemData> sessionRemovedBlueprints = new List<ItemData>();
    private List<ItemData> sessionAddedBlueprints = new List<ItemData>();

    // 프리뷰 이동 시 기존 시설의 작업 데이터를 임시 보존하는 캐시 구조체
    private class PickedUpBuildingRuntimeState
    {
        public FacilityData facilityData;
        public List<MemData> deployedMems = new List<MemData>();
        public List<CapturedMemEntry> deployedMemEntries = new List<CapturedMemEntry>();
    }
    private PickedUpBuildingRuntimeState cachedPickedUpState = null;

    public int MouseGridX { get; private set; }
    public int MouseGridZ { get; private set; }
    public bool IsMouseOnGrid { get; private set; }

    // 이벤트 발행(UI 연결용)
    public static event Action<bool, List<BuildingData>> OnPlacementModeChanged;
    public static event Action OnGridDataChanged;

    // Test전역변수
    private int count = 5;

    private void Awake()
    {
        if (buildRecordManager == null) buildRecordManager = FindFirstObjectByType<BuildRecordManager>();

        InitGridMaterials();
    }

    private void Start()
    {
        int targetWidth = currentWidth > 0 ? currentWidth : 5;
        int targetHeight = currentHeight > 0 ? currentHeight : 5;

        InitializeGrid(targetWidth, targetHeight);

        if (defaultModeMaterial == null)
        {
            defaultModeMaterial = Resources.Load<Material>("Green_Mat");
        }
        if (defaultModeMaterial == null)
        {
            defaultModeMaterial = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m != null && m.name == "Green_Mat");
        }

        placeModeMaterial = CreateGridMaterial(true);
    }

    private void OnEnable()
    {
        PlacementUI.OnBuildingSelected += CreateBuildingPreview;
        PlacementUI.OnBuildingSaved += SavePlacement;
        PlacementUI.OnBuildingCancelled += CancelPlacement;
    }

    private void OnDisable()
    {
        PlacementUI.OnBuildingSelected -= CreateBuildingPreview;
        PlacementUI.OnBuildingSaved -= SavePlacement;
        PlacementUI.OnBuildingCancelled -= CancelPlacement;
    }

    void Update()
    {
        UpdateMouseGridPosition();

        if (currentPreviewInstance != null)
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
                if (EventSystem.current != null && IsPointerOverBlockingUI()) return;
                TryPickUpBuilding(MouseGridX, MouseGridZ);
            }
        }
        else if (!isPlacementMode && IsMouseOnGrid)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && IsPointerOverBlockingUI()) return;

                if (occupiedCells[MouseGridX, MouseGridZ] && buildingObjectsGrid[MouseGridX, MouseGridZ] != null)
                {
                    GameObject targetObj = buildingObjectsGrid[MouseGridX, MouseGridZ];

                    if (targetObj.TryGetComponent<ProductionFacilityRuntime>(out ProductionFacilityRuntime facility))
                    {
                        PanelManager.Instance.OpenProductionPanel(facility);
                    }

                    if (targetObj.TryGetComponent<ProductionCraftRuntime>(out ProductionCraftRuntime craft))
                    {
                        PanelManager.Instance.OpenCraftingPanel(craft);
                    }
                }
            }
        }
    }

    private void InitGridMaterials()
    {
        if (defaultModeMaterial != null) return;

        if (tilePrefab != null && tilePrefab.TryGetComponent<MeshRenderer>(out MeshRenderer prefabRenderer))
        {
            defaultModeMaterial = prefabRenderer.sharedMaterial;
        }

        if (defaultModeMaterial == null)
        {
            defaultModeMaterial = Resources.Load<Material>("Green_Mat");
        }
        if (defaultModeMaterial == null)
        {
            defaultModeMaterial = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m != null && m.name == "Green_Mat");
        }

        if (placeModeMaterial == null)
        {
            placeModeMaterial = CreateGridMaterial(true);
        }
    }

    public void InitializeGrid(int width, int height)
    {
        currentWidth = width;
        currentHeight = height;
        if (tileGrid == null || tileGrid.Length == 0)
            tileGrid = new GameObject[currentWidth, currentHeight];

        if (occupiedCells == null || occupiedCells.Length == 0)
            occupiedCells = new bool[currentWidth, currentHeight];

        if (buildingObjectsGrid == null || buildingObjectsGrid.Length == 0)
            buildingObjectsGrid = new GameObject[currentWidth, currentHeight];

        if (buildingDataGrid == null || buildingDataGrid.Length == 0)
            buildingDataGrid = new BuildingData[currentWidth, currentHeight];

        for (int i = 0; i < currentWidth; i++)
        {
            for (int j = 0; j < currentHeight; j++)
            {
                if (tileGrid[i, j] == null)
                {
                    tileGrid[i, j] = SpawnTile(i, j);
                }
            }
        }
    }

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

    private GameObject SpawnTile(int x, int z)
    {
        Vector3 spawnPosition = new Vector3(x + 0.5f, 0f, z + 0.5f);

        GameObject newTile = Instantiate(tilePrefab, spawnPosition, Quaternion.Euler(90, 0, 0), floorContainer);
        newTile.name = $"Tile_({x},{z})";

        if (newTile.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
        {
            meshRenderer.material = defaultModeMaterial;
        }

        GameObject gridOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        if (gridOverlay.TryGetComponent<Collider>(out var col)) DestroyImmediate(col);

        gridOverlay.name = "GridOverlay";
        gridOverlay.transform.SetParent(newTile.transform);
        gridOverlay.transform.localPosition = new Vector3(0f, 0f, -0.001f);
        gridOverlay.transform.localRotation = Quaternion.identity;
        gridOverlay.transform.localScale = Vector3.one;

        if (gridOverlay.TryGetComponent<MeshRenderer>(out MeshRenderer overlayRenderer))
        {
            overlayRenderer.material = placeModeMaterial;
        }

        gridOverlay.SetActive(isPlacementMode);

        return newTile;
    }

    public void ChangePlacementMode()
    {
        isPlacementMode = !isPlacementMode;

        if (isPlacementMode)
        {
            sessionRemovedBlueprints.Clear();
            sessionAddedBlueprints.Clear();

            buildRecordManager?.SaveRollbackData(buildingObjectsGrid, buildingDataGrid, currentWidth, currentHeight);
        }
        else
        {
            ClearPreview();
        }

        currentAvailableBuildings = GetAvailableBuildingsFromInventory();
        OnPlacementModeChanged?.Invoke(isPlacementMode, currentAvailableBuildings);

        if (tileGrid == null) return;

        for (int i = 0; i < currentWidth; i++)
        {
            for (int j = 0; j < currentHeight; j++)
            {
                if (tileGrid[i, j] != null)
                {
                    Transform overlay = tileGrid[i, j].transform.Find("GridOverlay");
                    if (overlay != null)
                    {
                        overlay.gameObject.SetActive(isPlacementMode);
                    }
                }
            }
        }

        Debug.Log($"배치 모드 상태 변경: {isPlacementMode} | 배치 가능 건물 수: {currentAvailableBuildings.Count}개");
    }

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

            
            cachedPickedUpState = null;
        }
    }

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

    private void UpdatePreviewPosition()
    {
        if (selectedBuildingData == null || currentPreviewInstance == null) return;
        if (isShaking) return;

        int currentRotationIndex = Mathf.RoundToInt(currentPreviewInstance.transform.eulerAngles.y / 90f) % 4;
        bool isRotated = (currentRotationIndex == 1 || currentRotationIndex == 3);

        currentTargetWidth = isRotated ? selectedBuildingData.height : selectedBuildingData.width;
        currentTargetHeight = isRotated ? selectedBuildingData.width : selectedBuildingData.height;

        currentStartGridX = Mathf.FloorToInt(raycastHitPoint.x - (currentTargetWidth / 2.0f));
        currentStartGridZ = Mathf.FloorToInt(raycastHitPoint.z - (currentTargetHeight / 2.0f));

        float offsetX = currentStartGridX + (currentTargetWidth / 2.0f);
        float offsetZ = currentStartGridZ + (currentTargetHeight / 2.0f);
        currentPreviewInstance.transform.position = new Vector3(offsetX, 0f, offsetZ);

        canPlaceCurrent = CheckPlacement(currentStartGridX, currentStartGridZ, currentTargetWidth, currentTargetHeight);

        if (canPlaceCurrent && selectedBuildingData.requireBlueprint != null)
        {
            var inventory = FindFirstObjectByType<PlayerInventory>();
            if (inventory == null || inventory.GetItemAmount(selectedBuildingData.requireBlueprint.Item_ID) <= 0)
            {
                canPlaceCurrent = false;
            }
        }

        UpdatePreviewVisual(canPlaceCurrent);
    }

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

    private Material CreateGridMaterial(bool isPlacementMode)
    {
        Texture2D texture = new Texture2D(64, 64);
        texture.filterMode = FilterMode.Point;

        Color gridBorderColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (x < 2 || x > 61 || y < 2 || y > 61)
                {
                    texture.SetPixel(x, y, gridBorderColor);
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }
        texture.Apply();

        Material mat = null;

        if (gridMaterialPrefab != null)
        {
            mat = new Material(gridMaterialPrefab);
        }
        else
        {
            Shader targetShader = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(targetShader);
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

        return mat;
    }

    private void TryPlaceBuilding()
    {
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

        string newUniqueId = $"{selectedBuildingData.buildingName}_{currentStartGridX}_{currentStartGridZ}";

        // 이전 이동(PickUp) 상태에서 집어 올린 작업 데이터가 있다면 새 위치에 이식
        if (cachedPickedUpState != null && cachedPickedUpState.facilityData != null)
        {
            cachedPickedUpState.facilityData.Building_ID = newUniqueId;

            if (realBuilding.TryGetComponent<ProductionFacilityRuntime>(out ProductionFacilityRuntime prodRuntime))
            {
                prodRuntime.buildingData = selectedBuildingData;
                prodRuntime.isProducing = cachedPickedUpState.facilityData.isActive;
                prodRuntime.currentProgressTime = cachedPickedUpState.facilityData.currentProgressTime;
                prodRuntime.currentStorageCount = cachedPickedUpState.facilityData.currentStorageCount;
                prodRuntime.craftingItem = FindItemDataInProject(cachedPickedUpState.facilityData.currentCraftingItemId);
                prodRuntime.UpdateMaxStorage();

                if (prodRuntime.DeployedMems != null && prodRuntime.DeployedMemEntries != null)
                {
                    prodRuntime.DeployedMems.Clear();
                    prodRuntime.DeployedMemEntries.Clear();
                    prodRuntime.DeployedMems.AddRange(cachedPickedUpState.deployedMems);
                    prodRuntime.DeployedMemEntries.AddRange(cachedPickedUpState.deployedMemEntries);
                }
                prodRuntime.CheckProductionCondition();
            }
            else if (realBuilding.TryGetComponent<ProductionCraftRuntime>(out ProductionCraftRuntime craftRuntime))
            {
                craftRuntime.buildingData = selectedBuildingData;
                craftRuntime.isProducing = cachedPickedUpState.facilityData.isActive;
                craftRuntime.targetQuantity = cachedPickedUpState.facilityData.targetQuantity;
                craftRuntime.remainingQuantity = cachedPickedUpState.facilityData.remainingQuantity;
                craftRuntime.currentProgressTime = cachedPickedUpState.facilityData.currentProgressTime;
                craftRuntime.currentStorageCount = cachedPickedUpState.facilityData.currentStorageCount;
                craftRuntime.currentCraftingItem = FindItemDataInProject(cachedPickedUpState.facilityData.currentCraftingItemId);

                if (craftRuntime.DeployedMems != null && craftRuntime.DeployedMemEntries != null)
                {
                    craftRuntime.DeployedMems.Clear();
                    craftRuntime.DeployedMemEntries.Clear();
                    craftRuntime.DeployedMems.AddRange(cachedPickedUpState.deployedMems);
                    craftRuntime.DeployedMemEntries.AddRange(cachedPickedUpState.deployedMemEntries);
                }
            }

            if (RecordManager.Instance != null)
            {
                RecordManager.Instance.UpdateFacilityData(newUniqueId, cachedPickedUpState.facilityData);
            }

            cachedPickedUpState = null;
        }
        else
        {
            if (realBuilding.TryGetComponent<ProductionFacilityRuntime>(out ProductionFacilityRuntime prodRuntime))
            {
                prodRuntime.buildingData = selectedBuildingData;
                prodRuntime.UpdateMaxStorage();
            }
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

        if (selectedBuildingData.requireBlueprint != null)
        {
            var inventory = FindFirstObjectByType<PlayerInventory>();
            if (inventory != null)
            {
                inventory.RemoveItem(selectedBuildingData.requireBlueprint.Item_ID, 1);
                sessionRemovedBlueprints.Add(selectedBuildingData.requireBlueprint);
            }
        }

        Debug.Log($"[Build] {selectedBuildingData.buildingName} 건설 및 데이터 보존 재배치 성공!");

        ClearPreview();

        currentAvailableBuildings = GetAvailableBuildingsFromInventory();
        OnPlacementModeChanged?.Invoke(isPlacementMode, currentAvailableBuildings);

        OnGridDataChanged?.Invoke();
        TotalHungerManager.Instance?.RecalculateTotalHunger();
    }

    private void TryPickUpBuilding(int x, int z)
    {
        if (x < 0 || x >= currentWidth || z < 0 || z >= currentHeight) return;
        if (!occupiedCells[x, z] || buildingObjectsGrid[x, z] == null) return;

        GameObject targetBuilding = buildingObjectsGrid[x, z];
        BuildingData retrievedData = buildingDataGrid[x, z];
        Quaternion targetRotation = targetBuilding.transform.rotation;

        // 건물을 지우기 전 실물 컴포넌트에서 작업 데이터 및 멤 배치 정보 캡처
        cachedPickedUpState = new PickedUpBuildingRuntimeState();
        cachedPickedUpState.facilityData = new FacilityData();

        if (targetBuilding.TryGetComponent<ProductionFacilityRuntime>(out var facility))
        {
            cachedPickedUpState.facilityData.isActive = facility.isProducing;
            cachedPickedUpState.facilityData.currentProgressTime = facility.currentProgressTime;
            cachedPickedUpState.facilityData.currentStorageCount = facility.currentStorageCount;
            cachedPickedUpState.facilityData.currentCraftingItemId = facility.craftingItem != null ? facility.craftingItem.Item_ID : "";

            if (facility.DeployedMems != null)
                cachedPickedUpState.deployedMems.AddRange(facility.DeployedMems);
            if (facility.DeployedMemEntries != null)
            {
                cachedPickedUpState.deployedMemEntries.AddRange(facility.DeployedMemEntries);
                foreach (var entry in facility.DeployedMemEntries)
                {
                    if (entry != null) cachedPickedUpState.facilityData.DeployedMemIDs.Add(entry.KeyId);
                }
            }
        }
        else if (targetBuilding.TryGetComponent<ProductionCraftRuntime>(out var craft))
        {
            cachedPickedUpState.facilityData.isActive = craft.isProducing;
            cachedPickedUpState.facilityData.targetQuantity = craft.targetQuantity;
            cachedPickedUpState.facilityData.remainingQuantity = craft.remainingQuantity;
            cachedPickedUpState.facilityData.currentProgressTime = craft.currentProgressTime;
            cachedPickedUpState.facilityData.currentStorageCount = craft.currentStorageCount;
            cachedPickedUpState.facilityData.currentCraftingItemId = craft.currentCraftingItem != null ? craft.currentCraftingItem.Item_ID : "";

            if (craft.DeployedMems != null)
                cachedPickedUpState.deployedMems.AddRange(craft.DeployedMems);
            if (craft.DeployedMemEntries != null)
            {
                cachedPickedUpState.deployedMemEntries.AddRange(craft.DeployedMemEntries);
                foreach (var entry in craft.DeployedMemEntries)
                {
                    if (entry != null) cachedPickedUpState.facilityData.DeployedMemIDs.Add(entry.KeyId);
                }
            }
        }

        // 2. 그리드 격자 정보에서 해당 건물 제거 및 실물 GameObject 파괴
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

        Destroy(targetBuilding);

        // 3. 설계도 세션 원복 (인벤토리 추가)
        if (retrievedData != null && retrievedData.requireBlueprint != null)
        {
            var inventory = FindFirstObjectByType<PlayerInventory>();
            if (inventory != null)
            {
                inventory.AddItem(retrievedData.requireBlueprint, 1);
                sessionAddedBlueprints.Add(retrievedData.requireBlueprint);
            }
        }

        currentAvailableBuildings = GetAvailableBuildingsFromInventory();
        OnPlacementModeChanged?.Invoke(isPlacementMode, currentAvailableBuildings);

        OnGridDataChanged?.Invoke();
        TotalHungerManager.Instance?.RecalculateTotalHunger();

        // 4. 프리뷰 생성
        int availableIndex = currentAvailableBuildings.IndexOf(retrievedData);
        if (availableIndex >= 0)
        {
            CreateBuildingPreview(availableIndex);

            if (currentPreviewInstance != null)
            {
                currentPreviewInstance.transform.rotation = targetRotation;
            }
        }
    }

    private void CreateBuildingPreview(int buildingIndex)
    {
        if (buildingIndex < 0 || buildingIndex >= currentAvailableBuildings.Count) return;

        ClearPreview();
        selectedBuildingData = currentAvailableBuildings[buildingIndex];

        if (selectedBuildingData.buildingPrefab != null)
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

    public void SavePlacement()
    {
        if (!isPlacementMode) return;
        if (buildRecordManager == null) return;

        buildRecordManager.ClearRecordOnSave();
        ChangePlacementMode();

        sessionRemovedBlueprints.Clear();
        sessionAddedBlueprints.Clear();

        TriggerSatisfactionUpdate();
    }

    public void CancelPlacement()
    {
        if (!isPlacementMode) return;
        if (buildRecordManager == null) return;

        ClearAllPlacedBuildings();

        List<BuildingSnapshot> rollbackData = buildRecordManager.Rollback();
        RestoreRollbackData(rollbackData);

        var inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory != null)
        {
            foreach (var item in sessionRemovedBlueprints)
            {
                inventory.AddItem(item, 1);
            }
            foreach (var item in sessionAddedBlueprints)
            {
                inventory.RemoveItem(item.Item_ID, 1);
            }
        }
        sessionRemovedBlueprints.Clear();
        sessionAddedBlueprints.Clear();

        ChangePlacementMode();

        TriggerSatisfactionUpdate();
    }

    private void ClearAllPlacedBuildings()
    {
        for (int x = 0; x < currentWidth; x++)
        {
            for (int z = 0; z < currentHeight; z++)
            {
                if (buildingObjectsGrid[x, z] != null)
                {
                    GameObject buildingData = buildingObjectsGrid[x, z];
                    for (int i = 0; i < currentWidth; i++)
                    {
                        for (int j = 0; j < currentHeight; j++)
                        {
                            if (buildingObjectsGrid[i, j] == buildingData)
                            {
                                buildingObjectsGrid[i, j] = null;
                                buildingDataGrid[i, j] = null;
                                occupiedCells[i, j] = false;
                            }
                        }
                    }
                    Destroy(buildingData);
                }
            }
        }
    }

    private void RestoreRollbackData(List<BuildingSnapshot> rollbackData)
    {
        if (rollbackData == null) return;

        foreach (var snap in rollbackData)
        {
            if (snap.data == null || snap.data.buildingPrefab == null) continue;

            int currentRotationIndex = Mathf.RoundToInt(snap.rotation.eulerAngles.y / 90f) % 4;
            bool isRotated = (currentRotationIndex == 1 || currentRotationIndex == 3);
            int bWidth = isRotated ? snap.data.height : snap.data.width;
            int bHeight = isRotated ? snap.data.width : snap.data.height;

            float offsetX = snap.startX + (bWidth / 2.0f);
            float offsetZ = snap.startZ + (bHeight / 2.0f);
            Vector3 spawnPos = new Vector3(offsetX, 0f, offsetZ);

            GameObject restoredBuilding = Instantiate(snap.data.buildingPrefab, spawnPos, snap.rotation, floorContainer);

            if (restoredBuilding.TryGetComponent<BuildingRuntime>(out BuildingRuntime buildingRuntime))
            {
                buildingRuntime.enabled = true;
                buildingRuntime.Initialize(snap.data, snap.startX, snap.startZ);
            }

            string uniqueId = $"{snap.data.buildingName}_{snap.startX}_{snap.startZ}";
            if (RecordManager.Instance != null)
            {
                FacilityData entry = RecordManager.Instance.GetFacilityData(uniqueId);
                if (entry != null)
                {
                    List<CapturedMemEntry> matchedEntries = new List<CapturedMemEntry>();
                    List<MemData> restoredMems = new List<MemData>();

                    var memManager = FindFirstObjectByType<MemCaptureManager>();
                    if (memManager != null && entry.DeployedMemIDs != null)
                    {
                        var warehouseList = memManager.CapturedMems;
                        foreach (var savedKeyId in entry.DeployedMemIDs)
                        {
                            var warehouseMatch = warehouseList.FirstOrDefault(m => m != null && m.KeyId == savedKeyId);
                            if (warehouseMatch != null)
                            {
                                warehouseMatch.IsActive = true;
                                matchedEntries.Add(warehouseMatch);

                                MemData mData = new MemData();
                                mData.memName = warehouseMatch.MemId;

                                var template = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(warehouseMatch.MemId) : null;
                                mData.maxHunger = (template != null) ? template.maxHunger : 10;

                                restoredMems.Add(mData);
                            }
                        }
                    }

                    if (restoredBuilding.TryGetComponent<ProductionFacilityRuntime>(out var facility))
                    {
                        facility.buildingData = snap.data;
                        facility.isProducing = entry.isActive;
                        facility.currentProgressTime = entry.currentProgressTime;
                        facility.currentStorageCount = entry.currentStorageCount;
                        facility.craftingItem = FindItemDataInProject(entry.currentCraftingItemId);
                        facility.UpdateMaxStorage();

                        if (facility.DeployedMems != null && facility.DeployedMemEntries != null)
                        {
                            facility.DeployedMems.Clear();
                            facility.DeployedMemEntries.Clear();
                            facility.DeployedMems.AddRange(restoredMems);
                            facility.DeployedMemEntries.AddRange(matchedEntries);
                        }
                        facility.CheckProductionCondition();
                    }
                    else if (restoredBuilding.TryGetComponent<ProductionCraftRuntime>(out var craft))
                    {
                        craft.buildingData = snap.data;
                        craft.isProducing = entry.isActive;
                        craft.targetQuantity = entry.targetQuantity;
                        craft.remainingQuantity = entry.remainingQuantity;
                        craft.currentProgressTime = entry.currentProgressTime;
                        craft.currentStorageCount = entry.currentStorageCount;
                        craft.currentCraftingItem = FindItemDataInProject(entry.currentCraftingItemId);

                        if (craft.DeployedMems != null && craft.DeployedMemEntries != null)
                        {
                            craft.DeployedMems.Clear();
                            craft.DeployedMemEntries.Clear();
                            craft.DeployedMems.AddRange(restoredMems);
                            craft.DeployedMemEntries.AddRange(matchedEntries);
                        }
                    }
                }
            }

            for (int x = snap.startX; x < snap.startX + bWidth; x++)
            {
                for (int z = snap.startZ; z < snap.startZ + bHeight; z++)
                {
                    occupiedCells[x, z] = true;
                    buildingObjectsGrid[x, z] = restoredBuilding;
                    buildingDataGrid[x, z] = snap.data;
                }
            }
        }
    }

    private ItemData FindItemDataInProject(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        ItemData[] allItems = Resources.FindObjectsOfTypeAll<ItemData>();
        return allItems.FirstOrDefault(item => item != null && item.Item_ID == itemId);
    }

    private bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null) return false;
        if (!EventSystem.current.IsPointerOverGameObject()) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            eventData.position = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }
        else
        {
            return false;
        }

        System.Collections.Generic.List<RaycastResult> results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (result.gameObject != null)
            {
                string uiName = result.gameObject.name.ToLower();

                if (uiName.Contains("root") || uiName.Contains("hud") || uiName.Equals("panel") || uiName.Contains("bg") || uiName.Contains("background"))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    public int GetTotalSatisfactionFromGrid()
    {
        int totalSatisfaction = 0;
        if (buildingObjectsGrid == null) return 0;

        HashSet<GameObject> countedBuildings = new HashSet<GameObject>();

        for (int x = 0; x < currentWidth; x++)
        {
            for (int z = 0; z < currentHeight; z++)
            {
                GameObject buildingObj = buildingObjectsGrid[x, z];

                if (buildingObj != null && !countedBuildings.Contains(buildingObj))
                {
                    countedBuildings.Add(buildingObj);

                    if (buildingObj.TryGetComponent<BuildingRuntime>(out BuildingRuntime runtime))
                    {
                        if (runtime.buildingData != null)
                        {
                            totalSatisfaction += runtime.buildingData.satisfaction;
                        }
                    }
                }
            }
        }

        return totalSatisfaction;
    }

    private void TriggerSatisfactionUpdate()
    {
        SatisFactoryUI satisfactionUI = UnityEngine.Object.FindFirstObjectByType<SatisFactoryUI>();
        if (satisfactionUI != null)
        {
            satisfactionUI.RecalculateSatisfaction();
        }
    }

    private List<BuildingData> GetAvailableBuildingsFromInventory()
    {
        List<BuildingData> filteredList = new List<BuildingData>();
        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();

        foreach (var bData in buildings)
        {
            if (bData == null) continue;

            if (bData.requireBlueprint == null || (inventory != null && inventory.GetItemAmount(bData.requireBlueprint.Item_ID) > 0))
            {
                filteredList.Add(bData);
            }
        }

        return filteredList;
    }

    [ContextMenu("Function: Expand to Test")]
    public void TestExpand()
    {
        count++;
        ExpandGrid(count, count);
    }
}