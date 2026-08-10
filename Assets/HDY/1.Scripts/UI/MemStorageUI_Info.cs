using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MemSystem.Data;
using HDY.Capture;
using HDY.Mem;

namespace HDY.UI
{
    /// <summary>
    /// 멤 정보 패널(그리드 옆 고정 표시) 담당.
    /// 멤창고(MemStorageUI)와 도감(MemDexUI) 둘 다에서 재사용한다.
    /// - 창고: 포획된 개체(CapturedMemEntry)가 있어 ShowInfo(entry, data) 사용, 탐험 스탯은 그 개체의 실제 값(단일 숫자).
    /// - 도감: 포획된 개체가 없어 ShowInfo(data, firstCapturedTimestamp) 사용. MemData.explorationStat 하나만으로는
    ///   개체별 실제 범위를 반영하지 못하므로, MemTierTable에서 해당 등급의 explorationMin~explorationMax 범위를
    ///   찾아 "20~100" 형식으로 보여준다(테이블/스펙이 없으면 MemData의 단일 값으로 대체).
    /// 두 오버로드 모두 내부적으로 RenderInfo로 렌더링을 위임한다.
    ///
    /// [HDY 요청 - 스탯 표시 구조] 제작/벌목/채광/이동/생산/탐험 6개 스탯 전부 스탯당 아이콘(Image) +
    /// 이름(TMP_Text, "제작" 등 고정 라벨) + 값(TMP_Text) 이렇게 3개 오브젝트로 표시한다(SetStatRow).
    /// 값이 1 이상이면 3개 전부 알파 1(불투명), 값이 0이거나 데이터가 없어 "??"로 표시될 때는 3개 전부
    /// 알파를 dimmedStatAlpha(기본 0.7)로 낮춰 "의미 없는 스탯"임을 시각적으로 구분한다. 탐험은 값 텍스트에
    /// 표시되는 문자열(단일 값 또는 도감의 범위)과 알파 판단 기준값이 서로 달라서(범위 텍스트는 알파 판단에
    /// 쓸 단일 숫자가 없음) SetStatRow의 문자열 오버로드를 쓴다 - 판단 기준값은 창고(ShowInfo(entry, data))
    /// 에서는 entry.ExplorationStat, 도감(ShowInfo(data, timestamp))에서는 data.explorationStat을 사용한다.
    ///
    /// [HDY 요청 - 데이터 없을 때 "??" 표시] 아직 아무 멤도 선택되지 않은 최초 상태(또는 이후 선택이
    /// 해제된 상태), 혹은 멤 데이터(SO)가 아직 입력되지 않은 경우에는 이름/티어/스탯 관련 텍스트들을
    /// 비활성화하지 않고 계속 켜둔 채로 값만 "??"(생산 스탯 값 텍스트는 "??")로 표시한다. 예전에는 이 상태에서
    /// 텍스트 오브젝트 자체를 꺼버렸는데, 그러면 "정보가 없다"는 사실이 빈 화면으로만 보여 구분이 안 됐다.
    /// 아이콘만 예외적으로 계속 숨긴다("??"로 대신할 만한 이미지가 없어서다).
    ///
    /// [HDY 요청 - 해상도 분리] 상세정보 패널이라 512px(GetIcon512)을 사용한다. 도감 슬롯(MemDexSlotUI)은
    /// 128px, 창고 그리드 슬롯(MemSlotUI)은 64px을 쓰는 것과 구분된다.
    ///
    /// [HDY 요청 - 등급 가시성] 등급 텍스트(infoTierText)만으로는 한눈에 구분하기 어려워서, 등급별 색상을
    /// 텍스트 색에도 적용하고 별도의 이미지(infoTierGradeImage, 모양은 인스펙터에서 지정)도 같은 색으로
    /// 함께 보여준다. 5개 등급(Rare/Epic/Unique/Legendary/Mythic) 색상은 인스펙터에서 각각 할당한다.
    /// 데이터가 없는 "??" 상태에서는 텍스트는 검정(Color.black)으로, 이미지는 비활성화로 되돌린다.
    ///
    /// [HDY 요청 - 최초 포획 정보, 도감 전용] infoFirstCapturedText는 도감(ShowInfo(MemData, long?))에서만
    /// 채워지고 보여진다 - 창고(ShowInfo(entry, data))에서는 이 줄 자체를 항상 숨긴다. 같은 패널을 두 화면이
    /// 공유하지만, 최초 포획 정보는 도감에서만 의미가 있다고 판단해서 화면(호출하는 오버로드)에 따라
    /// 켜고 끄는 방식으로 분기했다.
    /// </summary>
    public class MemStorageUI_Info : MonoBehaviour
    {
        [Header("데이터 참조")]
        [Tooltip("등급별 탐험 스탯 범위(최소~최대) 조회용. 도감(ShowInfo(MemData, long?))에서 범위 표시에 사용한다.")]
        [SerializeField] private MemTierTable tierTable;

        [Header("정보 패널 (그리드 옆 고정 표시)")]
        [SerializeField] private Image infoIconImage;
        [SerializeField] private TMP_Text infoNameText;
        [SerializeField] private TMP_Text infoTierText;

        [Header("스탯 표시 (제작/벌목/채광/이동/생산/탐험 - 스탯당 아이콘+이름+값 3개, HDY 요청)")]
        [Tooltip("값이 1 이상일 때만 알파 1(불투명), 그 외(값 0 또는 데이터 없음 \"??\")에는 알파 dimmedStatAlpha로 낮춰서 표시된다. 아이콘/이름/값 3개가 함께 알파 처리된다.")]
        [SerializeField] private Image infoCraftingIcon;
        [SerializeField] private TMP_Text infoCraftingNameText;
        [SerializeField] private TMP_Text infoCraftingValueText;
        [SerializeField] private Image infoLoggingIcon;
        [SerializeField] private TMP_Text infoLoggingNameText;
        [SerializeField] private TMP_Text infoLoggingValueText;
        [SerializeField] private Image infoMiningIcon;
        [SerializeField] private TMP_Text infoMiningNameText;
        [SerializeField] private TMP_Text infoMiningValueText;
        [SerializeField] private Image infoTransportIcon;
        [SerializeField] private TMP_Text infoTransportNameText;
        [SerializeField] private TMP_Text infoTransportValueText;
        [SerializeField] private Image infoFarmingIcon;
        [SerializeField] private TMP_Text infoFarmingNameText;
        [SerializeField] private TMP_Text infoFarmingValueText;
        [SerializeField] private Image infoExplorationIcon;
        [SerializeField] private TMP_Text infoExplorationNameText;
        [SerializeField] private TMP_Text infoExplorationValueText;
        [SerializeField] private float dimmedStatAlpha = 0.7f;

        [Header("등급(Tier) 가시성 강조 - 색상 + 이미지 (HDY 요청)")]
        [Tooltip("등급에 따라 색만 바뀌는 이미지. 모양/스프라이트는 인스펙터에서 직접 지정하고, 코드는 이 Image의 color만 등급별로 교체한다. 데이터가 없는 \"??\" 상태에서는 비활성화된다.")]
        [SerializeField] private Image infoTierGradeImage;
        [SerializeField] private Color rareTierColor = Color.white;
        [SerializeField] private Color epicTierColor = Color.white;
        [SerializeField] private Color uniqueTierColor = Color.white;
        [SerializeField] private Color legendaryTierColor = Color.white;
        [SerializeField] private Color mythicTierColor = Color.white;

        [Header("최초 포획 정보 (도감 전용 - 창고에서는 항상 숨김)")]
        [Tooltip("도감에서 멤을 선택했을 때 최초 포획 날짜+시간을 표시하는 텍스트. 창고 쪽 ShowInfo(entry, data)에서는 사용하지 않고 항상 숨긴다.")]
        [SerializeField] private TMP_Text infoFirstCapturedText;

        private void Awake()
        {
            // 아직 ShowInfo가 한 번도 호출되지 않은 최초 상태 - 아이콘은 숨기고, 이름/티어/스탯 텍스트는
            // 켠 채로 "??"를 보여준다. 최초 포획 정보 줄은 기본적으로 숨김(도감에서 ShowInfo(MemData, long?)를
            // 호출할 때만 켜진다).
            HideInfo();
        }

        /// <summary>클릭된 멤(CapturedMemEntry + SO 데이터)을 화면에 표시한다. (멤창고에서 사용) 탐험 스탯은 그 개체의 실제 값.</summary>
        public void ShowInfo(CapturedMemEntry entry, MemData data)
        {
            if (entry == null)
            {
                HideInfo();
                return;
            }

            RenderInfo(data, data != null ? entry.ExplorationStat.ToString() : null, data != null ? entry.ExplorationStat : (int?)null);
            ApplyIcon(data, true); // 창고에 있는 개체는 이미 포획된 것이므로 항상 원래 색(실루엣 아님)

            // [HDY 요청 - 최초 포획 정보는 도감 전용] 창고 화면에서는 이 줄을 항상 숨긴다.
            SetFirstCapturedVisible(false, null);
        }

        /// <summary>
        /// MemData만으로 정보를 표시한다. (도감에서 사용) 포획된 개체가 없어 탐험 스탯은 단일 값 대신
        /// MemTierTable에서 찾은 해당 등급의 "최소~최대" 범위로 보여준다.
        /// </summary>
        /// <param name="firstCapturedTimestamp">
        /// [HDY 요청 - 최초 포획 정보] 이 멤 종의 최초 포획 시각(UTC Unix, 초). MemDexRecordManager에
        /// 기록이 없으면(아직 발견되지 않았으면) null을 넘기면 되고, 이 경우 "최초 포획: ??"로 표시된다.
        /// </param>
        public void ShowInfo(MemData data, long? firstCapturedTimestamp = null)
        {
            if (data == null)
            {
                HideInfo();
                return;
            }

            // [HDY 요청 - 미발견 실루엣] 최초 포획 정보(firstCapturedTimestamp)가 없으면 아직 한 번도
            // 포획한 적 없는 종이므로, 이름/티어/스탯 정보는 노출하지 않고("??") 아이콘만 도감 그리드
            // (MemDexSlotUI)와 동일하게 검은 실루엣으로 보여준다(스포일러 방지).
            bool isDiscovered = firstCapturedTimestamp.HasValue;

            RenderInfo(isDiscovered ? data : null, isDiscovered ? BuildExplorationRangeText(data) : null, isDiscovered ? data.explorationStat : (int?)null);
            ApplyIcon(data, isDiscovered);

            SetFirstCapturedVisible(true, firstCapturedTimestamp);
        }

        /// <summary>표시할 정보가 없을 때(최초 상태, 선택 해제, 혹은 멤 데이터 미입력) 아이콘만 숨기고
        /// 텍스트들은 "??" 상태로 렌더링한다. 최초 포획 정보 줄도 함께 숨긴다.</summary>
        private void HideInfo()
        {
            RenderInfo(null, null, null);
            ApplyIcon(null, true);
            SetFirstCapturedVisible(false, null);
        }

        /// <summary>MemTierTable에서 이 멤 등급의 탐험 스탯 범위를 찾아 "최소~최대" 형식으로 반환한다. 테이블/스펙이 없으면 MemData의 단일 값으로 대체(경고 로그 남김).</summary>
        private string BuildExplorationRangeText(MemData data)
        {
            var spec = tierTable != null ? tierTable.GetSpec(data.tier) : null;

            if (spec != null)
            {
                return $"{spec.explorationMin}~{spec.explorationMax}";
            }

            Debug.LogWarning($"[MemStorageUI_Info] MemTierTable에서 '{data.tier}' 등급 스펙을 찾을 수 없어 MemData의 단일 값으로 대체합니다.", this);
            return data.explorationStat.ToString();
        }

        /// <summary>[HDY 요청 - 등급 가시성] 등급(Tier)에 대응하는 색상을 인스펙터에 할당된 값에서 찾아 반환한다.</summary>
        private Color GetTierColor(MemTier tier)
        {
            switch (tier)
            {
                case MemTier.Rare: return rareTierColor;
                case MemTier.Epic: return epicTierColor;
                case MemTier.Unique: return uniqueTierColor;
                case MemTier.Legendary: return legendaryTierColor;
                case MemTier.Mythic: return mythicTierColor;
                default: return Color.white;
            }
        }

        /// <summary>
        /// [HDY 요청 - 스탯 가시성] 생산 스탯 한 줄(아이콘+이름+값)을 렌더링한다. value가 1 이상이면 알파
        /// 1(불투명)로, 그 외(값이 0이거나 데이터가 없어 value가 null인 "??" 상태)에는 알파를
        /// dimmedStatAlpha로 낮춰서 "지금 의미 있는 스탯이 아니다"를 시각적으로 표시한다. 아이콘/이름/값
        /// 3개 오브젝트가 함께 알파 처리된다. 이름(label)은 고정 문구라 항상 그대로 표시한다.
        /// </summary>
        private void SetStatRow(Image icon, TMP_Text nameText, TMP_Text valueText, string label, int? value)
        {
            SetStatRow(icon, nameText, valueText, label, value.HasValue ? value.Value.ToString() : null, value);
        }

        /// <summary>
        /// [HDY 요청 - 스탯 가시성] 위 오버로드와 동일하지만, 화면에 표시할 문자열(displayValueText)과 알파
        /// 판단에 쓸 대표 숫자값(rawValueForAlpha)을 따로 받는다. 탐험(Exploration) 스탯처럼 화면 표시는
        /// "20~100" 같은 범위 문자열이지만 알파 판단은 별도의 단일 숫자로 해야 하는 경우에 쓴다.
        /// </summary>
        private void SetStatRow(Image icon, TMP_Text nameText, TMP_Text valueText, string label, string displayValueText, int? rawValueForAlpha)
        {
            float alpha = (rawValueForAlpha.HasValue && rawValueForAlpha.Value >= 1) ? 1f : dimmedStatAlpha;

            if (nameText != null)
            {
                nameText.gameObject.SetActive(true);
                nameText.text = label;
                SetTextAlpha(nameText, alpha);
            }

            if (valueText != null)
            {
                valueText.gameObject.SetActive(true);
                valueText.text = displayValueText ?? "??";
                SetTextAlpha(valueText, alpha);
            }

            if (icon != null)
            {
                SetImageAlpha(icon, alpha);
            }
        }

        /// <summary>TMP_Text의 색은 그대로 두고 알파(투명도)만 바꾼다.</summary>
        private static void SetTextAlpha(TMP_Text text, float alpha)
        {
            var color = text.color;
            color.a = alpha;
            text.color = color;
        }

        /// <summary>Image의 색은 그대로 두고 알파(투명도)만 바꾼다.</summary>
        private static void SetImageAlpha(Image image, float alpha)
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        /// <summary>
        /// [HDY 요청 - 최초 포획 정보, 도감 전용] infoFirstCapturedText를 켜고 끈다. show가 false면(창고
        /// 화면이거나 선택 해제 상태) 줄 자체를 숨긴다. show가 true면(도감 화면) timestamp가 있으면
        /// 로컬 시간 기준 "yyyy-MM-dd HH:mm" 형식으로, 없으면(미발견) "최초 포획: ??"로 표시한다.
        /// </summary>
        private void SetFirstCapturedVisible(bool show, long? timestamp)
        {
            if (infoFirstCapturedText == null) return;

            infoFirstCapturedText.gameObject.SetActive(show);
            if (!show) return;

            if (timestamp.HasValue)
            {
                var localTime = DateTimeOffset.FromUnixTimeSeconds(timestamp.Value).ToLocalTime();
                infoFirstCapturedText.text = $"최초 포획: {localTime:yyyy-MM-dd HH:mm}";
            }
            else
            {
                infoFirstCapturedText.text = "최초 포획: ??";
            }
        }

        /// <summary>
        /// 실제 텍스트/아이콘 렌더링. data가 null이면 아이콘은 숨기고 이름/티어/스탯 값은 전부 "??"로 표시한다.
        /// explorationDisplayText는 탐험 스탯 줄에 그대로 붙일 문자열(단일 값 또는 범위) - data가 null이면 무시되고 "??"로 대체된다.
        /// explorationRawValue는 탐험 스탯 알파 판단에만 쓰는 대표 숫자값(1 이상이면 알파 1, 아니면 dimmedStatAlpha) -
        /// 표시 텍스트(explorationDisplayText)와 별개다(도감의 범위 텍스트에는 알파 판단에 쓸 단일 숫자가 없어서 분리했다).
        /// </summary>
        private void RenderInfo(MemData data, string explorationDisplayText, int? explorationRawValue)
        {
            if (infoNameText != null)
            {
                infoNameText.gameObject.SetActive(true);
                infoNameText.text = data != null ? data.memName : "??";
            }

            if (infoTierText != null)
            {
                infoTierText.gameObject.SetActive(true);
                infoTierText.text = data != null ? data.tier.ToString() : "??";
            }

            // [HDY 요청 - 등급 가시성] 텍스트와 함께 등급 색을 보여주는 이미지. 모양/스프라이트는 인스펙터에서 지정하고
            // 여기서는 색만 등급에 맞게 바꾼다. 데이터가 없으면("??") 숨긴다.
            if (infoTierGradeImage != null)
            {
                if (data != null)
                {
                    infoTierGradeImage.gameObject.SetActive(true);
                    infoTierGradeImage.color = GetTierColor(data.tier);
                }
                else
                {
                    infoTierGradeImage.gameObject.SetActive(false);
                }
            }

            // [HDY 요청 - 스탯 가시성] 아이콘+이름+값 3개를 한 번에 그리고, 값이 1 미만(0 또는 "??")이면
            // 3개 전부 알파를 낮춘다(SetStatRow 참고).
            SetStatRow(infoCraftingIcon, infoCraftingNameText, infoCraftingValueText, "제작", data != null ? data.productionStats.crafting : (int?)null);
            SetStatRow(infoLoggingIcon, infoLoggingNameText, infoLoggingValueText, "벌목", data != null ? data.productionStats.logging : (int?)null);
            SetStatRow(infoMiningIcon, infoMiningNameText, infoMiningValueText, "채광", data != null ? data.productionStats.mining : (int?)null);
            SetStatRow(infoTransportIcon, infoTransportNameText, infoTransportValueText, "이동", data != null ? data.productionStats.transport : (int?)null);
            SetStatRow(infoFarmingIcon, infoFarmingNameText, infoFarmingValueText, "생산", data != null ? data.productionStats.farming : (int?)null);

            SetStatRow(infoExplorationIcon, infoExplorationNameText, infoExplorationValueText, "탐험", data != null ? explorationDisplayText : null, explorationRawValue);
        }

        /// <summary>
        /// [HDY 요청 - 미발견 실루엣] 아이콘을 표시한다. MemIconRenderer가 modelPrefab을 촬영해서 만든 아이콘을
        /// memId로 조회한다(없으면 감춤). isDiscovered가 false면(도감에서 최초 포획 정보가 없는 경우) 도감
        /// 그리드(MemDexSlotUI)와 동일하게 색만 검정으로 덮어씌워 실루엣처럼 보이게 한다 - 스프라이트(모양)
        /// 자체는 그대로 두고 Image.color만 바꾸는 방식이라 별도의 실루엣 전용 스프라이트가 필요 없다.
        /// [HDY 요청 - 해상도 분리] 상세정보 패널이라 512px(GetIcon512)을 사용한다.
        /// </summary>
        private void ApplyIcon(MemData data, bool isDiscovered)
        {
            if (infoIconImage == null) return;

            var sprite = (data != null && MemIconRenderer.Instance != null)
                ? MemIconRenderer.Instance.GetIcon512(data.memId)
                : null;

            infoIconImage.sprite = sprite;
            infoIconImage.gameObject.SetActive(sprite != null);
            infoIconImage.color = isDiscovered ? Color.white : Color.black;
        }
    }
}
