using UnityEngine;
using UnityEngine.InputSystem;
using NotMyBlood.Core;

namespace NotMyBlood.UI
{
    public class NotebookUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject notebookPanel; // Das Panel, das ein/ausgeschaltet wird

        private bool isOpen = false;

        private void Start()
        {
            if (notebookPanel != null)
            {
                notebookPanel.SetActive(false); // Zu Beginn ist das Notizbuch zu
            }
            UIState.IsNotebookOpen = false;
        }

        private void Update()
        {
            ToggleNotebook();
        }

        private void ToggleNotebook()
        {
            if (Keyboard.current == null) return;

            // Öffnen/Schließen mit der Tab-Taste oder I-Taste
            if (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.iKey.wasPressedThisFrame)
            {
                isOpen = !isOpen;
                UIState.IsNotebookOpen = isOpen; // sperrt Kamera, Bewegung und E-Taste

                if (notebookPanel != null)
                {
                    notebookPanel.SetActive(isOpen);
                }

                // Mauszeiger umschalten (im Notizbuch frei, im Spiel gefangen)
                if (isOpen)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    Debug.Log("[Notebook] Notizbuch geöffnet. Gesammelte Spuren: " + (ClueManager.Instance != null ? ClueManager.Instance.collectedClues.Count : 0));
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    Debug.Log("[Notebook] Notizbuch geschlossen.");
                }
            }
        }
    }
}