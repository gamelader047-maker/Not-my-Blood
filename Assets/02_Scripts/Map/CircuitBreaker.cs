using UnityEngine;
using NotMyBlood.Core;

namespace NotMyBlood.Map
{
    /// <summary>
    /// Steuert den Sicherungskasten für Sabotage und Stromausfälle.
    /// </summary>
    public class CircuitBreaker : MonoBehaviour, IInteractable
    {
        [SerializeField] private string interactionPrompt = "Sicherungskasten bedienen";

        /// <summary>
        /// Gibt den Interaktionsprompt zurück.
        /// </summary>
        public string GetInteractionPrompt()
        {
            return interactionPrompt;
        }

        /// <summary>
        /// Führt die Interaktion am Sicherungskasten aus.
        /// </summary>
        public void Interact(GameObject interactor)
        {
            if (PowerManager.Instance == null)
            {
                Debug.LogWarning("[CircuitBreaker] Kein PowerManager in der Szene gefunden!");
                return;
            }
            // PowerManager prüft selbst: nur in der Ermittlung und nur 1x pro Runde
            PowerManager.Instance.SabotagePower();
        }
    }
}