using UnityEngine;
using UnityEngine.InputSystem;

public class GridManager : MonoBehaviour
{
    [Header("타일 생성 관련 정보: Prefab, 생성될 위치, Grid Layer")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Transform floorContainer;
    [SerializeField] private LayerMask gridLayerMask;


    private int currentWidth;
    private int currentHeight;

    private GameObject[,] tileGrid;

    private Material greenMaterial;

    public int MouseGridX { get; private set; }
    public int MouseGridZ { get; private set; }
    public bool IsMouseOnGrid { get; private set; }

    void Start()
    {
        greenMaterial = CreateGridMaterial();

        InitializeGrid(5, 5);
    }

    void Update()
    {
        UpdateMouseGridPosition();
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
        Debug.Log($"[Grid] 초기 영지 생성 완료 ({currentWidth}x{currentHeight})");
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
            meshRenderer.material = greenMaterial;
        }

        return newTile;
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
    /// 임시. 초록색 타일 + 경계선 보이도록 처리하는 함수
    /// </summary>
    private Material CreateGridMaterial()
    {
        Texture2D texture = new Texture2D(64, 64);
        texture.filterMode = FilterMode.Point;

        Color grassGreen = new Color(0.3f, 0.75f, 0.3f);
        Color borderColor = new Color(0.15f, 0.5f, 0.15f);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (x < 2 || x > 61 || y < 2 || y > 61)
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

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = texture;

        return mat;
    }

    /// <summary>
    /// 확장에 대한 테스트함수
    /// </summary>
    [ContextMenu("Function: Expand to 6x6")]
    public void CheatExpand6x6() => ExpandGrid(6, 6);

    [ContextMenu("Function: Expand to 7x7")]
    public void CheatExpand7x7() => ExpandGrid(7, 7);
}