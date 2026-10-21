using UnityEngine;
using NotMyBlood.Core;

namespace NotMyBlood.Clues
{
    /// <summary>
    /// Repräsentiert einen Spurenpunkt in der Spielwelt.
    /// </summary>
    public class ClueSpot : MonoBehaviour
    {
        [SerializeField] private int spotId;
        [SerializeField] private string roomName = "Raum";
        [SerializeField] private bool allowBlood;
        [SerializeField] private bool allowShoe;
        [SerializeField] private bool allowFiber;

        public int SpotId => spotId;
        public string RoomName => roomName;
        public string LocationName => roomName;

        /// <summary>
        /// Prüft, ob die angegebene Spurkategorie an diesem Punkt erlaubt ist.
        /// </summary>
        public bool Allows(ClueCategory category)
        {
            switch (category)
            {
                case ClueCategory.Blut:
                    return allowBlood;
                case ClueCategory.Schuh:
                    return allowShoe;
                case ClueCategory.Faser:
                    return allowFiber;
                default:
                    return false;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Richtet den Spurenpunkt im Editor ein.
        /// </summary>
        public void EditorSetup(int id, string room, bool blood, bool shoe, bool fiber)
        {
            spotId = id;
            roomName = room;
            allowBlood = blood;
            allowShoe = shoe;
            allowFiber = fiber;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void OnDrawGizmos()
        {
            // Rote Kugel wenn Schuh (Boden) erlaubt, sonst blaue Kugel
            Gizmos.color = allowShoe ? Color.red : Color.blue;
            Gizmos.DrawSphere(transform.position, 0.3f);

            // Beschriftung im Szene-Fenster
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"S{spotId:00} {roomName}");
        }
#endif
    }
}