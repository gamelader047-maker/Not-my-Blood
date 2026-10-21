using UnityEngine;
using NotMyBlood.Clues; // Nutzt unser IInteractable
using NotMyBlood.Core;

namespace NotMyBlood.Environment
{
    public class CircuitBreaker : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptMessage = "Stromkasten manipulieren (E)";
        private bool hasBeenUsed = false;

        public string GetInteractionPrompt()
        {
            return hasBeenUsed ? "Sicherungskasten (Bereits manipuliert)" : promptMessage;
        }

        public void Interact(GameObject interactor)
        {
            if (hasBeenUsed) return;

            if (PowerManager.Instance != null)
            {
                PowerManager.Instance.SabotagePower();
                hasBeenUsed = true;
            }
            else
            {
                Debug.LogWarning("[CircuitBreaker] Kein PowerManager in der Szene gefunden!");
            }
        }
    }
}