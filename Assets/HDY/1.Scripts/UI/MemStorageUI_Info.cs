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
    /// - 도감: 포획된 개체가 없어 ShowInfo(data) 사용. MemData.explorationStat 하나만으로는 개체별 실제 범위를
    ///   반영하지 못하므로, MemTierTable에서 해당 등급의 explorationMin~explorationMax 범위를 찾아 "20~100"
    ///   형식으로 보여준다(테이블/스펙이 없으면 MemData의 단일 값으로 대체).
    /// 두 오버로드 모두 내부적으로 RenderInfo로 렌더링을 위임한다.
    ///
    /// [스탯 표시 = "이름: 숫자"] 제작/벌목/채광/이동/생산 5개 생산 스탯은 각각 "제작: 3"처럼 라벨과
    /// 숫자를 함께 표시한다. 예전에는 "라벨 : " + data != null ? ... : "0" 형태로 쓰여있었는데, 문자열
    /// 이어붙이기(+)가 !=보다 먼저 계산되는 C# 연산자 우선순위 때문에 실제로는
    /// ("라벨 : " + data) != null이 되어(문자열 이어붙이기 결과는 항상 null이 아니므로) 조건이 항상
    /// 참으로 평가되고, 그 결과 라벨 문구는 버려진 채 숫자만 표시되고 있었다(생산 줄은 추가로 farming이
    /// 아니라 transport를 잘못 참조하는 복사-붙여넣기 실수도 있었음). 이번에 두 문제 모두 고쳤다.
    ///
    /// [HDY 요청 - 데이터 없을 때 "??" 표시] 아직 아무 멤도 선택되지 않은 최초 상태(또는 이후 선택이
    /// 해제된 상태), 혹은 멤 데이터(SO)가 아직 입력되지 않은 경우에는 이름/티어/스탯 6종 텍스트를
    /// 비활성화하지 않고 계속 켜둔 채로 값만 "??"(스탯은 "라벨: ??")로 표시한다. 예전에는 이 상태에서
    /// 텍스트 오브젝트 자체를 꺼버렸는데, 그러면 "정보가 없다"는 사실이 빈 화면으로만 보여 구분이 안 됐다.
    /// 아이콘만 예외적으로 계속 숨긴다("??"로 대신할 만한 이미지가 없어서다).
    /// </summary>
    public class MemStorageUI_Info : MonoBehaviour
    {
        [Header("데이터 참조")]
        [Tooltip("등급별 탐험 스탯 범위(최소~최대) 조회용. 도감(ShowInfo(MemData))에서 범위 표시에 사용한다.")]
        [SerializeField] private MemTierTable tierTable;

        [Header("정보 패널 (그리드 옆 고정 표시)")]
        [SerializeField] private Image infoIconImage;
        [SerializeField] private TMP_Text infoNameText;
        [SerializeField] private TMP_Text infoTierText;
        [SerializeField] private TMP_Text infoCraftingText;
        [SerializeField] private TMP_Text infoLoggingText;
        [SerializeField] private TMP_Text infoMiningText;
        [SerializeField] private TMP_Text infoTransportText;
        [SerializeField] private TMP_Text infoFarmingText;
        [SerializeField] private TMP_Text infoExplorationText;

        private void Awake()
        {
            // 아직 ShowInfo가 한 번도 호출되지 않은 최초 상태 - 아이콘은 숨기고, 이름/티어/스탯 텍스트는
            // 켠 채로 "??"를 보여준다.
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

            RenderInfo(data, data != null ? entry.ExplorationStat.ToString() : null);
        }

        /// <summary>
        /// MemData만으로 정보를 표시한다. (도감에서 사용) 포획된 개체가 없어 탐험 스탯은 단일 값 대신
        /// MemTierTable에서 찾은 해당 등급의 "최소~최대" 범위로 보여준다.
        /// </summary>
        public void ShowInfo(MemData data)
        {
            if (data == null)
            {
                HideInfo();
                return;
            }

            RenderInfo(data, BuildExplorationRangeText(data));
        }

        /// <summary>표시할 정보가 없을 때(최초 상태, 선택 해제, 혹은 멤 데이터 미입력) 아이콘만 숨기고
        /// 텍스트들은 "??" 상태로 렌더링한다.</summary>
        private void HideInfo()
        {
            RenderInfo(null, null);
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

        /// <summary>
        /// 실제 텍스트/아이콘 렌더링. data가 null이면 아이콘은 숨기고 이름/티어/스탯 값은 전부 "??"로 표시한다.
        /// explorationDisplayText는 탐험 스탯 줄에 그대로 붙일 문자열(단일 값 또는 범위) - data가 null이면 무시되고 "??"로 대체된다.
        /// </summary>
        private void RenderInfo(MemData data, string explorationDisplayText)
        {
            if (infoIconImage != null)
            {
                // MemIconRenderer가 modelPrefab을 촬영해서 만든 아이콘을 memId로 조회한다(없으면 감춤).
                var sprite = (data != null && MemIconRenderer.Instance != null)
                    ? MemIconRenderer.Instance.GetIcon(data.memId)
                    : null;

                infoIconImage.sprite = sprite;
                infoIconImage.gameObject.SetActive(sprite != null);
            }

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

            if (infoCraftingText != null)
            {
                infoCraftingText.gameObject.SetActive(true);
                infoCraftingText.text = data != null ? $"제작: {data.productionStats.crafting}" : "제작: ??";
            }

            if (infoLoggingText != null)
            {
                infoLoggingText.gameObject.SetActive(true);
                infoLoggingText.text = data != null ? $"벌목: {data.productionStats.logging}" : "벌목: ??";
            }

            if (infoMiningText != null)
            {
                infoMiningText.gameObject.SetActive(true);
                infoMiningText.text = data != null ? $"채광: {data.productionStats.mining}" : "채광: ??";
            }

            if (infoTransportText != null)
            {
                infoTransportText.gameObject.SetActive(true);
                infoTransportText.text = data != null ? $"이동: {data.productionStats.transport}" : "이동: ??";
            }

            if (infoFarmingText != null)
            {
                infoFarmingText.gameObject.SetActive(true);
                infoFarmingText.text = data != null ? $"생산: {data.productionStats.farming}" : "생산: ??";
            }

            if (infoExplorationText != null)
            {
                infoExplorationText.gameObject.SetActive(true);
                infoExplorationText.text = "탐험 : " + (data != null ? explorationDisplayText : "??");
            }
        }
    }
}
