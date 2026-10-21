using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NotMyBlood.Core;

namespace NotMyBlood.UI
{
    /// <summary>
    /// Zeigt die gesammelten Spuren im Notizbuch an:
    /// links die Liste, rechts die Details zur gewählten Spur und eine Übersicht nach Merkmal.
    /// Liegt auf dem NotebookPanel und baut sich bei jedem Öffnen neu auf (OnEnable).
    /// </summary>
    public class NotebookDisplay : MonoBehaviour
    {
        [Header("Linke Seite – Liste")]
        [SerializeField] private RectTransform listContent;
        [SerializeField] private NotebookClueRow rowTemplate;
        [Tooltip("Gestrichelte Platzhalter-Zeile am Ende der Liste (optional).")]
        [SerializeField] private GameObject emptyRowTemplate;
        [SerializeField] private GameObject emptyListHint;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Rechte Seite – Details")]
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private GameObject detailEmptyHint;
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailDescription;
        [SerializeField] private TMP_Text detailTrait;
        [SerializeField] private TMP_Text detailLocation;
        [SerializeField] private TMP_Text detailTime;
        [SerializeField] private TMP_Text stampValue;
        [SerializeField] private TMP_Text stampLabel;

        [Header("Rechte Seite – Übersicht nach Merkmal")]
        [SerializeField] private NotebookChip chipTemplate;
        [SerializeField] private RectTransform bloodChips;
        [SerializeField] private RectTransform shoeChips;
        [SerializeField] private RectTransform fiberChips;

        [Header("Icons")]
        [SerializeField] private Sprite bloodIcon;
        [SerializeField] private Sprite shoeIcon;
        [SerializeField] private Sprite fiberIcon;

        [Header("Farben der Faser-Punkte")]
        [SerializeField] private Color colorRot = new Color(0.66f, 0.20f, 0.16f);
        [SerializeField] private Color colorSchwarz = new Color(0.15f, 0.13f, 0.16f);
        [SerializeField] private Color colorGrau = new Color(0.58f, 0.58f, 0.60f);

        private static readonly List<ClueData> NoClues = new List<ClueData>();

        private readonly List<NotebookClueRow> rows = new List<NotebookClueRow>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private int selectedIndex = -1;
        private int seenCount;                 // so viele Spuren kannte der Spieler beim letzten Schliessen
        private ClueManager subscribedManager;

        private List<ClueData> Clues => ClueManager.Instance != null ? ClueManager.Instance.collectedClues : NoClues;

        private void OnEnable()
        {
            Subscribe();
            Refresh(selectNewest: true);
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        private void OnDisable()
        {
            Unsubscribe();
            seenCount = Clues.Count;   // alles, was jetzt da ist, gilt beim nächsten Öffnen nicht mehr als "neu"
        }

        private void Subscribe()
        {
            var manager = ClueManager.Instance;
            if (manager == null || manager == subscribedManager) return;
            Unsubscribe();
            subscribedManager = manager;
            subscribedManager.ClueAdded += OnClueAdded;
        }

        private void Unsubscribe()
        {
            if (subscribedManager != null) subscribedManager.ClueAdded -= OnClueAdded;
            subscribedManager = null;
        }

        private void OnClueAdded(ClueData clue) => Refresh(selectNewest: true);

        /// <summary>Baut Liste, Details und Übersicht neu auf.</summary>
        public void Refresh(bool selectNewest = false)
        {
            var clues = Clues;
            ClearSpawned();

            // Linke Seite: eine Zeile pro Spur
            if (rowTemplate != null && listContent != null)
            {
                for (int i = 0; i < clues.Count; i++)
                {
                    int index = i;
                    var clue = clues[i];
                    var row = Instantiate(rowTemplate, listContent);
                    row.name = $"Row_{i:00}_{clue.DisplayName}";
                    row.gameObject.SetActive(true);
                    row.Setup(clue, IconFor(clue.category), DotFor(clue), i >= seenCount, () => Select(index));
                    rows.Add(row);
                    spawned.Add(row.gameObject);
                }
            }

            if (emptyRowTemplate != null && listContent != null)
            {
                var empty = Instantiate(emptyRowTemplate, listContent);
                empty.name = "EmptyRow";
                empty.SetActive(true);
                spawned.Add(empty);
            }

            if (emptyListHint != null) emptyListHint.SetActive(clues.Count == 0);

            // Auswahl: neue Spur bevorzugen, sonst bisherige Auswahl behalten
            if (selectNewest && clues.Count > seenCount) selectedIndex = clues.Count - 1;
            if (selectedIndex < 0 || selectedIndex >= clues.Count) selectedIndex = clues.Count > 0 ? 0 : -1;
            Select(selectedIndex);

            BuildOverview(clues);
        }

        /// <summary>Zeigt die Spur mit diesem Index rechts im Detail an.</summary>
        public void Select(int index)
        {
            var clues = Clues;
            selectedIndex = index;
            for (int i = 0; i < rows.Count; i++) rows[i].SetSelected(i == index);

            bool hasClue = index >= 0 && index < clues.Count;
            if (detailRoot != null) detailRoot.SetActive(hasClue);
            if (detailEmptyHint != null) detailEmptyHint.SetActive(!hasClue);
            if (!hasClue) return;

            var clue = clues[index];
            if (detailIcon != null) detailIcon.sprite = IconFor(clue.category);
            SetText(detailTitle, clue.DisplayName);
            SetText(detailDescription, clue.DescriptionText);
            SetText(detailTrait, clue.TraitLabel);
            SetText(detailLocation, clue.LocationText);
            SetText(detailTime, clue.FoundTimeText);
            SetText(stampValue, clue.ValueText);
            SetText(stampLabel, clue.TraitLabel.ToUpperInvariant());
        }

        private void BuildOverview(List<ClueData> clues)
        {
            if (chipTemplate == null) return;

            var shown = new HashSet<string>();
            foreach (var clue in clues)
            {
                // Jeden Wert pro Merkmal nur einmal zeigen
                if (!shown.Add(clue.category + ":" + clue.ValueText)) continue;

                RectTransform parent = clue.category switch
                {
                    ClueCategory.Blut => bloodChips,
                    ClueCategory.Schuh => shoeChips,
                    _ => fiberChips,
                };
                if (parent == null) continue;

                var chip = Instantiate(chipTemplate, parent);
                chip.gameObject.SetActive(true);
                chip.Set(clue.ValueText, DotFor(clue));
                spawned.Add(chip.gameObject);
            }
        }

        private void ClearSpawned()
        {
            foreach (var go in spawned)
            {
                if (go == null) continue;
                go.SetActive(false);   // sofort aus dem Layout nehmen, Destroy passiert erst am Frame-Ende
                Destroy(go);
            }
            spawned.Clear();
            rows.Clear();
        }

        private Sprite IconFor(ClueCategory category) => category switch
        {
            ClueCategory.Blut => bloodIcon,
            ClueCategory.Schuh => shoeIcon,
            _ => fiberIcon,
        };

        private Color? DotFor(ClueData clue)
        {
            if (clue.category != ClueCategory.Faser) return null;
            return clue.clothingColor switch
            {
                ClothingColor.Rot => colorRot,
                ClothingColor.Schwarz => colorSchwarz,
                _ => colorGrau,
            };
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
