using System;
using System.Collections.Generic;
using UnityEngine;
using NotMyBlood.Clues;
using NotMyBlood.Player;

namespace NotMyBlood.Core
{
    public enum VoteOutcome { Entschieden, Gleichstand }

    /// <summary>
    /// Geheime Abstimmung (Konzept 1.1, Kap. 8):
    /// - jeder stimmt für genau eine andere Person
    /// - meiste Stimmen = beschuldigt; Gleichstand → Stichwahl zwischen den Gleichplatzierten
    /// - erneuter Gleichstand → der Mörder gewinnt
    /// Test-Dummies stimmen für den Spieler, der am besten zu den gespawnten Spuren passt (mit etwas Zufall).
    /// </summary>
    public class VoteManager : MonoBehaviour
    {
        public static VoteManager Instance { get; private set; }

        /// <summary>Abstimmung (oder Stichwahl) beginnt → UI öffnen.</summary>
        public event Action VotingStarted;
        /// <summary>Abstimmung vorbei → UI schliessen.</summary>
        public event Action VotingEnded;

        [Tooltip("Zufall bei der Wahl der Dummies (0 = immer der passendste Spieler).")]
        [SerializeField] private float dummyRandomness = 1.5f;

        public bool IsVoting { get; private set; }
        public bool IsRunoff { get; private set; }
        public IReadOnlyList<PlayerIdentity> Candidates => candidates;

        /// <summary>Ergebnis (gültig, sobald HasResult true ist).</summary>
        public bool HasResult { get; private set; }
        public PlayerIdentity AccusedPlayer { get; private set; }
        public bool MurdererWins { get; private set; }
        public IReadOnlyDictionary<PlayerIdentity, int> LastTally => tally;

        private readonly Dictionary<PlayerIdentity, PlayerIdentity> votes = new Dictionary<PlayerIdentity, PlayerIdentity>();
        private readonly Dictionary<PlayerIdentity, int> tally = new Dictionary<PlayerIdentity, int>();
        private readonly List<PlayerIdentity> candidates = new List<PlayerIdentity>();
        private bool skipPending;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
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
        }

        private void Update()
        {
            // Phase erst im nächsten Frame beenden, nicht mitten in einem Phasenwechsel
            if (!skipPending) return;
            skipPending = false;
            if (IsVoting && GameRoundManager.Instance != null) GameRoundManager.Instance.SkipCurrentPhase();
        }

        private void OnDestroy()
        {
            if (GameRoundManager.Instance != null)
            {
                GameRoundManager.Instance.PhaseChanged -= OnPhaseChanged;
            }
            if (Instance == this) Instance = null;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Cutscene:
                    HasResult = false;
                    AccusedPlayer = null;
                    MurdererWins = false;
                    tally.Clear();
                    break;
                case GamePhase.Abstimmung:
                    BeginVoting(GameRoundManager.Instance.Players, runoff: false);
                    break;
                case GamePhase.Stichwahl:
                    BeginVoting(new List<PlayerIdentity>(candidates), runoff: true);
                    break;
            }
        }

        // ------------------------------------------------------------------ Abstimmen

        private void BeginVoting(IReadOnlyList<PlayerIdentity> candidateList, bool runoff)
        {
            votes.Clear();
            candidates.Clear();
            foreach (var p in candidateList)
            {
                if (p != null) candidates.Add(p);
            }
            IsRunoff = runoff;
            IsVoting = true;
            VotingStarted?.Invoke();

            CastDummyVotes();
            CheckAllVoted();
        }

        /// <summary>Kann dieser Spieler für diesen Kandidaten stimmen?</summary>
        public bool CanVoteFor(PlayerIdentity voter, PlayerIdentity target) =>
            IsVoting && voter != null && target != null && voter != target && candidates.Contains(target) && !votes.ContainsKey(voter);

        /// <summary>Gibt eine geheime Stimme ab. Jeder darf genau einmal.</summary>
        public bool CastVote(PlayerIdentity voter, PlayerIdentity target)
        {
            if (!CanVoteFor(voter, target)) return false;
            votes[voter] = target;
            CheckAllVoted();
            return true;
        }

        public PlayerIdentity GetVote(PlayerIdentity voter) =>
            voter != null && votes.TryGetValue(voter, out var target) ? target : null;

        private void CheckAllVoted()
        {
            var round = GameRoundManager.Instance;
            if (!IsVoting || round == null) return;
            foreach (var p in round.Players)
            {
                if (p != null && !votes.ContainsKey(p)) return;
            }
            // Alle haben abgestimmt → Phase gleich beenden
            skipPending = true;
        }

        /// <summary>Zählt die Stimmen aus. Wird vom GameRoundManager am Ende der Phase aufgerufen.</summary>
        public VoteOutcome Evaluate(bool runoff)
        {
            IsVoting = false;
            skipPending = false;
            VotingEnded?.Invoke();

            tally.Clear();
            foreach (var c in candidates) tally[c] = 0;
            foreach (var target in votes.Values)
            {
                if (tally.ContainsKey(target)) tally[target]++;
            }

            int max = 0;
            foreach (var count in tally.Values) max = Mathf.Max(max, count);
            var leaders = new List<PlayerIdentity>();
            foreach (var pair in tally)
            {
                if (pair.Value == max) leaders.Add(pair.Key);
            }

            if (leaders.Count > 1 && !runoff)
            {
                // Stichwahl nur zwischen den Gleichplatzierten
                candidates.Clear();
                candidates.AddRange(leaders);
                Debug.Log($"[VoteManager] Gleichstand ({max} Stimmen) → Stichwahl zwischen {Names(leaders)}");
                return VoteOutcome.Gleichstand;
            }

            HasResult = true;
            if (leaders.Count != 1)
            {
                AccusedPlayer = null;
                MurdererWins = true;
                Debug.Log("[VoteManager] Wieder Gleichstand → der Mörder gewinnt.");
            }
            else
            {
                AccusedPlayer = leaders[0];
                MurdererWins = !AccusedPlayer.IsMurderer;
                Debug.Log($"[VoteManager] Beschuldigt: {AccusedPlayer.PlayerName} ({max} Stimmen) → {(MurdererWins ? "Mörder gewinnt" : "Ermittler gewinnen")}");
            }
            return VoteOutcome.Entschieden;
        }

        // ------------------------------------------------------------------ Dummies

        /// <summary>Dummies stimmen für den Kandidaten, der am besten zu den Spuren auf der Map passt.</summary>
        private void CastDummyVotes()
        {
            var round = GameRoundManager.Instance;
            if (round == null) return;
            var clues = ClueSpawner.Instance != null ? ClueSpawner.Instance.GetSpawnedClues() : new List<ClueObject>();

            foreach (var voter in round.Players)
            {
                if (voter == null || voter.IsLocal) continue;

                PlayerIdentity best = null;
                float bestScore = float.MinValue;
                foreach (var candidate in candidates)
                {
                    if (candidate == voter) continue;
                    float score = MatchingClues(candidate, clues) + UnityEngine.Random.Range(0f, dummyRandomness);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }
                if (best != null) votes[voter] = best;
            }
        }

        private static int MatchingClues(PlayerIdentity candidate, List<ClueObject> clues)
        {
            int count = 0;
            foreach (var clue in clues)
            {
                var data = clue.Data;
                bool match = data.category switch
                {
                    ClueCategory.Blut => data.bloodType == candidate.Traits.blood,
                    ClueCategory.Schuh => data.shoeProfile == candidate.Traits.shoe,
                    _ => data.clothingColor == candidate.Traits.color,
                };
                if (match) count++;
            }
            return count;
        }

        private static string Names(List<PlayerIdentity> players)
        {
            var names = new List<string>();
            foreach (var p in players) names.Add(p.PlayerName);
            return string.Join(", ", names);
        }
    }
}
