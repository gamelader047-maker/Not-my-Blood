using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NotMyBlood.Player;

namespace NotMyBlood.UI
{
    /// <summary>
    /// HUD in der Bildmitte: Ein Ring füllt sich, solange E auf einem Halte-Ziel gehalten wird
    /// (z. B. 3 s Koffer-Analyse). Danach wird kurz das Ergebnis angezeigt – nur für diesen Spieler.
    /// </summary>
    public class InteractionHoldUI : MonoBehaviour
    {
        [Tooltip("Leer = wird automatisch in der Szene gesucht (auch wenn der Player später spawnt).")]
        [SerializeField] private PlayerInteraction player;
        [SerializeField] private GameObject holdRoot;
        [Tooltip("Image vom Typ Filled (Radial 360).")]
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text holdLabel;
        [SerializeField] private TMP_Text resultLabel;
        [SerializeField] private float resultDisplayTime = 3f;

        private PlayerInteraction subscribed;
        private PlayerKnowledge knowledgeSubscribed;
        private float hideResultAt;
        private float nextSearchTime;

        private void OnDisable()
        {
            Subscribe(null);
            if (knowledgeSubscribed != null) knowledgeSubscribed.Feedback -= ShowResult;
            knowledgeSubscribed = null;
        }

        private void Update()
        {
            FindPlayerIfNeeded();
            if (player != subscribed) Subscribe(player);

            // Meldungen vom Nadeltest, z. B. "Nadeltest Dummy 2: Blutgruppe A"
            if (PlayerKnowledge.Instance != knowledgeSubscribed)
            {
                if (knowledgeSubscribed != null) knowledgeSubscribed.Feedback -= ShowResult;
                knowledgeSubscribed = PlayerKnowledge.Instance;
                if (knowledgeSubscribed != null) knowledgeSubscribed.Feedback += ShowResult;
            }

            bool holding = player != null && player.IsHolding;
            if (holdRoot != null && holdRoot.activeSelf != holding) holdRoot.SetActive(holding);

            if (holding)
            {
                if (fillImage != null) fillImage.fillAmount = player.HoldProgress;
                if (holdLabel != null) holdLabel.text = $"{player.HoldLabel} … {player.HoldRemaining:0.0} s";
            }

            if (resultLabel != null && resultLabel.gameObject.activeSelf && Time.time >= hideResultAt)
            {
                resultLabel.gameObject.SetActive(false);
            }
        }

        private void FindPlayerIfNeeded()
        {
            if (player != null || Time.time < nextSearchTime) return;
            nextSearchTime = Time.time + 0.5f;
            var found = FindObjectsByType<PlayerInteraction>();
            if (found.Length > 0) player = found[0];
        }

        private void Subscribe(PlayerInteraction target)
        {
            if (subscribed != null) subscribed.HoldCompleted -= ShowResult;
            subscribed = target;
            if (subscribed != null) subscribed.HoldCompleted += ShowResult;
        }

        private void ShowResult(string message)
        {
            if (resultLabel == null || string.IsNullOrEmpty(message)) return;
            resultLabel.text = message;
            resultLabel.gameObject.SetActive(true);
            hideResultAt = Time.time + resultDisplayTime;
        }
    }
}
