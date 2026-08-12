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
    /// [HDY 요청] memStatPanel(MemStatIcon/MemStatText를 감싸는 패널)도 MemSlotUI와 동일하게 statInfo.IsVisible
    /// 기준으로 함께 숨긴다 - 정렬 중이 아니거나 id 정렬이면 아이콘/텍스트뿐 아니라 패널 자체도 꺼진다.
    ///
    /// [HDY 요청 - 최초 포획 실루엣] MemDexRecordManager에 최초 포획 기록이 없는(아직 한 번도 포획한 적
    /// 없는) 종은 아이콘을 검게 틴트해서 실루엣처럼 보이게 한다. 아이콘 스프라이트(모양) 자체는 그대로
    /// 두고 Image.color만 검정으로 덮어씌우는 방식이라 별도의 실루엣 전용 스프라이트가 필요 없다.
    /// 발견 여부 판단(MemDexRecordManager 조회)은 이 클래스가 하지 않는다 - 카탈로그 순회와 마찬가지로
    /// 상위(MemDexUI)가 판단해서 SetData 호출 시 isDiscovered로 넘겨준다.
    ///
    /// [HDY 요청 - 선택 표시] selectedImage는 이 슬롯이 지금 선택된(정보 패널에 표시 중인) 상태인지
    /// 나타낸다. 이 클래스는 자기가 선택됐는지 스스로 판단하지 않는다 - 클릭 시 발생시키는 OnSlotClicked를
    /// 상위(MemDexUI_Grid/MemDexUI)가 구독해서, "지금까지 선택되어 있던 슬롯은 끄고 새로 클릭된 슬롯만
    /// 켠다"를 SetSelected로 직접 지시한다. SetData가 호출될 때마다(그리드가 다시 채워질 때마다) 일단
    /// 꺼진 상태로 초기화되고, 그 직후 그리드가 현재 선택된 데이터와 일치하는 슬롯에만 다시 켜준다.
    /// </summary>
    public class MemDexSlotUI : MonoBehaviour
    {
        [Header("슬롯 UI 참조")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image memStatIcon;
        [SerializeField] private TMP_Text memStatText;
        [Tooltip("MemStatIcon/MemStatText를 감싸는 패널(HDY 요청). 정렬 중이 아니거나 id 정렬일 때(statInfo.IsVisible=false) 이 패널도 함께 숨긴다.")]
        [SerializeField] private GameObject memStatPanel;
        [Tooltip("이 슬롯이 선택된 상태임을 나타내는 이미지(HDY 요청). 도감에서는 한 번에 하나의 슬롯만 켜져 있어야 한다.")]
        [SerializeField] private GameObject selectedImage;

        private MemData cachedData;

        /// <summary>[HDY 요청 - 선택 표시] 이 슬롯이 현재 표시하고 있는 도감 데이터(원본 참조). MemDexUI_Grid가 선택 판정(참조 비교)에 사용한다.</summary>
        public MemData BoundData => cachedData;

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
        /// <param name="isDiscovered">이 종의 최초 포획 기록이 있는지 여부. false면 아이콘을 검게 틴트해 실루엣으로 표시한다.</param>
        public void SetData(MemData data, MemStatDisplayInfo statInfo, bool isDiscovered)
        {
            cachedData = data;

            ApplyIcon(data);
            ApplyDiscoveryTint(isDiscovered);
            ApplyStatDisplay(statInfo);

            // [HDY 요청 - 선택 표시] 새로 채워질 때는 일단 꺼둔다 - 켜야 하는 슬롯은 그리드가 곧이어
            // SetSelected(true)로 다시 켠다.
            SetSelected(false);
        }

        /// <summary>[HDY 요청 - 선택 표시] 이 슬롯의 선택 표시 이미지를 켜고 끈다.</summary>
        public void SetSelected(bool isSelected)
        {
            if (selectedImage != null) selectedImage.SetActive(isSelected);
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

        /// <summary>
        /// [HDY 요청 - 최초 포획 실루엣] 미발견 종은 아이콘 색을 검정으로 틴트하고, 발견된 종은 흰색(틴트 없음)으로
        /// 되돌린다. 스프라이트 자체는 ApplyIcon이 이미 채운 그대로 두고 색상만 바꾼다.
        /// </summary>
        private void ApplyDiscoveryTint(bool isDiscovered)
        {
            if (iconImage == null) return;

            iconImage.color = isDiscovered ? Color.white : Color.black;
        }

        /// <summary>
        /// MemStatIcon/MemStatText/memStatPanel을 statInfo에 맞게 켜고 끈다. 스탯/티어 정렬 중이 아니거나
        /// id 정렬이면(statInfo.IsVisible=false) 셋 다 감춘다(HDY 요청 - memStatPanel도 함께 숨김, MemSlotUI와 동일).
        ///
        /// [HDY 요청 - ContentSizeFitter 갱신] memStatText/memStatPanel에 ContentSizeFitter가 붙어있으면,
        /// 텍스트 내용이나 활성 상태가 바뀐 직후 자동으로는 다음 레이아웃 패스가 되어서야 크기를 다시
        /// 계산한다 - 그 사이 한 프레임 동안 옛 크기로 남아있어(특히 정렬 기준을 바꿀 때마다 숫자 자릿수가
        /// 바뀌면) 패널이 텍스트보다 좁거나 넓게 잘못 표시되는 문제가 있었다. 그래서 여기서 강제로 즉시 다시
        /// 계산한다 - memStatText 자신의 크기부터 먼저 갱신한 뒤(자기 텍스트 내용 기준), 그 크기에 의존할 수
        /// 있는 memStatPanel을 그다음에 갱신한다(순서가 바뀌면 패널이 갱신 전 텍스트 크기를 기준으로 계산될
        /// 수 있음). MemSlotUI와 동일한 처리.
        /// </summary>
        private void ApplyStatDisplay(MemStatDisplayInfo statInfo)
        {
            if (memStatPanel != null)
            {
                memStatPanel.SetActive(statInfo.IsVisible);
            }

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

            if (memStatText != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(memStatText.rectTransform);
            }

            if (memStatPanel != null && memStatPanel.transform is RectTransform panelRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            }
        }

        private void HandleClick()
        {
            if (cachedData == null) return;
            OnSlotClicked?.Invoke(cachedData);
        }
    }
}
