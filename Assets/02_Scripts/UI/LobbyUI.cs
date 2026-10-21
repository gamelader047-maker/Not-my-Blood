using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using NotMyBlood.Core;
using NotMyBlood.Network;

namespace NotMyBlood.UI
{
    /// <summary>
    /// Hauptmenü (Name, Hosten, Beitreten per Code, Solo-Test), Code-Anzeige im Spiel und Pause-Menü (Esc).
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [Header("Hauptmenü")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button soloButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Im Spiel")]
        [SerializeField] private GameObject sessionInfo;
        [SerializeField] private TMP_Text sessionInfoText;

        [Header("Pause (Esc)")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private TMP_Text pauseCodeText;
        [SerializeField] private TMP_Text pausePlayersText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button leaveButton;

        [Header("Kamera, solange man noch nicht im Spiel ist")]
        [SerializeField] private Camera lobbyCamera;

        private bool wasInGame;
        private bool wasMenuOpen;

        private void Start()
        {
            if (hostButton != null) hostButton.onClick.AddListener(Host);
            if (joinButton != null) joinButton.onClick.AddListener(Join);
            if (soloButton != null) soloButton.onClick.AddListener(Solo);
            if (quitButton != null) quitButton.onClick.AddListener(Application.Quit);
            if (resumeButton != null) resumeButton.onClick.AddListener(() => SetPause(false));
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);

            if (nameInput != null)
            {
                nameInput.characterLimit = SessionManager.MaxNameLength;
                nameInput.text = SessionManager.LocalPlayerName;
            }
            if (codeInput != null)
            {
                codeInput.characterLimit = 10;
                codeInput.onValidateInput += (text, index, c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
                codeInput.onSubmit.AddListener(_ => Join());
            }

            if (pausePanel != null) pausePanel.SetActive(false);
            ApplyInGame(false, force: true);
        }

        private void Update()
        {
            var session = SessionManager.Instance;
            bool inGame = OnlinePlayer.Local != null;
            if (inGame != wasInGame) ApplyInGame(inGame, force: false);

            if (!inGame)
            {
                bool ready = session != null && !session.IsBusy && !session.IsConnected;
                SetInteractable(hostButton, ready);
                SetInteractable(joinButton, ready);
                SetInteractable(soloButton, ready);
                if (statusText != null) statusText.text = session != null ? session.Status : "";
            }
            else
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !UIState.IsNotebookOpen)
                {
                    SetPause(pausePanel == null || !pausePanel.activeSelf);
                }
                UpdateSessionTexts(session);
            }

            bool menuOpen = (menuPanel != null && menuPanel.activeSelf) || (pausePanel != null && pausePanel.activeSelf);
            UIState.IsMenuOpen = menuOpen;
            if (menuOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (wasMenuOpen && !UIState.BlocksPlayerInput)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            wasMenuOpen = menuOpen;
        }

        // ------------------------------------------------------------------ Buttons

        private void Host()
        {
            SaveName();
            SessionManager.Instance?.Host();
        }

        private void Join()
        {
            SaveName();
            SessionManager.Instance?.Join(codeInput != null ? codeInput.text : "");
        }

        private void Solo()
        {
            SaveName();
            SessionManager.Instance?.StartSolo();
        }

        private void Leave()
        {
            SetPause(false);
            SessionManager.Instance?.Leave("Du hast das Spiel verlassen.");
        }

        private void SaveName()
        {
            if (nameInput != null) SessionManager.LocalPlayerName = nameInput.text;
        }

        // ------------------------------------------------------------------ Anzeige

        private void ApplyInGame(bool inGame, bool force)
        {
            wasInGame = inGame;
            if (menuPanel != null) menuPanel.SetActive(!inGame);
            if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(!inGame);
            if (!inGame && pausePanel != null) pausePanel.SetActive(false);
            if (sessionInfo != null) sessionInfo.SetActive(false);
        }

        private void SetPause(bool open)
        {
            if (pausePanel != null) pausePanel.SetActive(open);
        }

        private void UpdateSessionTexts(SessionManager session)
        {
            bool online = session != null && !session.IsSolo;
            string code = online ? session.JoinCode : "";
            int count = OnlinePlayer.All.Count;

            if (sessionInfo != null)
            {
                bool show = online && !string.IsNullOrEmpty(code);
                if (sessionInfo.activeSelf != show) sessionInfo.SetActive(show);
                if (show && sessionInfoText != null)
                {
                    sessionInfoText.text = $"Code: <b>{code}</b>   ·   {count} Spieler   ·   [Esc] Menü";
                }
            }

            if (pausePanel != null && pausePanel.activeSelf)
            {
                if (pauseCodeText != null) pauseCodeText.text = online ? $"Code: <b>{code}</b>" : "Solo-Test";
                if (pausePlayersText != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"<b>Im Spiel ({count}/{SessionManager.MaxPlayers}):</b>");
                    foreach (var p in OnlinePlayer.All)
                    {
                        if (p == null) continue;
                        string me = p == OnlinePlayer.Local ? "  <color=#7a5a3c>(du)</color>" : "";
                        sb.AppendLine($"• {p.DisplayName}{me}");
                    }
                    pausePlayersText.text = sb.ToString();
                }
            }
        }

        private static void SetInteractable(Button button, bool on)
        {
            if (button != null && button.interactable != on) button.interactable = on;
        }
    }
}
