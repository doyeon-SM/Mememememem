using UnityEngine;
using TMPro;
using MemSystem.Data;
using HDY.Capture;

namespace HDY.UI
{
    /// <summary>
    /// 멤 창고의 정보 패널(그리드 옆 고정 표시) 담당.
    /// 슬롯 클릭 시 MemStorageUI(컨트롤러)가 전달해주는 데이터를 화면에 표시하기만 한다.
    /// </summary>
    public class MemStorageUI_Info : MonoBehaviour
    {
        [Header("정보 패널 (그리드 옆 고정 표시)")]
        [SerializeField] private TMP_Text infoNameText;
        [SerializeField] private TMP_Text infoTierText;
        [SerializeField] private TMP_Text infoCraftingText;
        [SerializeField] private TMP_Text infoLoggingText;
        [SerializeField] private TMP_Text infoMiningText;
        [SerializeField] private TMP_Text infoTransportText;
        [SerializeField] private TMP_Text infoFarmingText;
        [SerializeField] private TMP_Text infoExplorationText;

        /// <summary>클릭된 멤(CapturedMemEntry + SO 데이터)을 화면에 표시한다.</summary>
        public void ShowInfo(CapturedMemEntry entry, MemData data)
        {
            if (entry == null) return;

            if (infoNameText != null)
            {
                infoNameText.text = data != null ? data.memName : entry.MemId;
            }

            if (infoTierText != null)
            {
                infoTierText.text = data != null ? data.tier.ToString() : "-";
            }

            if(infoCraftingText != null)
            {
                infoCraftingText.text = data != null ? data.productionStats.crafting.ToString() : "0";
            }
            if(infoLoggingText !=null)
            {
                infoLoggingText.text = data != null ? data.productionStats.logging.ToString() : "0";
            }
            if(infoMiningText != null)
            {
                infoMiningText.text = data != null ? data.productionStats.mining.ToString() : "0";
            }
            if(infoTransportText != null)
            {
                infoTransportText.text = data != null ? data.productionStats.transport.ToString() : "0";
            }
            if(infoFarmingText != null)
            {
                infoFarmingText.text = data != null ? data.productionStats.transport.ToString() : "0";
            }
            if (infoExplorationText != null)
            {
                infoExplorationText.text = entry.ExplorationStat.ToString();
            }
        }
    }
}
