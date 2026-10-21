using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NotMyBlood.Clues;
using NotMyBlood.Core;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Tools > Not My Blood > Spuren-Prefabs erstellen
    /// Erstellt Clue_Blut, Clue_Schuh und Clue_Faser in Assets/03_Prefabs/Clues
    /// und verknüpft sie mit dem ClueSpawner in der Szene (legt ihn an, falls er fehlt).
    ///
    /// Modelle (flache Aufkleber mit Textur aus Assets/08_Models/Textures):
    /// - Blut: Lache mit Tropfen (Boden, Möbel) und Spritzer (Wand) – ClueObject wählt je nach Spurenpunkt
    /// - Schuh: zwei Abdrücke, je ein Kind pro Profil (Kreise, Zickzack, Glatt) – ClueObject zeigt das passende
    /// - Faser: Stofffetzen mit Fäden, wird im Spiel in Rot/Schwarz/Grau eingefärbt
    /// </summary>
    public static class CluePrefabBuilder
    {
        private const string PrefabDir = "Assets/03_Prefabs/Clues";

        [MenuItem("Tools/Not My Blood/Spuren-Prefabs erstellen")]
        public static void Build()
        {
            ModelKit.EnsureFolder(PrefabDir);

            var blood = BuildBlood();
            var shoe = BuildShoe();
            var fiber = BuildFiber();
            if (blood == null || shoe == null || fiber == null)
            {
                Debug.LogError("[Spuren-Prefabs] Abgebrochen – Texturen fehlen (siehe Fehler oben).");
                return;
            }

            AssignToSpawner(blood, shoe, fiber);
            AssetDatabase.SaveAssets();
            Debug.Log("[Spuren-Prefabs] Clue_Blut, Clue_Schuh und Clue_Faser mit neuen Modellen erstellt und dem ClueSpawner zugewiesen.");
        }

        // ------------------------------------------------------------------ Blut

        private static GameObject BuildBlood()
        {
            var pool = ModelKit.Texture("blood_pool.png");
            var spatter = ModelKit.Texture("blood_spatter.png");
            if (pool == null || spatter == null) return null;

            var matPool = ModelKit.Cutout("M_Blut_Lache", pool, "ffffff", smoothness: 0.75f);
            var matSpatter = ModelKit.Cutout("M_Blut_Spritzer", spatter, "ffffff", smoothness: 0.6f);

            var root = NewClueRoot("Clue_Blut", ClueCategory.Blut, new Vector3(0.6f, 0.25f, 0.6f));
            var visual = ModelKit.Group("Visual", root.transform).transform;
            ModelKit.Decal("Blut_Boden", visual, matPool, 0.55f, 0.55f);
            var wall = ModelKit.Decal("Blut_Wand", visual, matSpatter, 0.5f, 0.5f);
            wall.SetActive(false);
            return SavePrefab(root);
        }

        // ------------------------------------------------------------------ Schuh

        private static GameObject BuildShoe()
        {
            var kreise = ModelKit.Texture("shoe_kreise.png");
            var zickzack = ModelKit.Texture("shoe_zickzack.png");
            var glatt = ModelKit.Texture("shoe_glatt.png");
            if (kreise == null || zickzack == null || glatt == null) return null;

            var root = NewClueRoot("Clue_Schuh", ClueCategory.Schuh, new Vector3(0.6f, 0.25f, 0.85f));
            var visual = ModelKit.Group("Visual", root.transform).transform;

            // Textur 512 x 1024 → Aufkleber 0.36 x 0.72 m (zwei Abdrücke, je ca. 28 cm lang)
            ModelKit.Decal("Profil_Kreise", visual, ModelKit.Cutout("M_Schuh_Kreise", kreise, "ffffff", 0.1f), 0.36f, 0.72f);
            ModelKit.Decal("Profil_Zickzack", visual, ModelKit.Cutout("M_Schuh_Zickzack", zickzack, "ffffff", 0.1f), 0.36f, 0.72f)
                .SetActive(false);
            ModelKit.Decal("Profil_Glatt", visual, ModelKit.Cutout("M_Schuh_Glatt", glatt, "ffffff", 0.1f), 0.36f, 0.72f)
                .SetActive(false);
            return SavePrefab(root);
        }

        // ------------------------------------------------------------------ Faser

        private static GameObject BuildFiber()
        {
            var scrap = ModelKit.Texture("fabric_scrap.png");
            if (scrap == null) return null;

            var root = NewClueRoot("Clue_Faser", ClueCategory.Faser, new Vector3(0.45f, 0.25f, 0.45f));
            var visual = ModelKit.Group("Visual", root.transform).transform;
            // Weiss – die Farbe kommt im Spiel per ClueObject (Rot, Schwarz, Grau)
            ModelKit.Decal("Stofffetzen", visual, ModelKit.Cutout("M_Faser", scrap, "ffffff", 0.05f), 0.2f, 0.2f);
            return SavePrefab(root);
        }

        // ------------------------------------------------------------------ Helfer

        private static GameObject NewClueRoot(string name, ClueCategory category, Vector3 triggerSize)
        {
            var root = new GameObject(name);

            // Grosszügiger Trigger: mit E gut zu treffen, man kann trotzdem darüberlaufen
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.1f, 0f);
            trigger.size = triggerSize;

            var clue = root.AddComponent<ClueObject>();
            var so = new SerializedObject(clue);
            so.FindProperty("category").enumValueIndex = (int)category;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject SavePrefab(GameObject root)
        {
            string path = $"{PrefabDir}/{root.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AssignToSpawner(GameObject blood, GameObject shoe, GameObject fiber)
        {
            var spawners = Object.FindObjectsByType<ClueSpawner>(FindObjectsInactive.Include);
            ClueSpawner spawner;
            if (spawners.Length > 0)
            {
                spawner = spawners[0];
            }
            else
            {
                var go = new GameObject("ClueSpawner", typeof(ClueSpawner));
                Undo.RegisterCreatedObjectUndo(go, "ClueSpawner erstellen");
                spawner = go.GetComponent<ClueSpawner>();
                Debug.Log("[Spuren-Prefabs] Kein ClueSpawner gefunden – habe einen erstellt.");
            }

            var so = new SerializedObject(spawner);
            so.FindProperty("prefabBlood").objectReferenceValue = blood;
            so.FindProperty("prefabShoe").objectReferenceValue = shoe;
            so.FindProperty("prefabFiber").objectReferenceValue = fiber;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        }
    }
}
