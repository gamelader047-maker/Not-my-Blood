using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Rendering;
using NotMyBlood.Map;
using NotMyBlood.Player;

namespace NotMyBlood.Network
{
    /// <summary>
    /// Netzwerk-Teil des Spieler-Prefabs.
    /// - Eigener Spieler: Steuerung, Kamera und E-Taste an, eigener Körper unsichtbar (nur Schatten)
    /// - Andere Spieler: nur Körper + Namensschild, Position kommt über den NetworkTransform
    /// - Name und Ducken werden an alle übertragen
    /// </summary>
    public class OnlinePlayer : NetworkBehaviour
    {
        /// <summary>Der Spieler an diesem Rechner (null, solange man nicht verbunden ist).</summary>
        public static OnlinePlayer Local { get; private set; }

        /// <summary>Alle Spieler im Spiel (auf jedem Rechner gleich).</summary>
        public static readonly List<OnlinePlayer> All = new List<OnlinePlayer>();

        /// <summary>Der eigene Spieler ist gespawnt (Menü ausblenden, Lobby-Kamera aus).</summary>
        public static event Action<OnlinePlayer> LocalSpawned;

        [Header("Nur für den eigenen Spieler")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;

        [Header("Für die anderen sichtbar")]
        [SerializeField] private Transform body;
        [SerializeField] private Renderer[] bodyRenderers = new Renderer[0];
        [SerializeField] private Transform nameTag;
        [SerializeField] private TMP_Text nameTagText;
        [Tooltip("Körpergrösse beim Ducken (Ducken halbiert die Spielerhöhe).")]
        [SerializeField] private float crouchBodyScale = 0.5f;

        private NetworkVariable<FixedString64Bytes> displayName = new NetworkVariable<FixedString64Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<bool> crouching = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public string DisplayName => displayName.Value.ToString();
        public Camera PlayerCamera => playerCamera;
        public PlayerIdentity Identity { get; private set; }

        private NetworkTransform netTransform;
        private Vector3 bodyBaseScale = Vector3.one;

        private void Awake()
        {
            Identity = GetComponent<PlayerIdentity>();
            netTransform = GetComponent<NetworkTransform>();
            if (body != null) bodyBaseScale = body.localScale;
            SetLocalComponents(false);   // erst beim Spawnen entscheiden, ob das "ich" bin
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!All.Contains(this)) All.Add(this);
            displayName.OnValueChanged += OnNameChanged;

            if (IsOwner)
            {
                Local = this;
                SetLocalComponents(true);
                foreach (var r in bodyRenderers)
                {
                    if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
                if (nameTag != null) nameTag.gameObject.SetActive(false);

                string chosen = SessionManager.CleanName(SessionManager.LocalPlayerName);
                if (string.IsNullOrEmpty(chosen)) chosen = $"Spieler {OwnerClientId + 1}";
                displayName.Value = new FixedString64Bytes(chosen);

                if (controller != null) controller.Teleported += OnTeleported;
                StartCoroutine(MoveToSpawnPointNextFrame());
                LocalSpawned?.Invoke(this);
            }
            else
            {
                SetLocalComponents(false);
            }

            ApplyName();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            displayName.OnValueChanged -= OnNameChanged;
            if (controller != null) controller.Teleported -= OnTeleported;
            if (Local == this) Local = null;
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            All.Remove(this);
            if (Local == this) Local = null;
            base.OnDestroy();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                bool c = controller != null && controller.IsCrouching;
                if (crouching.Value != c) crouching.Value = c;
            }
            else if (body != null)
            {
                // Ducken der anderen sichtbar machen (die Position sinkt schon über den NetworkTransform mit)
                float target = crouching.Value ? crouchBodyScale : 1f;
                Vector3 goal = new Vector3(bodyBaseScale.x, bodyBaseScale.y * target, bodyBaseScale.z);
                body.localScale = Vector3.Lerp(body.localScale, goal, Time.deltaTime * 12f);
            }
        }

        private void LateUpdate()
        {
            if (IsOwner || nameTag == null || !nameTag.gameObject.activeInHierarchy) return;
            Camera cam = Local != null ? Local.playerCamera : Camera.main;
            if (cam == null) return;
            nameTag.rotation = Quaternion.LookRotation(nameTag.position - cam.transform.position);
        }

        // ------------------------------------------------------------------ Name

        private void OnNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue) => ApplyName();

        private void ApplyName()
        {
            string n = DisplayName;
            if (string.IsNullOrEmpty(n)) return;
            gameObject.name = $"Spieler_{n}";
            if (nameTagText != null) nameTagText.text = n;
            if (Identity != null) Identity.SetDisplayName(n);
        }

        // ------------------------------------------------------------------ Spawn + Teleport

        private IEnumerator MoveToSpawnPointNextFrame()
        {
            yield return null;   // NetworkTransform ist dann fertig gespawnt

            var starts = new List<MapMarker>();
            foreach (var marker in FindObjectsByType<MapMarker>())
            {
                if (marker.Type == MapMarkerType.Spielerstart) starts.Add(marker);
            }
            if (starts.Count == 0 || controller == null) yield break;

            starts.Sort((a, b) => a.Index.CompareTo(b.Index));
            Transform start = starts[(int)(OwnerClientId % (ulong)starts.Count)].transform;
            controller.Teleport(start.position, start.rotation);
        }

        /// <summary>Nach einem Teleport (Spawn, Meeting) nicht über die Map "gleiten", sondern sofort springen.</summary>
        private void OnTeleported()
        {
            if (!IsSpawned || netTransform == null || !netTransform.CanCommitToTransform) return;
            netTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        }

        private void SetLocalComponents(bool on)
        {
            if (controller != null) controller.enabled = on;
            if (interaction != null) interaction.enabled = on;
            if (playerCamera != null) playerCamera.enabled = on;
            if (audioListener != null) audioListener.enabled = on;
        }
    }
}
