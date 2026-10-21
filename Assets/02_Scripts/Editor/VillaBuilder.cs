using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NotMyBlood.Clues;
using NotMyBlood.Core;
using NotMyBlood.Map;
using NotMyBlood.Player;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Tools > Not My Blood > Villa erstellen
    /// Baut eine zweistöckige Villa (Greybox, ohne Deko) und ersetzt damit das alte Test-Haus:
    ///
    /// Erdgeschoss:  Eingangshalle (Treppe, Spieler-Starts, Analyse-Koffer), Salon, Bibliothek,
    ///               Speisesaal (Meeting-Tisch), Küche (Tatort), Arbeitszimmer, Hauswirtschaftsraum (Sicherungskasten)
    /// Obergeschoss: Galerie, Schlafzimmer, Bad, Gästezimmer, Kinderzimmer, Atelier
    ///
    /// Koordinaten unten sind "Villa-Meter": x = 0..28 (West → Ost), z = 0..18 (Süd → Nord),
    /// y = Höhe. Die Villa-Mitte liegt im Ursprung der Szene. Kann beliebig oft neu gebaut werden.
    /// </summary>
    public static class VillaBuilder
    {
        private const string RootName = "Villa";
        private const string OldHouseName = "TestHouse";

        private const float Story = 3.2f;          // Boden EG → Boden OG
        private const float WallHeight = 3f;       // pro Stockwerk, darüber liegt die Decke (0.2 m)
        private const float DoorHeight = 2.3f;
        private const float Wall = 0.2f;
        private const float FloorLift = 0.02f;     // Bodenoberkante knapp über der Stockwerkshöhe

        // Treppe in der Eingangshalle, läuft nach Norden hoch. Das Loch in der Decke hat dieselbe Grösse.
        private const float StairX0 = 16f, StairX1 = 17.9f, StairZ0 = 2f, StairZ1 = 8f;
        private const int StairSteps = 16;

        private static readonly Vector3 Origin = new Vector3(14f, 0f, 9f);

        private static Material matWall, matCeiling, matStairs, matWood, matWoodDark, matFurniture, matWhite,
            matStone, matDoor, matRail, matGarden, matMetal, matChalk;

        private struct Room
        {
            public string name;
            public int level;
            public float x0, x1, z0, z1;
            public string floorHex;

            public Room(string name, int level, float x0, float x1, float z0, float z1, string floorHex)
            {
                this.name = name;
                this.level = level;
                this.x0 = x0;
                this.x1 = x1;
                this.z0 = z0;
                this.z1 = z1;
                this.floorHex = floorHex;
            }

            public float CenterX => (x0 + x1) * 0.5f;
            public float CenterZ => (z0 + z1) * 0.5f;
        }

        private static readonly Room[] Rooms =
        {
            // Erdgeschoss
            new Room("Eingangshalle", 0, 10, 18, 0, 10, "b9a888"),
            new Room("Salon", 0, 0, 10, 0, 10, "c9a06c"),
            new Room("Bibliothek", 0, 0, 10, 10, 18, "8e6a48"),
            new Room("Speisesaal", 0, 10, 18, 10, 18, "b58a5a"),
            new Room("Küche", 0, 18, 28, 10, 18, "b9c4c0"),
            new Room("Arbeitszimmer", 0, 18, 28, 4, 10, "9c7e5c"),
            new Room("Hauswirtschaftsraum", 0, 18, 28, 0, 4, "9a948a"),
            // Obergeschoss
            new Room("Galerie", 1, 10, 18, 0, 10, "cdbb95"),
            new Room("Schlafzimmer", 1, 0, 10, 0, 10, "a9b59a"),
            new Room("Bad", 1, 0, 10, 10, 18, "c8d3d6"),
            new Room("Gästezimmer", 1, 10, 18, 10, 18, "b7a6c2"),
            new Room("Kinderzimmer", 1, 18, 28, 10, 18, "d6c08a"),
            new Room("Atelier", 1, 18, 28, 0, 10, "c2b8a3"),
        };

        private static int nextSpotId;

        // ------------------------------------------------------------------ Menü

        [MenuItem("Tools/Not My Blood/Villa-Test einrichten (Villa + Modelle + Rollen-Karte)", priority = 0)]
        public static void BuildAll()
        {
            if (ModelKit.Texture("blood_pool.png") == null)
            {
                EditorUtility.DisplayDialog("Villa-Test",
                    "Die Texturen in Assets/08_Models/Textures fehlen noch.\n\nKurz warten, bis Unity sie importiert hat (oder Assets > Refresh), dann nochmals ausführen.",
                    "OK");
                return;
            }

            CluePrefabBuilder.Build();
            if (!Build(askBeforeReplacing: true)) return;
            RoleRevealBuilder.Build();

            EditorUtility.DisplayDialog("Villa-Test",
                "Fertig:\n• Villa mit 2 Stockwerken und 13 Räumen\n• neue Modelle für Spuren, Koffer und Sicherungskasten\n• Rollen-Karte in der Cutscene\n\nJetzt die Szene speichern (Cmd+S) und \"Solo testen\".",
                "OK");
        }

        [MenuItem("Tools/Not My Blood/Villa erstellen")]
        public static void BuildMenu() => Build(askBeforeReplacing: true);

        public static bool Build(bool askBeforeReplacing)
        {
            var oldHouse = GameObject.Find(OldHouseName);
            var oldVilla = GameObject.Find(RootName);
            if (askBeforeReplacing && (oldHouse != null || oldVilla != null))
            {
                string what = oldHouse != null ? "das alte Test-Haus" : "die bestehende Villa";
                if (!EditorUtility.DisplayDialog("Villa", $"Die Villa ersetzt {what} (Rückgängig mit Cmd+Z geht).", "Ersetzen", "Abbrechen"))
                {
                    return false;
                }
            }
            if (oldHouse != null) Undo.DestroyObjectImmediate(oldHouse);
            if (oldVilla != null) Undo.DestroyObjectImmediate(oldVilla);

            CreateMaterials();
            nextSpotId = 1;

            var root = new GameObject(RootName);
            var structure = ModelKit.Group("Bau", root.transform).transform;
            var floors = ModelKit.Group("Boeden", structure).transform;
            var walls = ModelKit.Group("Waende", structure).transform;
            var ceilings = ModelKit.Group("Decken", structure).transform;
            var stairs = ModelKit.Group("Treppe", structure).transform;
            var furniture = ModelKit.Group("Moebel", root.transform).transform;
            var lights = ModelKit.Group("Lichter", root.transform).transform;
            var spots = ModelKit.Group("Spurenpunkte", root.transform).transform;
            var interaction = ModelKit.Group("Interaktion", root.transform).transform;
            var spawns = ModelKit.Group("Spielerstarts", root.transform).transform;
            var seats = ModelKit.Group("MeetingPlaetze", root.transform).transform;

            BuildGarden(root.transform);
            BuildFloors(floors);
            BuildCeilings(ceilings);
            BuildGroundFloorWalls(walls);
            BuildUpperFloorWalls(walls);
            BuildStairs(stairs);
            BuildFurniture(furniture);
            BuildCrimeScene(furniture);
            BuildLights(lights);
            BuildClueSpots(spots);
            BuildInteraction(interaction);
            var starts = BuildSpawns(spawns);
            BuildSeats(seats);

            Undo.RegisterCreatedObjectUndo(root, "Villa erstellen");

            LinkPowerManager(lights.gameObject);
            PlaceDummies(starts);
            PlaceLobbyCamera();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log($"[Villa] Fertig: 13 Räume auf 2 Stockwerken, {nextSpotId - 1} Spurenpunkte, Sicherungskasten, Analyse-Koffer, 6 Spieler-Starts, 6 Meeting-Plätze. Szene speichern!");
            return true;
        }

        // ------------------------------------------------------------------ Bau

        private static void BuildGarden(Transform parent)
        {
            Block("Garten", parent, -30f, 58f, -0.2f, -0.08f, -30f, 48f, matGarden, collider: true);
        }

        private static void BuildFloors(Transform parent)
        {
            foreach (var room in Rooms)
            {
                var mat = ModelKit.Solid($"VB_Boden_{ModelKit.Ascii(room.name)}", room.floorHex, 0.15f);
                float top = Top(room.level);
                float bottom = room.level == 0 ? -0.08f : Base(1);
                string name = $"Boden_{ModelKit.Ascii(room.name)}";

                if (room.level == 1 && room.name == "Galerie")
                {
                    // Treppenloch aussparen
                    Block(name + "_West", parent, room.x0, StairX0, bottom, top, room.z0, room.z1, mat, true);
                    Block(name + "_Sued", parent, StairX0, room.x1, bottom, top, room.z0, StairZ0, mat, true);
                    Block(name + "_Nord", parent, StairX0, room.x1, bottom, top, StairZ1, room.z1, mat, true);
                }
                else
                {
                    Block(name, parent, room.x0, room.x1, bottom, top, room.z0, room.z1, mat, true);
                }
            }
        }

        private static void BuildCeilings(Transform parent)
        {
            // Decke EG = Boden OG (mit Treppenloch)
            float y0 = WallHeight, y1 = Base(1);
            Block("Decke_EG_West", parent, -0.1f, StairX0, y0, y1, -0.1f, 18.1f, matCeiling, true);
            Block("Decke_EG_Treppe_Sued", parent, StairX0, StairX1, y0, y1, -0.1f, StairZ0, matCeiling, true);
            Block("Decke_EG_Treppe_Nord", parent, StairX0, StairX1, y0, y1, StairZ1, 18.1f, matCeiling, true);
            Block("Decke_EG_Ost", parent, StairX1, 28.1f, y0, y1, -0.1f, 18.1f, matCeiling, true);

            // Dach
            Block("Decke_OG", parent, -0.1f, 28.1f, Base(1) + WallHeight, Base(1) + WallHeight + 0.2f, -0.1f, 18.1f, matCeiling, true);
        }

        private static void BuildGroundFloorWalls(Transform parent)
        {
            const int l = 0;
            // Aussen (pro Raum ein Stück, damit Lichter sauber bleiben)
            WallX(parent, "EG_Aussen_Sued_West", l, 0f, -0.1f, 10f);
            WallX(parent, "EG_Aussen_Sued_Mitte", l, 0f, 10f, 18f, new Vector2(13f, 15f));   // Haustür
            WallX(parent, "EG_Aussen_Sued_Ost", l, 0f, 18f, 28.1f);
            WallX(parent, "EG_Aussen_Nord_West", l, 18f, -0.1f, 10f);
            WallX(parent, "EG_Aussen_Nord_Mitte", l, 18f, 10f, 18f);
            WallX(parent, "EG_Aussen_Nord_Ost", l, 18f, 18f, 28.1f);
            WallZ(parent, "EG_Aussen_West_Sued", l, 0f, 0f, 10f);
            WallZ(parent, "EG_Aussen_West_Nord", l, 0f, 10f, 18f);
            WallZ(parent, "EG_Aussen_Ost_Sued", l, 28f, 0f, 4f);
            WallZ(parent, "EG_Aussen_Ost_Mitte", l, 28f, 4f, 10f);
            WallZ(parent, "EG_Aussen_Ost_Nord", l, 28f, 10f, 18f);

            // Innen mit Türen (Start, Ende)
            WallZ(parent, "EG_Salon_Halle", l, 10f, 0f, 10f, new Vector2(4.2f, 5.8f));
            WallZ(parent, "EG_Bibliothek_Speisesaal", l, 10f, 10f, 18f, new Vector2(13.2f, 14.8f));
            WallZ(parent, "EG_Halle_Ost", l, 18f, 0f, 10f, new Vector2(0.3f, 1.7f), new Vector2(8.4f, 9.8f));
            WallZ(parent, "EG_Speisesaal_Kueche", l, 18f, 10f, 18f, new Vector2(13.2f, 14.8f));
            WallX(parent, "EG_Salon_Bibliothek", l, 10f, 0f, 10f, new Vector2(6f, 7.6f));
            WallX(parent, "EG_Halle_Speisesaal", l, 10f, 10f, 18f, new Vector2(11.5f, 13.5f));
            WallX(parent, "EG_Arbeitszimmer_Kueche", l, 10f, 18f, 28f, new Vector2(23.6f, 25.2f));
            WallX(parent, "EG_Hauswirtschaft_Arbeitszimmer", l, 4f, 18f, 28f, new Vector2(20f, 21.6f));

            // Haustür (geschlossen)
            Block("Haustuer", parent, 13.05f, 14.95f, FloorLift, 2.28f, -0.05f, 0.05f, matDoor, true);
            ModelKit.Cylinder("Haustuer_Knauf", parent, H(14.6f, 1.05f, 0.1f), 0.07f, 0.08f, matMetal, new Vector3(90f, 0f, 0f));
        }

        private static void BuildUpperFloorWalls(Transform parent)
        {
            const int l = 1;
            WallX(parent, "OG_Aussen_Sued_West", l, 0f, -0.1f, 10f);
            WallX(parent, "OG_Aussen_Sued_Mitte", l, 0f, 10f, 18f);
            WallX(parent, "OG_Aussen_Sued_Ost", l, 0f, 18f, 28.1f);
            WallX(parent, "OG_Aussen_Nord_West", l, 18f, -0.1f, 10f);
            WallX(parent, "OG_Aussen_Nord_Mitte", l, 18f, 10f, 18f);
            WallX(parent, "OG_Aussen_Nord_Ost", l, 18f, 18f, 28.1f);
            WallZ(parent, "OG_Aussen_West_Sued", l, 0f, 0f, 10f);
            WallZ(parent, "OG_Aussen_West_Nord", l, 0f, 10f, 18f);
            WallZ(parent, "OG_Aussen_Ost_Sued", l, 28f, 0f, 10f);
            WallZ(parent, "OG_Aussen_Ost_Nord", l, 28f, 10f, 18f);

            WallZ(parent, "OG_Schlafzimmer_Galerie", l, 10f, 0f, 10f, new Vector2(5f, 6.6f));
            WallZ(parent, "OG_Bad_Gaestezimmer", l, 10f, 10f, 18f, new Vector2(14f, 15.6f));
            WallZ(parent, "OG_Galerie_Atelier", l, 18f, 0f, 10f, new Vector2(8.4f, 9.8f));
            WallZ(parent, "OG_Gaestezimmer_Kinderzimmer", l, 18f, 10f, 18f, new Vector2(14f, 15.6f));
            WallX(parent, "OG_Schlafzimmer_Bad", l, 10f, 0f, 10f, new Vector2(2f, 3.6f));
            WallX(parent, "OG_Galerie_Gaestezimmer", l, 10f, 10f, 18f, new Vector2(11f, 12.6f));
            WallX(parent, "OG_Atelier_Kinderzimmer", l, 10f, 18f, 28f, new Vector2(24f, 25.6f));
        }

        /// <summary>
        /// Treppe: sichtbare Stufen (massiv, mit Collider an den Seiten) und darüber eine unsichtbare Rampe,
        /// damit man ohne Ruckeln hoch- und runterläuft. Oben ein Geländer um das Treppenloch.
        /// </summary>
        private static void BuildStairs(Transform parent)
        {
            float run = (StairZ1 - StairZ0) / StairSteps;    // 0.375 m
            float rise = Story / StairSteps;                   // 0.2 m
            for (int i = 0; i < StairSteps; i++)
            {
                float z0 = StairZ0 + i * run;
                Block($"Stufe_{i + 1:00}", parent, StairX0, StairX1, 0f, FloorLift + (i + 1) * rise, z0, z0 + run, matStairs, true);
            }

            // Rampe durch die Stufenkanten, beginnt eine Stufe vor der Treppe auf dem Boden
            float rz0 = StairZ0 - run, rz1 = StairZ1 - run;
            float ry0 = FloorLift, ry1 = FloorLift + Story;
            float length = Mathf.Sqrt((rz1 - rz0) * (rz1 - rz0) + (ry1 - ry0) * (ry1 - ry0));
            float angle = Mathf.Atan2(ry1 - ry0, rz1 - rz0) * Mathf.Rad2Deg;
            const float thickness = 0.1f;
            var tilt = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 normal = tilt * Vector3.up;

            var ramp = new GameObject("Treppe_Rampe (unsichtbar)");
            ramp.transform.SetParent(parent, false);
            ramp.transform.localPosition = H((StairX0 + StairX1) * 0.5f, (ry0 + ry1) * 0.5f, (rz0 + rz1) * 0.5f) - normal * (thickness * 0.5f);
            ramp.transform.localRotation = tilt;
            ramp.transform.localScale = new Vector3(StairX1 - StairX0, thickness, length);
            ramp.AddComponent<BoxCollider>();

            // Handlauf an der offenen Seite (nur Optik)
            var rail = new GameObject("Handlauf");
            rail.transform.SetParent(parent, false);
            rail.transform.localPosition = H(StairX0 + 0.04f, (ry0 + ry1) * 0.5f + 0.95f, (rz0 + rz1) * 0.5f);
            rail.transform.localRotation = tilt;
            ModelKit.Box("Handlauf_Stange", rail.transform, Vector3.zero, new Vector3(0.06f, 0.06f, length), matRail);

            // Geländer oben um das Treppenloch (die Nordseite bleibt offen – dort kommt man an)
            float top = Top(1);
            Railing(parent, "Gelaender_West", StairX0, StairZ0, StairX0, StairZ1, top);
            Railing(parent, "Gelaender_Sued", StairX0, StairZ0, StairX1, StairZ0, top);
        }

        private static void BuildFurniture(Transform parent)
        {
            // --- Erdgeschoss
            var salon = RoomGroup(parent, "Salon");
            Furn(salon, "Kamin", 0, 0.1f, 0.8f, 2f, 4f, 1.2f, matStone);
            Furn(salon, "Sofa", 0, 1.2f, 4.2f, 7.2f, 8.2f, 0.8f, matFurniture);
            Furn(salon, "Couchtisch", 0, 1.8f, 3.6f, 5f, 5.9f, 0.45f, matWood);
            Furn(salon, "Sessel", 0, 6f, 7f, 7.4f, 8.4f, 0.8f, matFurniture);

            var library = RoomGroup(parent, "Bibliothek");
            Furn(library, "Regal_1", 0, 2f, 8f, 12.2f, 12.7f, 2.2f, matWoodDark);
            Furn(library, "Regal_2", 0, 2f, 8f, 15f, 15.5f, 2.2f, matWoodDark);
            Furn(library, "Wandregal", 0, 0.5f, 9.5f, 17.4f, 17.9f, 2.2f, matWoodDark);
            Furn(library, "Lesepult", 0, 9.2f, 9.9f, 15.8f, 16.6f, 1.1f, matWood);

            var dining = RoomGroup(parent, "Speisesaal");
            Furn(dining, "MeetingTisch", 0, 12.5f, 15.5f, 13.3f, 14.7f, 0.77f, matWoodDark);
            foreach (var c in ChairPositions())
            {
                Furn(dining, "Stuhl", 0, c.x - 0.25f, c.x + 0.25f, c.y - 0.25f, c.y + 0.25f, 0.47f, matFurniture, collider: false);
            }
            Furn(dining, "Anrichte", 0, 12.5f, 15.5f, 17.4f, 17.9f, 0.9f, matWood);

            var kitchen = RoomGroup(parent, "Küche");
            Furn(kitchen, "Theke", 0, 19f, 26f, 17.3f, 17.9f, 0.92f, matFurniture);
            Furn(kitchen, "Kuehlschrank", 0, 26.6f, 27.8f, 17.1f, 17.9f, 2f, matWhite);
            Furn(kitchen, "Kuecheninsel", 0, 20.5f, 23.5f, 14.6f, 15.6f, 0.92f, matWood);

            var study = RoomGroup(parent, "Arbeitszimmer");
            Furn(study, "Schreibtisch", 0, 24f, 26.4f, 6.4f, 7.4f, 0.76f, matWoodDark);
            Furn(study, "Aktenschrank", 0, 27.3f, 27.9f, 4.4f, 6.2f, 1.4f, matMetal);

            var utility = RoomGroup(parent, "Hauswirtschaftsraum");
            Furn(utility, "Waschmaschine", 0, 24.9f, 25.7f, 0.2f, 1f, 0.9f, matWhite);
            Furn(utility, "Trockner", 0, 25.9f, 26.7f, 0.2f, 1f, 0.9f, matWhite);
            Furn(utility, "Regal", 0, 19f, 21f, 0.15f, 0.55f, 1.8f, matWood);

            var hall = RoomGroup(parent, "Eingangshalle");
            Furn(hall, "Kommode", 0, 10.15f, 10.85f, 6.8f, 8.6f, 0.9f, matWood);

            // --- Obergeschoss
            var gallery = RoomGroup(parent, "Galerie");
            Furn(gallery, "Kommode", 1, 10.15f, 10.75f, 0.8f, 2.4f, 0.9f, matWood);
            Furn(gallery, "Bank", 1, 13f, 15f, 9.4f, 9.85f, 0.45f, matFurniture);

            var bedroom = RoomGroup(parent, "Schlafzimmer");
            Furn(bedroom, "Bett", 1, 0.2f, 2.4f, 3.6f, 5.4f, 0.55f, matFurniture);
            Furn(bedroom, "Nachttisch", 1, 0.2f, 0.7f, 5.6f, 6.1f, 0.6f, matWood);
            Furn(bedroom, "Kleiderschrank", 1, 6f, 9f, 0.15f, 0.8f, 2.2f, matWoodDark);

            var bath = RoomGroup(parent, "Bad");
            Furn(bath, "Badewanne", 1, 0.2f, 1.9f, 15f, 17.8f, 0.6f, matWhite);
            Furn(bath, "Waschbecken", 1, 5f, 6.2f, 17.4f, 17.9f, 0.85f, matWhite);

            var guest = RoomGroup(parent, "Gästezimmer");
            Furn(guest, "Bett", 1, 15.6f, 17.8f, 15.9f, 17.8f, 0.55f, matFurniture);
            Furn(guest, "Kommode", 1, 10.15f, 10.75f, 11f, 12.6f, 0.9f, matWood);

            var kids = RoomGroup(parent, "Kinderzimmer");
            Furn(kids, "Bett", 1, 25.9f, 27.8f, 15.8f, 17.8f, 0.5f, matFurniture);
            Furn(kids, "Spielzeugkiste", 1, 18.8f, 19.8f, 17.2f, 17.85f, 0.5f, matWood);
            Furn(kids, "Kindertisch", 1, 22f, 23.4f, 13f, 14f, 0.6f, matWood);

            var studio = RoomGroup(parent, "Atelier");
            Furn(studio, "Arbeitstisch", 1, 23f, 26f, 4f, 5.4f, 0.85f, matWood);
            Furn(studio, "Regal", 1, 27.3f, 27.9f, 1f, 4f, 1.6f, matWoodDark);
        }

        /// <summary>Kreide-Umriss in der Küche: Hier wurde die Leiche gefunden.</summary>
        private static void BuildCrimeScene(Transform parent)
        {
            var group = ModelKit.Group("Tatort", parent).transform;
            ModelKit.Decal("Kreideumriss", group, matChalk, 0.95f, 1.9f, H(23.2f, FloorLift, 12.2f), 80f);
        }

        private static void BuildLights(Transform parent)
        {
            var warm = new Color(1f, 0.86f, 0.68f);
            foreach (var room in Rooms)
            {
                float size = Mathf.Max(room.x1 - room.x0, room.z1 - room.z0);
                PointLight($"Licht_{ModelKit.Ascii(room.name)}", parent, room.CenterX, Base(room.level) + 2.7f, room.CenterZ,
                    size * 0.6f + 3f, 1.8f, warm);
            }
            // Treppenhaus etwas heller, damit man die Stufen sieht
            PointLight("Licht_Treppe", parent, (StairX0 + StairX1) * 0.5f, Base(1) + 2.6f, 6f, 7f, 1.2f, warm);
        }

        // ------------------------------------------------------------------ Spurenpunkte

        private static void BuildClueSpots(Transform parent)
        {
            var east = Vector3.right;      // Wand im Westen → Spur zeigt nach Osten
            var west = Vector3.left;

            // Erdgeschoss
            Floor(parent, "Salon", 0, 1.4f, 3f, "Boden vor dem Kamin");
            Floor(parent, "Salon", 0, 7f, 2.2f, "Boden Südecke");
            Surface(parent, "Salon", 0, 0.45f, 1.2f, 3f, 0.7f, "Kaminsims");
            Surface(parent, "Salon", 0, 2.7f, 0.45f, 5.45f, 0.9f, "Couchtisch");

            Floor(parent, "Bibliothek", 0, 1f, 16.4f, "Boden zwischen den Regalen");
            Floor(parent, "Bibliothek", 0, 9f, 11.2f, "Boden beim Eingang");
            Surface(parent, "Bibliothek", 0, 9.55f, 1.1f, 16.2f, 0.7f, "Lesepult");
            WallSpot(parent, "Bibliothek", 0, 0.1f, 1.3f, 11.4f, east, "Westwand");

            Surface(parent, "Speisesaal", 0, 14f, 0.9f, 17.65f, 0.5f, "Anrichte");
            Floor(parent, "Speisesaal", 0, 16.9f, 11f, "Boden Südostecke");

            Floor(parent, "Küche", 0, 25.6f, 12.6f, "Boden neben dem Kreideumriss");
            Floor(parent, "Küche", 0, 19f, 12.2f, "Boden bei der Tür");
            Surface(parent, "Küche", 0, 22f, 0.92f, 15.1f, 1f, "Kücheninsel");
            Surface(parent, "Küche", 0, 20.5f, 0.92f, 17.6f, 0.6f, "Theke");
            WallSpot(parent, "Küche", 0, 27.9f, 1.2f, 12.8f, west, "Ostwand");

            Surface(parent, "Arbeitszimmer", 0, 25.2f, 0.76f, 6.9f, 1f, "Schreibtisch");
            Floor(parent, "Arbeitszimmer", 0, 21.2f, 7f, "Boden Mitte");
            Surface(parent, "Arbeitszimmer", 0, 27.6f, 1.4f, 5.3f, 0.6f, "Aktenschrank");

            Floor(parent, "Hauswirtschaftsraum", 0, 22.8f, 2.4f, "Boden");
            Surface(parent, "Hauswirtschaftsraum", 0, 25.3f, 0.9f, 0.6f, 0.8f, "Waschmaschine");

            Floor(parent, "Eingangshalle", 0, 12.2f, 1.4f, "Boden beim Eingang");
            Floor(parent, "Eingangshalle", 0, 15.2f, 1f, "Boden vor der Treppe");
            Surface(parent, "Eingangshalle", 0, 10.5f, 0.9f, 7.2f, 0.7f, "Kommode");

            // Obergeschoss
            Floor(parent, "Galerie", 1, 12.6f, 7.8f, "Boden");
            Floor(parent, "Galerie", 1, 14.6f, 0.9f, "Boden Südseite");
            Surface(parent, "Galerie", 1, 10.45f, 0.9f, 1.6f, 0.6f, "Kommode");

            Floor(parent, "Schlafzimmer", 1, 3.2f, 4.5f, "Boden neben dem Bett");
            Surface(parent, "Schlafzimmer", 1, 0.45f, 0.6f, 5.85f, 0.5f, "Nachttisch");
            Floor(parent, "Schlafzimmer", 1, 7.5f, 1.6f, "Boden vor dem Schrank");

            Surface(parent, "Bad", 1, 1.05f, 0.6f, 16.4f, 1.7f, "Badewanne");
            Surface(parent, "Bad", 1, 5.6f, 0.85f, 17.65f, 0.5f, "Waschbecken");
            Floor(parent, "Bad", 1, 4.5f, 13f, "Boden");
            WallSpot(parent, "Bad", 1, 9.9f, 1.3f, 11.6f, west, "Fliesenwand");

            Floor(parent, "Gästezimmer", 1, 13.8f, 13.6f, "Boden Mitte");
            Surface(parent, "Gästezimmer", 1, 10.45f, 0.9f, 11.8f, 0.6f, "Kommode");

            Floor(parent, "Kinderzimmer", 1, 24.2f, 11.6f, "Boden");
            Surface(parent, "Kinderzimmer", 1, 19.3f, 0.5f, 17.52f, 0.65f, "Spielzeugkiste");
            Surface(parent, "Kinderzimmer", 1, 22.7f, 0.6f, 13.5f, 1f, "Kindertisch");

            Floor(parent, "Atelier", 1, 20.6f, 3f, "Boden");
            Surface(parent, "Atelier", 1, 24.5f, 0.85f, 4.7f, 1.4f, "Arbeitstisch");
            WallSpot(parent, "Atelier", 1, 27.9f, 1.3f, 7.6f, west, "Ostwand");
        }

        /// <summary>Boden: Blut oder Schuhabdruck.</summary>
        private static void Floor(Transform parent, string room, int level, float x, float z, string description) =>
            Spot(parent, room, description, H(x, Top(level), z), Quaternion.identity, blood: true, shoe: true, fiber: false);

        /// <summary>
        /// Möbel-Oberfläche (height über dem Boden): Blut oder Faser.
        /// topSize = schmalste Seite der Oberfläche → kleinere Spur, damit nichts über die Kante hängt.
        /// </summary>
        private static void Surface(Transform parent, string room, int level, float x, float height, float z, float topSize, string description) =>
            Spot(parent, room, description, H(x, Top(level) + height, z), Quaternion.identity, blood: true, shoe: false, fiber: true,
                scale: Mathf.Clamp(topSize / 0.8f, 0.55f, 1f));

        /// <summary>Wand (x/z = Wandoberfläche, normal = Richtung in den Raum): Blutspritzer oder Faser.</summary>
        private static void WallSpot(Transform parent, string room, int level, float x, float height, float z, Vector3 normal, string description) =>
            Spot(parent, room, description, H(x, Top(level) + height, z), Quaternion.FromToRotation(Vector3.up, normal), blood: true, shoe: false, fiber: true);

        private static void Spot(Transform parent, string room, string description, Vector3 position, Quaternion rotation,
            bool blood, bool shoe, bool fiber, float scale = 1f)
        {
            int id = nextSpotId++;
            var go = new GameObject($"S{id:00}_{ModelKit.Ascii(room)}_{ModelKit.Ascii(description)}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.AddComponent<ClueSpot>().EditorSetup(id, room, blood, shoe, fiber, scale);
        }

        // ------------------------------------------------------------------ Koffer + Sicherungskasten

        private static void BuildInteraction(Transform parent)
        {
            // Analyse-Koffer: steht in der Eingangshalle auf der Kommode, Vorderseite zum Raum
            var kitStart = new GameObject("AnalyseKoffer_Start");
            kitStart.transform.SetParent(parent, false);
            kitStart.transform.localPosition = H(10.5f, Top(0) + 0.9f, 8.1f);
            kitStart.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            kitStart.AddComponent<MapMarker>().EditorSetup(MapMarkerType.AnalyseKoffer, 1);
            BuildAnalysisKit(kitStart.transform);

            // Sicherungskasten: Hauswirtschaftsraum, Ostwand, Vorderseite nach Westen
            var box = BuildCircuitBreaker(parent);
            box.transform.localPosition = H(27.9f - 0.08f, Top(0) + 1.6f, 2.3f);
            box.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        /// <summary>Koffer mit Griff, Schlössern und "FORENSIK"-Schild. Pivot unten in der Mitte, Vorderseite = lokal -Z.</summary>
        private static void BuildAnalysisKit(Transform parent)
        {
            var kit = new GameObject("AnalyseKoffer");
            kit.transform.SetParent(parent, false);

            var bodyMat = ModelKit.Solid("M_Koffer", "d9822b", 0.35f);
            var trimMat = ModelKit.Solid("M_Koffer_Kante", "2a2622", 0.3f);
            var metal = ModelKit.Solid("M_Metall_hell", "c9ccd1", 0.7f, 0.8f);
            var plate = ModelKit.Solid("M_Schild_Weiss", "f2efe6", 0.2f);

            const float w = 0.46f, h = 0.34f, d = 0.13f;
            var t = kit.transform;
            ModelKit.Box("Koerper", t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), bodyMat);
            ModelKit.Box("Naht", t, new Vector3(0f, h * 0.62f, 0f), new Vector3(w + 0.006f, 0.014f, d + 0.006f), trimMat);
            foreach (float sx in new[] { -1f, 1f })
            {
                ModelKit.Box("Kante_Seite", t, new Vector3(sx * (w * 0.5f - 0.008f), h * 0.5f, 0f), new Vector3(0.022f, h + 0.004f, d + 0.008f), trimMat);
                ModelKit.Box("Griff_Halter", t, new Vector3(sx * 0.07f, h + 0.02f, 0f), new Vector3(0.022f, 0.04f, 0.03f), trimMat);
                ModelKit.Box("Schloss", t, new Vector3(sx * 0.15f, h * 0.78f, -d * 0.5f - 0.006f), new Vector3(0.045f, 0.03f, 0.012f), metal);
            }
            ModelKit.Box("Griff", t, new Vector3(0f, h + 0.045f, 0f), new Vector3(0.19f, 0.022f, 0.032f), trimMat);
            ModelKit.Box("Schild", t, new Vector3(0f, h * 0.36f, -d * 0.5f - 0.003f), new Vector3(0.26f, 0.085f, 0.004f), plate);
            ModelKit.Label("Schild_Text", t, "FORENSIK", new Vector3(0f, h * 0.36f, -d * 0.5f - 0.0055f), Vector3.zero,
                0.42f, ModelKit.Hex("2a2622"), new Vector2(0.25f, 0.08f));

            var col = kit.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, (h + 0.06f) * 0.5f, 0f);
            col.size = new Vector3(w + 0.04f, h + 0.06f, d + 0.06f);
            kit.AddComponent<AnalysisKit>();
        }

        /// <summary>Grauer Kasten mit Warnschild, zwei Reihen Sicherungen und rotem Hauptschalter. Vorderseite = lokal -Z.</summary>
        private static GameObject BuildCircuitBreaker(Transform parent)
        {
            var box = new GameObject("Sicherungskasten");
            box.transform.SetParent(parent, false);
            var t = box.transform;

            var casing = ModelKit.Solid("M_Kasten_Grau", "8d949a", 0.45f, 0.5f);
            var panel = ModelKit.Solid("M_Kasten_Panel", "5b6268", 0.35f, 0.3f);
            var black = ModelKit.Solid("M_Schwarz", "1d1d1f", 0.3f);
            var white = ModelKit.Solid("M_Schild_Weiss", "f2efe6", 0.2f);
            var red = ModelKit.Solid("M_Rot", "c0281f", 0.4f);
            var sign = ModelKit.Cutout("M_Warnschild", ModelKit.Texture("warning_sign.png"), "ffffff", 0.3f);

            const float w = 0.6f, h = 0.8f, d = 0.16f;
            float front = -d * 0.5f;
            ModelKit.Box("Gehaeuse", t, Vector3.zero, new Vector3(w, h, d), casing);
            ModelKit.Box("Panel", t, new Vector3(0f, -0.06f, front - 0.002f), new Vector3(w - 0.08f, h - 0.24f, 0.004f), panel);

            // Zwei Reihen Sicherungen
            for (int row = 0; row < 2; row++)
            {
                float y = -0.02f - row * 0.2f;
                ModelKit.Box($"Schiene_{row + 1}", t, new Vector3(-0.04f, y, front - 0.006f), new Vector3(0.4f, 0.03f, 0.008f), black);
                for (int i = 0; i < 7; i++)
                {
                    float x = -0.21f + i * 0.057f;
                    ModelKit.Box($"Sicherung_{row + 1}_{i + 1}", t, new Vector3(x, y, front - 0.02f), new Vector3(0.04f, 0.08f, 0.03f), black);
                    ModelKit.Box($"Kippschalter_{row + 1}_{i + 1}", t, new Vector3(x, y + 0.012f, front - 0.04f), new Vector3(0.016f, 0.026f, 0.014f), white);
                }
            }

            // Hauptschalter rechts
            ModelKit.Box("Hauptschalter_Sockel", t, new Vector3(0.22f, -0.12f, front - 0.015f), new Vector3(0.07f, 0.2f, 0.025f), black);
            ModelKit.Box("Hauptschalter_Hebel", t, new Vector3(0.22f, -0.07f, front - 0.045f), new Vector3(0.035f, 0.1f, 0.04f), red);

            // Warnschild + Beschriftung oben
            var signHolder = new GameObject("Warnschild");
            signHolder.transform.SetParent(t, false);
            signHolder.transform.localPosition = new Vector3(-0.12f, 0.29f, front - 0.003f);
            signHolder.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // Aufkleber-Oberseite zeigt nach vorne (-Z)
            ModelKit.Decal("Schild", signHolder.transform, sign, 0.15f, 0.15f);
            ModelKit.Label("Beschriftung", t, "STROM", new Vector3(0.1f, 0.29f, front - 0.004f), Vector3.zero,
                0.7f, ModelKit.Hex("f2efe6"), new Vector2(0.3f, 0.1f));

            var col = box.AddComponent<BoxCollider>();
            col.size = new Vector3(w + 0.04f, h + 0.04f, d + 0.1f);
            col.center = new Vector3(0f, 0f, -0.04f);
            box.AddComponent<CircuitBreaker>();
            box.AddComponent<MapMarker>().EditorSetup(MapMarkerType.Sicherungskasten, 1);
            return box;
        }

        // ------------------------------------------------------------------ Starts + Meeting

        private static List<Transform> BuildSpawns(Transform parent)
        {
            // Alle starten zusammen in der Eingangshalle und schauen sich an
            var points = new[]
            {
                new Vector2(11.6f, 3.2f), new Vector2(13f, 2.4f), new Vector2(14.4f, 3.2f),
                new Vector2(11.6f, 6.8f), new Vector2(13f, 7.6f), new Vector2(14.4f, 6.8f),
            };
            var center = new Vector2(13f, 5f);
            var list = new List<Transform>();
            for (int i = 0; i < points.Length; i++)
            {
                var go = new GameObject($"Start_{i + 1}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = H(points[i].x, Top(0), points[i].y);
                go.transform.localRotation = Look(points[i], center);
                go.AddComponent<MapMarker>().EditorSetup(MapMarkerType.Spielerstart, i + 1);
                list.Add(go.transform);
            }
            return list;
        }

        private static void BuildSeats(Transform parent)
        {
            var table = new Vector2(14f, 14f);
            int i = 1;
            foreach (var c in ChairPositions())
            {
                var go = new GameObject($"Platz_{i}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = H(c.x, Top(0), c.y);
                go.transform.localRotation = Look(c, table);
                go.AddComponent<MapMarker>().EditorSetup(MapMarkerType.MeetingPlatz, i);
                i++;
            }
        }

        private static Vector2[] ChairPositions() => new[]
        {
            new Vector2(13f, 12.6f), new Vector2(15f, 12.6f), new Vector2(13f, 15.4f),
            new Vector2(15f, 15.4f), new Vector2(11.8f, 14f), new Vector2(16.2f, 14f),
        };

        // ------------------------------------------------------------------ Szene verknüpfen

        private static void LinkPowerManager(GameObject lights)
        {
            foreach (var power in Object.FindObjectsByType<PowerManager>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(power);
                var prop = so.FindProperty("lightsGroup");
                if (prop == null) continue;
                prop.objectReferenceValue = lights;
                so.ApplyModifiedProperties();
            }
        }

        /// <summary>Die Test-Dummies (TestDummy_Spieler_2 …) auf die neuen Starts stellen.</summary>
        private static void PlaceDummies(List<Transform> starts)
        {
            if (starts.Count == 0) return;
            const string prefix = "TestDummy_Spieler_";
            foreach (var identity in Object.FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Include))
            {
                if (identity == null || EditorUtility.IsPersistent(identity)) continue;
                if (identity.GetComponent<PlayerController>() != null) continue;
                string n = identity.gameObject.name;
                if (!n.StartsWith(prefix) || !int.TryParse(n.Substring(prefix.Length), out int index)) continue;

                Transform start = starts[Mathf.Clamp(index - 1, 0, starts.Count - 1)];
                Undo.RecordObject(identity.transform, "Test-Dummy versetzen");
                identity.transform.SetPositionAndRotation(start.position + Vector3.up * 1f, start.rotation);
            }
        }

        /// <summary>Die Kamera im Hauptmenü zeigt die Villa von schräg vorne.</summary>
        private static void PlaceLobbyCamera()
        {
            var cam = GameObject.Find("LobbyCamera");
            if (cam == null) return;
            Undo.RecordObject(cam.transform, "Lobby-Kamera versetzen");
            cam.transform.position = H(-7f, 13f, -12f);
            cam.transform.LookAt(H(14f, 2f, 9f));
        }

        // ------------------------------------------------------------------ Bausteine

        private static float Base(int level) => level * Story;
        private static float Top(int level) => Base(level) + FloorLift;

        private static Vector3 H(float x, float y, float z) => new Vector3(x - Origin.x, y, z - Origin.z);

        private static Quaternion Look(Vector2 from, Vector2 to)
        {
            var dir = new Vector3(to.x - from.x, 0f, to.y - from.y);
            return dir.sqrMagnitude < 0.001f ? Quaternion.identity : Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private static Transform RoomGroup(Transform parent, string name) => ModelKit.Group(ModelKit.Ascii(name), parent).transform;

        private static GameObject Block(string name, Transform parent, float x0, float x1, float y0, float y1, float z0, float z1,
            Material material, bool collider)
        {
            var center = H((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            var size = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
            return ModelKit.Box(name, parent, center, size, material, collider);
        }

        /// <summary>Möbelstück als Quader, steht auf dem Boden des Stockwerks.</summary>
        private static void Furn(Transform parent, string name, int level, float x0, float x1, float z0, float z1, float height,
            Material material, bool collider = true)
        {
            Block(name, parent, x0, x1, Top(level), Top(level) + height, z0, z1, material, collider);
        }

        /// <summary>Wand entlang der x-Achse bei z, mit Türöffnungen (Start, Ende in x).</summary>
        private static void WallX(Transform parent, string name, int level, float z, float x0, float x1, params Vector2[] doors)
        {
            float b = Base(level);
            float z0 = z - Wall * 0.5f, z1 = z + Wall * 0.5f;
            float cursor = x0;
            int part = 1;
            foreach (var door in doors)
            {
                if (door.x > cursor) Block($"{name}_{part++}", parent, cursor, door.x, b, b + WallHeight, z0, z1, matWall, true);
                Block($"{name}_Sturz_{part++}", parent, door.x, door.y, b + DoorHeight, b + WallHeight, z0, z1, matWall, true);
                cursor = door.y;
            }
            if (x1 > cursor) Block($"{name}_{part}", parent, cursor, x1, b, b + WallHeight, z0, z1, matWall, true);
        }

        /// <summary>Wand entlang der z-Achse bei x, mit Türöffnungen (Start, Ende in z).</summary>
        private static void WallZ(Transform parent, string name, int level, float x, float z0, float z1, params Vector2[] doors)
        {
            float b = Base(level);
            float x0 = x - Wall * 0.5f, x1 = x + Wall * 0.5f;
            float cursor = z0;
            int part = 1;
            foreach (var door in doors)
            {
                if (door.x > cursor) Block($"{name}_{part++}", parent, x0, x1, b, b + WallHeight, cursor, door.x, matWall, true);
                Block($"{name}_Sturz_{part++}", parent, x0, x1, b + DoorHeight, b + WallHeight, door.x, door.y, matWall, true);
                cursor = door.y;
            }
            if (z1 > cursor) Block($"{name}_{part}", parent, x0, x1, b, b + WallHeight, cursor, z1, matWall, true);
        }

        /// <summary>Gerades Geländer (1 m hoch) von (x0, z0) nach (x1, z1) mit unsichtbarer Kollision.</summary>
        private static void Railing(Transform parent, string name, float x0, float z0, float x1, float z1, float floorY)
        {
            var go = ModelKit.Group(name, parent);
            var t = go.transform;
            var a = new Vector3(x0, 0f, z0);
            var b = new Vector3(x1, 0f, z1);
            float length = Vector3.Distance(a, b);
            t.localPosition = H((x0 + x1) * 0.5f, floorY, (z0 + z1) * 0.5f);
            t.localRotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);

            ModelKit.Box("Handlauf", t, new Vector3(0f, 1f, 0f), new Vector3(0.07f, 0.06f, length + 0.07f), matRail);
            ModelKit.Box("Fussleiste", t, new Vector3(0f, 0.08f, 0f), new Vector3(0.06f, 0.06f, length), matRail);
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 0.3f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float z = -length * 0.5f + length * i / (posts - 1);
                ModelKit.Box("Stab", t, new Vector3(0f, 0.55f, z), new Vector3(0.035f, 0.9f, 0.035f), matRail);
            }

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(0.12f, 1.2f, length);
        }

        private static void PointLight(string name, Transform parent, float x, float y, float z, float range, float intensity, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = H(x, y, z);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
        }

        private static void CreateMaterials()
        {
            matWall = ModelKit.Solid("VB_Wand", "e9e4da", 0.1f);
            matCeiling = ModelKit.Solid("VB_Decke", "d9d5cc", 0.1f);
            matStairs = ModelKit.Solid("VB_Treppe", "7a5636", 0.25f);
            matWood = ModelKit.Solid("VB_Holz", "a8875f", 0.25f);
            matWoodDark = ModelKit.Solid("VB_Holz_dunkel", "6e4f33", 0.25f);
            matFurniture = ModelKit.Solid("VB_Polster", "7d5a4a", 0.1f);
            matWhite = ModelKit.Solid("VB_Weiss", "eceae4", 0.5f);
            matStone = ModelKit.Solid("VB_Stein", "8a857c", 0.1f);
            matDoor = ModelKit.Solid("VB_Tuer", "5a3a24", 0.3f);
            matRail = ModelKit.Solid("VB_Gelaender", "4a3322", 0.35f);
            matGarden = ModelKit.Solid("VB_Garten", "5d7a45", 0.05f);
            matMetal = ModelKit.Solid("VB_Metall", "8d949a", 0.55f, 0.6f);
            matChalk = ModelKit.Cutout("M_Kreide", ModelKit.Texture("chalk_outline.png"), "ffffff", 0.05f);
            AssetDatabase.SaveAssets();
        }
    }
}
