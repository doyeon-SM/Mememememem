// ============================================================================
// MemAccessoryTester.cs
// 악세서리 부착 위치/회전/크기를 Play Mode에서 실시간으로 맞추는 테스트 도구
//
// [사용법]
// 1. 씬의 아무 빈 오브젝트에 이 컴포넌트를 붙입니다.
// 2. targetMem에 확인할 멤을 넣습니다. (비워두면 씬의 첫 번째 활성 멤을 자동으로 잡습니다)
// 3. accessory에 조정할 MemAccessoryData 에셋을 넣습니다.
// 4. Play → 화면 좌측 상단 버튼으로 장착/해제하고,
//    Inspector의 오프셋 슬라이더를 움직이면 즉시 반영됩니다.
// 5. 값이 맞으면 [현재 값을 에셋에 저장] 버튼을 눌러 MemAccessoryData에 기록합니다.
//    (Play Mode를 빠져나가도 유지됩니다)
//
// ⚠️ 테스트 전용입니다. 빌드에 포함되어도 동작에는 영향이 없지만,
//    출시 전에 씬에서 제거하는 것을 권장합니다.
// ============================================================================

using UnityEngine;
using MemSystem.Core;
using MemSystem.Data;
using MemSystem.Visual;

/// <summary>
/// 악세서리 오프셋을 Play Mode에서 실시간 조정하기 위한 에디터 테스트 컴포넌트.
/// </summary>
public class MemAccessoryTester : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("악세서리를 붙여볼 멤. 비워두면 씬의 첫 번째 활성 멤을 자동 탐색합니다.")]
    [SerializeField] private Mem targetMem;

    [Tooltip("조정할 악세서리 데이터 에셋")]
    [SerializeField] private MemAccessoryData accessory;

    [Header("실시간 오프셋 (에셋 값으로 초기화됩니다)")]
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private Vector3 rotationOffset;
    [SerializeField] private Vector3 scaleMultiplier = Vector3.one;

    /// <summary>현재 장착된 인스턴스 (오프셋 실시간 반영 대상)</summary>
    private GameObject equippedInstance;

    /// <summary>Inspector 값이 바뀌었는지 비교하기 위한 직전 프레임 값</summary>
    private Vector3 lastPosition, lastRotation, lastScale;

    private void Start()
    {
        if (accessory != null)
        {
            positionOffset  = accessory.positionOffset;
            rotationOffset  = accessory.rotationOffset;
            scaleMultiplier = accessory.scaleMultiplier;
        }

        CacheOffsets();
    }

    private void Update()
    {
        if (equippedInstance == null) return;

        // Inspector에서 값이 바뀐 프레임에만 Transform을 갱신합니다.
        if (positionOffset == lastPosition &&
            rotationOffset == lastRotation &&
            scaleMultiplier == lastScale) return;

        ApplyOffsetsToInstance();
        CacheOffsets();
    }

    // ---------------------------------------------------------------
    // 조작
    // ---------------------------------------------------------------

    /// <summary>현재 설정된 악세서리를 대상 멤에 장착합니다.</summary>
    public void Equip()
    {
        MemVisual visual = ResolveVisual();
        if (visual == null || accessory == null) return;

        equippedInstance = visual.EquipAccessory(accessory);

        if (equippedInstance == null)
        {
            Debug.LogWarning("[MemAccessoryTester] 장착 실패. 부착 뼈를 찾지 못했거나 prefab이 비어 있습니다.");
            return;
        }

        // 에셋 값이 아니라 현재 Inspector 값으로 즉시 맞춰줍니다.
        ApplyOffsetsToInstance();
        CacheOffsets();

        Debug.Log($"[MemAccessoryTester] '{accessory.displayName}' 장착 ({accessory.slot})");
    }

    /// <summary>장착된 악세서리를 해제합니다.</summary>
    public void Unequip()
    {
        MemVisual visual = ResolveVisual();
        if (visual == null || accessory == null) return;

        visual.UnequipAccessory(accessory.slot);
        equippedInstance = null;

        Debug.Log($"[MemAccessoryTester] '{accessory.displayName}' 해제 ({accessory.slot})");
    }

    /// <summary>
    /// 현재 조정값을 MemAccessoryData 에셋에 기록합니다.
    /// 에디터 전용 — Play Mode를 나가도 값이 유지됩니다.
    /// </summary>
    public void SaveToAsset()
    {
        if (accessory == null) return;

        accessory.positionOffset  = positionOffset;
        accessory.rotationOffset  = rotationOffset;
        accessory.scaleMultiplier = scaleMultiplier;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(accessory);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(accessory);
#endif

        Debug.Log($"[MemAccessoryTester] '{accessory.name}' 에셋에 오프셋 저장 완료. " +
                  $"pos={positionOffset} rot={rotationOffset} scale={scaleMultiplier}");
    }

    // ---------------------------------------------------------------
    // 내부 구현
    // ---------------------------------------------------------------

    private void ApplyOffsetsToInstance()
    {
        if (equippedInstance == null) return;

        equippedInstance.transform.localPosition = positionOffset;
        equippedInstance.transform.localRotation = Quaternion.Euler(rotationOffset);

        Vector3 baseScale = (accessory != null && accessory.prefab != null)
            ? accessory.prefab.transform.localScale
            : Vector3.one;

        equippedInstance.transform.localScale = Vector3.Scale(baseScale, scaleMultiplier);
    }

    private void CacheOffsets()
    {
        lastPosition = positionOffset;
        lastRotation = rotationOffset;
        lastScale    = scaleMultiplier;
    }

    /// <summary>대상 멤의 MemVisual을 반환합니다. targetMem이 비어 있으면 씬에서 자동 탐색합니다.</summary>
    private MemVisual ResolveVisual()
    {
        if (targetMem == null)
        {
            foreach (Mem mem in FindObjectsByType<Mem>(FindObjectsSortMode.None))
            {
                if (mem != null && mem.IsActive) { targetMem = mem; break; }
            }
        }

        if (targetMem == null)
        {
            Debug.LogWarning("[MemAccessoryTester] 씬에서 활성 멤을 찾지 못했습니다.");
            return null;
        }

        return targetMem.Visual;
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        const float w = 210f, h = 30f;
        float y = 10f;

        string label = accessory != null ? accessory.name : "(악세서리 미지정)";
        GUI.Label(new Rect(10, y, 400, 20), $"[악세서리 테스터] {label}");
        y += 24;

        if (GUI.Button(new Rect(10, y, w, h), "장착 (Equip)")) Equip();
        y += h + 4;

        if (GUI.Button(new Rect(10, y, w, h), "해제 (Unequip)")) Unequip();
        y += h + 4;

        if (GUI.Button(new Rect(10, y, w, h), "현재 값을 에셋에 저장")) SaveToAsset();
    }
#endif
}
