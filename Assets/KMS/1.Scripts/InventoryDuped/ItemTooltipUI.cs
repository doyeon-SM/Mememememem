using System.Collections.Generic;
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

    public Sprite hpIcon;
    public Sprite hungerIcon;
    public Sprite thirstIcon;
    public Sprite mentalIcon;
    public Sprite damageIcon;
    public Sprite staminaIcon;

    public Color nameBackgroundColor;
    public Color categoryBackgroundColor;
    public Color actionBackgroundColor;

    public Color hpBackgroundColor;
    public Color hungerBackgroundColor;
    public Color thirstBackgroundColor;
    public Color mentalBackgroundColor;
    public Color damageBackgroundColor;
    public Color staminaBackgroundColor;

    public Color darkTextColor;
    public Color lightTextColor;

    private readonly List<TooltipTagUI> activeTags = new List<TooltipTagUI>();

    // 필요한 참조를 보정하고 템플릿 태그를 숨김
    private void Awake()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (tagParent == null) tagParent = transform;
        if (tagTemplate != null) tagTemplate.gameObject.SetActive(false);

        Hide();
    }

    // 아이템 스택 정보를 기준으로 툴팁을 표시
    public void Show(ItemStack stack, Vector2 screenPosition)
    {
        if (stack == null || stack.IsEmpty)
        {
            Hide();
            return;
        }

        Show(stack.item, screenPosition);
    }

    // 아이템 정의 정보를 기준으로 태그를 생성하고 툴팁을 표시
    public void Show(ItemDefinition item, Vector2 screenPosition)
    {
        if (item == null || tagTemplate == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        ClearTags();

        CreateTag(null, item.displayName, nameBackgroundColor, darkTextColor);
        CreateTag(null, GetCategoryText(item.category), categoryBackgroundColor, lightTextColor);

        string useActionText = GetUseActionText(item.useAction);

        if (!string.IsNullOrEmpty(useActionText))
        {
            CreateTag(null, useActionText, actionBackgroundColor, lightTextColor);
        }

        CreateEffectTags(item);

        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        
        Move(screenPosition);
    }

    // 마우스 위치를 기준으로 툴팁 위치를 갱신
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

    // 툴팁을 숨기고 생성된 태그를 제거
    public void Hide()
    {
        ClearTags();
        gameObject.SetActive(false);
    }

    // 효과 목록을 태그로 생성
    private void CreateEffectTags(ItemDefinition item)
    {
        if (item.effects == null) return;

        for (int i = 0; i < item.effects.Count; i++)
        {
            ItemEffectData effect = item.effects[i];

            if (effect == null || effect.amount == 0) continue;
            if(effect.isOverTime)
            {
                CreateTag(
                GetEffectIcon(effect.type),
                (GetEffectText(effect)+"/"+ (effect.tickInterval == 1 ? "s" : effect.tickInterval +"s") + $" ({effect.duration}s)"),
                GetEffectBackgroundColor(effect.type),
                lightTextColor
                );
            }
            else
            {
                CreateTag(
                GetEffectIcon(effect.type),
                GetEffectText(effect),
                GetEffectBackgroundColor(effect.type),
                lightTextColor
                );
            }

        }
    }

    // 태그 하나를 생성하고 내용을 설정
    private void CreateTag(Sprite icon, string label, Color backgroundColor, Color textColor)
    {
        TooltipTagUI tag = Instantiate(tagTemplate, tagParent);

        tag.gameObject.SetActive(true);
        tag.Set(icon, label, backgroundColor, textColor);

        activeTags.Add(tag);
    }

    // 생성된 태그 오브젝트를 모두 제거
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

    // 카테고리를 한글 표시명으로 변환
    private string GetCategoryText(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Material:
                return "재료";
            case ItemCategory.Food:
                return "음식";
            case ItemCategory.Drink:
                return "음료";
            case ItemCategory.Tool:
                return "도구";
            case ItemCategory.Weapon:
                return "무기";
            case ItemCategory.Misc:
            default:
                return "기타";
        }
    }

    // 사용 액션을 한글 표시명으로 변환
    private string GetUseActionText(ItemUseAction useAction)
    {
        switch (useAction)
        {
            case ItemUseAction.SpearAttack:
                return "공격 가능";
            case ItemUseAction.HoeUse:
                return "사용 가능";
            case ItemUseAction.Eat:
                return "먹기 가능";
            case ItemUseAction.Drink:
                return "마시기 가능";
            case ItemUseAction.Throw:
                return "던지기 가능";
            case ItemUseAction.WaterCanUse:
                return "사용 가능";
            case ItemUseAction.None:
            default:
                return string.Empty;
        }
    }

    // 효과 정보를 표시할 텍스트로 변환
    private string GetEffectText(ItemEffectData effect)
    {
        Sprite icon = GetEffectIcon(effect.type);
        string amountText = GetEffectAmountText(effect.type, effect.amount);

        if (icon != null) return amountText;

        return $"{GetEffectName(effect.type)} {amountText}";
    }

    // 효과 수치를 표시용 문자열로 변환
    private string GetEffectAmountText(ItemEffectType type, int amount)
    {
        if (type == ItemEffectType.Damage) return amount.ToString();

        return amount > 0 ? $"+{amount}" : amount.ToString();
    }

    // 효과 종류를 한글 표시명으로 변환
    private string GetEffectName(ItemEffectType type)
    {
        switch (type)
        {
            case ItemEffectType.Hp:
                return "체력";
            case ItemEffectType.Hunger:
                return "허기";
            case ItemEffectType.Thirst:
                return "갈증";
            case ItemEffectType.Mental:
                return "멘탈";
            case ItemEffectType.Damage:
                return "공격력";
            case ItemEffectType.Stamina:
                return "스태미너";
            default:
                return "효과";
        }
    }

    // 효과 종류에 맞는 아이콘을 반환
    public Sprite GetEffectIcon(ItemEffectType type)
    {
        switch (type)
        {
            case ItemEffectType.Hp:
                return hpIcon;
            case ItemEffectType.Hunger:
                return hungerIcon;
            case ItemEffectType.Thirst:
                return thirstIcon;
            case ItemEffectType.Mental:
                return mentalIcon;
            case ItemEffectType.Damage:
                return damageIcon;
            case ItemEffectType.Stamina:
                return staminaIcon;
            default:
                return null;
        }
    }

    // 효과 종류에 맞는 태그 배경색을 반환
    private Color GetEffectBackgroundColor(ItemEffectType type)
    {
        switch (type)
        {
            case ItemEffectType.Hp:
                return hpBackgroundColor;
            case ItemEffectType.Hunger:
                return hungerBackgroundColor;
            case ItemEffectType.Thirst:
                return thirstBackgroundColor;
            case ItemEffectType.Mental:
                return mentalBackgroundColor;
            case ItemEffectType.Damage:
                return damageBackgroundColor;
            case ItemEffectType.Stamina:
                return staminaBackgroundColor;
            default:
                return categoryBackgroundColor;
        }
    }

    // 캔버스 스케일을 반영한 툴팁 크기를 반환
    private Vector2 GetScaledTooltipSize()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;

        return rectTransform.rect.size * scaleFactor;
    }
}
}
