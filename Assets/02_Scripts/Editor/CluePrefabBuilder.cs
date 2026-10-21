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
    /// </summary>
    public static class CluePrefabBuilder
    {
        private const string PrefabDir = "Assets/03_Prefabs/Clues";
        private const string MaterialDir = "Assets/04_Materials/Greybox";

        [MenuItem("Tools/Not My Blood/Spuren-Prefabs erstellen")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/03_Prefabs")) AssetDatabase.CreateFolder("Assets", "03_Prefabs");
                AssetDatabase.CreateFolder("Assets/03_Prefabs", "Clues");
            }

            var blood = CreatePrefab("Clue_Blut", ClueCategory.Blut, new Vector3(0.35f, 0.01f, 0.3f), "GB_Spur_Blut", "6e1410");
            var shoe = CreatePrefab("Clue_Schuh", ClueCategory.Schuh, new Vector3(0.14f, 0.01f, 0.32f), "GB_Spur_Schuh", "2e2018");
            var fiber = CreatePrefab("Clue_Faser", ClueCategory.Faser, new Vector3(0.18f, 0.02f, 0.03f), "GB_Spur_Faser", "ffffff");

            AssignToSpawner(blood, shoe, fiber);
            AssetDatabase.SaveAssets();
            Debug.Log("[Spuren-Prefabs] Clue_Blut, Clue_Schuh und Clue_Faser erstellt und dem ClueSpawner zugewiesen. Szene speichern!");
        }

        private static GameObject CreatePrefab(string name, ClueCategory category, Vector3 visualSize, string materialName, string hex)
        {
            var root = new GameObject(name);

            // Sichtbarer Platzhalter (ohne Collider), knapp über dem Spurenpunkt
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = visualSize;
            visual.transform.localPosition = new Vector3(0f, visualSize.y * 0.5f + 0.001f, 0f);
            visual.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(materialName, hex);

            // Grosszügiger Trigger: mit E gut zu treffen, man kann trotzdem darüberlaufen
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.1f, 0f);
            trigger.size = new Vector3(0.5f, 0.25f, 0.5f);

            var clue = root.AddComponent<ClueObject>();
            var so = new SerializedObject(clue);
            so.FindProperty("category").enumValueIndex = (int)category;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material GetMaterial(string name, string hex)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            if (!AssetDatabase.IsValidFolder(MaterialDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/04_Materials")) AssetDatabase.CreateFolder("Assets", "04_Materials");
                AssetDatabase.CreateFolder("Assets/04_Materials", "Greybox");
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
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
