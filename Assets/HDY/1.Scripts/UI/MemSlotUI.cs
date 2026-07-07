using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MemSystem.Data;
using HDY.Capture;

namespace HDY.UI
{
    /// <summary>
    /// 정렬 기준이 Mem스탯(제작/벌목/채광/이동/생산/탐험) 또는 티어일 때 슬롯에 표시할 아이콘/텍스트 정보.
    /// 스탯/티어 정렬이 아니거나 빈 칸이면 IsVisible=false로 감춘다. 어떤 아이콘/텍스트를 보여줄지는
    /// 카탈로그(MemData)에 접근 가능한 MemStorageUI가 계산해서 넘겨준다 - 이 구조체는 결과만 담는다.
    /// DisplayText는 스탯이면 숫자 문자열("3"), 티어면 앞글자 대문자("R"/"E"/"U"/"L"/"M")가 들어온다.
    /// </summary>
    public readonly struct MemStatDisplayInfo
    {
        public readonly bool IsVisible;
        public readonly Sprite Icon;
        public readonly string DisplayText;

        public MemStatDisplayInfo(bool isVisible, Sprite icon, string displayText)
        {
            IsVisible = isVisible;
            Icon = icon;
            DisplayText = displayText;
        }

        /// <summary>스탯/티어 정렬 중이 아닐 때(또는 빈 칸일 때) 사용하는 "표시 안 함" 상태.</summary>
        public static readonly MemStatDisplayInfo Hidden = new MemStatDisplayInfo(false, null, string.Empty);
    }

    /// <summary>
    /// 멤 창고 그리드의 슬롯 한 칸.
    /// 아이콘(Sprite)은 추후 MemData에 필드가 추가되면 채워질 예정이며, 현재는 비워둔다.
    /// ActiveImage: 이 멤이 활성화(CapturedMemEntry.IsActive) 상태일 때 표시.
    /// MemStatIcon/MemStatText: 창고가 Mem스탯 또는 티어 기준으로 정렬되어 있을 때만 활성화되어, 그 아이콘과
    /// 값(스탯 숫자 또는 티어 앞글자)을 보여준다 (어떤 아이콘/값을 보여줄지는 MemStorageUI가 계산해서
    /// MemStatDisplayInfo로 넘겨준다).
    /// 드래그앤드롭으로 슬롯끼리 위치를 바꾸는 조작(마우스를 누른 채 끌어서 다른 슬롯에 놓기)도 이 클래스가 감지한다.
    /// 빈 슬롯으로도 멤을 옮길 수 있다 - 대상 슬롯이 비어있어도 유효한 이동으로 처리한다.
    /// 드래그 도중 휠로 페이지가 바뀔 수 있으므로(MemStorageUI_Grid), 실제 데이터 교체 판단에 필요한
    /// "드래그 시작/종료" 신호만 이벤트로 올리고, 어떤 항목을 옮기는지의 판단은 상위(Grid/MemStorageUI)가 담당한다.
    /// </summary>
    public class MemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("슬롯 UI 참조")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image activeImage;
        [SerializeField] private Image memStatIcon;
        [SerializeField] private TMP_Text memStatText;

        private CapturedMemEntry cachedEntry;
        private MemData cachedData;

        private Canvas rootCanvas;
        private RectTransform dragGhost;

        /// <summary>슬롯이 클릭되었을 때 발생. 채워진 슬롯일 때만 발생한다.</summary>
        public event Action<CapturedMemEntry, MemData> OnSlotClicked;

        /// <summary>
        /// 다른 슬롯을 드래그하다가 이 슬롯 위에 놓았을 때 발생. (드래그를 시작한 슬롯, 드롭된 슬롯=this) 순서로 전달.
        /// 대상(this)이 비어있어도 유효한 이동으로 처리한다(빈 칸으로 멤을 옮길 수 있음).
        /// </summary>
        public event Action<MemSlotUI, MemSlotUI> OnSlotSwapRequested;

        /// <summary>
        /// 드래그가 실제로 시작되었을 때 발생(빈 슬롯이면 발생하지 않음). Grid가 구독해서 드래그 시작 시점의
        /// 전체 인덱스를 미리 기억해두는 데 사용한다 - 드래그 도중 휠로 페이지가 바뀌어도(Populate로 이 슬롯의
        /// cachedEntry가 다른 항목으로 덮어써지더라도) 원래 옮기려던 항목을 잃지 않기 위함이다.
        /// </summary>
        public event Action<MemSlotUI> OnSlotDragBegan;

        /// <summary>드래그가 끝났을 때(성공/실패 관계없이) 발생. Grid가 구독해서 기억해둔 인덱스를 정리하는 데 사용한다.</summary>
        public event Action<MemSlotUI> OnSlotDragEnded;

        /// <summary>이 슬롯에 포획된 멤이 채워져 있는지 여부.</summary>
        public bool HasData => cachedEntry != null;

        private void Awake()
        {
            if (slotButton == null)
            {
                Debug.LogWarning($"[MemSlotUI] slotButton이 비어있습니다 ({gameObject.name}). 클릭이 동작하지 않습니다.", this);
            }
            else
            {
                slotButton.onClick.AddListener(HandleClick);
            }

            if (iconImage == null)
            {
                Debug.LogWarning($"[MemSlotUI] iconImage가 비어있습니다 ({gameObject.name}).", this);
            }

            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
            {
                Debug.LogWarning($"[MemSlotUI] 상위에서 Canvas를 찾을 수 없습니다 ({gameObject.name}). 드래그 중 아이콘이 표시되지 않을 수 있습니다.", this);
            }
        }

        /// <summary>
        /// 슬롯에 포획된 멤 데이터를 채운다.
        /// TODO: MemData에 아이콘(Sprite) 필드가 추가되면 iconImage.sprite = data.icon; 으로 교체.
        /// </summary>
        /// <param name="statInfo">현재 창고가 Mem스탯/티어 기준으로 정렬 중일 때 표시할 아이콘/값. 정렬 중이 아니면 Hidden.</param>
        public void SetData(CapturedMemEntry entry, MemData data, MemStatDisplayInfo statInfo)
        {
            cachedEntry = entry;
            cachedData = data;

            if (slotButton != null)
            {
                slotButton.interactable = true;
            }

            if (iconImage != null)
            {
                iconImage.sprite = null;
            }

            if (activeImage != null)
            {
                activeImage.gameObject.SetActive(entry != null && entry.IsActive);
            }

            ApplyStatDisplay(statInfo);
        }

        /// <summary>슬롯을 빈 상태로 되돌린다.</summary>
        public void Clear()
        {
            cachedEntry = null;
            cachedData = null;

            if (slotButton != null)
            {
                slotButton.interactable = false;
            }

            if (iconImage != null)
            {
                iconImage.sprite = null;
            }

            if (activeImage != null)
            {
                activeImage.gameObject.SetActive(false);
            }

            ApplyStatDisplay(MemStatDisplayInfo.Hidden);
        }

        /// <summary>MemStatIcon/MemStatText를 statInfo에 맞게 켜고 끈다. 스탯/티어 정렬 중이 아니면 둘 다 감춘다.</summary>
        private void ApplyStatDisplay(MemStatDisplayInfo statInfo)
        {
            if (memStatIcon != null)
            {
                memStatIcon.gameObject.SetActive(statInfo.IsVisible);
                memStatIcon.sprite = statInfo.Icon;
            }

            if (memStatText != null)
            {
                memStatText.gameObject.SetActive(statInfo.IsVisible);
                memStatText.text = statInfo.IsVisible ? statInfo.DisplayText : string.Empty;
            }
        }

        private void HandleClick()
        {
            // 클릭 자체가 들어오는지 확인하기 위한 로그. cachedEntry가 없으면(빈 슬롯) 여기서 멈춘다.
            Debug.Log($"[MemSlotUI] 클릭 수신: {gameObject.name} / cachedEntry={(cachedEntry != null)}");

            if (cachedEntry == null) return;
            OnSlotClicked?.Invoke(cachedEntry, cachedData);
        }

        /// <summary>드래그 시작. 빈 슬롯이거나 아이콘/캔버스를 찾을 수 없으면 드래그 자체를 취소한다.</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!HasData || iconImage == null || rootCanvas == null)
            {
                eventData.pointerDrag = null; // 드래그 파이프라인을 취소한다 (이후 OnDrag/OnEndDrag가 호출되지 않음)
                return;
            }

            var ghostObject = new GameObject("DragGhostIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dragGhost = ghostObject.GetComponent<RectTransform>();
            dragGhost.SetParent(rootCanvas.transform, false);
            dragGhost.SetAsLastSibling();
            dragGhost.sizeDelta = iconImage.rectTransform.sizeDelta;

            var ghostImage = ghostObject.GetComponent<Image>();
            ghostImage.sprite = iconImage.sprite;
            ghostImage.color = iconImage.color;
            ghostImage.raycastTarget = false; // 드롭 대상 탐지를 가리지 않도록 함

            // 캔버스가 Screen Space - Overlay 라는 가정 하에 스크린 좌표를 그대로 사용한다.
            // Screen Space - Camera / World Space 캔버스라면 좌표 변환 로직 보정이 필요하다.
            dragGhost.position = eventData.position;

            OnSlotDragBegan?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragGhost != null)
            {
                dragGhost.position = eventData.position;
            }
        }

        /// <summary>
        /// 드래그 종료. 유효한 위치에 놓였는지 여부와 관계없이 임시 아이콘(고스트)만 정리한다.
        /// 실제 위치 교체 요청은 OnDrop에서 이미 이벤트로 전달했으므로 여기서는 시각적 정리만 한다.
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragGhost != null)
            {
                Destroy(dragGhost.gameObject);
                dragGhost = null;
            }

            OnSlotDragEnded?.Invoke(this);
        }

        /// <summary>
        /// 다른 슬롯의 드래그가 이 슬롯 위에서 끝났을 때 호출된다.
        /// 이 슬롯이 비어있어도(빈 칸) 유효한 이동으로 처리한다 - 빈 칸으로 멤을 옮길 수 있다.
        /// 그리드/부모가 아닌 배경 등 IDropHandler가 없는 곳에 놓으면 이 메서드 자체가 호출되지 않아
        /// 자연스럽게 "잘못된 위치 -> 원래 자리로 되돌아감" 이 된다(아무 것도 바뀌지 않으므로).
        ///
        /// [드래그 도중 휠로 페이지 이동] 드래그 중 다른 페이지로 넘어가면 sourceSlot의 cachedEntry는 새 페이지
        /// 데이터로 이미 덮어써진 상태일 수 있어(Populate가 슬롯을 가리지 않고 전부 갱신하기 때문) sourceSlot.HasData로
        /// "원래 드래그하던 항목이 맞는지"를 다시 검사하지 않는다 - OnBeginDrag 시점에 이미 검증되었고, 실제 어떤
        /// 항목을 옮기는지는 Grid가 드래그 시작 시점에 기억해둔 인덱스로 판단한다.
        /// </summary>
        public void OnDrop(PointerEventData eventData)
        {
            var sourceSlot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<MemSlotUI>() : null;

            if (sourceSlot == null || sourceSlot == this) return; // 슬롯이 아니거나 자기 자신이면 무시

            Debug.Log($"[MemSlotUI] 슬롯 교체 요청: {sourceSlot.gameObject.name} -> {gameObject.name}");
            OnSlotSwapRequested?.Invoke(sourceSlot, this);
        }
    }
}
