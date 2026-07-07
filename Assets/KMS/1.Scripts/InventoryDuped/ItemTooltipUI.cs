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
        public Color darkTextColor;

        private readonly List<TooltipTagUI> activeTags = new List<TooltipTagUI>();

        private void Awake()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (tagParent == null) tagParent = transform;
            if (tagTemplate != null) tagTemplate.gameObject.SetActive(false);

            Hide();
        }

        public void Show(ItemStack stack, Vector2 screenPosition)
        {
            if (stack == null || stack.IsEmpty)
            {
                Hide();
                return;
            }

            Show(stack.item, screenPosition);
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

            CreateTag(item.ItemName);

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

        private void CreateTag(string label)
        {
            TooltipTagUI tag = Instantiate(tagTemplate, tagParent);

            tag.gameObject.SetActive(true);
            tag.Set(null, label, nameBackgroundColor, darkTextColor);

            activeTags.Add(tag);
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
