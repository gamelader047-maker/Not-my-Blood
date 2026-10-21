using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NotMyBlood.Core;
using NotMyBlood.Player;

namespace NotMyBlood.UI
{
    /// <summary>Eine Karte auf der Notizbuch-Seite "Verdächtige".</summary>
    public class SuspectRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private NotebookChip bloodChip;
        [SerializeField] private NotebookChip shoeChip;
        [SerializeField] private NotebookChip colorChip;
        [SerializeField] private Button statusButton;
        [SerializeField] private Image statusImage;
        [SerializeField] private TMP_Text statusText;

        [Header("Stempel-Farben")]
        [SerializeField] private Color unclearColor = new Color(0.86f, 0.83f, 0.76f);
        [SerializeField] private Color suspiciousColor = new Color(0.96f, 0.62f, 0.56f);
        [SerializeField] private Color clearedColor = new Color(0.70f, 0.86f, 0.62f);
        [SerializeField] private Color selfColor = new Color(0.95f, 0.85f, 0.55f);

        public void Setup(SuspectInfo info, Action onStatusClicked)
        {
            if (nameText != null) nameText.text = info.isSelf ? $"{info.Name}" : info.Name;
            if (bloodChip != null) bloodChip.Set(info.bloodKnown ? $"Blut {ClueData.BloodTypeText(info.blood)}" : "Blut ?");
            if (shoeChip != null) shoeChip.Set(info.shoeKnown ? info.shoe.ToString() : "Schuh ?");
            if (colorChip != null) colorChip.Set(info.color.ToString(), ClothingColorOf(info.color));

            if (statusButton != null)
            {
                statusButton.onClick.RemoveAllListeners();
                statusButton.interactable = !info.isSelf;
                if (!info.isSelf && onStatusClicked != null) statusButton.onClick.AddListener(() => onStatusClicked());
            }

            string label;
            Color color;
            if (info.isSelf)
            {
                label = "DU";
                color = selfColor;
            }
            else
            {
                (label, color) = info.status switch
                {
                    Suspicion.Verdaechtig => ("VERDÄCHTIG", suspiciousColor),
                    Suspicion.Entlastet => ("ENTLASTET", clearedColor),
                    _ => ("UNKLAR", unclearColor),
                };
            }
            if (statusText != null) statusText.text = label;
            if (statusImage != null) statusImage.color = color;
        }

        public static Color ClothingColorOf(ClothingColor color) => color switch
        {
            ClothingColor.Rot => new Color(0.66f, 0.20f, 0.16f),
            ClothingColor.Schwarz => new Color(0.15f, 0.13f, 0.16f),
            _ => new Color(0.58f, 0.58f, 0.60f),
        };
    }
}
