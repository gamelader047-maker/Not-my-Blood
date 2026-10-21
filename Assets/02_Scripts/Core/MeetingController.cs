using System.Collections.Generic;
using UnityEngine;
using NotMyBlood.Map;
using NotMyBlood.Player;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Setzt die Spieler passend zur Phase hin (Konzept 1.1, Kap. 2 und 8):
    /// - Cutscene: alle auf die Spieler-Starts, Bewegung frei
    /// - Meeting: alle auf die Plätze am Meeting-Tisch, nur noch umschauen
    /// - Abstimmung, Stichwahl, Auflösung: bleiben sitzen
    /// </summary>
    public class MeetingController : MonoBehaviour
    {
        private void Start()
        {
            if (GameRoundManager.Instance != null)
            {
                GameRoundManager.Instance.PhaseChanged += OnPhaseChanged;
            }
        }

        private void OnDestroy()
        {
            if (GameRoundManager.Instance != null)
            {
                GameRoundManager.Instance.PhaseChanged -= OnPhaseChanged;
            }
            UIState.IsMovementLocked = false;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Cutscene:
                    UIState.IsMovementLocked = false;
                    MovePlayersTo(MapMarkerType.Spielerstart);
                    break;
                case GamePhase.Ermittlung:
                    UIState.IsMovementLocked = false;
                    break;
                case GamePhase.Meeting:
                    UIState.IsMovementLocked = true;
                    MovePlayersTo(MapMarkerType.MeetingPlatz);
                    break;
                default:
                    // Abstimmung, Stichwahl, Auflösung: sitzen bleiben
                    UIState.IsMovementLocked = phase != GamePhase.Lobby;
                    break;
            }
        }

        private void MovePlayersTo(MapMarkerType type)
        {
            var markers = new List<MapMarker>();
            foreach (var marker in FindObjectsByType<MapMarker>())
            {
                if (marker.Type == type) markers.Add(marker);
            }
            if (markers.Count == 0)
            {
                Debug.LogWarning($"[MeetingController] Keine Marker vom Typ {type} gefunden – erst das Test-Haus erstellen.");
                return;
            }
            markers.Sort((a, b) => a.Index.CompareTo(b.Index));

            var round = GameRoundManager.Instance;
            if (round == null) return;

            // Eigener Spieler zuerst (Start_1 / Platz_1), dann die anderen
            var players = new List<PlayerIdentity>();
            foreach (var p in round.Players)
            {
                if (p == null) continue;
                if (p.IsLocal) players.Insert(0, p);
                else players.Add(p);
            }

            for (int i = 0; i < players.Count; i++)
            {
                Transform target = markers[i % markers.Count].transform;
                Teleport(players[i], target.position, target.rotation);
            }
        }

        private static void Teleport(PlayerIdentity player, Vector3 floorPosition, Quaternion rotation)
        {
            if (player.TryGetComponent(out PlayerController controller))
            {
                controller.Teleport(floorPosition, rotation);
                return;
            }

            // Dummy (Kapsel): Pivot liegt in der Mitte
            float pivotHeight = 1f;
            if (player.TryGetComponent(out CapsuleCollider capsule))
            {
                pivotHeight = capsule.height * 0.5f * player.transform.lossyScale.y - capsule.center.y;
            }
            player.transform.SetPositionAndRotation(floorPosition + Vector3.up * pivotHeight, rotation);
        }
    }
}
