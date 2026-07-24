using System.Collections.Generic;
using HDY.Forge;
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

        /// <summary>
        /// [HDY 요청] 미리보기 전용 - 이름/종류는 item 그대로 쓰되, 연마 효과 태그는 item.Item_ID로
        /// 조회하지 않고 refinementOverride를 그대로 사용한다. 전승처럼 "아직 실제로 일어나지 않은
        /// 결과"를 미리 보여줘야 할 때 사용한다(예: 대상 도구의 아이콘/강화표시는 유지하면서, 연마
        /// 효과만 재료 도구 것을 보여주는 전승 결과 미리보기).
        /// </summary>
        public void ShowWithRefinementOverride(ItemData item, IReadOnlyList<ForgeRefinementSlotData> refinementOverride, Vector2 screenPosition)
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

            if (refinementOverride != null)
            {
                for (int i = 0; i < refinementOverride.Count; i++)
                {
                    var slot = refinementOverride[i];
                    if (slot == null || string.IsNullOrEmpty(slot.DisplayName)) continue;

                    CreateTag($"{slot.DisplayName}+{slot.Value:0.#}", effectBackgroundColor, lightTextColor);
                }
            }

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

        /// <summary>
        /// [HDY 요청] 음식(Eat) 아이템은 기존처럼 섭취효과 태그를 그대로 보여주고, 그 외 아이템은
        /// 대장간 연마 슬롯이 있으면(도구는 섭취효과가 없어 이 칸이 항상 비어있었음) 같은 태그 UI를
        /// 재사용해서 연마 효과를 대신 보여준다.
        /// </summary>
        private void CreateEffectTags(ItemData item)
        {
            if (item.UseAction == HDY.Item.UseAction.Eat)
            {
                if (item.EatEffects == null) return;

                for (int i = 0; i < item.EatEffects.Count; i++)
                {
                    ItemEffect effect = item.EatEffects[i];
                    if (effect == null || Mathf.Approximately(effect.Value, 0f)) continue;

                    string sign = effect.Value > 0f ? "+" : string.Empty;
                    CreateTag($"{GetEffectText(effect.Effect)} {sign}{effect.Value:g}", effectBackgroundColor, lightTextColor);
                }

                return;
            }

            CreateRefinementEffectTags(item);
        }

        /// <summary>
        /// [HDY 요청] Item_ID가 대장간 합성 ID("BaseItemId@InstanceId")면 연마 슬롯을 조회해서
        /// 슬롯 하나당 태그 하나("표시명+수치")로 보여준다(여러 칸이면 세로로 여러 줄 쌓임).
        /// ForgeInstanceRegistry를 직접 조회만 하는 순수 읽기라 부수효과가 없다.
        /// </summary>
        private void CreateRefinementEffectTags(ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.Item_ID)) return;
            if (!ForgeInstanceRegistry.TryParseCompositeId(item.Item_ID, out _, out var instanceId)) return;

            var registry = ForgeInstanceRegistry.Instance;
            var instance = registry != null ? registry.GetInstance(instanceId) : null;
            if (instance?.RefinementSlots == null) return;

            foreach (var slot in instance.RefinementSlots)
            {
                if (slot == null || string.IsNullOrEmpty(slot.DisplayName)) continue;

                CreateTag($"{slot.DisplayName}+{slot.Value:0.#}", effectBackgroundColor, lightTextColor);
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
