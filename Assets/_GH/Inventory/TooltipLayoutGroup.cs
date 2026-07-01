using UnityEngine;
using UnityEngine.UI;

public class TooltipLayoutGroup : LayoutGroup
{
    public float maxWidth;
    public float spacingX;
    public float spacingY;

    private float layoutPreferredWidth;
    private float layoutPreferredHeight;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        CalculateFlowSize();
        SetLayoutInputForAxis(layoutPreferredWidth, layoutPreferredWidth, -1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        CalculateFlowSize();
        SetLayoutInputForAxis(layoutPreferredHeight, layoutPreferredHeight, -1f, 1);
    }

    public override void SetLayoutHorizontal()
    {
        SetChildrenAlogFlow();
    }

    public override void SetLayoutVertical()
    {
        SetChildrenAlogFlow();
    }

    // 태그들을 줄바꿈 기준으로 배치하고 전체 크기를 계산
    private void CalculateFlowSize()
    {
        float contentMaxWidth = Mathf.Max(1f, maxWidth - padding.horizontal);

        float rowWidth = 0f;
        float rowHeight = 0f;
        float usedWidth = 0f;
        float totalHeight = padding.vertical;

        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];

            float childWidth = LayoutUtility.GetPreferredWidth(child);
            float childHeight = LayoutUtility.GetPreferredHeight(child);

            bool needsNewLine = rowWidth > 0f && rowWidth + spacingX + childWidth > contentMaxWidth;

            if (needsNewLine)
            {
                usedWidth = Mathf.Max(usedWidth, rowWidth);
                totalHeight += rowHeight + spacingY;

                rowWidth = 0f;
                rowHeight = 0f;
            }

            if (rowWidth > 0f) rowWidth += spacingX;

            rowWidth += childWidth;
            rowHeight = Mathf.Max(rowHeight, childHeight);
        }

        usedWidth = Mathf.Max(usedWidth, rowWidth);

        if (rectChildren.Count > 0)
        {
            totalHeight += rowHeight;
        }

        layoutPreferredWidth = usedWidth + padding.horizontal;
        layoutPreferredHeight = totalHeight;
    }

    // 위치 배치
    private void SetChildrenAlogFlow()
    {
        float contentMaxWidth = Mathf.Max(1f, maxWidth - padding.horizontal);

        float x = padding.left;
        float y = padding.top;
        float rowHeight = 0f;

        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];

            float childWidth = LayoutUtility.GetPreferredWidth(child);
            float childHeight = LayoutUtility.GetPreferredHeight(child);

            bool needsNewLine = x > padding.left && x + childWidth > padding.left + contentMaxWidth;

            if (needsNewLine)
            {
                x = padding.left;
                y += rowHeight + spacingY;
                rowHeight = 0f;
            }

            SetChildAlongAxis(child, 0, x, childWidth);
            SetChildAlongAxis(child, 1, y, childHeight);

            x += childWidth + spacingX;
            rowHeight = Mathf.Max(rowHeight, childHeight);
        }
    }
}
