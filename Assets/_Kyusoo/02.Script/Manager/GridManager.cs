using System;
using System.Collections.Generic;
using UnityEngine;
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

    private BuildingData selectedBuildingData;
    private GameObject currentPreviewInstance;

    private int currentWidth;
    private int currentHeight;


    private GameObject[,] tileGrid;
    private Vector3 raycastHitPoint;

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

        if(currentPreviewInstance != null && IsMouseOnGrid)
        {
            UpdatePreviewPosition();
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
        for (int i = 0; i < currentWidth; i++)
        {
            for (int j = 0; j < currentHeight; j++)
            {
                newTileGrid[i, j] = tileGrid[i, j];
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
        currentWidth = newWidth;
        currentHeight = newHeight;
        Debug.Log($"[Grid] 영지 확장 성공! 현재 크기: ({currentWidth}x{currentHeight})");
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
            Destroy(currentPreviewInstance);
            selectedBuildingData = null;
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

    private void UpdatePreviewPosition()
    {
        if (selectedBuildingData == null) return;

        int adjustedGridX = Mathf.FloorToInt(raycastHitPoint.x - (selectedBuildingData.width / 2.0f));
        int adjustedGridZ = Mathf.FloorToInt(raycastHitPoint.z - (selectedBuildingData.height / 2.0f));

        float offsetX = adjustedGridX + (selectedBuildingData.width / 2.0f);
        float offsetZ = adjustedGridZ + (selectedBuildingData.height / 2.0f);

        currentPreviewInstance.transform.position = new Vector3(offsetX, 0f, offsetZ);
    }

    /// <summary>
    /// 임시. 초록색 타일 + 경계선 보이도록 처리하는 함수
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

            MeshRenderer[] renderers = currentPreviewInstance.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                renderer.material = previewMaterial;
            }
        }
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