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
        public ClueCategory Category => category;

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

        /// <summary>
        /// Richtet die Spur programmatisch ein (vom ClueSpawner aufgerufen).
        /// </summary>
        public void Setup(ClueCategory cat, BloodType bType, ShoeProfile sProfile, ClothingColor cColor, string loc, bool fake)
        {
            category = cat;
            bloodType = bType;
            shoeProfile = sProfile;
            clothingColor = cColor;
            location = loc;
            isFake = fake;
            ApplyVisual();
        }

        /// <summary>
        /// Passt das Modell an den Wert an (Prefabs aus "Spuren-Prefabs erstellen"):
        /// - Blut: Lache am Boden/auf Möbeln, Spritzer an der Wand
        /// - Schuh: das passende Sohlenprofil (Kreise, Zickzack, Glatt)
        /// - Faser: Farbe wie im Notizbuch
        /// Dazu eine zufällige Drehung, damit gleiche Spuren nicht identisch aussehen.
        /// </summary>
        private void ApplyVisual()
        {
            Transform visual = transform.Find("Visual");
            if (visual != null && visual.childCount > 0)
            {
                visual.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            switch (category)
            {
                case ClueCategory.Blut:
                    // Spurenpunkte an Wänden sind so gedreht, dass ihre Oberseite von der Wand weg zeigt
                    bool onWall = Vector3.Dot(transform.up, Vector3.up) < 0.5f;
                    SetVariant(visual, "Blut_Boden", !onWall);
                    SetVariant(visual, "Blut_Wand", onWall);
                    break;

                case ClueCategory.Schuh:
                    SetVariant(visual, "Profil_Kreise", shoeProfile == ShoeProfile.Kreise);
                    SetVariant(visual, "Profil_Zickzack", shoeProfile == ShoeProfile.Zickzack);
                    SetVariant(visual, "Profil_Glatt", shoeProfile == ShoeProfile.Glatt);
                    break;

                case ClueCategory.Faser:
                    Color color = clothingColor switch
                    {
                        ClothingColor.Rot => new Color(0.66f, 0.20f, 0.16f),
                        ClothingColor.Schwarz => new Color(0.15f, 0.13f, 0.16f),
                        _ => new Color(0.58f, 0.58f, 0.60f),
                    };

                    var block = new MaterialPropertyBlock();
                    foreach (var r in GetComponentsInChildren<Renderer>(true))
                    {
                        r.GetPropertyBlock(block);
                        block.SetColor("_BaseColor", color);
                        block.SetColor("_Color", color);
                        r.SetPropertyBlock(block);
                    }
                    break;
            }
        }

        private static void SetVariant(Transform visual, string childName, bool active)
        {
            if (visual == null) return;
            Transform child = visual.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
        }

        public void SetFakeStatus(bool fakeStatus)
        {
            isFake = fakeStatus;
        }

        public string GetInteractionPrompt()
        {
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