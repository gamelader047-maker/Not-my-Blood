using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NotMyBlood.UI
{
    /// <summary>Kleines Wert-Kästchen im Notizbuch, z. B. "A", "Kreise" oder "● Schwarz".</summary>
    public class NotebookChip : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [Tooltip("Farbpunkt, nur bei Fasern sichtbar.")]
        [SerializeField] private Image colorDot;

        public void Set(string text, Color? dotColor = null)
        {
            if (label != null) label.text = text;

            if (colorDot != null)
            {
                colorDot.gameObject.SetActive(dotColor.HasValue);
                if (dotColor.HasValue) colorDot.color = dotColor.Value;
            }
        }
    }
}
