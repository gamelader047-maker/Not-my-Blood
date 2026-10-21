using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NotMyBlood.Core;
using NotMyBlood.Player;

namespace NotMyBlood.UI
{
    /// <summary>Ein Spieler-Button im Abstimmungsfenster.</summary>
    public class VoteCandidateButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image colorDot;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;

        public PlayerIdentity Player { get; private set; }

        public void Setup(PlayerIdentity player, Action onClick)
        {
            Player = player;
            if (nameText != null) nameText.text = player.PlayerName;
            if (colorDot != null) colorDot.color = SuspectRow.ClothingColorOf(player.Traits.color);

            // Was der lokale Spieler über ihn weiss (aus dem Notizbuch)
            if (detailText != null)
            {
                var info = PlayerKnowledge.Instance != null ? PlayerKnowledge.Instance.Get(player) : null;
                string blood = info != null && info.bloodKnown ? ClueData.BloodTypeText(info.blood) : "?";
                string shoe = info != null && info.shoeKnown ? info.shoe.ToString() : "?";
                detailText.text = $"{player.Traits.color} · Blut {blood} · Schuh {shoe}";
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(() => onClick());
            }
            SetSelected(false);
            SetInteractable(true);
        }

        public void SetSelected(bool selected)
        {
            if (background != null) background.sprite = selected ? selectedSprite : normalSprite;
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null) button.interactable = interactable;
        }
    }
}
