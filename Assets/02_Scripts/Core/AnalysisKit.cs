using UnityEngine;

namespace NotMyBlood.Core
{
    public class AnalysisKit : MonoBehaviour, IInteractable
    {
        [Header("Kit Settings")]
        [SerializeField] private string promptMessage = "Analyse-Koffer aufheben";
        [SerializeField] private int remainingCharges = 1; // Startwert laut GDD V1.1 (1 bei 3-5 Spielern)

        private bool isCarried = false;
        private Transform carrierTransform;

        public bool IsCarried => isCarried;
        public bool HasCharges => remainingCharges > 0;

        // Startzustand für jede neue Runde
        private Transform initialParent;
        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        private int initialCharges;
        private bool initialKinematic;

        private void Awake()
        {
            initialParent = transform.parent;
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            initialCharges = remainingCharges;
            if (TryGetComponent(out Rigidbody body)) initialKinematic = body.isKinematic;
        }

        private void Start()
        {
            if (GameRoundManager.Instance != null) GameRoundManager.Instance.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (GameRoundManager.Instance != null) GameRoundManager.Instance.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Cutscene) ResetKit();
        }

        /// <summary>Neue Runde: Koffer zurück an den Startplatz, Ladungen wieder voll.</summary>
        public void ResetKit()
        {
            transform.SetParent(initialParent, false);
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
            isCarried = false;
            carrierTransform = null;
            remainingCharges = initialCharges;
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = true;
            if (TryGetComponent(out Rigidbody body)) body.isKinematic = initialKinematic;
        }

        public string GetInteractionPrompt()
        {
            if (isCarried)
            {
                return "Analyse-Koffer wird getragen";
            }
            // Kurzer Text für den HUD-Hinweis; die Taste zeigt das HUD selbst an
            string label = promptMessage.Replace("(E)", "").Trim();
            return $"{label} ({remainingCharges} {(remainingCharges == 1 ? "Ladung" : "Ladungen")})";
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
