using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// 도구별 드롭 개수 확률표에 대한 전역 접근점을 제공합니다.
/// 규칙 또는 매니저가 없을 때 호출 측은 기본 1개 드롭을 사용합니다.
/// </summary>
public class ToolDropManager : MonoBehaviour
{
    /// <summary>현재 씬의 도구 드롭 매니저입니다.</summary>
    public static ToolDropManager Instance { get; private set; }

    [SerializeField] private ToolDropTable dropTable;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>지정 도구의 확률 규칙을 한 번 추첨해 드롭 개수를 반환합니다.</summary>
    /// <param name="toolItemId">규칙을 찾을 도구의 Item Id입니다.</param>
    /// <returns>최소 1개 이상의 드롭 개수입니다.</returns>
    public int RollDropCount(string toolItemId)
    {
        return dropTable != null
            ? dropTable.RollDropCount(toolItemId)
            : 1;
    }
}
