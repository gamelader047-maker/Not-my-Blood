using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using NotMyBlood.Core;

namespace NotMyBlood.Player
{
    /// <summary>
    /// Interaktion per Raycast nach vorne und Taste E:
    /// - Kurz drücken → IInteractable.Interact() (Spur notieren, Koffer aufheben, …)
    /// - Gedrückt halten → IHoldInteractable.HoldInteract() nach HoldDuration (z. B. 3 s Koffer-Analyse)
    ///
    /// Hat das Ziel eine Halte-Aktion, die der Spieler gerade ausführen darf, wird Interact() erst beim
    /// Loslassen ausgelöst – und nur bei einem kurzen Druck. So kann eine Analyse nie aus Versehen
    /// zusätzlich die Spur notieren.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float interactionRange = 3f;
        [Tooltip("Bis zu dieser Dauer gilt E als kurzer Druck. Längeres Halten ohne Abschluss = abgebrochen.")]
        [SerializeField] private float tapMaxDuration = 0.3f;

        /// <summary>Wird ausgelöst, wenn eine Halte-Aktion fertig ist. Parameter: Ergebnistext für die Anzeige.</summary>
        public event Action<string> HoldCompleted;

        /// <summary>True, solange E auf einem Halte-Ziel gehalten wird und die Aktion noch läuft.</summary>
        public bool IsHolding => holdTarget != null && !holdFinished;

        /// <summary>Fortschritt 0..1 der laufenden Halte-Aktion (für die Anzeige).</summary>
        public float HoldProgress => IsHolding ? Mathf.Clamp01(holdTime / Mathf.Max(0.01f, holdTarget.HoldDuration)) : 0f;

        /// <summary>Verbleibende Sekunden der laufenden Halte-Aktion.</summary>
        public float HoldRemaining => IsHolding ? Mathf.Max(0f, holdTarget.HoldDuration - holdTime) : 0f;

        /// <summary>Beschriftung der laufenden Halte-Aktion, z. B. "Analysiere Spur".</summary>
        public string HoldLabel => IsHolding ? holdTarget.HoldActionLabel : string.Empty;

        private const int MaxHits = 8;
        private readonly RaycastHit[] hits = new RaycastHit[MaxHits];
        private Camera playerCamera;

        private IHoldInteractable holdTarget;     // Ziel, auf dem E gerade gehalten wird
        private IInteractable holdTargetTap;      // dasselbe Objekt als IInteractable (für den kurzen Druck)
        private float holdTime;
        private bool holdFinished;                // Halte-Aktion ausgeführt → bis zum Loslassen nichts mehr tun

        private void Start()
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
            {
                Debug.LogError("PlayerInteraction: No Camera found on Player!");
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (playerCamera == null || keyboard == null || UIState.BlocksPlayerInput)
            {
                CancelHold();
                return;
            }

            IInteractable target = FindTarget();
            KeyControl interactKey = keyboard.eKey;

            if (holdTarget != null)
            {
                UpdateHold(target, interactKey);
                return;
            }

            if (target == null || !interactKey.wasPressedThisFrame) return;

            if (target is IHoldInteractable holdable && holdable.CanHoldInteract(gameObject))
            {
                BeginHold(holdable, target);
            }
            else
            {
                // Kein Halte-Ziel → sofort reagieren wie bisher
                target.Interact(gameObject);
            }
        }

        // ------------------------------------------------------------------ Halten

        private void BeginHold(IHoldInteractable holdable, IInteractable tapTarget)
        {
            holdTarget = holdable;
            holdTargetTap = tapTarget;
            holdTime = 0f;
            holdFinished = false;
        }

        private void UpdateHold(IInteractable currentTarget, KeyControl interactKey)
        {
            // Losgelassen: kurzer Druck → normales Interact(), sonst abbrechen
            if (!interactKey.isPressed)
            {
                if (!holdFinished && holdTime <= tapMaxDuration && IsAlive(holdTargetTap))
                {
                    holdTargetTap.Interact(gameObject);
                }
                ResetHold();
                return;
            }

            if (holdFinished) return;   // Aktion schon ausgeführt, warten bis E losgelassen wird

            // Weggeschaut oder Ziel verschwunden → abbrechen (ohne Interact)
            if (!IsAlive(holdTargetTap) || !ReferenceEquals(currentTarget, holdTargetTap))
            {
                CancelHold();
                return;
            }

            holdTime += Time.deltaTime;
            if (holdTime < holdTarget.HoldDuration) return;

            // Zeit abgelaufen → Halte-Aktion ausführen
            holdFinished = true;
            string result = holdTarget.CanHoldInteract(gameObject) ? holdTarget.HoldInteract(gameObject) : null;
            HoldCompleted?.Invoke(result);
        }

        /// <summary>Bricht ein laufendes Halten ab. E muss danach neu gedrückt werden.</summary>
        private void CancelHold()
        {
            if (holdTarget == null) return;
            // holdFinished = true sorgt dafür, dass beim Loslassen kein Interact() mehr kommt
            holdFinished = true;
            holdTime = 0f;
            if (!IsKeyHeld()) ResetHold();
        }

        private void ResetHold()
        {
            holdTarget = null;
            holdTargetTap = null;
            holdTime = 0f;
            holdFinished = false;
        }

        // ------------------------------------------------------------------ Ziel finden

        /// <summary>Nächstes Objekt im Blick, das nicht zum eigenen Player gehört (auch kein getragener Koffer).</summary>
        private IInteractable FindTarget()
        {
            Transform cam = playerCamera.transform;
            var ray = new Ray(cam.position + cam.forward * 0.3f, cam.forward);
            int count = Physics.RaycastNonAlloc(ray, hits, interactionRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            Collider closest = null;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (hits[i].distance < closestDistance)
                {
                    closestDistance = hits[i].distance;
                    closest = col;
                }
            }

            // Wände usw. blockieren: Nur das vorderste Objekt zählt
            return closest != null && closest.TryGetComponent(out IInteractable interactable) ? interactable : null;
        }

        private static bool IsKeyHeld() => Keyboard.current != null && Keyboard.current.eKey.isPressed;

        /// <summary>Unity-sicherer Null-Check für Interface-Referenzen (zerstörte Objekte).</summary>
        private static bool IsAlive(object target) =>
            target is UnityEngine.Object unityObject ? unityObject != null : target != null;
    }
}
