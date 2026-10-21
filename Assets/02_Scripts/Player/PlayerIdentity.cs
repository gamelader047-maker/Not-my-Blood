using System;
using UnityEngine;
using NotMyBlood.Core;

namespace NotMyBlood.Player
{
    /// <summary>
    /// Identität, Merkmale und Mörder-Rolle eines Spielers.
    /// Andere Spieler können hier interagieren:
    /// - E kurz drücken → Nadeltest (Blutgruppe, max. 2 pro Runde, das Opfer merkt es)
    /// - geduckt E halten → Foto vom Schuh (Schuhprofil)
    /// </summary>
    public class PlayerIdentity : MonoBehaviour, IInteractable, IHoldInteractable
    {
        [SerializeField] private string playerName = "Spieler";
        [SerializeField] private PlayerTraits traits;
        [SerializeField] private bool isMurderer;
        [SerializeField] private Renderer playerRenderer;

        [Header("Schuh-Foto")]
        [Tooltip("So lange muss man geduckt E halten, um den Schuh zu fotografieren.")]
        [SerializeField] private float photoDuration = 1f;

        /// <summary>Jemand hat diesen Spieler gepikst (Parameter: wer).</summary>
        public event Action<PlayerIdentity> Pricked;

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

        /// <summary>True für den Spieler, der an diesem Rechner spielt (hat einen PlayerController).</summary>
        public bool IsLocal => TryGetComponent<PlayerController>(out _);

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

        /// <summary>Wird vom Nadeltest aufgerufen. Später im Multiplayer: Meldung "Autsch!" beim Opfer.</summary>
        public void NotifyPricked(PlayerIdentity by)
        {
            Debug.Log($"[{PlayerName}] Autsch! {(by != null ? by.PlayerName : "Jemand")} hat mich gepikst.");
            Pricked?.Invoke(by);
        }

        // ------------------------------------------------------------------ IInteractable: Nadeltest

        public string GetInteractionPrompt()
        {
            var knowledge = PlayerKnowledge.Instance;
            if (knowledge == null) return PlayerName;

            var info = knowledge.Get(this);
            if (info != null && info.bloodKnown) return $"{PlayerName} (Blutgruppe bekannt)";
            return knowledge.NeedlesLeft > 0
                ? $"Nadeltest: {PlayerName} (noch {knowledge.NeedlesLeft})"
                : $"{PlayerName} (keine Nadeln mehr)";
        }

        public void Interact(GameObject interactor)
        {
            if (PlayerKnowledge.Instance == null)
            {
                Debug.LogWarning("[PlayerIdentity] Kein PlayerKnowledge in der Szene – Runden-UI erstellen!");
                return;
            }
            PlayerKnowledge.Instance.TryNeedleTest(this);
        }

        // ------------------------------------------------------------------ IHoldInteractable: Schuh-Foto

        public float HoldDuration => photoDuration;

        public string HoldActionLabel => "Fotografiere Schuh";

        public bool CanHoldInteract(GameObject interactor) =>
            interactor != null && interactor.TryGetComponent(out PlayerController controller) && controller.IsCrouching;

        public string HoldInteract(GameObject interactor) =>
            PlayerKnowledge.Instance != null ? PlayerKnowledge.Instance.TakeShoePhoto(this) : null;
    }
}
