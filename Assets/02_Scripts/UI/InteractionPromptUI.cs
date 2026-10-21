using TMPro;
using UnityEngine;
using NotMyBlood.Core;
using NotMyBlood.Player;

namespace NotMyBlood.UI
{
    /// <summary>
    /// Hinweis unter dem Fadenkreuz, sobald man auf etwas Interaktives schaut:
    /// "[E] Untersuchen: Blutspur" und – wenn möglich – "[E halten] Analysieren".
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction player;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private GameObject holdRow;
        [Tooltip("Leer = Text \"Text\" in der Hold-Zeile wird automatisch gesucht.")]
        [SerializeField] private TMP_Text holdText;

        private float nextSearch;

        private void Update()
        {
            if (player == null && Time.time >= nextSearch)
            {
                nextSearch = Time.time + 0.5f;
                var found = FindObjectsByType<PlayerInteraction>();
                if (found.Length > 0) player = found[0];
            }

            IInteractable target = player != null ? player.CurrentTarget : null;
            bool targetAlive = target is Object unityObject ? unityObject != null : target != null;
            bool visible = targetAlive && !player.IsHolding && !UIState.IsNotebookOpen;

            if (root != null && root.activeSelf != visible) root.SetActive(visible);
            if (!visible) return;

            if (promptText != null) promptText.text = target.GetInteractionPrompt();
            if (holdRow != null && holdRow.activeSelf != player.CanHoldCurrentTarget) holdRow.SetActive(player.CanHoldCurrentTarget);

            // Beschriftung passend zum Ziel: "Analysiere Spur" oder "Fotografiere Schuh"
            if (player.CanHoldCurrentTarget && target is IHoldInteractable holdable)
            {
                if (holdText == null && holdRow != null)
                {
                    var t = holdRow.transform.Find("Text");
                    if (t != null) holdText = t.GetComponent<TMP_Text>();
                }
                if (holdText != null) holdText.text = holdable.HoldActionLabel;
            }
        }
    }
}
