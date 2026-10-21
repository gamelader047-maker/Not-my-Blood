using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NotMyBlood.UI
{
    /// <summary>
    /// Lesezeichen-Tabs am Rand des Notizbuchs. Klick oder Q/E wechselt die Doppelseite.
    /// </summary>
    public class NotebookTabs : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public string name;
            public Button button;
            public Image background;
            [Tooltip("Rechts verankert (Pivot rechts), damit der aktive Tab nach links herausragt.")]
            public RectTransform rect;
            [Tooltip("Inhalte, die bei diesem Tab sichtbar sind (linke + rechte Seite).")]
            public GameObject[] pages;
        }

        [SerializeField] private Tab[] tabs = new Tab[0];
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite inactiveSprite;
        [Tooltip("So viel breiter ist der aktive Tab.")]
        [SerializeField] private float activeExtraWidth = 20f;

        private int current;
        private float[] baseWidths;

        private void Awake()
        {
            baseWidths = new float[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i].rect != null) baseWidths[i] = tabs[i].rect.sizeDelta.x;
                if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => Show(index));
            }
        }

        private void OnEnable()
        {
            if (tabs.Length > 0) Show(current);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || tabs.Length == 0) return;

            if (keyboard.qKey.wasPressedThisFrame) Show((current - 1 + tabs.Length) % tabs.Length);
            if (keyboard.eKey.wasPressedThisFrame) Show((current + 1) % tabs.Length);
        }

        public void Show(int index)
        {
            if (tabs.Length == 0) return;
            current = Mathf.Clamp(index, 0, tabs.Length - 1);

            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = i == current;
                var tab = tabs[i];

                if (tab.pages != null)
                {
                    foreach (var page in tab.pages)
                    {
                        if (page != null) page.SetActive(active);
                    }
                }

                if (tab.background != null) tab.background.sprite = active ? activeSprite : inactiveSprite;

                if (tab.rect != null && baseWidths != null && i < baseWidths.Length)
                {
                    var size = tab.rect.sizeDelta;
                    size.x = baseWidths[i] + (active ? activeExtraWidth : 0f);
                    tab.rect.sizeDelta = size;
                }
            }
        }
    }
}
