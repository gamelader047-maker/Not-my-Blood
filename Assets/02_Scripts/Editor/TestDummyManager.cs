using UnityEditor;
using UnityEngine;
using NotMyBlood.Player;
using NotMyBlood.Map;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Editor-Tool zum Erstellen und Ausrichten der Test-Dummies auf den Marker-Positionen Start_2 bis Start_4.
    /// </summary>
    public class TestDummySetupWindow : EditorWindow
    {
        [MenuItem("Not My Blood/Test-Dummies erstellen")]
        public static void ShowWindow()
        {
            GetWindow<TestDummySetupWindow>("Test-Dummies Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Test-Dummies auf Start_2 bis Start_4 platzieren", EditorStyles.boldLabel);

            if (GUILayout.Button("Dummies Spawnen / Ausrichten"))
            {
                CreateAndPlaceDummies();
            }
        }

        private static void CreateAndPlaceDummies()
        {
            MapMarker[] markers = FindObjectsByType<MapMarker>(FindObjectsSortMode.None);

            for (int i = 2; i <= 4; i++)
            {
                string targetMarkerName = $"Start_{i}";
                MapMarker targetMarker = System.Array.Find(markers, m => m.gameObject.name == targetMarkerName);

                Vector3 spawnPos = targetMarker != null ? targetMarker.transform.position : new Vector3((i - 1) * 2f, 1f, 0f);
                Quaternion spawnRot = targetMarker != null ? targetMarker.transform.rotation : Quaternion.identity;

                string dummyName = $"TestDummy_Player_{i}";
                GameObject dummyObj = GameObject.Find(dummyName);

                if (dummyObj == null)
                {
                    dummyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    dummyObj.name = dummyName;
                    Undo.RegisterCreatedObjectUndo(dummyObj, "Create Test Dummy");
                }

                dummyObj.transform.position = spawnPos;
                dummyObj.transform.rotation = spawnRot;

                PlayerIdentity identity = dummyObj.GetComponent<PlayerIdentity>();
                if (identity == null)
                {
                    identity = dummyObj.AddComponent<PlayerIdentity>();
                }

                Debug.Log($"[TestDummySetup] {dummyName} auf Position {spawnPos} platziert.");
            }
        }
    }
}