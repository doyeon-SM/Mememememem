using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MemSystem.Data;
using HDY.Capture;

namespace HDY.UI
{
    /// <summary>
    /// 멤 창고 그리드의 슬롯 한 칸.
    /// 아이콘(Sprite)은 추후 MemData에 필드가 추가되면 채워질 예정이며, 현재는 비워둔다.
    /// 드래그앤드롭으로 슬롯끼리 위치를 바꾸는 조작(마우스를 누른 채 끌어서 다른 슬롯에 놓기)도 이 클래스가 감지한다.
    /// 빈 슬롯으로도 멤을 옮길 수 있다 - 대상 슬롯이 비어있어도 유효한 이동으로 처리한다.
    /// 단, 실제 데이터 교체는 하지 않고 이벤트로만 알린다 - 처리는 MemStorageUI_Grid/MemStorageUI(상위)가 담당한다.
    /// </summary>
    public class MemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("슬롯 UI 참조")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image iconImage;

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
        public void SetData(CapturedMemEntry entry, MemData data)
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
        }

        /// <summary>
        /// 다른 슬롯의 드래그가 이 슬롯 위에서 끝났을 때 호출된다.
        /// 이 슬롯이 비어있어도(빈 칸) 유효한 이동으로 처리한다 - 빈 칸으로 멤을 옮길 수 있다.
        /// 그리드/부모가 아닌 배경 등 IDropHandler가 없는 곳에 놓으면 이 메서드 자체가 호출되지 않아
        /// 자연스럽게 "잘못된 위치 -> 원래 자리로 되돌아감" 이 된다(아무 것도 바뀌지 않으므로).
        /// </summary>
        public void OnDrop(PointerEventData eventData)
        {
            var sourceSlot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<MemSlotUI>() : null;

            if (sourceSlot == null || sourceSlot == this) return; // 슬롯이 아니거나 자기 자신이면 무시
            if (!sourceSlot.HasData) return; // 시작 슬롯에 데이터가 없으면 무시 (원칙적으로 발생하지 않음, 빈 슬롯은 드래그 시작 자체가 안 됨)

            Debug.Log($"[MemSlotUI] 슬롯 교체 요청: {sourceSlot.gameObject.name} -> {gameObject.name}");
            OnSlotSwapRequested?.Invoke(sourceSlot, this);
        }
    }
}
