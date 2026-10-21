using UnityEngine;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Globaler UI-Zustand. Spieler-Scripts fragen hier nach, ob gerade ein Menü offen ist,
    /// damit sich die Kamera nicht mitdreht, während man im Notizbuch klickt.
    /// </summary>
    public static class UIState
    {
        public static bool IsNotebookOpen { get; set; }

        /// <summary>True, solange Bewegung, Umschauen und Interagieren gesperrt sein sollen.</summary>
        public static bool BlocksPlayerInput => IsNotebookOpen;

        // Zurücksetzen beim Start (wichtig, falls "Domain Reload" in den Play-Mode-Optionen aus ist)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            IsNotebookOpen = false;
        }
    }
}
