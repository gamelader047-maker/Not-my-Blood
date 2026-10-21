using UnityEngine;
using NotMyBlood.Core;

namespace NotMyBlood.Player
{
    /// <summary>
    /// Verwaltet die Identität, Merkmale und Mörder-Rolle eines Spielers auf dem Spielfeld.
    /// </summary>
    public class PlayerIdentity : MonoBehaviour
    {
        [SerializeField] private string playerName = "Spieler";
        [SerializeField] private PlayerTraits traits;
        [SerializeField] private bool isMurderer;
        [SerializeField] private Renderer playerRenderer;

        public string PlayerName
        {
            get => playerName;
            private set => playerName = value;
        }

        public PlayerTraits Traits
        {
            get => traits;
            private set => traits = value;
        }

        public bool IsMurderer
        {
            get => isMurderer;
            private set => isMurderer = value;
        }

        private void Awake()
        {
            if (playerRenderer == null)
            {
                playerRenderer = GetComponentInChildren<Renderer>();
            }
        }

        /// <summary>
        /// Weist dem Spieler Identitätsdaten zu und aktualisiert die visuelle Darstellung.
        /// </summary>
        public void SetIdentity(string name, PlayerTraits newTraits, bool murderer)
        {
            PlayerName = name;
            Traits = newTraits;
            IsMurderer = murderer;

            ApplyClothingColor();
        }

        /// <summary>
        /// Färbt das Mesh basierend auf der zugewiesenen Kleidungsfarbe ein.
        /// </summary>
        public void ApplyClothingColor()
        {
            if (playerRenderer == null)
            {
                return;
            }

            Color targetColor = Traits.color switch
            {
                // Gleiche Töne wie die Farbpunkte im Notizbuch
                ClothingColor.Rot => new Color(0.66f, 0.20f, 0.16f),
                ClothingColor.Schwarz => new Color(0.15f, 0.13f, 0.16f),
                ClothingColor.Grau => new Color(0.58f, 0.58f, 0.60f),
                _ => Color.white
            };

            playerRenderer.material.color = targetColor;
        }
    }
}