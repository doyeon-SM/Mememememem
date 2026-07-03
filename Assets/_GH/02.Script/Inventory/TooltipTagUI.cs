using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipTagUI : MonoBehaviour
{
    public Image backgroundImage;
    public Image iconImage;
    public TMP_Text labelText;

    public void Set(Sprite icon, string label, Color backgroundColor, Color textColor)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }

        if (iconImage != null)
        {
            bool hasIcon = icon != null;

            iconImage.gameObject.SetActive(hasIcon);
            iconImage.sprite = icon;
        }

        if (labelText != null)
        {
            labelText.text = label;
            labelText.color = textColor;
        }
    }
}
