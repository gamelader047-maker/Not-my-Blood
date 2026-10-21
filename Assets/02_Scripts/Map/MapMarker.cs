using UnityEngine;

namespace NotMyBlood.Map
{
    public enum MapMarkerType
    {
        Sicherungskasten,
        AnalyseKoffer,
        Spielerstart,
        MeetingPlatz,
    }

    /// <summary>
    /// Markiert einen wichtigen Punkt auf der Map (Sicherungskasten, Start des Analyse-Koffers,
    /// Spieler-Starts, Plätze am Meeting-Tisch). Später lesen GameRoundManager & Co. diese Punkte aus.
    /// </summary>
    public class MapMarker : MonoBehaviour
    {
        [SerializeField] private MapMarkerType type = MapMarkerType.Spielerstart;
        [SerializeField] private int index = 1;

        public MapMarkerType Type => type;
        public int Index => index;

#if UNITY_EDITOR
        /// <summary>Wird vom Test-Haus-Builder benutzt.</summary>
        public void EditorSetup(MapMarkerType markerType, int markerIndex)
        {
            type = markerType;
            index = markerIndex;
        }

        private void OnDrawGizmos()
        {
            Vector3 p = transform.position;
            string label;
            switch (type)
            {
                case MapMarkerType.Sicherungskasten:
                    Gizmos.color = new Color(0.95f, 0.76f, 0.19f);
                    Gizmos.DrawWireCube(p, new Vector3(0.5f, 0.9f, 0.5f));
                    label = "Sicherungskasten";
                    break;
                case MapMarkerType.AnalyseKoffer:
                    Gizmos.color = new Color(0.88f, 0.48f, 0.18f);
                    Gizmos.DrawWireCube(p, new Vector3(0.6f, 0.35f, 0.45f));
                    label = "Analyse-Koffer (Start)";
                    break;
                case MapMarkerType.MeetingPlatz:
                    Gizmos.color = new Color(0.55f, 0.42f, 0.29f);
                    Gizmos.DrawWireSphere(p + Vector3.up * 0.5f, 0.25f);
                    Gizmos.DrawRay(p + Vector3.up * 0.5f, transform.forward * 0.6f);
                    label = $"Platz {index}";
                    break;
                default:
                    Gizmos.color = new Color(0.31f, 0.54f, 0.24f);
                    Gizmos.DrawWireCube(p + Vector3.up * 1f, new Vector3(0.6f, 2f, 0.6f));
                    Gizmos.DrawRay(p + Vector3.up * 1.6f, transform.forward * 0.8f);
                    label = $"Start {index}";
                    break;
            }
            UnityEditor.Handles.Label(p + Vector3.up * 2.2f, label);
        }
#endif
    }
}
