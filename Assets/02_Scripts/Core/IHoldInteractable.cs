using UnityEngine;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Ergänzung zu <see cref="IInteractable"/>: Objekte mit einer zusätzlichen "E gedrückt halten"-Aktion,
    /// z. B. eine Spur, die man mit dem Analyse-Koffer 3 Sekunden lang analysiert.
    /// Kurzer Druck ruft weiterhin IInteractable.Interact() auf.
    /// </summary>
    public interface IHoldInteractable
    {
        /// <summary>So lange muss E gehalten werden (Sekunden).</summary>
        float HoldDuration { get; }

        /// <summary>Text für die Fortschrittsanzeige, z. B. "Analysiere Spur".</summary>
        string HoldActionLabel { get; }

        /// <summary>Darf dieser Spieler die Halte-Aktion gerade ausführen (z. B. trägt er den Koffer)?</summary>
        bool CanHoldInteract(GameObject interactor);

        /// <summary>Führt die Halte-Aktion aus. Rückgabe: kurzer Ergebnistext für den Spieler (oder null).</summary>
        string HoldInteract(GameObject interactor);
    }
}
