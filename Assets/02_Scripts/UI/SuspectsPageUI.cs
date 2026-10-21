using System.Collections.Generic;
using TMPro;
using UnityEngine;
using NotMyBlood.Player;

namespace NotMyBlood.UI
{
    /// <summary>
    /// Notizbuch-Seite "Verdächtige": eine Karte pro Spieler mit dem, was man über ihn weiss
    /// (Kleidung sichtbar, Blut aus dem Nadeltest, Schuh aus dem Foto) und einem Status-Stempel
    /// (unklar / verdächtig / entlastet), den man per Klick wechselt.
    /// Liegt auf dem NotebookPanel und baut sich beim Öffnen und bei jeder Änderung neu auf.
    /// </summary>
    public class SuspectsPageUI : MonoBehaviour
    {
        [SerializeField] private RectTransform listContent;
        [SerializeField] private SuspectRow rowTemplate;
        [SerializeField] private TMP_Text needlesText;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private PlayerKnowledge subscribed;
        private bool refreshing;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            if (subscribed != null) subscribed.Changed -= Refresh;
            subscribed = null;
        }

        private void Subscribe()
        {
            var knowledge = PlayerKnowledge.Instance;
            if (knowledge == null || knowledge == subscribed) return;
            if (subscribed != null) subscribed.Changed -= Refresh;
            subscribed = knowledge;
            subscribed.Changed += Refresh;
        }

        public void Refresh()
        {
            // Beim ersten Zugriff einer neuen Runde baut PlayerKnowledge neu auf und meldet "Changed" –
            // dieser verschachtelte Aufruf wird ignoriert, sonst gäbe es jede Karte doppelt.
            if (refreshing) return;
            refreshing = true;
            try
            {
                RebuildRows();
            }
            finally
            {
                refreshing = false;
            }
        }

        private void RebuildRows()
        {
            foreach (var go in spawned)
            {
                if (go == null) continue;
                go.SetActive(false);
                Destroy(go);
            }
            spawned.Clear();

            var knowledge = PlayerKnowledge.Instance;
            if (knowledge == null || rowTemplate == null || listContent == null) return;

            foreach (var info in knowledge.Suspects)
            {
                var row = Instantiate(rowTemplate, listContent);
                row.name = "Suspect_" + info.Name;
                row.gameObject.SetActive(true);
                var player = info.player;
                row.Setup(info, () => knowledge.CycleStatus(player));
                spawned.Add(row.gameObject);
            }

            if (needlesText != null)
            {
                needlesText.text = $"Nadeltests übrig: <b>{knowledge.NeedlesLeft}</b> von {knowledge.NeedlesPerRound}";
            }
        }
    }
}
