using UnityEngine;
using NotMyBlood.Core;
using NotMyBlood.Player;

namespace NotMyBlood.UI
{
    /// <summary>
    /// Tasten-Hinweise unten links (Tab Notizbuch, E Untersuchen, …).
    /// Verschwindet, solange das Notizbuch offen ist. Die Zeile "E halten – Analysieren"
    /// erscheint nur, wenn der Spieler den Analyse-Koffer trägt.
    /// </summary>
    public class ControlHintsUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [Tooltip("Zeile \"E halten – Analysieren\", nur mit Koffer sichtbar.")]
        [SerializeField] private GameObject analyzeRow;
        [SerializeField] private PlayerInteraction player;

        private float nextCheck;
        private bool carriesKit;

        private void Update()
        {
            bool visible = !UIState.IsNotebookOpen;
            if (root != null && root.activeSelf != visible) root.SetActive(visible);

            // Nur zweimal pro Sekunde nachschauen, ob der Koffer getragen wird
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 0.5f;
                if (player == null)
                {
                    var found = FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
                    if (found.Length > 0) player = found[0];
                }
                carriesKit = player != null && player.GetComponentInChildren<AnalysisKit>() != null;
            }

            if (analyzeRow != null && analyzeRow.activeSelf != carriesKit) analyzeRow.SetActive(carriesKit);
        }
    }
}
