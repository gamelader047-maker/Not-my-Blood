using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NotMyBlood.Core;
using NotMyBlood.Player;

namespace NotMyBlood.UI
{
    /// <summary>
    /// Rollen-Karte während der Cutscene: Jeder sieht, ob er Ermittler oder Mörder ist,
    /// sein Ziel in einem Satz und seine eigenen Merkmale. Verschwindet mit dem Start der Ermittlung.
    /// </summary>
    public class RoleRevealUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private TMP_Text goalText;
        [SerializeField] private TMP_Text traitsText;
        [SerializeField] private TMP_Text hintText;
        [Tooltip("Farbiger Streifen oben auf der Karte (optional).")]
        [SerializeField] private Image accent;

        [Header("Farben")]
        [SerializeField] private Color investigatorColor = new Color(0.23f, 0.14f, 0.09f);
        [SerializeField] private Color murdererColor = new Color(0.66f, 0.20f, 0.16f);

        public const string InvestigatorGoal =
            "Einer von euch ist der Mörder. Die Spuren verraten seine Merkmale – aber manche sind gefälscht. " +
            "Findet ihn und wählt ihn raus.";

        public const string MurdererGoal =
            "Lenk den Verdacht auf einen anderen. Die echten Spuren zeigen auf dich, eine gefälschte liegt schon im Haus. " +
            "Am Sicherungskasten kannst du einmal das Licht ausschalten.";

        private int shownRound = -1;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            var round = GameRoundManager.Instance;
            bool show = round != null && round.CurrentPhase == GamePhase.Cutscene;
            PlayerIdentity me = show ? FindLocalPlayer(round) : null;
            show &= me != null;

            if (show && shownRound != round.RoundNumber)
            {
                shownRound = round.RoundNumber;
                Fill(me);
            }

            if (root != null && root.activeSelf != show) root.SetActive(show);
            if (show && hintText != null)
            {
                int seconds = Mathf.CeilToInt(round.PhaseTimeRemaining);
                hintText.text = $"Die Ermittlung beginnt in <b>{seconds}</b> s   ·   <b>Tab</b> Notizbuch   ·   <b>E</b> untersuchen";
            }
        }

        private void Fill(PlayerIdentity me)
        {
            bool murderer = me.IsMurderer;
            Color color = murderer ? murdererColor : investigatorColor;

            if (roleText != null)
            {
                roleText.text = murderer ? "Du bist der Mörder" : "Du bist Ermittler";
                roleText.color = color;
            }
            if (goalText != null) goalText.text = murderer ? MurdererGoal : InvestigatorGoal;
            if (accent != null) accent.color = color;

            if (traitsText != null)
            {
                var t = me.Traits;
                traitsText.text =
                    $"Deine Merkmale:   Blut <b>{ClueData.BloodTypeText(t.blood)}</b>   ·   Schuh <b>{t.shoe}</b>   ·   Kleidung <b>{t.color}</b>";
            }
        }

        private static PlayerIdentity FindLocalPlayer(GameRoundManager round)
        {
            foreach (var p in round.Players)
            {
                if (p != null && p.IsLocal) return p;
            }
            return null;
        }
    }
}
