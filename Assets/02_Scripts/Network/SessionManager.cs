using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using NotMyBlood.Core;
using NotMyBlood.Player;

namespace NotMyBlood.Network
{
    /// <summary>
    /// Verbindet die Spieler übers Internet (Unity Multiplayer Services: Session + Relay).
    /// - Hosten: erstellt eine private Session, die anderen treten mit dem Code bei
    /// - Beitreten: per Code
    /// - Solo: startet nur lokal als Host (ohne Internet), zum Testen mit Dummies
    /// Verlassen oder Verbindungsabbruch lädt die Szene neu – so startet alles sauber von vorne.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        public const int MaxPlayers = 6;
        public const int MaxNameLength = 16;
        private const string NamePrefKey = "nmb_player_name";

        /// <summary>Meldung, die nach dem Neuladen der Szene im Menü steht (z. B. "Der Host hat das Spiel beendet").</summary>
        private static string pendingStatus;
        /// <summary>Eigenes Profil pro Programmstart, damit zwei Fenster auf demselben Rechner verschiedene Spieler sind.</summary>
        private static string profileName;

        public bool IsBusy { get; private set; }
        public bool IsSolo { get; private set; }
        public ISession Session { get; private set; }
        public string JoinCode => Session != null ? Session.Code : "";
        public string Status { get; private set; } = "";

        public bool IsConnected
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && (nm.IsClient || nm.IsServer);
            }
        }

        public static string LocalPlayerName
        {
            get => PlayerPrefs.GetString(NamePrefKey, "");
            set
            {
                PlayerPrefs.SetString(NamePrefKey, CleanName(value));
                PlayerPrefs.Save();
            }
        }

        private bool leaving;
        private NetworkManager subscribedManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            UIState.ResetAll();
            if (pendingStatus != null)
            {
                Status = pendingStatus;
                pendingStatus = null;
            }
        }

        private void Start()
        {
            OnlinePlayer.LocalSpawned += OnLocalPlayerSpawned;
        }

        private void Update()
        {
            // NetworkManager kann nach einem Neuladen neu sein → Abo nachziehen
            var nm = NetworkManager.Singleton;
            if (nm == subscribedManager) return;
            if (subscribedManager != null) subscribedManager.OnClientStopped -= OnClientStopped;
            subscribedManager = nm;
            if (subscribedManager != null) subscribedManager.OnClientStopped += OnClientStopped;
        }

        private void OnDestroy()
        {
            OnlinePlayer.LocalSpawned -= OnLocalPlayerSpawned;
            if (subscribedManager != null) subscribedManager.OnClientStopped -= OnClientStopped;
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ Hosten / Beitreten / Solo

        public async void Host()
        {
            if (IsBusy || IsConnected) return;
            SetBusy(true, "Spiel wird erstellt …");
            try
            {
                await EnsureSignedInAsync();
                var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }.WithRelayNetwork();
                Session = await MultiplayerService.Instance.CreateSessionAsync(options);
                SetBusy(false, $"Spiel erstellt – Code: {Session.Code}");
                Debug.Log($"[Session] Gehostet. Beitritts-Code: {Session.Code}");
            }
            catch (Exception e)
            {
                Fail("Hosten hat nicht geklappt", e);
            }
        }

        public async void Join(string code)
        {
            if (IsBusy || IsConnected) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                SetStatus("Bitte zuerst den Code eingeben.");
                return;
            }

            SetBusy(true, $"Trete {code} bei …");
            try
            {
                await EnsureSignedInAsync();
                Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                SetBusy(false, "Verbunden.");
                Debug.Log($"[Session] Beigetreten: {code}");
            }
            catch (Exception e)
            {
                Fail("Beitreten hat nicht geklappt", e);
            }
        }

        /// <summary>Nur lokal als Host starten (kein Internet nötig) – die Runde startet sofort mit den Dummies.</summary>
        public void StartSolo()
        {
            if (IsBusy || IsConnected) return;
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                SetStatus("Kein NetworkManager in der Szene – \"Multiplayer einrichten\" ausführen.");
                return;
            }

            if (nm.NetworkConfig.NetworkTransport is UnityTransport transport)
            {
                transport.SetConnectionData("127.0.0.1", 7777);
            }

            IsSolo = true;
            if (!nm.StartHost())
            {
                IsSolo = false;
                SetStatus("Solo-Start hat nicht geklappt (Port 7777 belegt?).");
                return;
            }
            SetStatus("Solo-Test läuft.");
            StartCoroutine(StartSoloRoundWhenReady());
        }

        private IEnumerator StartSoloRoundWhenReady()
        {
            while (OnlinePlayer.Local == null) yield return null;
            yield return null;
            if (GameRoundManager.Instance != null) GameRoundManager.Instance.StartRound();
        }

        private void OnLocalPlayerSpawned(OnlinePlayer player)
        {
            if (IsSolo) return;

            // Mit echten Mitspielern: die Test-Dummies (nur lokal, nicht im Netzwerk) ausblenden
            foreach (var identity in FindObjectsByType<PlayerIdentity>())
            {
                if (!identity.TryGetComponent<NetworkObject>(out _)) identity.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ Verlassen

        /// <summary>Session verlassen und die Szene neu laden (zurück ins Hauptmenü).</summary>
        public void Leave(string message = null)
        {
            if (leaving) return;
            StartCoroutine(LeaveRoutine(message));
        }

        private IEnumerator LeaveRoutine(string message)
        {
            leaving = true;
            pendingStatus = message;

            if (Session != null)
            {
                Task task = null;
                try
                {
                    task = Session.LeaveAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Session] Verlassen: {e.Message}");
                }
                Session = null;

                float timeout = Time.realtimeSinceStartup + 5f;
                while (task != null && !task.IsCompleted && Time.realtimeSinceStartup < timeout) yield return null;
                if (task != null && task.IsFaulted) Debug.LogWarning($"[Session] Verlassen: {task.Exception?.GetBaseException().Message}");
            }

            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientStopped -= OnClientStopped;
                if (nm.IsListening) nm.Shutdown();
                float timeout = Time.realtimeSinceStartup + 3f;
                while (nm != null && nm.ShutdownInProgress && Time.realtimeSinceStartup < timeout) yield return null;
                if (nm != null) Destroy(nm.gameObject);   // die neu geladene Szene bringt einen frischen NetworkManager mit
            }

            yield return null;
            UIState.ResetAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OnClientStopped(bool wasHost)
        {
            if (leaving) return;
            var nm = NetworkManager.Singleton;
            string reason = nm != null ? nm.DisconnectReason : null;
            Leave(string.IsNullOrEmpty(reason)
                ? "Verbindung beendet – der Host hat das Spiel verlassen oder das Internet war weg."
                : $"Verbindung beendet: {reason}");
        }

        // ------------------------------------------------------------------ Anmelden

        private static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                if (profileName == null) profileName = "p" + Guid.NewGuid().ToString("N").Substring(0, 12);
                var options = new InitializationOptions().SetProfile(profileName);
                await UnityServices.InitializeAsync(options);
            }
            while (UnityServices.State == ServicesInitializationState.Initializing)
            {
                await Task.Yield();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        // ------------------------------------------------------------------ Status

        private void Fail(string what, Exception e)
        {
            Debug.LogException(e);
            string detail = e is SessionException se ? $"{se.Error} – {se.Message}" : e.Message;
            IsSolo = false;
            Session = null;
            SetBusy(false, $"{what}.\n<size=80%>{detail}</size>");
            if (IsConnected) Leave(Status);
        }

        private void SetBusy(bool busy, string status)
        {
            IsBusy = busy;
            Status = status;
        }

        private void SetStatus(string status) => Status = status;

        public static string CleanName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
            return name;
        }
    }
}
