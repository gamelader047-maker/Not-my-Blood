using UnityEngine;

namespace NotMyBlood.Core
{
    public class AnalysisKit : MonoBehaviour, IInteractable
    {
        [Header("Kit Settings")]
        [SerializeField] private string promptMessage = "Koffer aufheben (E)";
        [SerializeField] private int remainingCharges = 1; // Startwert laut GDD V1.1 (1 bei 3-5 Spielern)

        private bool isCarried = false;
        private Transform carrierTransform;

        public bool IsCarried => isCarried;
        public bool HasCharges => remainingCharges > 0;

        public string GetInteractionPrompt()
        {
            if (isCarried)
            {
                return "Analyse-Koffer wird getragen";
            }
            return $"{promptMessage} [Ladungen: {remainingCharges}]";
        }

        public void Interact(GameObject interactor)
        {
            if (!isCarried)
            {
                // Koffer aufheben und an den Spieler heften
                PickUpKit(interactor);
            }
        }

        private void PickUpKit(GameObject interactor)
        {
            // Sucht die Kamera oder den Player-Transform, um den Koffer mitzunehmen
            carrierTransform = interactor.transform;
            transform.SetParent(carrierTransform);

            // Platziere ihn leicht vor/neben dem Spieler (kann im Inspector angepasst werden)
            transform.localPosition = new Vector3(0.5f, -0.4f, 1.0f);
            transform.localRotation = Quaternion.identity;

            isCarried = true;

            // Eigene Collider aus, sonst blockiert der getragene Koffer den Interaktions-Raycast
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
            if (TryGetComponent(out Rigidbody body)) body.isKinematic = true;

            Debug.Log("[AnalysisKit] Koffer wurde aufgehoben! Restliche Ladungen: " + remainingCharges);
        }

        /// <summary>
        /// Wird aufgerufen, wenn der Spieler mit dem Koffer in der Hand eine Spur analysiert.
        /// </summary>
        public bool UseKitOnClue(bool isClueFake)
        {
            if (remainingCharges <= 0)
            {
                Debug.Log("[AnalysisKit] Der Koffer hat keine Ladungen mehr!");
                return false;
            }

            remainingCharges--;
            Debug.Log("[AnalysisKit] Analyse durchgeführt! Restladungen: " + remainingCharges);

            if (isClueFake)
            {
                Debug.Log("🔴 ERGEBNIS: Die Spur ist GEFÄLSCHT!");
            }
            else
            {
                Debug.Log("🟢 ERGEBNIS: Die Spur ist ECHT!");
            }

            return true;
        }

        public int GetRemainingCharges()
        {
            return remainingCharges;
        }
    }
}
