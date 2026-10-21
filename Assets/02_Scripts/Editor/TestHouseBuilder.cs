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
    /// Baut ein sehr einfaches Test-Haus aus Cubes (Greybox) nach dem Grundriss:
    /// Tools > Not My Blood > Test-Haus erstellen
    ///
    /// Koordinaten unten sind "Haus-Meter" wie im Grundriss: x = 0..20 (West → Ost), z = 0..10 (Süd → Nord).
    /// Der Meeting-Tisch (x 10, z 6.5) landet genau im Ursprung der Szene.
    /// </summary>
    public static class TestHouseBuilder
    {
        private const string MaterialDir = "Assets/04_Materials/Greybox";
        private const float WallHeight = 3f;
        private const float DoorHeight = 2.3f;
        private const float WallThickness = 0.2f;
        private const float FloorTop = 0.02f;   // knapp über 0, damit nichts mit einer Boden-Plane flimmert

        private static readonly Vector3 Origin = new Vector3(10f, 0f, 6.5f);

        private static Material matWall, matCeiling, matFurniture, matTable, matDoor,
            matFloorKitchen, matFloorLiving, matFloorBedroom, matFloorHall, matFloorStorage,
            matPowerBox, matCase, matCrimeScene, matBlood, matShoe, matFiberBlack;

        [MenuItem("Tools/Not My Blood/Test-Haus erstellen")]
        public static void Build()
        {
            var existing = GameObject.Find("TestHouse");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Test-Haus",
                        "In der Szene gibt es schon ein \"TestHouse\". Soll es ersetzt werden?", "Ersetzen", "Abbrechen"))
                {
                    return;
                }
                Undo.DestroyObjectImmediate(existing);
            }

            CreateMaterials();

            var root = new GameObject("TestHouse");
            var floors = Group("Boden", root.transform);
            var walls = Group("Waende", root.transform);
            var ceiling = Group("Decke", root.transform);
            var furniture = Group("Moebel", root.transform);
            var lights = Group("Lichter", root.transform);
            var spots = Group("Spurenpunkte", root.transform);
            var interaction = Group("Interaktion", root.transform);
            var spawns = Group("Spielerstarts", root.transform);
            var seats = Group("MeetingPlaetze", root.transform);
            var testClues = Group("TestSpuren", root.transform);

            BuildFloors(floors);
            BuildWalls(walls);
            Box("Decke", ceiling, -0.1f, 20.1f, WallHeight, WallHeight + 0.2f, -0.1f, 10.1f, matCeiling);
            BuildFurniture(furniture);
            BuildLights(lights);
            var spotList = BuildClueSpots(spots);
            BuildInteraction(interaction);
            var firstSpawn = BuildSpawns(spawns);
            BuildSeats(seats);
            BuildTestClues(testClues, spotList);

            Undo.RegisterCreatedObjectUndo(root, "Test-Haus erstellen");
            MovePlayerTo(firstSpawn);

            if (FindInScenes<ClueManager>().Count == 0)
            {
                Debug.LogWarning("[Test-Haus] Kein ClueManager in der Szene. Erstelle zuerst die Notizbuch-UI (Tools > Not My Blood > Notizbuch-UI erstellen), sonst landen die Test-Spuren nirgends.");
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("[Test-Haus] Fertig: 4 Räume + Kammer, 12 Spurenpunkte, Sicherungskasten, Analyse-Koffer, 6 Spieler-Starts, 5 Test-Spuren. Szene speichern nicht vergessen.");
        }

        // ------------------------------------------------------------------ Aufbau

        private static void BuildFloors(Transform parent)
        {
            Floor("Boden_Kueche", parent, 0, 6, 3, 10, matFloorKitchen);
            Floor("Boden_Wohnzimmer", parent, 6, 14, 3, 10, matFloorLiving);
            Floor("Boden_Schlafzimmer", parent, 14, 20, 3, 10, matFloorBedroom);
            Floor("Boden_Flur", parent, 0, 18, 0, 3, matFloorHall);
            Floor("Boden_Kammer", parent, 18, 20, 0, 3, matFloorStorage);
        }

        private static void BuildWalls(Transform parent)
        {
            // Aussenwände
            WallAlongX("Wand_Sued", parent, 0f, -0.1f, 20.1f);
            WallAlongX("Wand_Nord", parent, 10f, -0.1f, 20.1f);
            WallAlongZ("Wand_West", parent, 0f, 0f, 10f);
            WallAlongZ("Wand_Ost", parent, 20f, 0f, 10f);

            // Innenwände mit Türöffnungen (Start, Ende)
            WallAlongX("Wand_Flur", parent, 3f, 0f, 20f,
                new Vector2(2.0f, 3.4f),     // Flur ↔ Küche
                new Vector2(8.8f, 11.2f),    // Flur ↔ Wohnzimmer (breit)
                new Vector2(15.6f, 17.0f));  // Flur ↔ Schlafzimmer
            WallAlongZ("Wand_Kueche_Wohnzimmer", parent, 6f, 3f, 10f, new Vector2(7.5f, 8.9f));
            WallAlongZ("Wand_Wohnzimmer_Schlafzimmer", parent, 14f, 3f, 10f, new Vector2(7.5f, 8.9f));
            WallAlongZ("Wand_Kammer", parent, 18f, 0f, 3f, new Vector2(0.8f, 2.0f));

            // Haustür (geschlossen, nur Deko)
            Box("Haustuer", parent, 9.4f, 10.6f, FloorTop, 2.2f, 0.1f, 0.16f, matDoor);
        }

        private static void BuildFurniture(Transform parent)
        {
            var kitchen = Group("Kueche", parent);
            Box("Theke_Nord", kitchen, 0.1f, 4.0f, FloorTop, 0.92f, 9.3f, 9.9f, matFurniture);
            Box("Theke_West", kitchen, 0.1f, 0.7f, FloorTop, 0.92f, 6.0f, 9.3f, matFurniture);
            Box("Kuehlschrank", kitchen, 4.4f, 5.1f, FloorTop, 2.0f, 9.2f, 9.9f, matCeiling);
            Box("Kuechentisch", kitchen, 2.4f, 3.8f, FloorTop, 0.77f, 5.0f, 6.0f, matTable);
            Box("Tatort_Markierung", kitchen, 3.35f, 5.25f, FloorTop, FloorTop + 0.005f, 4.03f, 4.67f, matCrimeScene, collider: false);

            var living = Group("Wohnzimmer", parent);
            Box("MeetingTisch", living, 8.5f, 11.5f, FloorTop, 0.77f, 5.8f, 7.2f, matTable);
            foreach (var c in ChairPositions())
            {
                Box("Stuhl", living, c.x - 0.25f, c.x + 0.25f, FloorTop, 0.47f, c.y - 0.25f, c.y + 0.25f, matFurniture);
            }
            Box("Sideboard", living, 9.0f, 11.0f, FloorTop, 0.87f, 9.4f, 9.9f, matFurniture);

            var hall = Group("Flur", parent);
            Box("Kommode", hall, 5.0f, 7.0f, FloorTop, 0.92f, 0.1f, 0.5f, matFurniture);

            var bedroom = Group("Schlafzimmer", parent);
            Box("Bett", bedroom, 18.2f, 19.9f, FloorTop, 0.52f, 6.0f, 8.0f, matFurniture);
            Box("Nachttisch", bedroom, 17.6f, 18.1f, FloorTop, 0.62f, 8.1f, 8.6f, matTable);
            Box("Schrank", bedroom, 14.1f, 16.4f, FloorTop, 2.2f, 5.6f, 6.2f, matTable);
        }

        private static void BuildLights(Transform parent)
        {
            var warm = new Color(1f, 0.86f, 0.68f);
            PointLight("Licht_Kueche", parent, 3f, 6.5f, 8f, 1.6f, warm);
            PointLight("Licht_Wohnzimmer", parent, 10f, 6.5f, 9f, 1.8f, warm);
            PointLight("Licht_Schlafzimmer", parent, 17f, 6.5f, 8f, 1.4f, warm);
            PointLight("Licht_Flur_West", parent, 4.5f, 1.5f, 7f, 1.3f, warm);
            PointLight("Licht_Flur_Ost", parent, 13.5f, 1.5f, 7f, 1.3f, warm);
            PointLight("Licht_Kammer", parent, 19f, 1.5f, 3.5f, 0.8f, warm);
        }

        private static List<ClueSpot> BuildClueSpots(Transform parent)
        {
            // Nr, x, y, z, Raum, Blut, Schuh, Faser, Beschreibung
            var list = new List<ClueSpot>
            {
                Spot(parent, 1, 3.00f, FloorTop, 4.40f, "Küche", true, true, false, "Boden beim Tatort"),
                Spot(parent, 2, 3.75f, 0.77f, 5.50f, "Küche", true, false, true, "Kante Küchentisch"),
                Spot(parent, 3, 2.00f, 0.92f, 9.60f, "Küche", true, false, true, "Theke"),
                Spot(parent, 4, 4.75f, FloorTop, 8.60f, "Küche", true, true, false, "Boden vor Kühlschrank"),
                Spot(parent, 5, 5.85f, 1.00f, 7.35f, "Küche", true, false, true, "Türrahmen zum Wohnzimmer"),
                Spot(parent, 6, 2.70f, FloorTop, 2.20f, "Flur", true, true, false, "Boden vor Küchentür"),
                Spot(parent, 7, 6.00f, 0.92f, 0.30f, "Flur", true, false, true, "Kommode"),
                Spot(parent, 8, 12.50f, FloorTop, 1.50f, "Flur", true, true, false, "Boden Flurmitte"),
                Spot(parent, 9, 17.15f, 1.00f, 2.85f, "Flur", true, false, true, "Türrahmen Schlafzimmer"),
                Spot(parent, 10, 17.30f, FloorTop, 7.00f, "Schlafzimmer", true, true, false, "Boden neben Bett"),
                Spot(parent, 11, 17.85f, 0.62f, 8.35f, "Schlafzimmer", true, false, true, "Nachttisch"),
                Spot(parent, 12, 15.00f, FloorTop, 4.20f, "Schlafzimmer", true, true, false, "Ecke hinter Schrank"),
            };
            return list;
        }

        private static void BuildInteraction(Transform parent)
        {
            // Sicherungskasten: Kammer am Ende des Flurs, Ostwand
            var power = Box("Sicherungskasten", parent, 19.75f, 19.9f, 1.1f, 1.9f, 1.15f, 1.85f, matPowerBox);
            power.AddComponent<MapMarker>().EditorSetup(MapMarkerType.Sicherungskasten, 1);

            // Analyse-Koffer: Startpunkt auf dem Sideboard im Wohnzimmer
            var caseSpawn = new GameObject("AnalyseKoffer_Start");
            caseSpawn.transform.SetParent(parent, false);
            caseSpawn.transform.localPosition = H(10f, 0.87f, 9.65f);
            caseSpawn.AddComponent<MapMarker>().EditorSetup(MapMarkerType.AnalyseKoffer, 1);
            var kit = Box("AnalyseKoffer (Platzhalter)", caseSpawn.transform, 9.75f, 10.25f, 0.87f, 1.1f, 9.5f, 9.8f, matCase);
            kit.transform.localPosition = new Vector3(0f, 0.115f, 0f);
        }

        private static Transform BuildSpawns(Transform parent)
        {
            var points = new[]
            {
                new Vector2(1.0f, 1.5f), new Vector2(4.6f, 6.6f), new Vector2(7.2f, 4.0f),
                new Vector2(12.9f, 9.0f), new Vector2(18.6f, 4.4f), new Vector2(16.2f, 1.2f),
            };
            Transform first = null;
            for (int i = 0; i < points.Length; i++)
            {
                var go = new GameObject($"Start_{i + 1}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = H(points[i].x, FloorTop, points[i].y);
                go.transform.localRotation = LookAtTable(points[i]);
                go.AddComponent<MapMarker>().EditorSetup(MapMarkerType.Spielerstart, i + 1);
                if (first == null) first = go.transform;
            }
            return first;
        }

        private static void BuildSeats(Transform parent)
        {
            int i = 1;
            foreach (var c in ChairPositions())
            {
                var go = new GameObject($"Platz_{i}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = H(c.x, FloorTop, c.y);
                go.transform.localRotation = LookAtTable(c);
                go.AddComponent<MapMarker>().EditorSetup(MapMarkerType.MeetingPlatz, i);
                i++;
            }
        }

        /// <summary>Beispielrunde aus Konzept 1.1: echte Spuren Blut A, Kreise, Schwarz – Fälschungen Zickzack und Blut 0.</summary>
        private static void BuildTestClues(Transform parent, List<ClueSpot> spots)
        {
            TestClue(parent, spots[0], "Blut_A (echt)", ClueCategory.Blut, BloodType.A, ShoeProfile.Kreise, ClothingColor.Rot,
                "Getrocknetes Blut auf dem Küchenboden, direkt beim Tatort.");
            TestClue(parent, spots[5], "Schuh_Kreise (echt)", ClueCategory.Schuh, BloodType.A, ShoeProfile.Kreise, ClothingColor.Rot,
                "Ein Abdruck mit Kreis-Profil vor der Küchentür.");
            TestClue(parent, spots[1], "Faser_Schwarz (echt)", ClueCategory.Faser, BloodType.A, ShoeProfile.Kreise, ClothingColor.Schwarz,
                "Eine schwarze Faser hängt an der Tischkante.");
            TestClue(parent, spots[7], "Schuh_Zickzack (Faelschung 1)", ClueCategory.Schuh, BloodType.A, ShoeProfile.Zickzack, ClothingColor.Rot,
                "Ein Zickzack-Abdruck mitten im Flur.");
            TestClue(parent, spots[9], "Blut_0 (Faelschung 2)", ClueCategory.Blut, BloodType.Null, ShoeProfile.Kreise, ClothingColor.Rot,
                "Ein paar frische Blutstropfen neben dem Bett.");
        }

        private static void MovePlayerTo(Transform spawn)
        {
            if (spawn == null) return;
            var players = FindInScenes<PlayerController>();
            if (players.Count == 0)
            {
                Debug.Log("[Test-Haus] Kein Player mit PlayerController gefunden – setz ihn selbst auf \"Start_1\".");
                return;
            }

            var player = players[0].transform;
            var cc = player.GetComponent<CharacterController>();
            float pivotHeight = cc != null ? cc.height * 0.5f - cc.center.y + 0.05f : 1f;
            Undo.RecordObject(player, "Player auf Start_1 setzen");
            player.position = spawn.position + Vector3.up * pivotHeight;
            player.rotation = spawn.rotation;
            Debug.Log($"[Test-Haus] Player \"{player.name}\" auf Start_1 gesetzt (im Flur).", player);
        }

        // ------------------------------------------------------------------ Bausteine

        private static Vector3 H(float x, float y, float z) => new Vector3(x - Origin.x, y, z - Origin.z);

        private static Vector2[] ChairPositions() => new[]
        {
            new Vector2(9.0f, 5.25f), new Vector2(11.0f, 5.25f), new Vector2(9.0f, 7.75f),
            new Vector2(11.0f, 7.75f), new Vector2(7.9f, 6.5f), new Vector2(12.1f, 6.5f),
        };

        private static Quaternion LookAtTable(Vector2 from)
        {
            var dir = new Vector3(Origin.x - from.x, 0f, Origin.z - from.y);
            return dir.sqrMagnitude < 0.001f ? Quaternion.identity : Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Box(string name, Transform parent, float x0, float x1, float y0, float y1, float z0, float z1,
            Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = H((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            go.transform.localScale = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        private static void Floor(string name, Transform parent, float x0, float x1, float z0, float z1, Material material)
        {
            Box(name, parent, x0, x1, FloorTop - 0.1f, FloorTop, z0, z1, material);
        }

        /// <summary>Wand entlang der x-Achse bei z, mit Türöffnungen (Start, Ende in x).</summary>
        private static void WallAlongX(string name, Transform parent, float z, float x0, float x1, params Vector2[] doors)
        {
            float z0 = z - WallThickness * 0.5f, z1 = z + WallThickness * 0.5f;
            float cursor = x0;
            int part = 1;
            foreach (var door in doors)
            {
                if (door.x > cursor) Box($"{name}_{part++}", parent, cursor, door.x, 0f, WallHeight, z0, z1, matWall);
                Box($"{name}_Sturz_{part++}", parent, door.x, door.y, DoorHeight, WallHeight, z0, z1, matWall);
                cursor = door.y;
            }
            if (x1 > cursor) Box($"{name}_{part}", parent, cursor, x1, 0f, WallHeight, z0, z1, matWall);
        }

        /// <summary>Wand entlang der z-Achse bei x, mit Türöffnungen (Start, Ende in z).</summary>
        private static void WallAlongZ(string name, Transform parent, float x, float z0, float z1, params Vector2[] doors)
        {
            float x0 = x - WallThickness * 0.5f, x1 = x + WallThickness * 0.5f;
            float cursor = z0;
            int part = 1;
            foreach (var door in doors)
            {
                if (door.x > cursor) Box($"{name}_{part++}", parent, x0, x1, 0f, WallHeight, cursor, door.x, matWall);
                Box($"{name}_Sturz_{part++}", parent, x0, x1, DoorHeight, WallHeight, door.x, door.y, matWall);
                cursor = door.y;
            }
            if (z1 > cursor) Box($"{name}_{part}", parent, x0, x1, 0f, WallHeight, cursor, z1, matWall);
        }

        private static void PointLight(string name, Transform parent, float x, float z, float range, float intensity, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = H(x, 2.7f, z);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
        }

        private static ClueSpot Spot(Transform parent, int id, float x, float y, float z, string room,
            bool blood, bool shoe, bool fiber, string description)
        {
            var go = new GameObject($"S{id:00}_{Ascii(room)}_{Ascii(description).Replace(' ', '_')}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = H(x, y, z);
            var spot = go.AddComponent<ClueSpot>();
            spot.EditorSetup(id, room, blood, shoe, fiber);
            return spot;
        }

        private static void TestClue(Transform parent, ClueSpot spot, string label, ClueCategory category,
            BloodType blood, ShoeProfile shoe, ClothingColor color, string description)
        {
            var go = new GameObject($"TestSpur_S{spot.SpotId:00}_{label}");
            go.transform.SetParent(parent, false);
            go.transform.position = spot.transform.position;

            // Sichtbarer Platzhalter (ohne Collider)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            visual.transform.SetParent(go.transform, false);
            switch (category)
            {
                case ClueCategory.Blut:
                    visual.transform.localScale = new Vector3(0.35f, 0.01f, 0.3f);
                    visual.transform.localPosition = new Vector3(0f, 0.006f, 0f);
                    visual.GetComponent<MeshRenderer>().sharedMaterial = matBlood;
                    break;
                case ClueCategory.Schuh:
                    visual.transform.localScale = new Vector3(0.14f, 0.01f, 0.32f);
                    visual.transform.localPosition = new Vector3(0f, 0.006f, 0f);
                    visual.GetComponent<MeshRenderer>().sharedMaterial = matShoe;
                    break;
                default:
                    visual.transform.localScale = new Vector3(0.18f, 0.02f, 0.03f);
                    visual.transform.localPosition = new Vector3(0f, 0.012f, 0f);
                    visual.GetComponent<MeshRenderer>().sharedMaterial = matFiberBlack;
                    break;
            }

            // Grosszügiger Trigger, damit man die Spur mit E gut trifft und trotzdem darüberlaufen kann
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.1f, 0f);
            trigger.size = new Vector3(0.5f, 0.25f, 0.5f);

            var clue = go.AddComponent<ClueObject>();
            var so = new SerializedObject(clue);
            so.FindProperty("description").stringValue = description;
            so.FindProperty("location").stringValue = spot.RoomName;
            so.FindProperty("category").enumValueIndex = (int)category;
            so.FindProperty("bloodType").enumValueIndex = (int)blood;
            so.FindProperty("shoeProfile").enumValueIndex = (int)shoe;
            so.FindProperty("clothingColor").enumValueIndex = (int)color;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Materialien

        private static void CreateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/04_Materials")) AssetDatabase.CreateFolder("Assets", "04_Materials");
                AssetDatabase.CreateFolder("Assets/04_Materials", "Greybox");
            }

            matWall = Mat("GB_Wand", "e9e4da");
            matCeiling = Mat("GB_Decke", "d9d5cc");
            matFurniture = Mat("GB_Moebel", "a8875f");
            matTable = Mat("GB_Holz_dunkel", "6e4f33");
            matDoor = Mat("GB_Tuer", "5a3a24");
            matFloorKitchen = Mat("GB_Boden_Kueche", "b9c4c0");
            matFloorLiving = Mat("GB_Boden_Wohnzimmer", "c9a06c");
            matFloorBedroom = Mat("GB_Boden_Schlafzimmer", "a9b59a");
            matFloorHall = Mat("GB_Boden_Flur", "cdbb95");
            matFloorStorage = Mat("GB_Boden_Kammer", "9a948a");
            matPowerBox = Mat("GB_Sicherungskasten", "f2c230");
            matCase = Mat("GB_AnalyseKoffer", "e07b2e");
            matCrimeScene = Mat("GB_Tatort", "b8463c");
            matBlood = Mat("GB_Spur_Blut", "6e1410");
            matShoe = Mat("GB_Spur_Schuh", "2e2018");
            matFiberBlack = Mat("GB_Spur_Faser_Schwarz", "1c1a1e");
            AssetDatabase.SaveAssets();
        }

        private static Material Mat(string name, string hex)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ------------------------------------------------------------------ Helfer

        private static string Ascii(string s) =>
            s.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue");

        private static List<T> FindInScenes<T>() where T : Component
        {
            var list = new List<T>();
            foreach (var c in Resources.FindObjectsOfTypeAll<T>())
            {
                if (c == null || EditorUtility.IsPersistent(c)) continue;
                if (!c.gameObject.scene.IsValid()) continue;
                if ((c.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
                list.Add(c);
            }
            return list;
        }
    }
}
