using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MemSystem.Data;

public class ProductionMemSlotUI : MonoBehaviour, IDropHandler
{
    [Header("슬롯 자체 비주얼 Image 컴포넌트")]
    [SerializeField] private Image slotImage;

    public int SlotIndex { get; private set; }
    private bool isUnlocked = false;
    private MemData currentPlacedMem = null;

    public void InitializeSlot(int index)
    {
        SlotIndex = index;
        if (slotImage == null) slotImage = GetComponent<Image>();
    }

    /// <summary>
    /// 슬롯 해금 여부 및 실제 배치된 멤의 이미지를 적용
    /// </summary>
    public void RefreshStatus(bool unlocked, MemData memData)
    {
        isUnlocked = unlocked;
        currentPlacedMem = memData;

        if (!isUnlocked)
        {
            slotImage.color = Color.black; 
            
        }
        else
        {
            slotImage.color = Color.white; 

            if (currentPlacedMem != null)
            {
                // MemData에 Sprite가 있으면 그걸로 추가하려했는데 없어서 우선 코드로만 대략 추가
                //if (currentPlacedMem. != null) slotImage.sprite = currentPlacedMem.;

                slotImage.color = Color.green;
            }
            else
            {
                // slotImage.sprite = 플러스 스프라이트;
            }
        }
    }

    /// <summary>
    /// 멤을 슬롯 위에 드롭할때 동작처리시킬 함수 
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (!isUnlocked)
        {
            Debug.LogWarning("아직 해금되지 않았습니다");
            return;
        }
        
    }
}