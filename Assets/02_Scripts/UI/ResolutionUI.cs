using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using NotMyBlood.Clues;
using NotMyBlood.Core;

namespace NotMyBlood.UI
{
    /// <summary>
    /// Auflösung (Konzept 1.1, Kap. 2 + 8): Wer hat gewonnen, wer war der Mörder,
    /// wie wurde abgestimmt und welche Spuren waren echt oder gefälscht. Button/Taste R = neue Runde.
    /// </summary>
    public class ResolutionUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button newRoundButton;

        [Header("Farben")]
        [SerializeField] private Color investigatorsColor = new Color(0.31f, 0.54f, 0.24f);
        [SerializeField] private Color murdererColor = new Color(0.66f, 0.20f, 0.16f);

        private int shownRound = -1;
        private bool isOpen;

        private void Awake()
        {
            if (newRoundButton != null) newRoundButton.onClick.AddListener(StartNewRound);
        }

        private void Update()
        {
            var round = GameRoundManager.Instance;
            bool show = round != null && (round.CurrentPhase == GamePhase.Aufloesung || round.IsRoundOver);

            if (show && !isOpen) Open(round);
            else if (!show && isOpen) Close();

            if (!isOpen) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) StartNewRound();
        }

        private void Open(GameRoundManager round)
        {
            isOpen = true;
            UIState.IsOverlayOpen = true;
            if (root != null) root.SetActive(true);
            if (shownRound != round.RoundNumber)
            {
                shownRound = round.RoundNumber;
                BuildText(round);
            }
        }

        private void Close()
        {
            isOpen = false;
            UIState.IsOverlayOpen = false;
            if (root != null) root.SetActive(false);
            if (!UIState.IsNotebookOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void StartNewRound()
        {
            var round = GameRoundManager.Instance;
            if (round == null) return;
            Close();
            round.StartRound();
        }

        private void BuildText(GameRoundManager round)
        {
            var votes = VoteManager.Instance;
            bool murdererWins = votes == null || !votes.HasResult || votes.MurdererWins;

            if (titleText != null)
            {
                titleText.text = murdererWins ? "Der Mörder gewinnt" : "Die Ermittler gewinnen";
                titleText.color = murdererWins ? murdererColor : investigatorsColor;
            }

            var sb = new StringBuilder();

            // Beschuldigt
            if (votes != null && votes.HasResult && votes.AccusedPlayer != null)
            {
                votes.LastTally.TryGetValue(votes.AccusedPlayer, out int count);
                sb.AppendLine($"Beschuldigt: <b>{votes.AccusedPlayer.PlayerName}</b> ({count} {(count == 1 ? "Stimme" : "Stimmen")})");
            }
            else
            {
                sb.AppendLine("Gleichstand auch in der Stichwahl – niemand wurde beschuldigt.");
            }

            // Mörder
            foreach (var p in round.Players)
            {
                if (p == null || !p.IsMurderer) continue;
                string you = p.IsLocal ? "  <i>(das warst du!)</i>" : "";
                sb.AppendLine($"Der Mörder war: <b>{p.PlayerName}</b>{you}");
                sb.AppendLine($"<size=85%>Blut {ClueData.BloodTypeText(p.Traits.blood)} · {p.Traits.shoe} · {p.Traits.color}</size>");
            }

            // Stimmen
            if (votes != null && votes.LastTally.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<b>Stimmen</b>");
                foreach (var pair in votes.LastTally)
                {
                    if (pair.Key != null) sb.AppendLine($"{pair.Key.PlayerName}: {pair.Value}");
                }
            }

            // Spuren
            if (ClueSpawner.Instance != null)
            {
                sb.AppendLine();
                sb.AppendLine("<b>Spuren</b>");
                foreach (var clue in ClueSpawner.Instance.GetSpawnedClues())
                {
                    var data = clue.Data;
                    string verdict = clue.IsFake ? "<color=#a8322a>GEFÄLSCHT</color>" : "<color=#4f8a3c>echt</color>";
                    sb.AppendLine($"{data.DisplayName} {data.ValueText} ({data.LocationText}) – {verdict}");
                }
            }

            if (bodyText != null) bodyText.text = sb.ToString();
        }
    }
}
