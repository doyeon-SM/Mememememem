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

        /// <summary>클릭된 멤(CapturedMemEntry + SO 데이터)을 화면에 표시한다. (멤창고에서 사용) 탐험 스탯은 그 개체의 실제 값.</summary>
        public void ShowInfo(CapturedMemEntry entry, MemData data)
        {
            if (entry == null) return;

            RenderInfo(data, entry.MemId, entry.ExplorationStat.ToString());
        }

        /// <summary>
        /// MemData만으로 정보를 표시한다. (도감에서 사용) 포획된 개체가 없어 탐험 스탯은 단일 값 대신
        /// MemTierTable에서 찾은 해당 등급의 "최소~최대" 범위로 보여준다.
        /// </summary>
        public void ShowInfo(MemData data)
        {
            if (data == null) return;

            RenderInfo(data, data.memId, BuildExplorationRangeText(data));
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

            if(infoCraftingText != null)
            {
                infoCraftingText.text = "제작 : " + data != null ? data.productionStats.crafting.ToString() : "0";
            }
            if(infoLoggingText !=null)
            {
                infoLoggingText.text = "벌목 : " + data != null ? data.productionStats.logging.ToString() : "0";
            }
            if(infoMiningText != null)
            {
                infoMiningText.text = "채광 : " + data != null ? data.productionStats.mining.ToString() : "0";
            }
            if(infoTransportText != null)
            {
                infoTransportText.text = "운반 : " + data != null ? data.productionStats.transport.ToString() : "0";
            }
            if(infoFarmingText != null)
            {
                infoFarmingText.text = "생산 : " + data != null ? data.productionStats.transport.ToString() : "0";
            }
            if (infoExplorationText != null)
            {
                infoExplorationText.text = "탐험 : " + explorationDisplayText;
            }
        }
    }
}
