using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MemSystem.Data;
using HDY.Capture;
using HDY.UI;
using HDY.Mem;

public class MemSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("슬롯 UI 요소 참조 (미리 배치될 프리팹의 컴포넌트들)")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Button slotButton;

    public int SlotIndex { get; private set; }
    private bool isUnlocked = false;

    private MemData currentPlacedMem = null;
    private CapturedMemEntry currentPlacedEntry = null;

    public void InitializeSlot(int index)
    {
        SlotIndex = index;
        if (slotButton == null) slotButton = GetComponent<Button>();

        if (TryGetComponent<MemSlotUI>(out var duplicateComp))
        {
            if (duplicateComp != this) Destroy(duplicateComp);
        }

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnClickSlot);
        }
    }

    /// <summary>
    /// 마우스 좌클릭을 통한 배치된 슬롯 해제처리
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            ExecuteSlotReleaseProcess();
        }
    }

    /// <summary>
    /// 기존 OnClickSlot의 역할을 완벽하게 대체하는 실질 해제 트랜잭션 부서
    /// </summary>
    private void ExecuteSlotReleaseProcess()
    {

        if (currentPlacedMem == null)
        {
            return;
        }
        MonoBehaviour activePanel = GetCurrentActivePanel();

        if (activePanel is ProductionPanelUI prodPanel)
        {
            prodPanel.TryRemoveMemFromUI(currentPlacedMem);
        }
        else if (activePanel is CraftingPanelUI craftPanel)
        {
            craftPanel.TryRemoveMemFromUI(currentPlacedMem);
        }
    }

    /// <summary>
    /// 해금된 슬롯의 색상 변경 및 배치된 복사 멤의 비주얼 이미지 출력
    /// </summary>
    public void RefreshStatus(bool unlocked, MemData memData, CapturedMemEntry entryData)
    {
        isUnlocked = unlocked;
        currentPlacedMem = memData;
        currentPlacedEntry = entryData;

        if (slotButton != null)
        {
            slotButton.interactable = isUnlocked;
        }

        if (!isUnlocked)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = Color.black;
            }
        }
        else
        {
            if (iconImage != null)
            {
                if (currentPlacedMem != null)
                {
                    Sprite sprite = (isUnlocked && currentPlacedMem != null && MemIconRenderer.Instance != null)
                            ? MemIconRenderer.Instance.GetIcon(currentPlacedMem.memId)
                            : null;
                    if (currentPlacedMem.modelPrefab != null)
                    {
                        iconImage.sprite = sprite;
                        iconImage.gameObject.SetActive(sprite != null);
                    }
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = Color.white;
                }
            }
        }
    }
    private MonoBehaviour GetCurrentActivePanel()
    {
        string myPrefabName = gameObject.name;
        Debug.Log($"myPrefabName: {myPrefabName}");

        if (myPrefabName.Contains("Product"))
        {
            return ProductionPanelUI.Instance;
        }
        else if (myPrefabName.Contains("Craft"))
        {
            return CraftingPanelUI.Instance;
        }

        return null;
    }

    /// <summary>
    /// 시설 내 슬롯 클릭 시 해제 처리
    /// </summary>
    private void OnClickSlot()
    {
        Debug.Log("OnClickSlot 동작 시작");
        if (currentPlacedMem == null) return;
        Debug.Log($"currentPlacedMem 존재{currentPlacedMem}");
        MonoBehaviour activePanel = GetCurrentActivePanel();
        Debug.Log($"activePanel{activePanel}");
        if (activePanel is ProductionPanelUI prodPanel)
        {
            prodPanel.TryRemoveMemFromUI(currentPlacedMem);
        }
        else if (activePanel is CraftingPanelUI craftPanel)
        {
            craftPanel.TryRemoveMemFromUI(currentPlacedMem);
        }
    }

    /// <summary>
    /// 멤 Drag&Drop처리했을 때 슬롯에 배치처리
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (!isUnlocked)
        {
            Debug.LogWarning($"시설 레벨 조건이 충족되지 않아 잠겨있는 슬롯 칸입니다.");
            return;
        }

        if (eventData.pointerDrag != null && eventData.pointerDrag.TryGetComponent<HDY.UI.MemSlotUI>(out HDY.UI.MemSlotUI draggedSlot))
        {
            // C# Reflection으로 데이터 복사 접근 정밀화
            var type = typeof(HDY.UI.MemSlotUI);
            var fieldEntry = type.GetField("cachedEntry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fieldData = type.GetField("cachedData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (fieldEntry != null && fieldData != null)
            {
                CapturedMemEntry warehouseEntry = fieldEntry.GetValue(draggedSlot) as CapturedMemEntry;
                MemData warehouseData = fieldData.GetValue(draggedSlot) as MemData;

                // 테스트 데이터로 인해 발생한 사항에 대한 임시 방어코드.
                // 창고 원본 entry는 존재하나 데이터가 null인 테스트 상황에 대비합니다.
                // 실제로는 해당코드 제거 후 다시 테스트 필요
                if (warehouseEntry != null)
                {
                    if (warehouseData == null)
                    {
                        Debug.LogWarning($"<color=orange>[OnDrop 경고]</color> 창고 멤의 MemData가 null입니다. 테스트 연동을 위해 임시 디버그용 가방 데이터를 생성합니다.");

                        warehouseData = ScriptableObject.CreateInstance<MemData>();
                        warehouseData.memName = "디버그용 테스트 멤";
                        warehouseData.tier = MemTier.Rare;

                        warehouseData.productionStats.crafting = 1;
                        warehouseData.productionStats.logging = 1;
                        warehouseData.productionStats.mining = 1;
                        warehouseData.productionStats.transport = 1;
                        warehouseData.productionStats.farming = 1;
                    }
                    MonoBehaviour activePanel = GetCurrentActivePanel();

                    if (activePanel is ProductionPanelUI prodPanel)
                    {
                        prodPanel.TryDeployMemFromUI(warehouseData, warehouseEntry);
                    }
                    else if (activePanel is CraftingPanelUI craftPanel)
                    {
                        craftPanel.TryDeployMemFromUI(warehouseData, warehouseEntry);
                    }
                }
                else
                {
                    Debug.LogError("[OnDrop 에러] 드래그한 창고 슬롯의 CapturedMemEntry 자체가 null입니다. 완전한 빈 슬롯을 드래그했습니다.");
                }
            }
            else
            {
                Debug.LogError("[OnDrop 에러] 팀원 스크립트의 cachedEntry 또는 cachedData private 필드명을 찾을 수 없습니다.");
            }
        }
        else
        {
            Debug.LogWarning("[OnDrop 경고] 드래그해온 오브젝트에서 팀원의 MemSlotUI 컴포넌트를 찾지 못했습니다.");
        }
    }

    

    // 테스트함수 없이 실제 동작시킬때 사용할 함수(윗 버전은 임시용)
    //public void OnDrop(PointerEventData eventData)
    //{
    //    if (!isUnlocked)
    //    {
    //        Debug.LogWarning($"시설 레벨 조건이 충족되지 않아 잠겨있는 슬롯 칸입니다. (인덱스: {SlotIndex})");
    //        return;
    //    }

    //    if (eventData.pointerDrag != null && eventData.pointerDrag.TryGetComponent<HDY.UI.MemSlotUI>(out HDY.UI.MemSlotUI draggedSlot))
    //    {
    //        var type = typeof(HDY.UI.MemSlotUI);
    //        var fieldEntry = type.GetField("cachedEntry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    //        var fieldData = type.GetField("cachedData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    //        if (fieldEntry != null && fieldData != null)
    //        {
    //            CapturedMemEntry warehouseEntry = fieldEntry.GetValue(draggedSlot) as CapturedMemEntry;
    //            MemData warehouseData = fieldData.GetValue(draggedSlot) as MemData;

    //            if (warehouseEntry != null && warehouseData != null)
    //            {
    //                Debug.Log($"포획 멤 데이터 추출 완료: {warehouseData.memName}.");
    //                ProductionPanelUI.Instance.TryDeployMemFromUI(warehouseData, warehouseEntry);
    //            }
    //            else
    //            {
    //                Debug.LogWarning("[OnDrop 경고] 슬롯에 정상적인 멤 데이터가 존재하지 않아 배치를 취소합니다.");
    //            }
    //        }
    //    }
    //}
}