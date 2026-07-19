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
    /// [정보 없을 때 개별 요소 비활성화] 아직 아무 멤도 선택되지 않은 최초 상태(또는 이후 선택이 해제된
    /// 상태)에는 패널 전체가 아니라, 데이터가 실제로 표시되는 아이콘/텍스트 각각의 GameObject만
    /// 비활성화한다(패널 배경, 테두리 등 레이아웃 요소는 그대로 유지). Awake()에서 우선 모두 비활성화해두고,
    /// ShowInfo가 유효한 entry/data와 함께 호출될 때만 다시 활성화한다. 아이콘은 sprite 유무에 따라
    /// RenderInfo 안에서 한 번 더 개별 판단한다(모델은 있는데 아이콘 렌더링에 실패한 경우 등).
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
            // 아직 ShowInfo가 한 번도 호출되지 않은 최초 상태 - 보여줄 정보가 없으므로 데이터 표시용
            // 아이콘/텍스트 요소들만 감춰둔다(패널 자체는 그대로 유지).
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

            SetElementsActive(true);
            RenderInfo(data, entry.MemId, entry.ExplorationStat.ToString());
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

            SetElementsActive(true);
            RenderInfo(data, data.memId, BuildExplorationRangeText(data));
        }

        /// <summary>표시할 정보가 없을 때(최초 상태, 또는 선택 해제) 데이터 표시용 아이콘/텍스트 요소들만 비활성화한다.</summary>
        private void HideInfo()
        {
            SetElementsActive(false);
        }

        /// <summary>아이콘/텍스트 요소 전체의 활성 상태를 한 번에 바꾼다. 아이콘은 sprite 유무에 따라
        /// RenderInfo에서 다시 한 번 개별 판단해 덮어쓴다(예: sprite가 없으면 활성화 후에도 다시 감춤).</summary>
        private void SetElementsActive(bool active)
        {
            if (infoIconImage != null) infoIconImage.gameObject.SetActive(active);
            if (infoNameText != null) infoNameText.gameObject.SetActive(active);
            if (infoTierText != null) infoTierText.gameObject.SetActive(active);
            if (infoCraftingText != null) infoCraftingText.gameObject.SetActive(active);
            if (infoLoggingText != null) infoLoggingText.gameObject.SetActive(active);
            if (infoMiningText != null) infoMiningText.gameObject.SetActive(active);
            if (infoTransportText != null) infoTransportText.gameObject.SetActive(active);
            if (infoFarmingText != null) infoFarmingText.gameObject.SetActive(active);
            if (infoExplorationText != null) infoExplorationText.gameObject.SetActive(active);
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

        /// <summary>실제 텍스트/아이콘 렌더링. fallbackName은 data가 없을 때 이름 대신 표시할 값(memId), explorationDisplayText는 탐험 스탯 줄에 그대로 붙일 문자열(단일 값 또는 범위).</summary>
        private void RenderInfo(MemData data, string fallbackName, string explorationDisplayText)
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
                infoNameText.text = data != null ? data.memName : fallbackName;
            }

            if (infoTierText != null)
            {
                infoTierText.text = data != null ? data.tier.ToString() : "-";
            }

            if (infoCraftingText != null)
            {
                infoCraftingText.text = data != null ? $"제작: {data.productionStats.crafting}" : "제작: 0";
            }

            if (infoLoggingText != null)
            {
                infoLoggingText.text = data != null ? $"벌목: {data.productionStats.logging}" : "벌목: 0";
            }

            if (infoMiningText != null)
            {
                infoMiningText.text = data != null ? $"채광: {data.productionStats.mining}" : "채광: 0";
            }

            if (infoTransportText != null)
            {
                infoTransportText.text = data != null ? $"이동: {data.productionStats.transport}" : "이동: 0";
            }

            if (infoFarmingText != null)
            {
                infoFarmingText.text = data != null ? $"생산: {data.productionStats.farming}" : "생산: 0";
            }

            if (infoExplorationText != null)
            {
                infoExplorationText.text = "탐험 : " + explorationDisplayText;
            }
        }
    }
}
