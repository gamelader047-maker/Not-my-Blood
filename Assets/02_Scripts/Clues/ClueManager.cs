using System;
using System.Collections.Generic;
using UnityEngine;
using NotMyBlood.Core;

namespace NotMyBlood.Clues
{
    /// <summary>
    /// Notizbuch-Protokoll: alle Spuren, die DIESER Spieler notiert hat, in Fund-Reihenfolge.
    /// Das Spawnen von Spuren wird über den ClueSpawner abgewickelt.
    /// </summary>
    public class ClueManager : MonoBehaviour
    {
        public static ClueManager Instance { get; private set; }

        /// <summary>Wird ausgelöst, sobald eine neue Spur notiert wurde (z. B. für das Notizbuch).</summary>
        public event Action<ClueData> ClueAdded;

        [SerializeField] private List<ClueData> m_CollectedClues = new List<ClueData>();

        public List<ClueData> collectedClues => m_CollectedClues;
        public IReadOnlyList<ClueData> DiscoveredClues => m_CollectedClues;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Abo in Start, weil GameRoundManager.Instance erst nach Awake existiert
            if (GameRoundManager.Instance != null) GameRoundManager.Instance.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (GameRoundManager.Instance != null) GameRoundManager.Instance.PhaseChanged -= OnPhaseChanged;
            if (Instance == this) Instance = null;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            // Neue Runde → Notizbuch-Protokoll leeren
            if (phase == GamePhase.Cutscene) ClearClues();
        }

        /// <summary>
        /// Notiert eine Spur und setzt die Fundzeit (Sekunden seit Beginn der Ermittlung).
        /// Doppeltes Notieren verhindert schon das ClueObject selbst – hier wird nicht nach Werten
        /// gefiltert, weil zwei verschiedene Spuren gleich aussehen können (z. B. zweimal "Blut 0" im Flur).
        /// </summary>
        public void AddClue(ClueData clue)
        {
            clue.foundTime = GameRoundManager.Instance != null ? GameRoundManager.Instance.InvestigationTime : Time.timeSinceLevelLoad;

            m_CollectedClues.Add(clue);
            Debug.Log($"[ClueManager] Spur notiert: {clue.DisplayName} ({clue.TraitLabel}: {clue.ValueText}, {clue.LocationText}, {clue.FoundTimeText}) – insgesamt {m_CollectedClues.Count}");

            ClueAdded?.Invoke(clue);
        }

        /// <summary>Leert das Protokoll, z. B. zu Beginn einer neuen Runde.</summary>
        public void ClearClues()
        {
            m_CollectedClues.Clear();
        }
    }
}