using System;
using UnityEngine;
using UnityEngine.UI;
using MemSystem.Data;
using HDY.Capture;

namespace HDY.UI
{
    /// <summary>
    /// 멤 창고 그리드의 슬롯 한 칸.
    /// 아이콘(Sprite)은 추후 MemData에 필드가 추가되면 채워질 예정이며, 현재는 비워둔다.
    /// </summary>
    public class MemSlotUI : MonoBehaviour
    {
        [Header("슬롯 UI 참조")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image iconImage;

        private CapturedMemEntry cachedEntry;
        private MemData cachedData;

        /// <summary>슬롯이 클릭되었을 때 발생. 채워진 슬롯일 때만 발생한다.</summary>
        public event Action<CapturedMemEntry, MemData> OnSlotClicked;

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
    }
}
