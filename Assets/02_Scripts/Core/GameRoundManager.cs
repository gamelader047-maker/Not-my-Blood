using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using NotMyBlood.Player;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Steuert den Ablauf der Spielrunde, Phasenwechsel, Timer und Initialisierung der Spieleridentitäten.
    /// </summary>
    public class GameRoundManager : MonoBehaviour
    {
        public static GameRoundManager Instance { get; private set; }

        public event Action<GamePhase> PhaseChanged;

        [Header("Runden-Einstellungen")]
        [SerializeField] private GamePhase currentPhase = GamePhase.Ermittlung;
        [SerializeField] private float phaseDuration = 300f; // 5 Minuten Standarddauer
        [SerializeField] private List<PlayerIdentity> registeredPlayers = new List<PlayerIdentity>();

        public GamePhase CurrentPhase
        {
            get => currentPhase;
            private set
            {
                if (currentPhase != value)
                {
                    currentPhase = value;
                    PhaseChanged?.Invoke(currentPhase);
                }
            }
        }

        public float PhaseTimeRemaining { get; private set; }

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
            PhaseTimeRemaining = phaseDuration;
            SetupTestDummiesAndPlayers();
            StartRound();
        }

        private void Update()
        {
            // Timer herunterzählen, sofern nicht in Lobby oder Auflösung
            if (CurrentPhase != GamePhase.Lobby && CurrentPhase != GamePhase.Aufloesung)
            {
                if (PhaseTimeRemaining > 0f)
                {
                    PhaseTimeRemaining -= Time.deltaTime;
                }
            }

#if UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
            {
                LogDebugPlayerStatus();
            }
#endif
        }

        /// <summary>
        /// Sucht alle aktiven PlayerIdentity-Komponenten in der Szene.
        /// </summary>
        public void SetupTestDummiesAndPlayers()
        {
            registeredPlayers.Clear();
            PlayerIdentity[] foundPlayers = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None);
            registeredPlayers.AddRange(foundPlayers);
        }

        /// <summary>
        /// Startet eine neue Spielrunde und verteilt Merkmale sowie die Mörder-Rolle.
        /// </summary>
        public void StartRound()
        {
            if (registeredPlayers.Count == 0)
            {
                SetupTestDummiesAndPlayers();
            }

            bool success = TraitAssigner.Assign(registeredPlayers);
            if (success)
            {
                Debug.Log($"[GameRoundManager] Runde erfolgreich gestartet. {registeredPlayers.Count} Spieler zugewiesen.");
                CurrentPhase = GamePhase.Ermittlung;
                PhaseTimeRemaining = phaseDuration;
            }
        }

        /// <summary>
        /// Loggt den aktuellen Status aller Spieler und deren Eigenschaften für Debug-Zwecke.
        /// </summary>
        public void LogDebugPlayerStatus()
        {
            Debug.Log("=== PLAYER IDENTITIES & TRAITS (F6 DEBUG) ===");
            foreach (var p in registeredPlayers)
            {
                string murdererLabel = p.IsMurderer ? " [MÖRDER]" : "";
                Debug.Log($"{p.PlayerName}{murdererLabel} | Blut: {p.Traits.blood} | Schuh: {p.Traits.shoe} | Farbe: {p.Traits.color}");
            }
            Debug.Log("==============================================");
        }
    }
}