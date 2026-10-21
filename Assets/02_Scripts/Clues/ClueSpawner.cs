using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using NotMyBlood.Core;
using NotMyBlood.Player;

namespace NotMyBlood.Clues
{
    /// <summary>
    /// Spawnt pro Runde die Spuren auf die Spurenpunkte (Konzept 1.1, Kap. 4):
    /// - Start der Ermittlung: 2 echte Spuren (Werte des Mörders) + Fälschung 1 (Schuh oder Faser)
    /// - zwischen Minute 1 und 4: die 3. echte Spur (die noch fehlende Kategorie)
    /// - Fälschung 2: live durch den Mörder (PlaceLiveFake), 1x pro Runde
    /// Eine Fälschung zeigt immer einen Wert, den ein Unschuldiger hat und der NICHT der echte Wert ist.
    /// </summary>
    public class ClueSpawner : MonoBehaviour
    {
        public static ClueSpawner Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject prefabBlood;
        [SerializeField] private GameObject prefabShoe;
        [SerializeField] private GameObject prefabFiber;

        [Header("Verzögerte echte Spur (Sekunden nach Beginn der Ermittlung)")]
        [SerializeField] private Vector2 delayedClueWindow = new Vector2(60f, 240f);

        [Header("Test")]
        [Tooltip("Die Gruppe \"TestSpuren\" aus dem Test-Haus. Leer = wird per Name gesucht und beim Start ausgeblendet.")]
        [SerializeField] private GameObject testSpurenGroup;

        private readonly Dictionary<ClueSpot, GameObject> spawnedClues = new Dictionary<ClueSpot, GameObject>();
        private readonly List<ClueCategory> startCategories = new List<ClueCategory>();
        private ClueCategory delayedCategory;
        private float delayedClueTime;
        private bool delayedClueSpawned;
        private bool liveFakeUsed;

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
            if (GameRoundManager.Instance != null)
            {
                GameRoundManager.Instance.PhaseChanged += OnPhaseChanged;
            }

            if (testSpurenGroup == null) testSpurenGroup = GameObject.Find("TestSpuren");
            if (testSpurenGroup != null) testSpurenGroup.SetActive(false);
        }

        private void OnDestroy()
        {
            if (GameRoundManager.Instance != null)
            {
                GameRoundManager.Instance.PhaseChanged -= OnPhaseChanged;
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            HandleEditorInput();

            var round = GameRoundManager.Instance;
            if (delayedClueSpawned || round == null || round.CurrentPhase != GamePhase.Ermittlung) return;

            if (round.InvestigationTime >= delayedClueTime)
            {
                delayedClueSpawned = true;
                SpawnRealClue(delayedCategory);
                Debug.Log($"[ClueSpawner] Verzögerte echte Spur ({delayedCategory}) nach {round.InvestigationTime:0} s gespawnt.");
            }
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Ermittlung) StartNewRoundSpawns();
            else if (phase == GamePhase.Cutscene || phase == GamePhase.Lobby) ClearAllSpawnedClues();
        }

        // ------------------------------------------------------------------ Runde

        /// <summary>Legt die Start-Spuren und plant die verzögerte Spur.</summary>
        public void StartNewRoundSpawns()
        {
            ClearAllSpawnedClues();
            liveFakeUsed = false;
            delayedClueSpawned = false;
            delayedClueTime = Random.Range(delayedClueWindow.x, delayedClueWindow.y);

            if (FindMurderer() == null)
            {
                Debug.LogWarning("[ClueSpawner] Kein Mörder gefunden – sind Merkmale verteilt (Test-Dummies + GameRoundManager)?");
            }

            // Kategorien mischen: die ersten zwei liegen ab Start, die dritte kommt verzögert
            var categories = new List<ClueCategory> { ClueCategory.Blut, ClueCategory.Schuh, ClueCategory.Faser };
            Shuffle(categories);
            startCategories.Clear();
            startCategories.Add(categories[0]);
            startCategories.Add(categories[1]);
            delayedCategory = categories[2];

            foreach (var category in startCategories) SpawnRealClue(category);

            // Fälschung 1 (während der Cutscene gewählt): nur Schuh oder Faser
            var fakeCategory = Random.value < 0.5f ? ClueCategory.Schuh : ClueCategory.Faser;
            SpawnFakeClue(fakeCategory, FreeSpotFor(fakeCategory));

            Debug.Log($"[ClueSpawner] Runde: echt ab Start = {startCategories[0]}, {startCategories[1]} · verzögert = {delayedCategory} " +
                      $"(nach {delayedClueTime:0} s) · Fälschung 1 = {fakeCategory}");
        }

        /// <summary>
        /// Fälschung 2: Der Mörder legt live eine Spur auf einen freien Spurenpunkt. Nur 1x pro Runde.
        /// </summary>
        public bool PlaceLiveFake(ClueSpot spot, ClueCategory category, GameObject murderer)
        {
            if (liveFakeUsed)
            {
                Debug.Log("[ClueSpawner] Die Live-Fälschung wurde in dieser Runde schon benutzt.");
                return false;
            }
            if (murderer != null && murderer.TryGetComponent(out PlayerIdentity identity) && !identity.IsMurderer)
            {
                Debug.Log("[ClueSpawner] Nur der Mörder kann Spuren fälschen.");
                return false;
            }
            if (spot == null || spawnedClues.ContainsKey(spot) || !spot.Allows(category))
            {
                return false;
            }

            if (!SpawnFakeClue(category, spot)) return false;
            liveFakeUsed = true;
            Debug.Log($"[ClueSpawner] Live-Fälschung ({category}) auf S{spot.SpotId:00} ({spot.RoomName}) gelegt.");
            return true;
        }

        // ------------------------------------------------------------------ Spawnen

        private void SpawnRealClue(ClueCategory category)
        {
            var murderer = FindMurderer();
            PlayerTraits traits = murderer != null ? murderer.Traits : default;
            Spawn(category, FreeSpotFor(category), traits, isFake: false);
        }

        private bool SpawnFakeClue(ClueCategory category, ClueSpot spot)
        {
            if (spot == null) return false;
            if (!TryGetFakeTraits(category, out PlayerTraits fakeTraits))
            {
                Debug.LogWarning($"[ClueSpawner] Kein Unschuldiger mit anderem Wert für {category} – Fälschung übersprungen.");
                return false;
            }
            return Spawn(category, spot, fakeTraits, isFake: true);
        }

        private bool Spawn(ClueCategory category, ClueSpot spot, PlayerTraits values, bool isFake)
        {
            if (spot == null)
            {
                Debug.LogWarning($"[ClueSpawner] Kein freier Spurenpunkt für {category}.");
                return false;
            }

            GameObject prefab = PrefabFor(category);
            if (prefab == null)
            {
                Debug.LogWarning($"[ClueSpawner] Kein Prefab für {category} zugewiesen.");
                return false;
            }

            GameObject obj = Instantiate(prefab, spot.transform.position, spot.transform.rotation, transform);
            obj.name = $"{prefab.name}_S{spot.SpotId:00}{(isFake ? "_FAKE" : "")}";
            if (obj.TryGetComponent(out ClueObject clue))
            {
                clue.Setup(category, values.blood, values.shoe, values.color, spot.RoomName, isFake);
            }
            spawnedClues[spot] = obj;
            return true;
        }

        /// <summary>
        /// Wert für eine Fälschung: Er gehört einem Unschuldigen und ist NICHT der echte Wert des Mörders.
        /// </summary>
        private bool TryGetFakeTraits(ClueCategory category, out PlayerTraits result)
        {
            result = default;
            var round = GameRoundManager.Instance;
            var murderer = FindMurderer();
            if (round == null || murderer == null) return false;

            var candidates = new List<PlayerTraits>();
            foreach (var player in round.Players)
            {
                if (player == null || player.IsMurderer) continue;
                bool differs = category switch
                {
                    ClueCategory.Blut => player.Traits.blood != murderer.Traits.blood,
                    ClueCategory.Schuh => player.Traits.shoe != murderer.Traits.shoe,
                    _ => player.Traits.color != murderer.Traits.color,
                };
                if (differs) candidates.Add(player.Traits);
            }

            if (candidates.Count == 0) return false;
            result = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        private ClueSpot FreeSpotFor(ClueCategory category)
        {
            var free = new List<ClueSpot>();
            foreach (var spot in FindObjectsByType<ClueSpot>())
            {
                if (spot.Allows(category) && !spawnedClues.ContainsKey(spot)) free.Add(spot);
            }
            return free.Count > 0 ? free[Random.Range(0, free.Count)] : null;
        }

        private static PlayerIdentity FindMurderer()
        {
            var round = GameRoundManager.Instance;
            if (round == null) return null;
            foreach (var player in round.Players)
            {
                if (player != null && player.IsMurderer) return player;
            }
            return null;
        }

        private GameObject PrefabFor(ClueCategory category) => category switch
        {
            ClueCategory.Blut => prefabBlood,
            ClueCategory.Schuh => prefabShoe,
            _ => prefabFiber,
        };

        private void ClearAllSpawnedClues()
        {
            foreach (var obj in spawnedClues.Values)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedClues.Clear();
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ------------------------------------------------------------------ Test (nur Editor)

        private void HandleEditorInput()
        {
#if UNITY_EDITOR
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // F7: Live-Fälschung auf einen zufälligen freien Spot (wie der Mörder)
            if (keyboard.f7Key.wasPressedThisFrame)
            {
                var categories = new List<ClueCategory> { ClueCategory.Blut, ClueCategory.Schuh, ClueCategory.Faser };
                Shuffle(categories);
                var murderer = FindMurderer();
                foreach (var category in categories)
                {
                    var spot = FreeSpotFor(category);
                    if (spot != null && PlaceLiveFake(spot, category, murderer != null ? murderer.gameObject : null)) break;
                }
            }

            // F8: alle gespawnten Spuren mit echt/gefälscht loggen
            if (keyboard.f8Key.wasPressedThisFrame)
            {
                Debug.Log($"=== SPUREN ({spawnedClues.Count}) ===");
                foreach (var pair in spawnedClues)
                {
                    if (pair.Value == null || !pair.Value.TryGetComponent(out ClueObject clue)) continue;
                    Debug.Log($"S{pair.Key.SpotId:00} {pair.Key.RoomName}: {clue.Data.DisplayName} {clue.Data.ValueText}{(clue.IsFake ? "  [FÄLSCHUNG]" : "  [echt]")}");
                }
            }
#endif
        }
    }
}
