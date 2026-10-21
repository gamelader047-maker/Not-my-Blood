using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NotMyBlood.Core;

namespace NotMyBlood.UI
{
    /// <summary>Eine Zeile in der Spurenliste (linke Buchseite).</summary>
    public class NotebookClueRow : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image iconFrame;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text metaText;
        [SerializeField] private NotebookChip valueChip;
        [SerializeField] private GameObject newMarker;
        [SerializeField] private GameObject selectionBar;

        [Header("Sprites für normal / ausgewählt")]
        [SerializeField] private Sprite normalBackground;
        [SerializeField] private Sprite selectedBackground;
        [SerializeField] private Sprite normalFrame;
        [SerializeField] private Sprite selectedFrame;

        public void Setup(ClueData clue, Sprite iconSprite, Color? dotColor, bool isNew, Action onClick)
        {
            if (titleText != null) titleText.text = clue.DisplayName;
            if (metaText != null) metaText.text = $"{clue.LocationText} · {clue.FoundTimeText}";
            if (icon != null) icon.sprite = iconSprite;
            if (valueChip != null) valueChip.Set(clue.ValueText, dotColor);
            if (newMarker != null) newMarker.SetActive(isNew);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(() => onClick());
            }

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (background != null) background.sprite = selected ? selectedBackground : normalBackground;
            if (iconFrame != null) iconFrame.sprite = selected ? selectedFrame : normalFrame;
            if (selectionBar != null) selectionBar.SetActive(selected);
        }
    }
}
