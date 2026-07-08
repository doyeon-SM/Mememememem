using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Recipe;

namespace HDY.UI
{
    /// <summary>
    /// 여신상 UI에서 "요구 영지 레벨이 같은 레시피들"을 한 줄(레벨 아이콘 + 그리드)로 표시하는 컴포넌트.
    /// 그리드 부분(slotsParent)에는 Unity의 Grid Layout Group을 붙이고 Constraint를
    /// Fixed Row Count = 2로 설정해두면, 슬롯이 늘어나도 항상 최대 2줄까지만 채우고 가로로 늘어난다
    /// (줄바꿈 계산은 이 스크립트가 하지 않고 Grid Layout Group에 맡긴다).
    /// 슬롯은 매번 Setup()에서 slotPrefab을 필요한 개수만큼 Instantiate해서 채운다(레시피 데이터 기반이라
    /// 미리 배치해두기 어려움 - Mem 창고 그리드와 다른 점).
    /// </summary>
    public class GoddessStatueUI_LevelRow : MonoBehaviour
    {
        [Header("레벨 표시 (텍스트, 아이콘은 선택 사항)")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image levelIconImage; // 순수 장식용. 값에 따라 바뀌지 않음(원하면 직접 커스터마이징)

        [Header("슬롯 배치 (Grid Layout Group의 Constraint를 Fixed Row Count=2로 설정)")]
        [SerializeField] private Transform slotsParent;
        [SerializeField] private GoddessRecipeSlotUI slotPrefab;

        private readonly List<GoddessRecipeSlotUI> slots = new List<GoddessRecipeSlotUI>();

        /// <summary>이 줄 안의 슬롯이 클릭되었을 때 발생. Item_ID를 함께 전달한다.</summary>
        public event Action<string> OnSlotSelected;

        /// <summary>
        /// 이 줄을 특정 요구 레벨과 그 레벨의 레시피 목록으로 채운다. 기존에 있던 슬롯은 전부 지우고 새로 만든다.
        /// </summary>
        /// <param name="requiredLevel">이 줄이 대표하는 요구 영지 레벨</param>
        /// <param name="entries">이 레벨을 요구하는 레시피 항목들</param>
        /// <param name="findItemIcon">Item_ID로 아이콘 스프라이트를 찾는 함수(카탈로그 조회는 상위가 담당)</param>
        /// <param name="canAttemptUnlock">이 항목이 지금 선택(구매 시도) 가능한지 판단하는 함수</param>
        public void Setup(
            int requiredLevel,
            IReadOnlyList<RecipeUnlockEntry> entries,
            Func<string, Sprite> findItemIcon,
            Func<RecipeUnlockEntry, bool> canAttemptUnlock)
        {
            if (levelText != null)
            {
                levelText.text = $"Lv.{requiredLevel}";
            }

            ClearSlots();

            if (entries == null || slotsParent == null || slotPrefab == null) return;

            foreach (var entry in entries)
            {
                var slot = Instantiate(slotPrefab, slotsParent);
                var icon = findItemIcon != null ? findItemIcon(entry.Item_ID) : null;
                bool interactable = canAttemptUnlock != null && canAttemptUnlock(entry);

                slot.SetData(entry.Item_ID, icon, entry.IsUnlocked, interactable);
                slot.OnClicked += HandleSlotClicked;

                slots.Add(slot);
            }
        }

        /// <summary>현재 선택된 Item_ID에 맞게 이 줄의 슬롯들 강조 표시를 갱신한다.</summary>
        public void RefreshSelection(string selectedItemId)
        {
            foreach (var slot in slots)
            {
                slot.SetSelected(slot.ItemId == selectedItemId);
            }
        }

        private void HandleSlotClicked(string itemId)
        {
            OnSlotSelected?.Invoke(itemId);
        }

        private void ClearSlots()
        {
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                slot.OnClicked -= HandleSlotClicked;
                Destroy(slot.gameObject);
            }

            slots.Clear();
        }

        private void OnDestroy()
        {
            ClearSlots();
        }
    }
}
