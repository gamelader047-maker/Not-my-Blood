using UnityEngine;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Globaler UI-Zustand. Spieler-Scripts fragen hier nach, was gerade gesperrt ist.
    /// </summary>
    public static class UIState
    {
        /// <summary>Notizbuch offen (Maus frei).</summary>
        public static bool IsNotebookOpen { get; set; }

        /// <summary>Abstimmung oder Auflösung offen (Maus frei).</summary>
        public static bool IsOverlayOpen { get; set; }

        /// <summary>Meeting, Abstimmung, Auflösung: Spieler sitzen am Tisch und dürfen nur umschauen.</summary>
        public static bool IsMovementLocked { get; set; }

        /// <summary>Umschauen mit der Maus gesperrt.</summary>
        public static bool BlocksPlayerInput => IsNotebookOpen || IsOverlayOpen;

        /// <summary>Laufen und Ducken gesperrt.</summary>
        public static bool BlocksMovement => BlocksPlayerInput || IsMovementLocked;

        /// <summary>Taste E (Spuren, Nadeltest, Koffer …) gesperrt.</summary>
        public static bool BlocksInteraction => BlocksPlayerInput || IsMovementLocked;

        // Zurücksetzen beim Start (wichtig, falls "Domain Reload" in den Play-Mode-Optionen aus ist)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            IsNotebookOpen = false;
            IsOverlayOpen = false;
            IsMovementLocked = false;
        }
    }
}
