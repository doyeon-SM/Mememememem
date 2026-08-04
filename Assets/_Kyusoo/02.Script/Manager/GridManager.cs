using DG.Tweening;
using HDY.Capture;
using HDY.Cook;
using HDY.Inventory;
using HDY.Item;
using HDY.Mem;
using HDY.Recipe;
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
    public enum BlueprintSource
    {
        Inventory,
        Warehouse
    }

    private class SessionBlueprintRecord
    {
        public ItemData blueprintItem;
        public BlueprintSource source;
    }

    [Header("타일 생성 관련 정보: Prefabs, 생성될 위치, Grid Layer")]
    [SerializeField] private GameObject outerTilePrefab;
    [SerializeField] private GameObject innerTilePrefab;
    [SerializeField] private Transform floorContainer;
    [SerializeField] private LayerMask gridLayerMask;

    [Header("내부 상단 Plane 설정")]
    [SerializeField] private GameObject innerSurfacePlane;
    [SerializeField] private float planeInsetMargin = 1.2f;
    [SerializeField] private float innerPlaneY = 0.501f;
    [SerializeField] private float gridOverlayY = 0.502f;

    [Header("시설 데이터 정보: SO, 프리뷰")]
    [SerializeField] private List<BuildingData> buildings = new List<BuildingData>();
    [SerializeField] private Material previewMaterial;
    [SerializeField] private Material gridMaterialPrefab;

    [Header("타일 색상 정보: 배치 가능, 배치 불가")]
    [SerializeField] private Color buildableColor = new Color(0f, 0.5f, 1f, 0.4f);
    [SerializeField] private Color unbuildableColor = new Color(1f, 0f, 0f, 0.4f);

    [Header("점유 타일 테두리 설정 (인스펙터 조절 가능)")]
    [SerializeField] private Material occupiedMaterialPrefab;
    [SerializeField] private Color occupiedBorderColor = new Color(0.95f, 0.65f, 0.2f, 0.85f);
    [SerializeField][Range(1, 16)] private int occupiedBorderWidth = 3;

    // 🌟 [추가] 가동 중인 시설 이동 시도 시 출력할 경고 팝업 UI (CanvasGroup 부착 필요)
    [Header("가동 중 시설 이동 차단 알림 UI")]
    [SerializeField] private CanvasGroup activeBuildingWarningCanvasGroup;
    private Sequence warningPopupSequence;

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

    private Dictionary<GameObject, BlueprintSource> buildingBlueprintSourceMap = new Dictionary<GameObject, BlueprintSource>();

    private GameObject[,] occupiedOverlayGrid;
    private Material occupiedOverlayMaterial;
    private Texture2D[] cachedBorderTextures = new Texture2D[16];

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

    private List<SessionBlueprintRecord> sessionRemovedBlueprints = new List<SessionBlueprintRecord>();
    private List<SessionBlueprintRecord> sessionAddedBlueprints = new List<SessionBlueprintRecord>();

    private Dictionary<string, FacilityData> rollbackFacilityDatabaseBackup = new Dictionary<string, FacilityData>();

    private class PickedUpBuildingRuntimeState
    {
        public FacilityData facilityData;
        public List<MemData> deployedMems = new List<MemData>();
        public List<CapturedMemEntry> deployedMemEntries = new List<CapturedMemEntry>();
        public BlueprintSource? originalBlueprintSource;
    }
    private PickedUpBuildingRuntimeState cachedPickedUpState = null;

    public int MouseGridX { get; private set; }
    public int MouseGridZ { get; private set; }
    public bool IsMouseOnGrid { get; private set; }

    public static event Action<bool, List<BuildingData>> OnPlacementModeChanged;
    public static event Action OnGridDataChanged;

    private int count = 10;

    private void Awake()
    {
        if (buildRecordManager == null) buildRecordManager = FindFirstObjectByType<BuildRecordManager>();
        InitGridMaterials();

        // 🌟 경고 팝업 초기화
        if (activeBuildingWarningCanvasGroup != null)
        {
            activeBuildingWarningCanvasGroup.alpha = 0f;
            activeBuildingWarningCanvasGroup.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        int targetWidth = currentWidth > 0 ? currentWidth : 10;
        int targetHeight = currentHeight > 0 ? currentHeight : 10;
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
                    else if (targetObj.TryGetComponent<RanchFacilityRuntime>(out RanchFacilityRuntime ranch))
                    {
                        PanelManager.Instance.OpenRanchPanel(ranch);
                    }
                    else if (targetObj.TryGetComponent<GeneratorRuntime>(out GeneratorRuntime gen))
                    {
                        PanelManager.Instance.OpenGeneratorPanel(gen);
                    }
                    else if (targetObj.TryGetComponent<TransportRuntime>(out TransportRuntime transport))
                    {
                        PanelManager.Instance.OpenTransportPanel(transport);
                    }
                    else if (targetObj.TryGetComponent<CampFireRuntime>(out CampFireRuntime campFire))
                    {
                        PanelManager.Instance.OpenCampFirePanel(campFire);
                    }
                    else if (targetObj.TryGetComponent<KitchenRuntime>(out KitchenRuntime kitchen))
                    {
                        PanelManager.Instance.OpenKitchenPanel(kitchen);
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

        if (tileGrid == null || tileGrid.Length == 0) tileGrid = new GameObject[currentWidth, currentHeight];
        if (occupiedCells == null || occupiedCells.Length == 0) occupiedCells = new bool[currentWidth, currentHeight];
        if (buildingObjectsGrid == null || buildingObjectsGrid.Length == 0) buildingObjectsGrid = new GameObject[currentWidth, currentHeight];
        if (buildingDataGrid == null || buildingDataGrid.Length == 0) buildingDataGrid = new BuildingData[currentWidth, currentHeight];

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
                    if (tileGrid[i, j] != null) Destroy(tileGrid[i, j]);
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

    private bool IsOuterTile(int x, int z, int width, int height) => x == 0 || x == width - 1 || z == 0 || z == height - 1;

    private GameObject SpawnTile(int x, int z, int width, int height)
    {
        Vector3 spawnPosition = new Vector3(x + 0.5f, 0f, z + 0.5f);
        bool isOuter = IsOuterTile(x, z, width, height);
        GameObject targetPrefab = isOuter ? outerTilePrefab : innerTilePrefab;
        if (targetPrefab == null) targetPrefab = outerTilePrefab != null ? outerTilePrefab : innerTilePrefab;

        GameObject newTile = Instantiate(targetPrefab, spawnPosition, Quaternion.identity, floorContainer);
        newTile.name = $"Tile_({x},{z})";

        int maskLayer = GetFirstLayerFromMask(gridLayerMask);
        if (maskLayer >= 0) SetLayerRecursively(newTile, maskLayer);

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
                if (Application.isPlaying) Destroy(col);
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

            if (RecordManager.Instance != null)
            {
                rollbackFacilityDatabaseBackup = RecordManager.Instance.GetFacilityDatabaseClone();
            }
        }
        else
        {
            ClearPreview();
        }

        currentAvailableBuildings = GetAvailableBuildingsFromInventory();
        OnPlacementModeChanged?.Invoke(isPlacementMode, currentAvailableBuildings);

        if (globalGridOverlay != null) globalGridOverlay.SetActive(isPlacementMode);

        UpdateTileOccupiedVisuals();

        Debug.Log($"배치 모드 상태 변경: {isPlacementMode} | 배치 가능 건물 수: {currentAvailableBuildings.Count}개");
    }

    private Material GetOccupiedOverlayMaterial()
    {
        if (occupiedOverlayMaterial == null)
        {
            if (occupiedMaterialPrefab != null)
            {
                occupiedOverlayMaterial = new Material(occupiedMaterialPrefab);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Transparent")
                             ?? Shader.Find("Sprites/Default");

                occupiedOverlayMaterial = new Material(shader);

                if (shader.name.Contains("Universal Render Pipeline"))
                {
                    occupiedOverlayMaterial.SetFloat("_Surface", 1f);
                    occupiedOverlayMaterial.SetFloat("_Blend", 0f);
                    occupiedOverlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    occupiedOverlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    occupiedOverlayMaterial.SetInt("_ZWrite", 0);
                    occupiedOverlayMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    occupiedOverlayMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 150;
                }
            }
        }
        return occupiedOverlayMaterial;
    }

    private Texture2D GetBorderTexture(bool left, bool right, bool bottom, bool top)
    {
        int mask = (left ? 1 : 0) | (right ? 2 : 0) | (bottom ? 4 : 0) | (top ? 8 : 0);

        if (cachedBorderTextures == null || cachedBorderTextures.Length != 16)
        {
            cachedBorderTextures = new Texture2D[16];
        }

        if (cachedBorderTextures[mask] == null)
        {
            Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color whiteColor = Color.white;
            Color transparentColor = new Color(0f, 0f, 0f, 0f);

            int bw = Mathf.Clamp(occupiedBorderWidth, 1, 16);

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    bool isLeft = left && (x < bw);
                    bool isRight = right && (x >= 64 - bw);
                    bool isBottom = bottom && (y < bw);
                    bool isTop = top && (y >= 64 - bw);

                    bool isBorder = isLeft || isRight || isBottom || isTop;
                    tex.SetPixel(x, y, isBorder ? whiteColor : transparentColor);
                }
            }
            tex.Apply();
            cachedBorderTextures[mask] = tex;
        }

        return cachedBorderTextures[mask];
    }

    private void UpdateTileOccupiedVisuals()
    {
        if (currentWidth == 0 || currentHeight == 0) return;

        if (occupiedOverlayGrid == null || occupiedOverlayGrid.GetLength(0) != currentWidth || occupiedOverlayGrid.GetLength(1) != currentHeight)
        {
            if (occupiedOverlayGrid != null)
            {
                foreach (var obj in occupiedOverlayGrid)
                {
                    if (obj != null) Destroy(obj);
                }
            }
            occupiedOverlayGrid = new GameObject[currentWidth, currentHeight];
        }

        Material baseMat = GetOccupiedOverlayMaterial();

        for (int x = 0; x < currentWidth; x++)
        {
            for (int z = 0; z < currentHeight; z++)
            {
                bool shouldShow = isPlacementMode && occupiedCells[x, z];

                if (shouldShow)
                {
                    GameObject currentBuilding = buildingObjectsGrid[x, z];

                    bool borderLeft = (x == 0 || buildingObjectsGrid[x - 1, z] != currentBuilding);
                    bool borderRight = (x == currentWidth - 1 || buildingObjectsGrid[x + 1, z] != currentBuilding);
                    bool borderBottom = (z == 0 || buildingObjectsGrid[x, z - 1] != currentBuilding);
                    bool borderTop = (z == currentHeight - 1 || buildingObjectsGrid[x, z + 1] != currentBuilding);

                    Texture2D borderTex = GetBorderTexture(borderLeft, borderRight, borderBottom, borderTop);

                    if (occupiedOverlayGrid[x, z] == null)
                    {
                        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        quad.name = $"OccupiedOverlay_({x},{z})";
                        if (quad.TryGetComponent<Collider>(out var col)) Destroy(col);

                        quad.transform.SetParent(floorContainer != null ? floorContainer : transform);
                        quad.transform.position = new Vector3(x + 0.5f, gridOverlayY + 0.003f, z + 0.5f);
                        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                        MeshRenderer mr = quad.GetComponent<MeshRenderer>();
                        mr.material = new Material(baseMat);

                        occupiedOverlayGrid[x, z] = quad;
                    }

                    var renderer = occupiedOverlayGrid[x, z].GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.material != null)
                    {
                        renderer.material.SetTexture("_BaseMap", borderTex);
                        renderer.material.SetTexture("_MainTex", borderTex);
                        renderer.material.SetColor("_BaseColor", occupiedBorderColor);
                        renderer.material.SetColor("_Color", occupiedBorderColor);
                    }
                    occupiedOverlayGrid[x, z].SetActive(true);
                }
                else
                {
                    if (occupiedOverlayGrid[x, z] != null)
                    {
                        occupiedOverlayGrid[x, z].SetActive(false);
                    }
                }
            }
        }
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

            if (cachedPickedUpState != null && cachedPickedUpState.deployedMemEntries != null)
            {
                foreach (var entry in cachedPickedUpState.deployedMemEntries)
                {
                    if (entry != null) entry.IsActive = false;
                }
            }
            cachedPickedUpState = null;
        }
    }

    private void UpdateMouseGridPosition()
    {
        if (Mouse.current == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);

        Plane gridPlane = new Plane(Vector3.up, new Vector3(0f, innerPlaneY, 0f));
        if (gridPlane.Raycast(ray, out float enter))
        {
            raycastHitPoint = ray.GetPoint(enter);
            MouseGridX = Mathf.FloorToInt(raycastHitPoint.x);
            MouseGridZ = Mathf.FloorToInt(raycastHitPoint.z);

            IsMouseOnGrid = (MouseGridX >= 0 && MouseGridX < currentWidth &&
                             MouseGridZ >= 0 && MouseGridZ < currentHeight);
        }
        else
        {
            IsMouseOnGrid = false;
        }
    }

    private void UpdatePreviewPosition()
    {
        if (selectedBuildingData == null || currentPreviewInstance == null || isShaking) return;

        int currentRotationIndex = Mathf.RoundToInt(currentPreviewInstance.transform.eulerAngles.y / 90f) % 4;
        bool isRotated = (currentRotationIndex == 1 || currentRotationIndex == 3);

        currentTargetWidth = isRotated ? selectedBuildingData.height : selectedBuildingData.width;
        currentTargetHeight = isRotated ? selectedBuildingData.width : selectedBuildingData.height;

        float offsetX = (currentTargetWidth % 2 == 0) ? 0.5f : 0f;
        float offsetZ = (currentTargetHeight % 2 == 0) ? 0.5f : 0f;

        currentStartGridX = Mathf.FloorToInt(raycastHitPoint.x + offsetX - (currentTargetWidth / 2.0f));
        currentStartGridZ = Mathf.FloorToInt(raycastHitPoint.z + offsetZ - (currentTargetHeight / 2.0f));

        float previewX = currentStartGridX + (currentTargetWidth / 2.0f);
        float previewZ = currentStartGridZ + (currentTargetHeight / 2.0f);
        float previewY = gridOverlayY + 0.008f;

        currentPreviewInstance.transform.position = new Vector3(previewX, previewY, previewZ);

        canPlaceCurrent = CheckPlacement(currentStartGridX, currentStartGridZ, currentTargetWidth, currentTargetHeight);

        if (canPlaceCurrent && !string.IsNullOrEmpty(selectedBuildingData.requireBlueprint))
        {
            var inventory = FindFirstObjectByType<PlayerInventory>();
            var warehouse = FindFirstObjectByType<WarehouseInventory>();

            int totalBlueprintCount = 0;
            if (inventory != null) totalBlueprintCount += inventory.GetItemAmount(selectedBuildingData.requireBlueprint);
            if (warehouse != null) totalBlueprintCount += warehouse.GetItemAmount(selectedBuildingData.requireBlueprint);

            if (totalBlueprintCount <= 0)
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
                if (x < 0 || x >= currentWidth || z < 0 || z >= currentHeight) return false;
                if (occupiedCells[x, z]) return false;
            }
        }
        return true;
    }

    private Material CreateGridMaterial(bool isPlacementMode)
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        Color gridBorderColor = new Color(0.1f, 0.1f, 0.1f, 0.45f);
        Color transparentColor = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                texture.SetPixel(x, y, (x < 2 || x > 61 || y < 2 || y > 61) ? gridBorderColor : transparentColor);
            }
        }
        texture.Apply();

        Material mat = gridMaterialPrefab != null ? new Material(gridMaterialPrefab) : new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent"));
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
                    .OnComplete(() => isShaking = false);
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
                prodRuntime.currentLevel = cachedPickedUpState.facilityData.currentLevel > 0 ? cachedPickedUpState.facilityData.currentLevel : 1;
                prodRuntime.craftingItem = cachedPickedUpState.facilityData.currentCraftingItemId;

                if (prodRuntime.DeployedMems != null) prodRuntime.DeployedMems.Clear();
                if (prodRuntime.DeployedMemEntries != null) prodRuntime.DeployedMemEntries.Clear();

                for (int i = 0; i < cachedPickedUpState.deployedMems.Count && i < cachedPickedUpState.deployedMemEntries.Count; i++)
                {
                    var mData = cachedPickedUpState.deployedMems[i];
                    var mEntry = cachedPickedUpState.deployedMemEntries[i];
                    if (mData != null && mEntry != null)
                    {
                        mEntry.IsActive = false;
                        prodRuntime.TryAddMem(mData, mEntry);
                    }
                }

                prodRuntime.currentStorageCount = cachedPickedUpState.facilityData.currentStorageCount;
                float baseDuration = prodRuntime.baseProductionTime;
                prodRuntime.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, prodRuntime.DeployedMems);
                prodRuntime.currentProgressTime = cachedPickedUpState.facilityData.currentProgressTime;
                prodRuntime.isProducing = cachedPickedUpState.facilityData.isActive;
                prodRuntime.CheckProductionCondition();
            }
            else if (realBuilding.TryGetComponent<ProductionCraftRuntime>(out ProductionCraftRuntime craftRuntime))
            {
                craftRuntime.buildingData = selectedBuildingData;
                craftRuntime.currentCraftingItem = cachedPickedUpState.facilityData.currentCraftingItemId;
                craftRuntime.targetQuantity = cachedPickedUpState.facilityData.targetQuantity;
                craftRuntime.remainingQuantity = cachedPickedUpState.facilityData.remainingQuantity;

                if (craftRuntime.DeployedMems != null) craftRuntime.DeployedMems.Clear();
                if (craftRuntime.DeployedMemEntries != null) craftRuntime.DeployedMemEntries.Clear();

                for (int i = 0; i < cachedPickedUpState.deployedMems.Count && i < cachedPickedUpState.deployedMemEntries.Count; i++)
                {
                    var mData = cachedPickedUpState.deployedMems[i];
                    var mEntry = cachedPickedUpState.deployedMemEntries[i];
                    if (mData != null && mEntry != null)
                    {
                        mEntry.IsActive = false;
                        craftRuntime.TryAddMem(mData, mEntry);
                    }
                }

                craftRuntime.currentStorageCount = cachedPickedUpState.facilityData.currentStorageCount;
                if (!string.IsNullOrEmpty(craftRuntime.currentCraftingItem))
                {
                    RecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindRecipeData(craftRuntime.currentCraftingItem) : null;
                    float baseDuration = recipe != null ? recipe.time : 20f;
                    craftRuntime.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, craftRuntime.DeployedMems);
                }
                craftRuntime.currentProgressTime = cachedPickedUpState.facilityData.currentProgressTime;
                craftRuntime.isProducing = cachedPickedUpState.facilityData.isActive;
            }
            else if (realBuilding.TryGetComponent<RanchFacilityRuntime>(out RanchFacilityRuntime ranchRuntime))
            {
                ranchRuntime.buildingData = selectedBuildingData;
                ranchRuntime.currentLevel = cachedPickedUpState.facilityData.currentLevel > 0 ? cachedPickedUpState.facilityData.currentLevel : 1;
                ranchRuntime.UpdateSlotCapacity();

                if (cachedPickedUpState.facilityData.ranchSlots != null && cachedPickedUpState.facilityData.ranchSlots.Count > 0)
                {
                    foreach (var slotSave in cachedPickedUpState.facilityData.ranchSlots)
                    {
                        if (slotSave.slotIndex >= 0 && slotSave.slotIndex < ranchRuntime.Slots.Count)
                        {
                            var slotRuntime = ranchRuntime.Slots[slotSave.slotIndex];
                            slotRuntime.isUnlocked = slotSave.isUnlocked;

                            if (!string.IsNullOrEmpty(slotSave.deployedMemKeyId))
                            {
                                int entryIndex = cachedPickedUpState.deployedMemEntries.FindIndex(e => e != null && e.KeyId == slotSave.deployedMemKeyId);
                                if (entryIndex >= 0)
                                {
                                    var mData = cachedPickedUpState.deployedMems[entryIndex];
                                    var mEntry = cachedPickedUpState.deployedMemEntries[entryIndex];
                                    mEntry.IsActive = false;
                                    ranchRuntime.TryAddMemToSlot(slotSave.slotIndex, mData, mEntry);
                                }
                            }

                            string produceItemId = !string.IsNullOrEmpty(slotSave.craftingItemId)
                                ? slotSave.craftingItemId
                                : (slotRuntime.deployedMem != null ? ranchRuntime.GetRanchProduceItemId(slotRuntime.deployedMem) : string.Empty);

                            slotRuntime.craftingItemId = produceItemId;
                            slotRuntime.currentProgressTime = slotSave.currentProgressTime;
                            slotRuntime.currentStorageCount = slotSave.currentStorageCount;
                            slotRuntime.isProducing = slotSave.isProducing;
                        }
                    }
                }
                ranchRuntime.CheckAllSlotsProductionCondition();
            }
            else if (realBuilding.TryGetComponent<GeneratorRuntime>(out GeneratorRuntime genRuntime))
            {
                genRuntime.buildingData = selectedBuildingData;
                genRuntime.currentLevel = cachedPickedUpState.facilityData.currentLevel > 0 ? cachedPickedUpState.facilityData.currentLevel : 1;
                genRuntime.UpdateMaxPowerStorage();

                if (genRuntime.DeployedMems != null) genRuntime.DeployedMems.Clear();
                if (genRuntime.DeployedMemEntries != null) genRuntime.DeployedMemEntries.Clear();

                for (int i = 0; i < cachedPickedUpState.deployedMems.Count && i < cachedPickedUpState.deployedMemEntries.Count; i++)
                {
                    var mData = cachedPickedUpState.deployedMems[i];
                    var mEntry = cachedPickedUpState.deployedMemEntries[i];
                    if (mData != null && mEntry != null)
                    {
                        mEntry.IsActive = false;
                        genRuntime.TryAddMem(mData, mEntry);
                    }
                }

                genRuntime.currentPowerStorage = cachedPickedUpState.facilityData.currentStorageCount;
                if (genRuntime.DeployedMems.Count > 0)
                {
                    genRuntime.totalPowerRequiredTime = ProductionCalculator.CalculatePowerGenerationTime(genRuntime.basePowerGenerationTime, genRuntime.DeployedMems[0]);
                }
                genRuntime.currentPowerProgressTime = cachedPickedUpState.facilityData.currentProgressTime;
                genRuntime.isPowerGenerating = cachedPickedUpState.facilityData.isActive;
                genRuntime.CheckPowerCondition();
            }
            else if (realBuilding.TryGetComponent<TransportRuntime>(out TransportRuntime transRuntime))
            {
                transRuntime.buildingData = selectedBuildingData;
                transRuntime.currentLevel = cachedPickedUpState.facilityData.currentLevel > 0 ? cachedPickedUpState.facilityData.currentLevel : 1;

                if (transRuntime.DeployedMems != null) transRuntime.DeployedMems.Clear();
                if (transRuntime.DeployedMemEntries != null) transRuntime.DeployedMemEntries.Clear();

                for (int i = 0; i < cachedPickedUpState.deployedMems.Count && i < cachedPickedUpState.deployedMemEntries.Count; i++)
                {
                    var mData = cachedPickedUpState.deployedMems[i];
                    var mEntry = cachedPickedUpState.deployedMemEntries[i];
                    if (mData != null && mEntry != null)
                    {
                        mEntry.IsActive = false;
                        transRuntime.TryAddMem(mData, mEntry);
                    }
                }

                transRuntime.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(transRuntime.baseIntervalTime, transRuntime.DeployedMems);
                transRuntime.currentProgressTime = cachedPickedUpState.facilityData.currentProgressTime;
                transRuntime.isWorking = cachedPickedUpState.facilityData.isActive;
                transRuntime.CheckProductionCondition();
            }
            else if (realBuilding.TryGetComponent<CampFireRuntime>(out CampFireRuntime campFireRuntime))
            {
                campFireRuntime.buildingData = selectedBuildingData;
                campFireRuntime.currentCookingItem = cachedPickedUpState.facilityData.currentCraftingItemId;
                campFireRuntime.targetQuantity = cachedPickedUpState.facilityData.targetQuantity;
                campFireRuntime.remainingQuantity = cachedPickedUpState.facilityData.remainingQuantity;

                if (campFireRuntime.DeployedMems != null) campFireRuntime.DeployedMems.Clear();
                if (campFireRuntime.DeployedMemEntries != null) campFireRuntime.DeployedMemEntries.Clear();

                for (int i = 0; i < cachedPickedUpState.deployedMems.Count && i < cachedPickedUpState.deployedMemEntries.Count; i++)
                {
                    var mData = cachedPickedUpState.deployedMems[i];
                    var mEntry = cachedPickedUpState.deployedMemEntries[i];
                    if (mData != null && mEntry != null)
                    {
                        mEntry.IsActive = false;
                        campFireRuntime.TryAddMem(mData, mEntry);
                    }
                }

                campFireRuntime.currentStorageCount = cachedPickedUpState.facilityData.currentStorageCount;
                if (!string.IsNullOrEmpty(campFireRuntime.currentCookingItem))
                {
                    CookRecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindCookRecipeData(campFireRuntime.currentCookingItem) : null;
                    float baseDuration = recipe != null ? recipe.Time : 15f;
                    campFireRuntime.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, campFireRuntime.DeployedMems);
                }
                campFireRuntime.currentProgressTime = cachedPickedUpState.facilityData.currentProgressTime;
                campFireRuntime.isCooking = cachedPickedUpState.facilityData.isActive;
            }
            else if (realBuilding.TryGetComponent<KitchenRuntime>(out KitchenRuntime kitchenRuntime))
            {
                kitchenRuntime.buildingData = selectedBuildingData;
                kitchenRuntime.currentCookingItem = cachedPickedUpState.facilityData.currentCraftingItemId;
                kitchenRuntime.targetQuantity = cachedPickedUpState.facilityData.targetQuantity;
                kitchenRuntime.remainingQuantity = cachedPickedUpState.facilityData.remainingQuantity;

                if (kitchenRuntime.DeployedMems != null) kitchenRuntime.DeployedMems.Clear();
                if (kitchenRuntime.DeployedMemEntries != null) kitchenRuntime.DeployedMemEntries.Clear();

                for (int i = 0; i < cachedPickedUpState.deployedMems.Count && i < cachedPickedUpState.deployedMemEntries.Count; i++)
                {
                    var mData = cachedPickedUpState.deployedMems[i];
                    var mEntry = cachedPickedUpState.deployedMemEntries[i];
                    if (mData != null && mEntry != null)
                    {
                        mEntry.IsActive = false;
                        kitchenRuntime.TryAddMem(mData, mEntry);
                    }
                }

                kitchenRuntime.currentStorageCount = cachedPickedUpState.facilityData.currentStorageCount;
                if (!string.IsNullOrEmpty(kitchenRuntime.currentCookingItem))
                {
                    CookRecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindCookRecipeData(kitchenRuntime.currentCookingItem) : null;
                    float baseDuration = recipe != null ? recipe.Time : 15f;
                    kitchenRuntime.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, kitchenRuntime.DeployedMems);
                }
                kitchenRuntime.currentProgressTime = cachedPickedUpState.facilityData.currentProgressTime;
                kitchenRuntime.isCooking = cachedPickedUpState.facilityData.isActive;
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
            }
            else if (realBuilding.TryGetComponent<ProductionCraftRuntime>(out ProductionCraftRuntime craftRuntime))
            {
                craftRuntime.buildingData = selectedBuildingData;
            }
            else if (realBuilding.TryGetComponent<RanchFacilityRuntime>(out RanchFacilityRuntime ranchRuntime))
            {
                ranchRuntime.buildingData = selectedBuildingData;
                ranchRuntime.UpdateSlotCapacity();
            }
            else if (realBuilding.TryGetComponent<GeneratorRuntime>(out GeneratorRuntime genRuntime))
            {
                genRuntime.buildingData = selectedBuildingData;
                genRuntime.UpdateMaxPowerStorage();
            }
            else if (realBuilding.TryGetComponent<CampFireRuntime>(out CampFireRuntime campFireRuntime))
            {
                campFireRuntime.buildingData = selectedBuildingData;
            }
            else if (realBuilding.TryGetComponent<KitchenRuntime>(out KitchenRuntime kitchenRuntime))
            {
                kitchenRuntime.buildingData = selectedBuildingData;
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
            var warehouse = FindFirstObjectByType<WarehouseInventory>();
            string bpId = selectedBuildingData.requireBlueprint;

            int invCount = inventory != null ? inventory.GetItemAmount(bpId) : 0;
            ItemData bpItem = FindItemDataInProject(bpId);

            BlueprintSource usedSource = BlueprintSource.Inventory;

            if (invCount >= 1 && inventory != null)
            {
                inventory.RemoveItem(bpId, 1);
                usedSource = BlueprintSource.Inventory;
            }
            else if (warehouse != null && warehouse.GetItemAmount(bpId) >= 1)
            {
                warehouse.RemoveItem(bpId, 1);
                usedSource = BlueprintSource.Warehouse;
            }

            buildingBlueprintSourceMap[realBuilding] = usedSource;

            if (bpItem != null)
            {
                sessionRemovedBlueprints.Add(new SessionBlueprintRecord
                {
                    blueprintItem = bpItem,
                    source = usedSource
                });
            }
        }

        ClearPreview();
        currentAvailableBuildings = GetAvailableBuildingsFromInventory();
        OnPlacementModeChanged?.Invoke(isPlacementMode, currentAvailableBuildings);

        UpdateTileOccupiedVisuals();

        OnGridDataChanged?.Invoke();
        TotalHungerManager.Instance?.RecalculateTotalHunger();
    }

    // 🌟 [추가] 시설의 가동(작업) 중 여부를 판별하는 헬퍼 함수
    private bool IsFacilityActive(GameObject building)
    {
        if (building == null) return false;

        if (building.TryGetComponent<ProductionFacilityRuntime>(out var prod)) return prod.isProducing;
        if (building.TryGetComponent<ProductionCraftRuntime>(out var craft)) return craft.isProducing;
        if (building.TryGetComponent<RanchFacilityRuntime>(out var ranch)) return ranch.isProducing;
        if (building.TryGetComponent<GeneratorRuntime>(out var gen)) return gen.isPowerGenerating;
        if (building.TryGetComponent<TransportRuntime>(out var trans)) return trans.isWorking;
        if (building.TryGetComponent<CampFireRuntime>(out var campFire)) return campFire.isCooking;
        if (building.TryGetComponent<KitchenRuntime>(out var kitchen)) return kitchen.isCooking;

        return false;
    }

    // 🌟 [추가] DOTween을 이용해 가동 중 경과 팝업을 2초간 출력하는 연출 함수
    private void ShowActiveFacilityWarningPopup()
    {
        if (activeBuildingWarningCanvasGroup == null) return;

        if (warningPopupSequence != null && warningPopupSequence.IsActive())
        {
            warningPopupSequence.Kill();
        }

        activeBuildingWarningCanvasGroup.gameObject.SetActive(true);
        activeBuildingWarningCanvasGroup.alpha = 0f;

        warningPopupSequence = DOTween.Sequence();
        warningPopupSequence.Append(activeBuildingWarningCanvasGroup.DOFade(1f, 0.25f))
                            .AppendInterval(2.0f)
                            .Append(activeBuildingWarningCanvasGroup.DOFade(0f, 0.35f))
                            .OnComplete(() =>
                            {
                                activeBuildingWarningCanvasGroup.gameObject.SetActive(false);
                            });
    }

    private void TryPickUpBuilding(int x, int z)
    {
        if (x < 0 || x >= currentWidth || z < 0 || z >= currentHeight) return;
        if (!occupiedCells[x, z] || buildingObjectsGrid[x, z] == null) return;

        GameObject targetBuilding = buildingObjectsGrid[x, z];

        // 🌟 [수정] 클릭한 시설이 가동 중이면 들어올리기를 차단하고 경고 팝업만 출력
        if (IsFacilityActive(targetBuilding))
        {
            ShowActiveFacilityWarningPopup();
            return;
        }

        BuildingData retrievedData = buildingDataGrid[x, z];
        Quaternion targetRotation = targetBuilding.transform.rotation;

        cachedPickedUpState = new PickedUpBuildingRuntimeState();
        cachedPickedUpState.facilityData = new FacilityData();

        if (buildingBlueprintSourceMap.TryGetValue(targetBuilding, out BlueprintSource source))
        {
            cachedPickedUpState.originalBlueprintSource = source;
        }

        if (targetBuilding.TryGetComponent<ProductionFacilityRuntime>(out var facility))
        {
            cachedPickedUpState.facilityData.currentLevel = facility.currentLevel;
            cachedPickedUpState.facilityData.isActive = facility.isProducing;
            cachedPickedUpState.facilityData.currentProgressTime = facility.currentProgressTime;
            cachedPickedUpState.facilityData.currentStorageCount = facility.currentStorageCount;
            cachedPickedUpState.facilityData.currentCraftingItemId = facility.craftingItem ?? "";

            if (facility.DeployedMems != null) cachedPickedUpState.deployedMems.AddRange(facility.DeployedMems);
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

            if (craft.DeployedMems != null) cachedPickedUpState.deployedMems.AddRange(craft.DeployedMems);
            if (craft.DeployedMemEntries != null)
            {
                cachedPickedUpState.deployedMemEntries.AddRange(craft.DeployedMemEntries);
                foreach (var entry in craft.DeployedMemEntries)
                {
                    if (entry != null) cachedPickedUpState.facilityData.DeployedMemIDs.Add(entry.KeyId);
                }
            }
        }
        else if (targetBuilding.TryGetComponent<RanchFacilityRuntime>(out var ranch))
        {
            cachedPickedUpState.facilityData.currentLevel = ranch.currentLevel;
            cachedPickedUpState.facilityData.isActive = ranch.isProducing;
            cachedPickedUpState.facilityData.ranchSlots = new List<RanchSlotSaveData>();

            if (ranch.Slots != null)
            {
                foreach (var slot in ranch.Slots)
                {
                    string keyId = slot.deployedMemEntry != null ? slot.deployedMemEntry.KeyId : "";
                    var slotSave = new RanchSlotSaveData
                    {
                        slotIndex = slot.slotIndex,
                        isUnlocked = slot.isUnlocked,
                        deployedMemKeyId = keyId,
                        craftingItemId = slot.craftingItemId ?? "",
                        isProducing = slot.isProducing,
                        currentProgressTime = slot.currentProgressTime,
                        currentStorageCount = slot.currentStorageCount
                    };
                    cachedPickedUpState.facilityData.ranchSlots.Add(slotSave);

                    if (slot.deployedMem != null) cachedPickedUpState.deployedMems.Add(slot.deployedMem);
                    if (slot.deployedMemEntry != null)
                    {
                        cachedPickedUpState.deployedMemEntries.Add(slot.deployedMemEntry);
                        cachedPickedUpState.facilityData.DeployedMemIDs.Add(slot.deployedMemEntry.KeyId);
                    }
                }
            }
        }
        else if (targetBuilding.TryGetComponent<GeneratorRuntime>(out var gen))
        {
            cachedPickedUpState.facilityData.currentLevel = gen.currentLevel;
            cachedPickedUpState.facilityData.isActive = gen.isPowerGenerating;
            cachedPickedUpState.facilityData.currentProgressTime = gen.currentPowerProgressTime;
            cachedPickedUpState.facilityData.currentStorageCount = gen.currentPowerStorage;

            if (gen.DeployedMems != null) cachedPickedUpState.deployedMems.AddRange(gen.DeployedMems);
            if (gen.DeployedMemEntries != null)
            {
                cachedPickedUpState.deployedMemEntries.AddRange(gen.DeployedMemEntries);
                foreach (var entry in gen.DeployedMemEntries)
                {
                    if (entry != null) cachedPickedUpState.facilityData.DeployedMemIDs.Add(entry.KeyId);
                }
            }
        }
        else if (targetBuilding.TryGetComponent<TransportRuntime>(out var trans))
        {
            cachedPickedUpState.facilityData.currentLevel = trans.currentLevel;
            cachedPickedUpState.facilityData.isActive = trans.isWorking;
            cachedPickedUpState.facilityData.currentProgressTime = trans.currentProgressTime;

            if (trans.DeployedMems != null) cachedPickedUpState.deployedMems.AddRange(trans.DeployedMems);
            if (trans.DeployedMemEntries != null)
            {
                cachedPickedUpState.deployedMemEntries.AddRange(trans.DeployedMemEntries);
                foreach (var entry in trans.DeployedMemEntries)
                {
                    if (entry != null) cachedPickedUpState.facilityData.DeployedMemIDs.Add(entry.KeyId);
                }
            }
        }
        else if (targetBuilding.TryGetComponent<CampFireRuntime>(out var campFire))
        {
            cachedPickedUpState.facilityData.isActive = campFire.isCooking;
            cachedPickedUpState.facilityData.targetQuantity = campFire.targetQuantity;
            cachedPickedUpState.facilityData.remainingQuantity = campFire.remainingQuantity;
            cachedPickedUpState.facilityData.currentProgressTime = campFire.currentProgressTime;
            cachedPickedUpState.facilityData.currentStorageCount = campFire.currentStorageCount;
            cachedPickedUpState.facilityData.currentCraftingItemId = campFire.currentCookingItem ?? "";

            if (campFire.DeployedMems != null) cachedPickedUpState.deployedMems.AddRange(campFire.DeployedMems);
            if (campFire.DeployedMemEntries != null)
            {
                cachedPickedUpState.deployedMemEntries.AddRange(campFire.DeployedMemEntries);
                foreach (var entry in campFire.DeployedMemEntries)
                {
                    if (entry != null) cachedPickedUpState.facilityData.DeployedMemIDs.Add(entry.KeyId);
                }
            }
        }
        else if (targetBuilding.TryGetComponent<KitchenRuntime>(out var kitchen))
        {
            cachedPickedUpState.facilityData.isActive = kitchen.isCooking;
            cachedPickedUpState.facilityData.targetQuantity = kitchen.targetQuantity;
            cachedPickedUpState.facilityData.remainingQuantity = kitchen.remainingQuantity;
            cachedPickedUpState.facilityData.currentProgressTime = kitchen.currentProgressTime;
            cachedPickedUpState.facilityData.currentStorageCount = kitchen.currentStorageCount;
            cachedPickedUpState.facilityData.currentCraftingItemId = kitchen.currentCookingItem ?? "";

            if (kitchen.DeployedMems != null) cachedPickedUpState.deployedMems.AddRange(kitchen.DeployedMems);
            if (kitchen.DeployedMemEntries != null)
            {
                cachedPickedUpState.deployedMemEntries.AddRange(kitchen.DeployedMemEntries);
                foreach (var entry in kitchen.DeployedMemEntries)
                {
                    if (entry != null) cachedPickedUpState.facilityData.DeployedMemIDs.Add(entry.KeyId);
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

        buildingBlueprintSourceMap.Remove(targetBuilding);

        targetBuilding.SetActive(false);
        Destroy(targetBuilding);

        if (retrievedData != null && !string.IsNullOrEmpty(retrievedData.requireBlueprint))
        {
            var inventory = FindFirstObjectByType<PlayerInventory>();
            var warehouse = FindFirstObjectByType<WarehouseInventory>();

            ItemData bpItem = FindItemDataInProject(retrievedData.requireBlueprint);
            if (bpItem != null)
            {
                BlueprintSource targetSource = cachedPickedUpState.originalBlueprintSource ?? BlueprintSource.Inventory;
                int remaining = 1;

                if (targetSource == BlueprintSource.Inventory)
                {
                    if (inventory != null) remaining = inventory.AddItem(bpItem, 1);
                    if (remaining > 0 && warehouse != null) remaining = warehouse.AddItem(bpItem, 1);
                }
                else
                {
                    if (warehouse != null) remaining = warehouse.AddItem(bpItem, 1);
                    if (remaining > 0 && inventory != null) remaining = inventory.AddItem(bpItem, 1);
                }

                if (remaining > 0)
                {
                    Debug.LogWarning($"[GridManager] 인벤토리와 창고가 가득 차서 설계도 '{bpItem.ItemName}'을(를) 반환할 수 없습니다.");
                }

                sessionAddedBlueprints.Add(new SessionBlueprintRecord
                {
                    blueprintItem = bpItem,
                    source = targetSource
                });
            }
        }

        currentAvailableBuildings = GetAvailableBuildingsFromInventory();
        OnPlacementModeChanged?.Invoke(isPlacementMode, currentAvailableBuildings);

        UpdateTileOccupiedVisuals();

        OnGridDataChanged?.Invoke();
        TotalHungerManager.Instance?.RecalculateTotalHunger();

        int availableIndex = currentAvailableBuildings.IndexOf(retrievedData);
        if (availableIndex >= 0)
        {
            CreateBuildingPreview(availableIndex);
            if (currentPreviewInstance != null) currentPreviewInstance.transform.rotation = targetRotation;
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
            foreach (Collider col in colliders) col.enabled = false;

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
            if (renderer != null) renderer.material.SetColor("_BaseColor", targetColor);
        }
    }

    private void RotatePreview()
    {
        if (currentPreviewInstance == null) return;

        float baseY = 0f;
        if (selectedBuildingData != null && selectedBuildingData.buildingPrefab != null)
        {
            baseY = Mathf.Repeat(selectedBuildingData.buildingPrefab.transform.eulerAngles.y, 360f);
        }

        float targetAngle1 = baseY;
        float targetAngle2 = Mathf.Repeat(baseY + 90f, 360f);

        float currentY = Mathf.Repeat(currentPreviewInstance.transform.eulerAngles.y, 360f);

        float rotateY;
        if (Mathf.Abs(Mathf.DeltaAngle(currentY, targetAngle1)) < 1f)
        {
            rotateY = targetAngle2;
        }
        else
        {
            rotateY = targetAngle1;
        }

        currentPreviewInstance.transform.rotation = Quaternion.Euler(0f, rotateY, 0f);
        UpdatePreviewPosition();
    }

    public void SavePlacement()
    {
        if (!isPlacementMode) return;
        if (buildRecordManager == null) return;

        buildRecordManager.ClearRecordOnSave();
        rollbackFacilityDatabaseBackup.Clear();

        ChangePlacementMode();

        sessionRemovedBlueprints.Clear();
        sessionAddedBlueprints.Clear();

        TriggerSatisfactionUpdate();

        OnGridDataChanged?.Invoke();
    }

    public void CancelPlacement()
    {
        if (!isPlacementMode) return;
        if (buildRecordManager == null) return;

        if (RecordManager.Instance != null && rollbackFacilityDatabaseBackup != null && rollbackFacilityDatabaseBackup.Count > 0)
        {
            RecordManager.Instance.RestoreFacilityDatabase(rollbackFacilityDatabaseBackup);
            rollbackFacilityDatabaseBackup.Clear();
        }

        ClearAllPlacedBuildings();

        List<BuildingSnapshot> rollbackData = buildRecordManager.Rollback();
        RestoreRollbackData(rollbackData);

        var inventory = FindFirstObjectByType<PlayerInventory>();
        var warehouse = FindFirstObjectByType<WarehouseInventory>();

        foreach (var record in sessionRemovedBlueprints)
        {
            if (record == null || record.blueprintItem == null) continue;

            int remaining = 1;

            if (record.source == BlueprintSource.Inventory)
            {
                if (inventory != null) remaining = inventory.AddItem(record.blueprintItem, 1);
                if (remaining > 0 && warehouse != null) remaining = warehouse.AddItem(record.blueprintItem, 1);
            }
            else if (record.source == BlueprintSource.Warehouse)
            {
                if (warehouse != null) remaining = warehouse.AddItem(record.blueprintItem, 1);
                if (remaining > 0 && inventory != null) remaining = inventory.AddItem(record.blueprintItem, 1);
            }

            if (remaining > 0)
            {
                Debug.LogWarning($"[GridManager] 인벤토리와 창고가 가득 차서 설계도 '{record.blueprintItem.ItemName}'을(를) 반환할 수 없습니다.");
            }
        }

        foreach (var record in sessionAddedBlueprints)
        {
            if (record == null || record.blueprintItem == null) continue;

            string bpId = record.blueprintItem.Item_ID;

            if (record.source == BlueprintSource.Inventory && inventory != null)
            {
                inventory.RemoveItem(bpId, 1);
            }
            else if (record.source == BlueprintSource.Warehouse && warehouse != null)
            {
                warehouse.RemoveItem(bpId, 1);
            }
            else
            {
                if (inventory != null && inventory.GetItemAmount(bpId) > 0) inventory.RemoveItem(bpId, 1);
                else if (warehouse != null && warehouse.GetItemAmount(bpId) > 0) warehouse.RemoveItem(bpId, 1);
            }
        }

        sessionRemovedBlueprints.Clear();
        sessionAddedBlueprints.Clear();

        ChangePlacementMode();

        TriggerSatisfactionUpdate();

        OnGridDataChanged?.Invoke();
        TotalHungerManager.Instance?.RecalculateTotalHunger();
    }

    private void ClearAllPlacedBuildings()
    {
        var allBuildings = FindObjectsByType<BuildingRuntime>(FindObjectsSortMode.None);
        foreach (var building in allBuildings)
        {
            if (building != null)
            {
                building.gameObject.SetActive(false);
                Destroy(building.gameObject);
            }
        }
        buildingBlueprintSourceMap.Clear();
        if (buildingObjectsGrid != null) Array.Clear(buildingObjectsGrid, 0, buildingObjectsGrid.Length);
        if (buildingDataGrid != null) Array.Clear(buildingDataGrid, 0, buildingDataGrid.Length);
        if (occupiedCells != null) Array.Clear(occupiedCells, 0, occupiedCells.Length);
    }

    private void RestoreRollbackData(List<BuildingSnapshot> rollbackData)
    {
        if (rollbackData == null) return;

        var memManager = FindFirstObjectByType<MemCaptureManager>();
        if (memManager != null && memManager.CapturedMems != null)
        {
            foreach (var m in memManager.CapturedMems)
            {
                if (m != null) m.IsActive = false;
            }
        }

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
                    if (restoredBuilding.TryGetComponent<ProductionFacilityRuntime>(out var facility))
                    {
                        facility.buildingData = snap.data;
                        facility.currentLevel = entry.currentLevel > 0 ? entry.currentLevel : 1;
                        facility.craftingItem = entry.currentCraftingItemId;

                        if (facility.DeployedMems != null) facility.DeployedMems.Clear();
                        if (facility.DeployedMemEntries != null) facility.DeployedMemEntries.Clear();

                        if (memManager != null && entry.DeployedMemIDs != null)
                        {
                            int maxCapacity = ProductionCalculator.GetMaxMemCount(facility.currentLevel);
                            var safeMemIDs = entry.DeployedMemIDs.Distinct().Take(maxCapacity).ToList();
                            foreach (var savedKeyId in safeMemIDs)
                            {
                                var match = memManager.CapturedMems.FirstOrDefault(m => m != null && m.KeyId == savedKeyId);
                                if (match != null)
                                {
                                    MemData realMemData = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(match.MemId) : null;
                                    if (realMemData != null)
                                    {
                                        match.IsActive = false;
                                        facility.TryAddMem(realMemData, match);
                                    }
                                }
                            }
                        }

                        facility.currentStorageCount = entry.currentStorageCount;
                        float baseDuration = facility.baseProductionTime;
                        facility.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, facility.DeployedMems);
                        facility.currentProgressTime = entry.currentProgressTime;
                        facility.isProducing = entry.isActive;
                        facility.CheckProductionCondition();
                    }
                    else if (restoredBuilding.TryGetComponent<ProductionCraftRuntime>(out var craft))
                    {
                        craft.buildingData = snap.data;
                        craft.currentCraftingItem = entry.currentCraftingItemId;
                        craft.targetQuantity = entry.targetQuantity;
                        craft.remainingQuantity = entry.remainingQuantity;

                        if (craft.DeployedMems != null) craft.DeployedMems.Clear();
                        if (craft.DeployedMemEntries != null) craft.DeployedMemEntries.Clear();

                        if (memManager != null && entry.DeployedMemIDs != null)
                        {
                            foreach (var savedKeyId in entry.DeployedMemIDs)
                            {
                                var match = memManager.CapturedMems.FirstOrDefault(m => m != null && m.KeyId == savedKeyId);
                                if (match != null)
                                {
                                    MemData realMemData = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(match.MemId) : null;
                                    if (realMemData != null)
                                    {
                                        match.IsActive = false;
                                        craft.TryAddMem(realMemData, match);
                                    }
                                }
                            }
                        }

                        craft.currentStorageCount = entry.currentStorageCount;
                        if (!string.IsNullOrEmpty(craft.currentCraftingItem))
                        {
                            RecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindRecipeData(craft.currentCraftingItem) : null;
                            float baseDuration = recipe != null ? recipe.time : 20f;
                            craft.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, craft.DeployedMems);
                        }
                        craft.currentProgressTime = entry.currentProgressTime;
                        craft.isProducing = entry.isActive;
                    }
                    else if (restoredBuilding.TryGetComponent<RanchFacilityRuntime>(out var ranch))
                    {
                        ranch.buildingData = snap.data;
                        ranch.currentLevel = entry.currentLevel > 0 ? entry.currentLevel : 1;
                        ranch.UpdateSlotCapacity();

                        if (entry.ranchSlots != null && entry.ranchSlots.Count > 0)
                        {
                            foreach (var slotSave in entry.ranchSlots)
                            {
                                if (slotSave.slotIndex >= 0 && slotSave.slotIndex < ranch.Slots.Count)
                                {
                                    var slotRuntime = ranch.Slots[slotSave.slotIndex];
                                    slotRuntime.isUnlocked = slotSave.isUnlocked;

                                    if (!string.IsNullOrEmpty(slotSave.deployedMemKeyId) && memManager != null)
                                    {
                                        var match = memManager.CapturedMems.FirstOrDefault(m => m != null && m.KeyId == slotSave.deployedMemKeyId);
                                        if (match != null)
                                        {
                                            MemData realMemData = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(match.MemId) : null;
                                            if (realMemData != null)
                                            {
                                                match.IsActive = false;
                                                ranch.TryAddMemToSlot(slotSave.slotIndex, realMemData, match);
                                            }
                                        }
                                    }

                                    string produceItemId = !string.IsNullOrEmpty(slotSave.craftingItemId)
                                        ? slotSave.craftingItemId
                                        : (slotRuntime.deployedMem != null ? ranch.GetRanchProduceItemId(slotRuntime.deployedMem) : string.Empty);

                                    slotRuntime.craftingItemId = produceItemId;
                                    slotRuntime.currentProgressTime = slotSave.currentProgressTime;
                                    slotRuntime.currentStorageCount = slotSave.currentStorageCount;
                                    slotRuntime.isProducing = slotSave.isProducing;
                                }
                            }
                        }
                        ranch.CheckAllSlotsProductionCondition();
                    }
                    else if (restoredBuilding.TryGetComponent<GeneratorRuntime>(out var gen))
                    {
                        gen.buildingData = snap.data;
                        gen.currentLevel = entry.currentLevel > 0 ? entry.currentLevel : 1;
                        gen.UpdateMaxPowerStorage();

                        if (gen.DeployedMems != null) gen.DeployedMems.Clear();
                        if (gen.DeployedMemEntries != null) gen.DeployedMemEntries.Clear();

                        if (memManager != null && entry.DeployedMemIDs != null)
                        {
                            foreach (var savedKeyId in entry.DeployedMemIDs)
                            {
                                var match = memManager.CapturedMems.FirstOrDefault(m => m != null && m.KeyId == savedKeyId);
                                if (match != null)
                                {
                                    MemData realMemData = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(match.MemId) : null;
                                    if (realMemData != null)
                                    {
                                        match.IsActive = false;
                                        gen.TryAddMem(realMemData, match);
                                    }
                                }
                            }
                        }

                        gen.currentPowerStorage = entry.currentStorageCount;
                        if (gen.DeployedMems.Count > 0)
                        {
                            gen.totalPowerRequiredTime = ProductionCalculator.CalculatePowerGenerationTime(gen.basePowerGenerationTime, gen.DeployedMems[0]);
                        }
                        gen.currentPowerProgressTime = entry.currentProgressTime;
                        gen.isPowerGenerating = entry.isActive;
                        gen.CheckPowerCondition();
                    }
                    else if (restoredBuilding.TryGetComponent<TransportRuntime>(out var trans))
                    {
                        trans.buildingData = snap.data;
                        trans.currentLevel = entry.currentLevel > 0 ? entry.currentLevel : 1;

                        if (trans.DeployedMems != null) trans.DeployedMems.Clear();
                        if (trans.DeployedMemEntries != null) trans.DeployedMemEntries.Clear();

                        if (memManager != null && entry.DeployedMemIDs != null)
                        {
                            int maxCapacity = ProductionCalculator.GetTransportMaxMemCount(trans.currentLevel);
                            var safeMemIDs = entry.DeployedMemIDs.Distinct().Take(maxCapacity).ToList();
                            foreach (var savedKeyId in safeMemIDs)
                            {
                                var match = memManager.CapturedMems.FirstOrDefault(m => m != null && m.KeyId == savedKeyId);
                                if (match != null)
                                {
                                    MemData realMemData = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(match.MemId) : null;
                                    if (realMemData != null)
                                    {
                                        match.IsActive = false;
                                        trans.TryAddMem(realMemData, match);
                                    }
                                }
                            }
                        }

                        trans.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(trans.baseIntervalTime, trans.DeployedMems);
                        trans.currentProgressTime = entry.currentProgressTime;
                        trans.isWorking = entry.isActive;
                        trans.CheckProductionCondition();
                    }
                    else if (restoredBuilding.TryGetComponent<CampFireRuntime>(out var campFire))
                    {
                        campFire.buildingData = snap.data;
                        campFire.currentCookingItem = entry.currentCraftingItemId;
                        campFire.targetQuantity = entry.targetQuantity;
                        campFire.remainingQuantity = entry.remainingQuantity;

                        if (campFire.DeployedMems != null) campFire.DeployedMems.Clear();
                        if (campFire.DeployedMemEntries != null) campFire.DeployedMemEntries.Clear();

                        if (memManager != null && entry.DeployedMemIDs != null)
                        {
                            foreach (var savedKeyId in entry.DeployedMemIDs)
                            {
                                var match = memManager.CapturedMems.FirstOrDefault(m => m != null && m.KeyId == savedKeyId);
                                if (match != null)
                                {
                                    MemData realMemData = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(match.MemId) : null;
                                    if (realMemData != null)
                                    {
                                        match.IsActive = false;
                                        campFire.TryAddMem(realMemData, match);
                                    }
                                }
                            }
                        }

                        campFire.currentStorageCount = entry.currentStorageCount;
                        if (!string.IsNullOrEmpty(campFire.currentCookingItem))
                        {
                            CookRecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindCookRecipeData(campFire.currentCookingItem) : null;
                            float baseDuration = recipe != null ? recipe.Time : 15f;
                            campFire.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, campFire.DeployedMems);
                        }
                        campFire.currentProgressTime = entry.currentProgressTime;
                        campFire.isCooking = entry.isActive;
                    }
                    else if (restoredBuilding.TryGetComponent<KitchenRuntime>(out var kitchen))
                    {
                        kitchen.buildingData = snap.data;
                        kitchen.currentCookingItem = entry.currentCraftingItemId;
                        kitchen.targetQuantity = entry.targetQuantity;
                        kitchen.remainingQuantity = entry.remainingQuantity;

                        if (kitchen.DeployedMems != null) kitchen.DeployedMems.Clear();
                        if (kitchen.DeployedMemEntries != null) kitchen.DeployedMemEntries.Clear();

                        if (memManager != null && entry.DeployedMemIDs != null)
                        {
                            foreach (var savedKeyId in entry.DeployedMemIDs)
                            {
                                var match = memManager.CapturedMems.FirstOrDefault(m => m != null && m.KeyId == savedKeyId);
                                if (match != null)
                                {
                                    MemData realMemData = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(match.MemId) : null;
                                    if (realMemData != null)
                                    {
                                        match.IsActive = false;
                                        kitchen.TryAddMem(realMemData, match);
                                    }
                                }
                            }
                        }

                        kitchen.currentStorageCount = entry.currentStorageCount;
                        if (!string.IsNullOrEmpty(kitchen.currentCookingItem))
                        {
                            CookRecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindCookRecipeData(kitchen.currentCookingItem) : null;
                            float baseDuration = recipe != null ? recipe.Time : 15f;
                            kitchen.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, kitchen.DeployedMems);
                        }
                        kitchen.currentProgressTime = entry.currentProgressTime;
                        kitchen.isCooking = entry.isActive;
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

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 아이템 '{itemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        return ItemCatalogManager.Instance.FindItemData(itemId);
    }

    private bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            eventData.position = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }
        else
        {
            return false;
        }

        List<RaycastResult> results = new List<RaycastResult>();
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
        SatisFactoryUI satisfactionUI = FindFirstObjectByType<SatisFactoryUI>();
        if (satisfactionUI != null)
        {
            satisfactionUI.RecalculateSatisfaction();
        }
    }

    private List<BuildingData> GetAvailableBuildingsFromInventory()
    {
        List<BuildingData> filteredList = new List<BuildingData>();
        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        WarehouseInventory warehouse = FindFirstObjectByType<WarehouseInventory>();

        foreach (var bData in buildings)
        {
            if (bData == null) continue;

            if (string.IsNullOrEmpty(bData.requireBlueprint))
            {
                filteredList.Add(bData);
                continue;
            }

            int totalBlueprintCount = 0;
            if (inventory != null) totalBlueprintCount += inventory.GetItemAmount(bData.requireBlueprint);
            if (warehouse != null) totalBlueprintCount += warehouse.GetItemAmount(bData.requireBlueprint);

            if (totalBlueprintCount > 0)
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

        cachedBorderTextures = new Texture2D[16];

        if (Application.isPlaying)
        {
            UpdateInnerSurfacePlane();
            UpdateGlobalGridOverlay();
            UpdateTileOccupiedVisuals();
        }
    }

    [ContextMenu("Function: Expand to Test")]
    public void TestExpand()
    {
        count++;
        ExpandGrid(count, count);
    }

    [ContextMenu("Function: Add All Blueprints To Inventory")]
    public void TestAddAllBlueprints()
    {
        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning("[GridManager] PlayerInventory 컴포넌트를 씬에서 찾을 수 없습니다.");
            return;
        }

        string[] blueprintIds = new string[]
        {
            "blueprint_amber_quarry",
            "blueprint_berry_farm",
            "blueprint_birch_logging_farm",
            "blueprint_campfire",
            "blueprint_diamond_quarry",
            "blueprint_iron_ore_quarry",
            "blueprint_livestock_farm",
            "blueprint_logging_farm",
            "blueprint_production_stand",
            "blueprint_wheat_farm",
            "blueprint_generator",
            "blueprint_kitchen",
            "blueprint_transport_facility"
        };

        int successCount = 0;

        foreach (string bpId in blueprintIds)
        {
            ItemData bpItem = FindItemDataInProject(bpId);
            if (bpItem != null)
            {
                inventory.AddItem(bpItem, 1);
                successCount++;
            }
            else
            {
                int remaining = inventory.AddItem(bpId, 1);
                if (remaining == 0) successCount++;
                else Debug.LogWarning($"[GridManager] 설계도 '{bpId}' 지급 실패 (아이템 카탈로그 미등록 또는 인벤토리 가득 참)");
            }
        }

        Debug.Log($"<color=lime>[GridManager]</color> 🛠️ 총 {successCount}/{blueprintIds.Length}개의 설계도를 PlayerInventory에 지급했습니다.");
    }

    public void SyncRestoredBuilding(GameObject buildingObj, BuildingData data, int gridX, int gridZ, float rotationY)
    {
        if (buildingObj == null || data == null) return;

        int defaultSize = currentWidth > 0 ? currentWidth : 10;
        if (tileGrid == null || occupiedCells == null || buildingObjectsGrid == null)
        {
            InitializeGrid(defaultSize, defaultSize);
        }

        int currentRotationIndex = Mathf.RoundToInt(rotationY / 90f) % 4;
        bool isRotated = (currentRotationIndex == 1 || currentRotationIndex == 3);
        int bWidth = isRotated ? data.height : data.width;
        int bHeight = isRotated ? data.width : data.height;

        int requiredWidth = Mathf.Max(currentWidth, gridX + bWidth);
        int requiredHeight = Mathf.Max(currentHeight, gridZ + bHeight);
        if (requiredWidth > currentWidth || requiredHeight > currentHeight)
        {
            ExpandGrid(requiredWidth, requiredHeight);
        }

        for (int x = gridX; x < gridX + bWidth; x++)
        {
            for (int z = gridZ; z < gridZ + bHeight; z++)
            {
                if (x >= 0 && x < currentWidth && z >= 0 && z < currentHeight)
                {
                    occupiedCells[x, z] = true;
                    buildingObjectsGrid[x, z] = buildingObj;
                    buildingDataGrid[x, z] = data;
                }
            }
        }

        UpdateTileOccupiedVisuals();
    }
}