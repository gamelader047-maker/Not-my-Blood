using UnityEngine;
using NotMyBlood.Core;

namespace NotMyBlood.Clues
{
    /// <summary>
    /// Eine Spur am Tatort. Mit E untersuchen → landet im Notizbuch.
    /// Mit E-Halten (Koffer im Inventar/als Child) → 3s Analyse (Echt/Gefälscht).
    /// </summary>
    public class ClueObject : MonoBehaviour, IInteractable, IHoldInteractable
    {
        [Header("Spur")]
        [Tooltip("Leer lassen = Standardname (Blutspur, Schuhabdruck, Textilfaser).")]
        [SerializeField] private string clueName = "";
        [TextArea(2, 4)]
        [SerializeField] private string description = "";
        [Tooltip("Fundort, wie er im Notizbuch steht, z. B. \"Küche\".")]
        [SerializeField] private string location = "";
        [SerializeField] private ClueCategory category = ClueCategory.Blut;

        [Header("Wert (es zählt nur das Feld zur Kategorie)")]
        [SerializeField] private BloodType bloodType = BloodType.A;
        [SerializeField] private ShoeProfile shoeProfile = ShoeProfile.Kreise;
        [SerializeField] private ClothingColor clothingColor = ClothingColor.Rot;

        [Header("Server / System status")]
        [Tooltip("Vom ClueManager/Server gesetzt: Ist diese Spur eine Fälschung des Mörders?")]
        [SerializeField] private bool isFake = false;

        [Header("Verhalten")]
        [Tooltip("Aus = Spur bleibt liegen (Konzept). An = verschwindet nach dem Untersuchen.")]
        [SerializeField] private bool destroyOnCollect = false;

        [Header("Koffer-Analyse")]
        [Tooltip("So lange muss E gehalten werden (Konzept 1.1: 3 Sekunden).")]
        [SerializeField] private float analysisDuration = 3f;

        private bool recorded;

        public bool IsFake => isFake;

        public ClueData Data => new ClueData
        {
            clueName = clueName,
            description = description,
            location = location,
            category = category,
            bloodType = bloodType,
            shoeProfile = shoeProfile,
            clothingColor = clothingColor,
        };

        public void SetFakeStatus(bool fakeStatus)
        {
            isFake = fakeStatus;
        }

        public string GetInteractionPrompt()
        {
            // Kurzer Text für den HUD-Hinweis; die Tasten zeigt das HUD selbst an
            return recorded ? $"Bereits notiert: {Data.DisplayName}" : $"Untersuchen: {Data.DisplayName}";
        }

        public void Interact(GameObject interactor)
        {
            if (recorded) return;

            if (ClueManager.Instance == null)
            {
                Debug.LogWarning("Kein ClueManager in der Szene gefunden!");
                return;
            }

            ClueManager.Instance.AddClue(Data);
            recorded = true;

            if (destroyOnCollect) Destroy(gameObject);
        }

        // ---- IHoldInteractable: E halten, während der Spieler den Analyse-Koffer trägt

        public float HoldDuration => analysisDuration;

        public string HoldActionLabel => "Analysiere Spur";

        public bool CanHoldInteract(GameObject interactor)
        {
            AnalysisKit kit = FindCarriedKit(interactor);
            return kit != null && kit.HasCharges;
        }

        public string HoldInteract(GameObject interactor) => AnalyzeWithKit(FindCarriedKit(interactor));

        /// <summary>
        /// Wird aufgerufen, wenn der Spieler 3 Sekunden mit dem Koffer auf diese Spur schaut.
        /// Gibt das Ergebnis als kurzen Text für die Anzeige des analysierenden Spielers zurück.
        /// </summary>
        public string AnalyzeWithKit(AnalysisKit kit)
        {
            if (kit == null) return "Kein Analyse-Koffer";
            if (!kit.UseKitOnClue(isFake)) return "Keine Ladungen mehr";
            return isFake ? "Ergebnis: GEFÄLSCHT" : "Ergebnis: ECHT";
        }

        /// <summary>Der Koffer hängt beim Tragen als Child am Spieler.</summary>
        private static AnalysisKit FindCarriedKit(GameObject interactor) =>
            interactor != null ? interactor.GetComponentInChildren<AnalysisKit>() : null;
    }
}
