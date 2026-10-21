using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NotMyBlood.Clues;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Speichert alle Spuren, die der Spieler notiert hat (Fakten-Protokoll fürs Notizbuch).
    /// Verwaltet zudem das Spawnen der verzögerten echten Spur (GDD V1.1: 60s bis 240s).
    /// </summary>
    public class ClueManager : MonoBehaviour
    {
        public static ClueManager Instance { get; private set; }

        [Header("Prefabs für verzögerte Spur")]
        [SerializeField] private GameObject bloodCluePrefab;
        [SerializeField] private GameObject shoeCluePrefab;
        [SerializeField] private GameObject fiberCluePrefab;

        [Header("Verzögerte Spur Einstellungen (GDD V1.1)")]
        [Tooltip("Mindestzeit in Sekunden bis die verzögerte Spur erscheint (60s = Min 1).")]
        [SerializeField] private float minDelayedSpurTime = 60f;
        [Tooltip("Maximalzeit in Sekunden bis die verzögerte Spur erscheint (240s = Min 4).")]
        [SerializeField] private float maxDelayedSpurTime = 240f;

        [Header("Fakten-Protokoll (Notizbuch)")]
        [Tooltip("Alle notierten Spuren in Fund-Reihenfolge.")]
        public List<ClueData> collectedClues = new List<ClueData>();

        /// <summary>Wird ausgelöst, sobald eine neue Spur notiert wurde (z. B. für das Notizbuch).</summary>
        public event Action<ClueData> ClueAdded;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Startet den Timer für die verzögerte Spur
            StartDelayedClueTimer();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Fügt eine gefundene Spur zum Notizbuch-Protokoll hinzu.
        /// </summary>
        public void AddClue(ClueData clue)
        {
            // TODO: Sobald es den GameRoundManager gibt, hier die Zeit seit Beginn der Ermittlung nehmen.
            clue.foundTime = Time.timeSinceLevelLoad;

            collectedClues.Add(clue);
            Debug.Log($"[ClueManager] Spur notiert: {clue.DisplayName} ({clue.TraitLabel}: {clue.ValueText}, {clue.LocationText}, {clue.FoundTimeText}) – insgesamt {collectedClues.Count}");

            ClueAdded?.Invoke(clue);
        }

        /// <summary>Leert das Protokoll, z. B. zu Beginn einer neuen Runde.</summary>
        public void ClearClues()
        {
            collectedClues.Clear();
        }

        #region Verzögerte Spur Logik

        public void StartDelayedClueTimer()
        {
            float randomDelay = UnityEngine.Random.Range(minDelayedSpurTime, maxDelayedSpurTime);
            Debug.Log($"[ClueManager] Verzögerte echte Spur ist geplant in {randomDelay:F1} Sekunden (Minute {randomDelay / 60f:F1}).");
            StartCoroutine(SpawnDelayedClueRoutine(randomDelay));
        }

        private IEnumerator SpawnDelayedClueRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            SpawnDelayedClue();
        }

        private void SpawnDelayedClue()
        {
            // Alle Spurenpunkte (ClueSpot) in der Szene finden
            ClueSpot[] allSpots = FindObjectsByType<ClueSpot>(FindObjectsSortMode.None);
            List<ClueSpot> freeSpots = new List<ClueSpot>();

            foreach (var spot in allSpots)
            {
                // Prüft, ob der Spot frei ist (kein Child-Objekt hat)
                if (spot.transform.childCount == 0)
                {
                    freeSpots.Add(spot);
                }
            }

            if (freeSpots.Count == 0)
            {
                Debug.LogWarning("[ClueManager] Kein freier ClueSpot für die verzögerte Spur gefunden!");
                return;
            }

            // Zufälligen freien Spot wählen
            ClueSpot selectedSpot = freeSpots[UnityEngine.Random.Range(0, freeSpots.Count)];

            // Beispiel-Prefab wählen
            GameObject prefabToSpawn = bloodCluePrefab != null ? bloodCluePrefab : null;

            if (prefabToSpawn != null)
            {
                GameObject spawnedClue = Instantiate(prefabToSpawn, selectedSpot.transform.position, selectedSpot.transform.rotation, selectedSpot.transform);
                
                if (spawnedClue.TryGetComponent(out ClueObject clueObj))
                {
                    clueObj.SetFakeStatus(false); // Ist eine echte Spur!
                }

                Debug.Log($"[ClueManager] 🕵️ VERZÖGERTE SPUR GESPAWNT an Spot: {selectedSpot.name}!");
            }
            else
            {
                Debug.LogWarning("[ClueManager] Keine Prefabs für Spuren im ClueManager-Inspector zugewiesen!");
            }
        }

        #endregion
    }
}