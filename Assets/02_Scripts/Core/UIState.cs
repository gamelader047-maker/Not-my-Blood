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

        /// <summary>Hauptmenü oder Pause-Menü (Esc) offen – auch beim Tippen von Name/Code.</summary>
        public static bool IsMenuOpen { get; set; }

        /// <summary>Meeting, Abstimmung, Auflösung: Spieler sitzen am Tisch und dürfen nur umschauen.</summary>
        public static bool IsMovementLocked { get; set; }

        /// <summary>Umschauen mit der Maus gesperrt.</summary>
        public static bool BlocksPlayerInput => IsNotebookOpen || IsOverlayOpen || IsMenuOpen;

        /// <summary>Laufen und Ducken gesperrt.</summary>
        public static bool BlocksMovement => BlocksPlayerInput || IsMovementLocked;

        /// <summary>Taste E (Spuren, Nadeltest, Koffer …) gesperrt.</summary>
        public static bool BlocksInteraction => BlocksPlayerInput || IsMovementLocked;

        /// <summary>Alles zurücksetzen (Spielstart, Szene neu geladen).</summary>
        public static void ResetAll()
        {
            IsNotebookOpen = false;
            IsOverlayOpen = false;
            IsMenuOpen = false;
            IsMovementLocked = false;
        }

        // Zurücksetzen beim Start (wichtig, falls "Domain Reload" in den Play-Mode-Optionen aus ist)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => ResetAll();
    }
}
