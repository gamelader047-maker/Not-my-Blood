using System.Collections.Generic;
using TMPro;
using UnityEngine;
using NotMyBlood.Core;
using NotMyBlood.Player;

namespace NotMyBlood.UI
{
    /// <summary>
    /// Abstimmungsfenster: ein Button pro Mitspieler (nicht man selbst), geheime Wahl, Restzeit.
    /// Bei der Stichwahl stehen nur die Gleichplatzierten zur Wahl.
    /// </summary>
    public class VotingUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private VoteCandidateButton buttonTemplate;

        private readonly List<VoteCandidateButton> buttons = new List<VoteCandidateButton>();
        private VoteManager subscribed;
        private bool isOpen;

        private void Update()
        {
            var manager = VoteManager.Instance;
            if (manager != subscribed)
            {
                if (subscribed != null)
                {
                    subscribed.VotingStarted -= Open;
                    subscribed.VotingEnded -= Close;
                }
                subscribed = manager;
                if (subscribed != null)
                {
                    subscribed.VotingStarted += Open;
                    subscribed.VotingEnded += Close;
                    if (subscribed.IsVoting && !isOpen) Open();
                }
            }

            if (!isOpen) return;

            // Maus frei lassen, auch wenn das Notizbuch zwischendurch zugeht
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var round = GameRoundManager.Instance;
            if (subtitleText != null && round != null)
            {
                int t = Mathf.CeilToInt(round.PhaseTimeRemaining);
                subtitleText.text = $"Wer ist der Mörder? Geheime Wahl · {t / 60:00}:{t % 60:00}";
            }
        }

        private void OnDisable()
        {
            if (subscribed != null)
            {
                subscribed.VotingStarted -= Open;
                subscribed.VotingEnded -= Close;
            }
            subscribed = null;
        }

        private void Open()
        {
            var manager = VoteManager.Instance;
            if (manager == null) return;

            isOpen = true;
            UIState.IsOverlayOpen = true;
            if (root != null) root.SetActive(true);
            if (titleText != null) titleText.text = manager.IsRunoff ? "Stichwahl" : "Abstimmung";

            ClearButtons();
            var me = LocalPlayer();
            foreach (var candidate in manager.Candidates)
            {
                if (candidate == null || candidate == me) continue;
                var button = Instantiate(buttonTemplate, listContent);
                button.gameObject.SetActive(true);
                var target = candidate;
                button.Setup(candidate, () => Vote(target));
                buttons.Add(button);
            }

            SetStatus(manager.IsRunoff
                ? "Gleichstand! Wähle zwischen den Gleichplatzierten."
                : "Klick auf den Spieler, den du für den Mörder hältst.");
        }

        private void Vote(PlayerIdentity target)
        {
            var manager = VoteManager.Instance;
            var me = LocalPlayer();
            if (manager == null || me == null || !manager.CastVote(me, target)) return;

            foreach (var b in buttons)
            {
                b.SetSelected(b.Player == target);
                b.SetInteractable(false);
            }
            SetStatus($"Deine Stimme: {target.PlayerName} – warte auf die anderen …");
        }

        private void Close()
        {
            isOpen = false;
            UIState.IsOverlayOpen = false;
            if (root != null) root.SetActive(false);
            ClearButtons();
            if (!UIState.IsNotebookOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void ClearButtons()
        {
            foreach (var b in buttons)
            {
                if (b == null) continue;
                b.gameObject.SetActive(false);
                Destroy(b.gameObject);
            }
            buttons.Clear();
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        private static PlayerIdentity LocalPlayer()
        {
            var round = GameRoundManager.Instance;
            if (round == null) return null;
            foreach (var p in round.Players)
            {
                if (p != null && p.IsLocal) return p;
            }
            return null;
        }
    }
}
