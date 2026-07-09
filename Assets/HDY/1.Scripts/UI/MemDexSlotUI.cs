using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MemSystem.Data;
using HDY.Mem;

namespace HDY.UI
{
    /// <summary>
    /// 도감 그리드의 슬롯 한 칸.
    /// MemSlotUI(멤창고 슬롯)와 구조는 비슷하지만, 참조하는 데이터가 CapturedMemEntry(포획된 개체)가 아니라
    /// MemCatalogManager의 MemData(도감 데이터, 포획 여부와 무관) 하나뿐이다. 그래서:
    /// - ActiveImage(배치 활성 표시)가 없다 - 도감 항목은 "배치"라는 개념 자체가 없음.
    /// - 드래그앤드롭(슬롯 교체)이 없다 - 순서를 바꿀 "내 소유 데이터"가 아니라 읽기 전용 카탈로그이므로.
    /// 아이콘(MemIconRenderer)과 Mem스탯/티어 표시(MemStatDisplayInfo)는 MemSlotUI와 동일한 방식으로 재사용한다.
    /// </summary>
    public class MemDexSlotUI : MonoBehaviour
    {
        [Header("슬롯 UI 참조")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image memStatIcon;
        [SerializeField] private TMP_Text memStatText;

        private MemData cachedData;

        /// <summary>슬롯이 클릭되었을 때 발생. MemDexUI(컨트롤러)가 구독해서 정보 패널로 전달한다.</summary>
        public event Action<MemData> OnSlotClicked;

        private void Awake()
        {
            if (slotButton != null)
            {
                slotButton.onClick.AddListener(HandleClick);
            }
            else
            {
                Debug.LogWarning($"[MemDexSlotUI] slotButton이 비어있습니다 ({gameObject.name}). 클릭이 동작하지 않습니다.", this);
            }

            if (iconImage == null)
            {
                Debug.LogWarning($"[MemDexSlotUI] iconImage가 비어있습니다 ({gameObject.name}).", this);
            }
        }

        /// <summary>슬롯에 표시할 도감 데이터를 채운다.</summary>
        /// <param name="statInfo">현재 도감이 Mem스탯/티어 기준으로 정렬 중일 때 표시할 아이콘/값. 정렬 중이 아니면 Hidden.</param>
        public void SetData(MemData data, MemStatDisplayInfo statInfo)
        {
            cachedData = data;

            ApplyIcon(data);
            ApplyStatDisplay(statInfo);
        }

        /// <summary>
        /// MemIconRenderer(MemData.modelPrefab을 촬영해서 만든 Sprite)를 memId로 조회해서 iconImage에 채운다.
        /// 아이콘을 만들 수 없으면(데이터/모델 없음, 렌더러 없음) 아이콘 영역을 그냥 감춘다.
        /// </summary>
        private void ApplyIcon(MemData data)
        {
            if (iconImage == null) return;

            var sprite = (data != null && MemIconRenderer.Instance != null)
                ? MemIconRenderer.Instance.GetIcon(data.memId)
                : null;

            iconImage.sprite = sprite;
            iconImage.gameObject.SetActive(sprite != null);
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
            if (cachedData == null) return;
            OnSlotClicked?.Invoke(cachedData);
        }
    }
}
