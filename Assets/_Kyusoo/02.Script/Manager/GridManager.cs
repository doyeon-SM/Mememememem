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
    [Header("타일 생성 관련 정보: Prefabs, 생성될 위치, Grid Layer")]
    [Tooltip("영지 외곽(테두리/절벽)에 생성될 타일 프리팹 (A)")]
    [SerializeField] private GameObject outerTilePrefab;

    [Tooltip("영지 내부(평면)에 생성될 타일 프리팹 (B)")]
    [SerializeField] private GameObject innerTilePrefab;

    [SerializeField] private Transform floorContainer;
    [SerializeField] private LayerMask gridLayerMask;

    [Header("내부 상단 Plane 설정")]
    [Tooltip("타일 상단에 올려둘 Green Plane 오브젝트")]
    [SerializeField] private GameObject innerSurfacePlane;

    [Tooltip("외곽 타일 테두리 여백 (값이 크면 Plane이 더 작아집니다)")]
    [SerializeField] private float planeInsetMargin = 1.2f;

    [Tooltip("Green Plane 및 건물 배치 Y축 높이 (기본값: 0.501f)")]
    [SerializeField] private float innerPlaneY = 0.501f;

    [Tooltip("배치 모드 격자판의 Y축 높이 (Plane 직상단 밀착: 0.502f)")]
    [SerializeField] private float gridOverlayY = 0.502f;

    [Header("시설 데이터 정보: SO, 프리뷰")]
    [SerializeField] private List<BuildingData> buildings = new List<BuildingData>();
    [SerializeField] private Material previewMaterial;

    [Tooltip("URP/Unlit 기반의 Surface Type: Transparent로 설정된 머티리얼 에셋을 여기에 넣어주세요.")]
    [SerializeField] private Material gridMaterialPrefab;

    [Header("타일 색상 정보: 배치 가능, 배치 불가")]
    [SerializeField] private Color buildableColor = new Color(0f, 0.5f, 1f, 0.4f);
    [SerializeField] private Color unbuildableColor = new Color(1f, 0f, 0f, 0.4f);

    private BuildingData selectedBuildingData;
    private GameObject currentPreviewInstance;

    private MeshRenderer[] previewRenderers;

    private int currentWidth;
    private int currentHeight;

    private GameObject[,] tileGrid;
    private Vector3 raycastHitPoint;
    private bool[,] occupiedCells;

    private GameObject globalGridOverlay;

    private GameObject[,] buildingObjectsGrid;
    private BuildingData[,] buildingDataGrid;

    private int currentStartGridX;
    private int currentStartGridZ;

    private int currentTargetWidth;
    private int currentTargetHeight;

    private bool canPlaceCurrent = false;
    private bool isShaking = false;

    private Material placeModeMaterial;
    private bool isPlacementMode = false;

    private BuildRecordManager buildRecordManager;

    private List<BuildingData> currentAvailableBuildings = new List<BuildingData>();

    private List<ItemData> sessionRemovedBlueprints = new List<ItemData>();
    private List<ItemData> sessionAddedBlueprints = new List<ItemData>();

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

    public static event Action<bool, List<BuildingData>> OnPlacementModeChanged;
    public static event Action OnGridDataChanged;

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
                    else if (targetObj.TryGetComponent<ProductionCraftRuntime>(out ProductionCraftRuntime craft))
                    {
                        PanelManager.Instance.OpenCraftingPanel(craft);
                    }
                    else if (targetObj.TryGetComponent<RanchFacilityRuntime>(out RanchFacilityRuntime ranch)) // 🌟 [추가]: 목장 클릭 시 연동
                    {
                        PanelManager.Instance.OpenRanchPanel(ranch);
                    }
                }
            }
        }
    }

    private void InitGridMaterials()
    {
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
                    tileGrid[i, j] = SpawnTile(i, j, currentWidth, currentHeight);
                }
            }
        }

        UpdateInnerSurfacePlane();
        UpdateGlobalGridOverlay();
    }

    public void ExpandGrid(int newWidth, int newHeight)
    {
        if (newWidth == currentWidth && newHeight == currentHeight) return;

        GameObject[,] newTileGrid = new GameObject[newWidth, newHeight];
        bool[,] newOccupiedCells = new bool[newWidth, newHeight];

        GameObject[,] newBuildingObjectsGrid = new GameObject[newWidth, newHeight];
        BuildingData[,] newBuildingDataGrid = new BuildingData[newWidth, newHeight];

        for (int i = 0; i < currentWidth; i++)
        {
            for (int j = 0; j < currentHeight; j++)
            {
                newOccupiedCells[i, j] = occupiedCells[i, j];
                newBuildingObjectsGrid[i, j] = buildingObjectsGrid[i, j];
                newBuildingDataGrid[i, j] = buildingDataGrid[i, j];

                bool wasOuter = IsOuterTile(i, j, currentWidth, currentHeight);
                bool isNowOuter = IsOuterTile(i, j, newWidth, newHeight);

                if (wasOuter != isNowOuter)
                {
                    if (tileGrid[i, j] != null)
                    {
                        Destroy(tileGrid[i, j]);
                    }
                    newTileGrid[i, j] = SpawnTile(i, j, newWidth, newHeight);
                }
                else
                {
                    newTileGrid[i, j] = tileGrid[i, j];
                }
            }
        }

        for (int i = 0; i < newWidth; i++)
        {
            for (int j = 0; j < newHeight; j++)
            {
                if (i >= currentWidth || j >= currentHeight)
                {
                    newTileGrid[i, j] = SpawnTile(i, j, newWidth, newHeight);
                }
            }
        }

        tileGrid = newTileGrid;
        occupiedCells = newOccupiedCells;
        buildingObjectsGrid = newBuildingObjectsGrid;
        buildingDataGrid = newBuildingDataGrid;
        currentWidth = newWidth;
        currentHeight = newHeight;

        UpdateInnerSurfacePlane();
        UpdateGlobalGridOverlay();
    }

    private bool IsOuterTile(int x, int z, int width, int height)
    {
        return x == 0 || x == width - 1 || z == 0 || z == height - 1;
    }

    private GameObject SpawnTile(int x, int z, int width, int height)
    {
        Vector3 spawnPosition = new Vector3(x + 0.5f, 0f, z + 0.5f);

        bool isOuter = IsOuterTile(x, z, width, height);
        GameObject targetPrefab = isOuter ? outerTilePrefab : innerTilePrefab;

        if (targetPrefab == null)
        {
            targetPrefab = outerTilePrefab != null ? outerTilePrefab : innerTilePrefab;
        }

        GameObject newTile = Instantiate(targetPrefab, spawnPosition, Quaternion.identity, floorContainer);
        newTile.name = $"Tile_({x},{z})";

        int maskLayer = GetFirstLayerFromMask(gridLayerMask);
        if (maskLayer >= 0)
        {
            SetLayerRecursively(newTile, maskLayer);
        }

        var colliders = newTile.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            BoxCollider boxCol = newTile.AddComponent<BoxCollider>();
            boxCol.center = new Vector3(0f, 0.5f, 0f);
            boxCol.size = Vector3.one;
        }

        return newTile;
    }

    private void UpdateInnerSurfacePlane()
    {
        if (innerSurfacePlane == null) return;

        float centerX = currentWidth / 2.0f;
        float centerZ = currentHeight / 2.0f;

        innerSurfacePlane.transform.position = new Vector3(centerX, innerPlaneY, centerZ);

        float targetWidth = Mathf.Max(0.1f, currentWidth - planeInsetMargin);
        float targetHeight = Mathf.Max(0.1f, currentHeight - planeInsetMargin);

        innerSurfacePlane.transform.localScale = new Vector3(targetWidth / 10.0f, 1.0f, targetHeight / 10.0f);
    }

    private void UpdateGlobalGridOverlay()
    {
        if (globalGridOverlay == null)
        {
            globalGridOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);

            if (globalGridOverlay.TryGetComponent<Collider>(out var col))
            {
                col.enabled = false;

                if (Application.isPlaying)
                {
                    Destroy(col);
                }
            }

            globalGridOverlay.name = "GlobalGridOverlay";
            globalGridOverlay.transform.SetParent(floorContainer != null ? floorContainer : transform);
            globalGridOverlay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        if (globalGridOverlay.TryGetComponent<MeshRenderer>(out MeshRenderer overlayRenderer))
        {
            if (placeModeMaterial == null) placeModeMaterial = CreateGridMaterial(true);
            overlayRenderer.material = placeModeMaterial;
        }

        globalGridOverlay.transform.position = new Vector3(currentWidth / 2.0f, gridOverlayY, currentHeight / 2.0f);
        globalGridOverlay.transform.localScale = new Vector3(currentWidth, currentHeight, 1f);

        if (globalGridOverlay.TryGetComponent<MeshRenderer>(out MeshRenderer renderer) && renderer.material != null)
        {
            Vector2 tiling = new Vector2(currentWidth, currentHeight);
            if (renderer.material.HasProperty("_BaseMap")) renderer.material.SetTextureScale("_BaseMap", tiling);
            if (renderer.material.HasProperty("_MainTex")) renderer.material.SetTextureScale("_MainTex", tiling);
        }

        globalGridOverlay.SetActive(isPlacementMode);
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

        if (globalGridOverlay != null)
        {
            globalGridOverlay.SetActive(isPlacementMode);
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

        LayerMask maskToUse = gridLayerMask.value != 0 ? gridLayerMask : (LayerMask)(~0);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, maskToUse))
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

        float previewY = gridOverlayY + 0.008f;
        currentPreviewInstance.transform.position = new Vector3(offsetX, previewY, offsetZ);

        canPlaceCurrent = CheckPlacement(currentStartGridX, currentStartGridZ, currentTargetWidth, currentTargetHeight);

        if (canPlaceCurrent && !string.IsNullOrEmpty(selectedBuildingData.requireBlueprint))
        {
            var inventory = FindFirstObjectByType<PlayerInventory>();
            if (inventory == null || inventory.GetItemAmount(selectedBuildingData.requireBlueprint) <= 0)
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
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;

        Color gridBorderColor = new Color(0.1f, 0.1f, 0.1f, 0.45f);
        Color transparentColor = new Color(0f, 0f, 0f, 0f);

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
                    texture.SetPixel(x, y, transparentColor);
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
            if (targetShader == null) targetShader = Shader.Find("Unlit/Transparent");
            if (targetShader == null) targetShader = Shader.Find("Legacy Shaders/Transparent/Cutout/Unlit");

            mat = new Material(targetShader);
        }

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.SetOverrideTag("RenderType", "Transparent");

        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;

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

        Vector3 realSpawnPosition = new Vector3(
            currentPreviewInstance.transform.position.x,
            innerPlaneY,
            currentPreviewInstance.transform.position.z
        );

        GameObject realBuilding = Instantiate(
            selectedBuildingData.buildingPrefab,
            realSpawnPosition,
            currentPreviewInstance.transform.rotation,
            floorContainer
        );

        if (realBuilding.TryGetComponent<BuildingRuntime>(out BuildingRuntime buildingRuntime))
        {
            buildingRuntime.enabled = true;
            buildingRuntime.Initialize(selectedBuildingData, currentStartGridX, currentStartGridZ);
        }

        string newUniqueId = $"{selectedBuildingData.buildingName}_{currentStartGridX}_{currentStartGridZ}";

        if (cachedPickedUpState != null && cachedPickedUpState.facilityData != null)
        {
            cachedPickedUpState.facilityData.Building_ID = newUniqueId;

            if (realBuilding.TryGetComponent<ProductionFacilityRuntime>(out ProductionFacilityRuntime prodRuntime))
            {
                prodRuntime.buildingData = selectedBuildingData;
                prodRuntime.isProducing = cachedPickedUpState.facilityData.isActive;
                prodRuntime.currentProgressTime = cachedPickedUpState.facilityData.currentProgressTime;
                prodRuntime.currentStorageCount = cachedPickedUpState.facilityData.currentStorageCount;
                prodRuntime.craftingItem = cachedPickedUpState.facilityData.currentCraftingItemId;
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
                craftRuntime.currentCraftingItem = cachedPickedUpState.facilityData.currentCraftingItemId;

                if (craftRuntime.DeployedMems != null && craftRuntime.DeployedMemEntries != null)
                {
                    craftRuntime.DeployedMems.Clear();
                    craftRuntime.DeployedMemEntries.Clear();
                    craftRuntime.DeployedMems.AddRange(cachedPickedUpState.deployedMems);
                    craftRuntime.DeployedMemEntries.AddRange(cachedPickedUpState.deployedMemEntries);
                }
            }
            else if (realBuilding.TryGetComponent<RanchFacilityRuntime>(out RanchFacilityRuntime ranchRuntime)) // 🌟 [추가]: 목장 재배치 복원
            {
                ranchRuntime.buildingData = selectedBuildingData;
                ranchRuntime.UpdateSlotCapacity();

                for (int i = 0; i < cachedPickedUpState.deployedMems.Count && i < cachedPickedUpState.deployedMemEntries.Count; i++)
                {
                    ranchRuntime.TryAddMemToSlot(i, cachedPickedUpState.deployedMems[i], cachedPickedUpState.deployedMemEntries[i]);
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
            else if (realBuilding.TryGetComponent<RanchFacilityRuntime>(out RanchFacilityRuntime ranchRuntime)) // 🌟 [추가]: 목장 최초 배치
            {
                ranchRuntime.buildingData = selectedBuildingData;
                ranchRuntime.UpdateSlotCapacity();
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

        if (!string.IsNullOrEmpty(selectedBuildingData.requireBlueprint))
        {
            var inventory = FindFirstObjectByType<PlayerInventory>();
            if (inventory != null)
            {
                inventory.RemoveItem(selectedBuildingData.requireBlueprint, 1);

                ItemData bpItem = FindItemDataInProject(selectedBuildingData.requireBlueprint);
                if (bpItem != null)
                {
                    sessionRemovedBlueprints.Add(bpItem);
                }
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

        cachedPickedUpState = new PickedUpBuildingRuntimeState();
        cachedPickedUpState.facilityData = new FacilityData();

        if (targetBuilding.TryGetComponent<ProductionFacilityRuntime>(out var facility))
        {
            cachedPickedUpState.facilityData.isActive = facility.isProducing;
            cachedPickedUpState.facilityData.currentProgressTime = facility.currentProgressTime;
            cachedPickedUpState.facilityData.currentStorageCount = facility.currentStorageCount;
            cachedPickedUpState.facilityData.currentCraftingItemId = facility.craftingItem ?? "";

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
            cachedPickedUpState.facilityData.currentCraftingItemId = craft.currentCraftingItem ?? "";

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
        else if (targetBuilding.TryGetComponent<RanchFacilityRuntime>(out var ranch)) // 🌟 [추가]: 목장 들기 시 상태 저장
        {
            cachedPickedUpState.facilityData.isActive = ranch.isProducing;
            if (ranch.Slots != null)
            {
                foreach (var slot in ranch.Slots)
                {
                    if (slot.deployedMem != null) cachedPickedUpState.deployedMems.Add(slot.deployedMem);
                    if (slot.deployedMemEntry != null)
                    {
                        cachedPickedUpState.deployedMemEntries.Add(slot.deployedMemEntry);
                        cachedPickedUpState.facilityData.DeployedMemIDs.Add(slot.deployedMemEntry.KeyId);
                    }
                }
            }
        }

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

        if (retrievedData != null && !string.IsNullOrEmpty(retrievedData.requireBlueprint))
        {
            var inventory = FindFirstObjectByType<PlayerInventory>();
            if (inventory != null)
            {
                ItemData bpItem = FindItemDataInProject(retrievedData.requireBlueprint);
                if (bpItem != null)
                {
                    inventory.AddItem(bpItem, 1);
                    sessionAddedBlueprints.Add(bpItem);
                }
            }
        }

        currentAvailableBuildings = GetAvailableBuildingsFromInventory();
        OnPlacementModeChanged?.Invoke(isPlacementMode, currentAvailableBuildings);

        OnGridDataChanged?.Invoke();
        TotalHungerManager.Instance?.RecalculateTotalHunger();

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

                renderer.sortingOrder = 100;
                if (renderer.material != null)
                {
                    renderer.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
                }
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

            Vector3 spawnPos = new Vector3(offsetX, innerPlaneY, offsetZ);

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
                        facility.craftingItem = entry.currentCraftingItemId;
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
                        craft.currentCraftingItem = entry.currentCraftingItemId;

                        if (craft.DeployedMems != null && craft.DeployedMemEntries != null)
                        {
                            craft.DeployedMems.Clear();
                            craft.DeployedMemEntries.Clear();
                            craft.DeployedMems.AddRange(restoredMems);
                            craft.DeployedMemEntries.AddRange(matchedEntries);
                        }
                    }
                    else if (restoredBuilding.TryGetComponent<RanchFacilityRuntime>(out var ranch)) // 🌟 [추가]: 목장 롤백 복원
                    {
                        ranch.buildingData = snap.data;
                        ranch.UpdateSlotCapacity();

                        for (int i = 0; i < restoredMems.Count && i < matchedEntries.Count; i++)
                        {
                            ranch.TryAddMemToSlot(i, restoredMems[i], matchedEntries[i]);
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

    /// <summary>
    /// ItemCatalogManager에서만 ItemData SO를 탐색합니다.
    /// </summary>
    private ItemData FindItemDataInProject(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 아이템 '{itemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        ItemData targetItem = ItemCatalogManager.Instance.FindItemData(itemId);
        if (targetItem == null)
        {
            Debug.LogError($"[ItemCatalogManager] 카탈로그에서 아이템 ID '{itemId}'에 해당하는 ItemData를 찾을 수 없습니다.");
        }

        return targetItem;
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

            if (string.IsNullOrEmpty(bData.requireBlueprint) || (inventory != null && inventory.GetItemAmount(bData.requireBlueprint) > 0))
            {
                filteredList.Add(bData);
            }
        }

        return filteredList;
    }

    private int GetFirstLayerFromMask(LayerMask mask)
    {
        int maskVal = mask.value;
        if (maskVal == 0) return -1;
        for (int i = 0; i < 32; i++)
        {
            if ((maskVal & (1 << i)) != 0) return i;
        }
        return -1;
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private void OnValidate()
    {
        if (gridOverlayY < innerPlaneY) gridOverlayY = innerPlaneY + 0.001f;

        if (Application.isPlaying)
        {
            UpdateInnerSurfacePlane();
            UpdateGlobalGridOverlay();
        }
    }

    [ContextMenu("Function: Expand to Test")]
    public void TestExpand()
    {
        count++;
        ExpandGrid(count, count);
    }
}