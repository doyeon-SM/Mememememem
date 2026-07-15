using UnityEngine;
using TMPro;
using HDY.Territory;

namespace HDY.UI
{
    /// <summary>
    /// 영지 골드(TerritoryData.Gold)를 HUD에 텍스트로 표시하는 컴포넌트.
    /// "Gold: 000 " 형식으로 표시하며, 골드 값이 실제로 바뀐 프레임에만 텍스트를 다시 대입한다
    /// (KMS PlayerHUD.SetGoldText와 동일한 방식 - 매 프레임 불필요한 Text 대입을 피함).
    /// TerritoryData에는 골드 변경 이벤트가 없어서 Update()에서 매 프레임 값을 직접 확인한다.
    /// TerritoryData가 싱글톤(DontDestroyOnLoad)이라 TerritoryData.Resolve(existing)로 안전하게 참조를 받는다.
    /// </summary>
    public class TerritoryHUDManager : MonoBehaviour
    {
        [Header("데이터 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private TerritoryData territoryData;

        [Header("골드 텍스트 연결")]
        [Tooltip("\"Gold: 000 \" 형식으로 표시할 TMP_Text.")]
        [SerializeField] private TMP_Text goldText;

        private int lastDisplayedGold = int.MinValue;
        private bool hasDisplayedGold;

        private void Awake()
        {
            territoryData = TerritoryData.Resolve(territoryData);

            if (territoryData == null) Debug.LogWarning("[TerritoryHUDManager] TerritoryData를 찾을 수 없습니다. 골드가 표시되지 않습니다.", this);
            if (goldText == null) Debug.LogWarning("[TerritoryHUDManager] goldText가 비어있습니다.", this);
        }

        private void Update()
        {
            if (territoryData == null)
            {
                territoryData = TerritoryData.Resolve(territoryData);
                if (territoryData == null) return;
            }

            RefreshGoldText(territoryData.Gold);
        }

        /// <summary>골드 값이 실제로 바뀐 경우에만 텍스트를 다시 대입한다.</summary>
        private void RefreshGoldText(int gold)
        {
            if (goldText == null || (hasDisplayedGold && gold == lastDisplayedGold)) return;

            lastDisplayedGold = gold;
            hasDisplayedGold = true;
            goldText.text = $"Gold: {gold} ";
        }
    }
}
