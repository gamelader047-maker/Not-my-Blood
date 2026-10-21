using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NotMyBlood.Player;
using NotMyBlood.Map;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Tools > Not My Blood > Test-Dummies erstellen
    /// Stellt 3 Kapseln als Mitspieler auf Start_2 bis Start_4 und gibt auch dem eigenen Player
    /// eine PlayerIdentity, damit man mit 4 "Spielern" testen kann.
    /// </summary>
    public class TestDummySetupWindow : EditorWindow
    {
        [MenuItem("Tools/Not My Blood/Test-Dummies erstellen")]
        public static void ShowWindow()
        {
            GetWindow<TestDummySetupWindow>("Test-Dummies");
        }

        private void OnGUI()
        {
            GUILayout.Label("Test-Dummies auf Start_2 bis Start_4 platzieren", EditorStyles.boldLabel);
            GUILayout.Space(6);
            if (GUILayout.Button("Dummies erstellen / ausrichten", GUILayout.Height(28)))
            {
                CreateAndPlaceDummies();
            }
        }

        private static void CreateAndPlaceDummies()
        {
            MapMarker[] markers = FindObjectsByType<MapMarker>(FindObjectsInactive.Exclude);

            // Eigener Player bekommt auch eine Identität
            var players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
            if (players.Length > 0)
            {
                var me = players[0].gameObject;
                var myIdentity = me.GetComponent<PlayerIdentity>();
                if (myIdentity == null) myIdentity = Undo.AddComponent<PlayerIdentity>(me);
                SetName(myIdentity, "Spieler 1 (Du)");
            }
            else
            {
                Debug.LogWarning("[Test-Dummies] Kein Player mit PlayerController gefunden – nur Dummies werden erstellt.");
            }

            for (int i = 2; i <= 4; i++)
            {
                MapMarker marker = System.Array.Find(markers, m => m.gameObject.name == $"Start_{i}");
                // Kapsel ist 2 m hoch, Pivot in der Mitte → 1 m über dem Boden-Marker
                Vector3 position = marker != null ? marker.transform.position + Vector3.up * 1f : new Vector3(i * 2f, 1f, 0f);
                Quaternion rotation = marker != null ? marker.transform.rotation : Quaternion.identity;

                string dummyName = $"TestDummy_Spieler_{i}";
                GameObject dummy = GameObject.Find(dummyName);
                if (dummy == null)
                {
                    dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    dummy.name = dummyName;
                    Undo.RegisterCreatedObjectUndo(dummy, "Test-Dummy erstellen");
                }
                else
                {
                    Undo.RecordObject(dummy.transform, "Test-Dummy ausrichten");
                }

                dummy.transform.SetPositionAndRotation(position, rotation);

                var identity = dummy.GetComponent<PlayerIdentity>();
                if (identity == null) identity = Undo.AddComponent<PlayerIdentity>(dummy);
                SetName(identity, $"Dummy {i}");

                if (marker == null) Debug.LogWarning($"[Test-Dummies] Marker Start_{i} nicht gefunden – erst das Test-Haus erstellen.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Test-Dummies] 3 Dummies platziert. Szene speichern, dann Play → F6 zeigt die Merkmale.");
        }

        private static void SetName(PlayerIdentity identity, string name)
        {
            var so = new SerializedObject(identity);
            so.FindProperty("playerName").stringValue = name;
            so.ApplyModifiedProperties();
        }
    }
}
