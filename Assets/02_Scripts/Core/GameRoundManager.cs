using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using NotMyBlood.Player;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Steuert den Ablauf einer Runde (Konzept 1.1, Kap. 2):
    /// Cutscene → Ermittlung → Meeting → Abstimmung → Auflösung, jeweils mit Timer.
    /// Beim Rundenstart werden Merkmale und Mörder-Rolle verteilt (TraitAssigner).
    /// Andere Systeme abonnieren PhaseChanged in Start() (nicht OnEnable), weil Instance erst nach Awake gesetzt ist.
    /// </summary>
    public class GameRoundManager : MonoBehaviour
    {
        public static GameRoundManager Instance { get; private set; }

        public event Action<GamePhase> PhaseChanged;

        [Header("Phasen-Dauern (Sekunden)")]
        [SerializeField] private float cutsceneDuration = 20f;
        [SerializeField] private float investigationDuration = 360f;
        [SerializeField] private float meetingDuration = 180f;
        [SerializeField] private float votingDuration = 30f;
        [SerializeField] private float resolutionDuration = 15f;

        [Header("Einstellungen")]
        [SerializeField] private bool startOnPlay = true;
        [Tooltip("Ab dieser Ermittlungszeit darf man die Ermittlung überspringen (Konzept: Bereit-Button ab Minute 4).")]
        [SerializeField] private float earliestInvestigationSkip = 240f;

        [Header("Spieler (wird beim Rundenstart automatisch gefüllt)")]
        [SerializeField] private List<PlayerIdentity> registeredPlayers = new List<PlayerIdentity>();

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;
        public float PhaseTimeRemaining { get; private set; }
        /// <summary>Sekunden seit Beginn der Ermittlung (für Fundzeiten im Notizbuch).</summary>
        public float InvestigationTime { get; private set; }
        public IReadOnlyList<PlayerIdentity> Players => registeredPlayers;

        private bool isRunning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (startOnPlay) StartRound();
        }

        private void Update()
        {
            HandleEditorInput();
            if (!isRunning) return;

            if (CurrentPhase == GamePhase.Ermittlung)
            {
                InvestigationTime += Time.deltaTime;
            }

            PhaseTimeRemaining = Mathf.Max(0f, PhaseTimeRemaining - Time.deltaTime);
            if (PhaseTimeRemaining <= 0f)
            {
                ProceedToNextPhase();
            }
        }

        /// <summary>Startet eine neue Runde: Spieler suchen, Merkmale + Mörder verteilen, Cutscene beginnen.</summary>
        public void StartRound()
        {
            RefreshPlayers();
            if (!TraitAssigner.Assign(registeredPlayers))
            {
                Debug.LogWarning("[GameRoundManager] Merkmale konnten nicht verteilt werden – Runde läuft trotzdem (Test).");
            }

            InvestigationTime = 0f;
            isRunning = true;
            ChangePhase(GamePhase.Cutscene);
            Debug.Log($"[GameRoundManager] Runde gestartet mit {registeredPlayers.Count} Spielern.");
        }

        /// <summary>Sucht alle Spieler (inkl. Test-Dummies) in der Szene.</summary>
        public void RefreshPlayers()
        {
            registeredPlayers.Clear();
            registeredPlayers.AddRange(FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None));
        }

        public void SetPlayerReady(bool ready)
        {
            // Kommt mit dem Multiplayer: Phase endet früher, wenn alle bereit sind
        }

        private void ProceedToNextPhase()
        {
            switch (CurrentPhase)
            {
                case GamePhase.Cutscene: ChangePhase(GamePhase.Ermittlung); break;
                case GamePhase.Ermittlung: ChangePhase(GamePhase.Meeting); break;
                case GamePhase.Meeting: ChangePhase(GamePhase.Abstimmung); break;
                case GamePhase.Abstimmung: ChangePhase(GamePhase.Aufloesung); break;
                case GamePhase.Aufloesung: isRunning = false; break;
            }
        }

        private void ChangePhase(GamePhase newPhase)
        {
            CurrentPhase = newPhase;
            PhaseTimeRemaining = GetDurationForPhase(newPhase);
            if (newPhase == GamePhase.Ermittlung) InvestigationTime = 0f;

            Debug.Log($"[GameRoundManager] Phasenwechsel zu: {CurrentPhase}");
            PhaseChanged?.Invoke(CurrentPhase);
        }

        private float GetDurationForPhase(GamePhase phase) => phase switch
        {
            GamePhase.Cutscene => cutsceneDuration,
            GamePhase.Ermittlung => investigationDuration,
            GamePhase.Meeting => meetingDuration,
            GamePhase.Abstimmung => votingDuration,
            GamePhase.Aufloesung => resolutionDuration,
            _ => 0f,
        };

        private void HandleEditorInput()
        {
#if UNITY_EDITOR
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // F5: Phase überspringen (Ermittlung erst ab Minute 4)
            if (isRunning && keyboard.f5Key.wasPressedThisFrame)
            {
                if (CurrentPhase == GamePhase.Ermittlung && InvestigationTime < earliestInvestigationSkip)
                {
                    Debug.Log($"[GameRoundManager] Ermittlung kann erst nach {earliestInvestigationSkip / 60f:0} Minuten übersprungen werden.");
                }
                else
                {
                    Debug.Log("[GameRoundManager] Phase per F5 übersprungen.");
                    ProceedToNextPhase();
                }
            }

            // F6: alle Spieler mit Merkmalen loggen
            if (keyboard.f6Key.wasPressedThisFrame)
            {
                LogPlayers();
            }
#endif
        }

        public void LogPlayers()
        {
            Debug.Log($"=== SPIELER & MERKMALE ({registeredPlayers.Count}) ===");
            foreach (var p in registeredPlayers)
            {
                if (p == null) continue;
                string murderer = p.IsMurderer ? "  [MÖRDER]" : "";
                Debug.Log($"{p.PlayerName}{murderer} | Blut: {ClueData.BloodTypeText(p.Traits.blood)} | Schuh: {p.Traits.shoe} | Farbe: {p.Traits.color}");
            }
        }
    }
}
