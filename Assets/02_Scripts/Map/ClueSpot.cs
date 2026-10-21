using UnityEngine;
using NotMyBlood.Core;

namespace NotMyBlood.Clues
{
    /// <summary>
    /// Repräsentiert einen festen Spurenpunkt in der Szene (vom Editor-Tool generiert).
    /// </summary>
    public class ClueSpot : MonoBehaviour
    {
        [Header("Spot Infos")]
        [SerializeField] private int spotId;
        [SerializeField] private string roomName = "Wohnzimmer";
        [SerializeField] private bool allowsFloorClues = true;
        [SerializeField] private bool allowsWallClues = true;
        [SerializeField] private bool extraFlag = false;

        public int SpotId => spotId;
        public string RoomName => roomName;
        public bool AllowsFloorClues => allowsFloorClues;
        public bool AllowsWallClues => allowsWallClues;

        /// <summary>
        /// Prüft, ob an diesem Spurenpunkt bereits ein ClueObject liegt.
        /// </summary>
        public bool IsOccupied => transform.childCount > 0;

        /// <summary>
        /// Wird vom TestHouseBuilder aufgerufen (4 Argumente).
        /// </summary>
        public void EditorSetup(int id, string room, bool floorAllowed, bool wallAllowed)
        {
            spotId = id;
            roomName = room;
            allowsFloorClues = floorAllowed;
            allowsWallClues = wallAllowed;
        }

        /// <summary>
        /// Wird vom TestHouseBuilder aufgerufen (5 Argumente: int, string, bool, bool, bool).
        /// </summary>
        public void EditorSetup(int id, string room, bool floorAllowed, bool wallAllowed, bool flag5)
        {
            EditorSetup(id, room, floorAllowed, wallAllowed);
            extraFlag = flag5;
        }

        /// <summary>
        /// Überladung für ClueCategory (falls an anderer Stelle genutzt).
        /// </summary>
        public void EditorSetup(int id, string room, bool floorAllowed, bool wallAllowed, ClueCategory category)
        {
            EditorSetup(id, room, floorAllowed, wallAllowed);
        }
    }
}