using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NotMyBlood.Core;

namespace NotMyBlood.UI
{
    /// <summary>
    /// Zeigt oben in der Mitte die aktuelle Phase, die Restzeit und einen dünnen Fortschrittsbalken.
    /// In den letzten Sekunden einer Phase wird die Zeit rot.
    /// </summary>
    public class RoundTimerUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private TMP_Text timeText;
        [Tooltip("Image vom Typ Filled (Horizontal). Zeigt, wie viel der Phase schon vorbei ist.")]
        [SerializeField] private Image progressFill;

        [Header("Warnung")]
        [SerializeField] private float warningSeconds = 30f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = new Color(0.94f, 0.36f, 0.30f);

        private float phaseDuration;
        private GamePhase lastPhase = (GamePhase)(-1);

        private void Update()
        {
            var round = GameRoundManager.Instance;
            bool visible = round != null && round.CurrentPhase != GamePhase.Lobby;
            if (root != null && root.activeSelf != visible) root.SetActive(visible);
            if (!visible) return;

            // Beim Phasenwechsel die Gesamtdauer merken (Restzeit ist dann noch voll)
            if (round.CurrentPhase != lastPhase)
            {
                lastPhase = round.CurrentPhase;
                phaseDuration = Mathf.Max(0.01f, round.PhaseTimeRemaining);
                if (phaseText != null) phaseText.text = PhaseName(round.CurrentPhase).ToUpperInvariant();
            }

            float remaining = round.PhaseTimeRemaining;
            int total = Mathf.CeilToInt(remaining);
            if (timeText != null)
            {
                timeText.text = $"{total / 60:00}:{total % 60:00}";
                bool warn = remaining <= warningSeconds && round.CurrentPhase != GamePhase.Aufloesung;
                timeText.color = warn ? warningColor : normalColor;
            }

            if (progressFill != null) progressFill.fillAmount = 1f - remaining / phaseDuration;
        }

        public static string PhaseName(GamePhase phase) => phase switch
        {
            GamePhase.Lobby => "Lobby",
            GamePhase.Cutscene => "Cutscene",
            GamePhase.Ermittlung => "Ermittlung",
            GamePhase.Meeting => "Meeting",
            GamePhase.Abstimmung => "Abstimmung",
            GamePhase.Aufloesung => "Auflösung",
            _ => phase.ToString(),
        };
    }
}
