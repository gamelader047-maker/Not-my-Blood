using System;
using System.Collections.Generic;
using UnityEngine;
using NotMyBlood.Core;

namespace NotMyBlood.Clues
{
    /// <summary>
    /// Verwaltet das Notizbuch-Protokoll für gefundene Hinweise und benachrichtigt UI-Komponenten.
    /// </summary>
    public class ClueManager : MonoBehaviour
    {
        public static ClueManager Instance { get; private set; }

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

        /// <summary>
        /// Fügt einen neuen Hinweis dem Notizbuch hinzu und feuert das Event.
        /// </summary>
        public void AddClue(ClueData clue)
        {
            if (!m_CollectedClues.Contains(clue))
            {
                m_CollectedClues.Add(clue);
                ClueAdded?.Invoke(clue);
                Debug.Log($"[ClueManager] Neuer Hinweis hinzugefügt.");
            }
        }
    }
}