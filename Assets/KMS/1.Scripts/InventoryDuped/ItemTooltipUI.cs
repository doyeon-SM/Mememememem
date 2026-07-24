using System.Collections.Generic;
using HDY.Item;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{
    public class ItemTooltipUI : MonoBehaviour
    {
        public RectTransform rectTransform;
        public Transform tagParent;
        public TooltipTagUI tagTemplate;
        public Vector2 screenOffset;

        public Color nameBackgroundColor;
        public Color categoryBackgroundColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        public Color effectBackgroundColor = new Color(0.2f, 0.36f, 0.24f, 1f);
        public Color darkTextColor;
        public Color lightTextColor = Color.white;

        // [HDY 요청] ItemStack.itemId(string)로 실제 ItemData를 조회하기 위한 참조.
        [SerializeField] private ItemCatalogManager catalogManager;

        private readonly List<TooltipTagUI> activeTags = new List<TooltipTagUI>();

        private void Awake()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (tagParent == null) tagParent = transform;
            if (tagTemplate != null) tagTemplate.gameObject.SetActive(false);

            catalogManager = ItemCatalogManager.Resolve(catalogManager);

            Hide();
        }

        /// <summary>
        /// [HDY 요청] 슬롯(ItemStack)에는 itemId만 있으므로, 카탈로그에서 ItemData를 조회한 뒤
        /// 기존 Show(ItemData, Vector2)에 그대로 위임한다.
        /// </summary>
        public void Show(ItemStack stack, Vector2 screenPosition)
        {
            if (stack == null || stack.IsEmpty)
            {
                Hide();
                return;
            }

            if (catalogManager == null)
            {
                catalogManager = ItemCatalogManager.Resolve(null);
            }

            var data = catalogManager != null ? catalogManager.FindItemData(stack.itemId) : null;
            Show(data, screenPosition);
        }

        public void Show(ItemData item, Vector2 screenPosition)
        {
            if (item == null || tagTemplate == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            ClearTags();

            CreateTag(item.ItemName, nameBackgroundColor, darkTextColor);
            CreateTag($"종류: {GetCategoryText(item.Category)}", categoryBackgroundColor, lightTextColor);
            CreateEffectTags(item);

            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            Move(screenPosition);
        }

        public void Move(Vector2 screenPosition)
        {
            if (!gameObject.activeSelf || rectTransform == null) return;

            Vector2 targetPosition = screenPosition + screenOffset;
            Vector2 tooltipSize = GetScaledTooltipSize();

            if (targetPosition.x + tooltipSize.x > Screen.width)
            {
                targetPosition.x = screenPosition.x - tooltipSize.x - screenOffset.x;
            }

            if (targetPosition.y - tooltipSize.y < 0f)
            {
                targetPosition.y = screenPosition.y + tooltipSize.y + Mathf.Abs(screenOffset.y);
            }

            targetPosition.x = Mathf.Clamp(targetPosition.x, 0f, Screen.width - tooltipSize.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, tooltipSize.y, Screen.height);

            rectTransform.position = targetPosition;
        }

        public void Hide()
        {
            ClearTags();
            gameObject.SetActive(false);
        }

        private void CreateEffectTags(ItemData item)
        {
            if (item.UseAction != HDY.Item.UseAction.Eat) return;

            if (item.EatEffects == null) return;

            for (int i = 0; i < item.EatEffects.Count; i++)
            {
                ItemEffect effect = item.EatEffects[i];
                if (effect == null || Mathf.Approximately(effect.Value, 0f)) continue;

                string sign = effect.Value > 0f ? "+" : string.Empty;
                CreateTag($"{GetEffectText(effect.Effect)} {sign}{effect.Value:g}", effectBackgroundColor, lightTextColor);
            }
        }

        private void CreateTag(string label, Color backgroundColor, Color textColor)
        {
            TooltipTagUI tag = Instantiate(tagTemplate, tagParent);

            tag.gameObject.SetActive(true);
            tag.Set(null, label, backgroundColor, textColor);

            activeTags.Add(tag);
        }

        private string GetCategoryText(HDY.Item.ItemCategory category)
        {
            switch (category)
            {
                case HDY.Item.ItemCategory.Food:
                    return "음식";
                case HDY.Item.ItemCategory.Material:
                    return "재료";
                case HDY.Item.ItemCategory.Goods:
                    return "재화";
                case HDY.Item.ItemCategory.Capsule:
                    return "캡슐";
                case HDY.Item.ItemCategory.Tool:
                    return "도구";
                default:
                    return category.ToString();
            }
        }

        private string GetEffectText(HDY.Item.EffectType effect)
        {
            switch (effect)
            {
                case HDY.Item.EffectType.Satiety:
                    return "포만감";
                case HDY.Item.EffectType.Speed:
                    return "이동속도";
                default:
                    return effect.ToString();
            }
        }

        private void ClearTags()
        {
            for (int i = 0; i < activeTags.Count; i++)
            {
                if (activeTags[i] != null)
                {
                    Destroy(activeTags[i].gameObject);
                }
            }

            activeTags.Clear();
        }

        private Vector2 GetScaledTooltipSize()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;

            return rectTransform.rect.size * scaleFactor;
        }
    }
}
