using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NotMyBlood.Player;
using NotMyBlood.UI;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Tools > Not My Blood > Interaktions-HUD erstellen
    /// Legt ein kleines HUD an: Fadenkreuz-Punkt, Fortschrittsring für "E halten" und Ergebnistext.
    /// </summary>
    public static class HudBuilder
    {
        private const string RingPath = "Assets/07_UI/HUD/hud_ring.png";
        private const string FontDir = "Assets/07_UI/Notebook/Fonts";
        private static readonly Color Cream = new Color(0.96f, 0.91f, 0.80f);

        [MenuItem("Tools/Not My Blood/Interaktions-HUD erstellen")]
        public static void Build()
        {
            var existing = GameObject.Find("HUDCanvas");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Interaktions-HUD",
                        "In der Szene gibt es schon ein \"HUDCanvas\". Soll es ersetzt werden?", "Ersetzen", "Abbrechen"))
                {
                    return;
                }
                Undo.DestroyObjectImmediate(existing);
            }

            var ring = LoadRingSprite();
            var fontBody = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/PatrickHand-Regular SDF.asset");
            var fontMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/SpecialElite-Regular SDF.asset");

            var canvasGo = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;   // unter dem Notizbuch (50)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Fadenkreuz-Punkt (hilft beim Zielen auf Spuren)
            var dot = Centered("Crosshair", canvasGo.transform, 0, 0, 6, 6);
            dot.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.8f);
            dot.GetComponent<Image>().raycastTarget = false;

            // Halte-Anzeige
            var holdRoot = Centered("HoldIndicator", canvasGo.transform, 0, 0, 400, 200);
            var ringBg = Centered("RingBackground", holdRoot.transform, 0, 0, 72, 72);
            var bgImg = ringBg.AddComponent<Image>();
            bgImg.sprite = ring;
            bgImg.color = new Color(0f, 0f, 0f, 0.4f);
            bgImg.raycastTarget = false;

            var ringFill = Centered("RingFill", holdRoot.transform, 0, 0, 72, 72);
            var fillImg = ringFill.AddComponent<Image>();
            fillImg.sprite = ring;
            fillImg.color = Cream;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = true;
            fillImg.fillAmount = 0.35f;
            fillImg.raycastTarget = false;

            var label = Centered("Label", holdRoot.transform, 0, -62, 400, 40);
            var labelText = Text(label, "Analysiere Spur … 2.0 s", fontBody, 28);
            holdRoot.SetActive(false);

            // Ergebnis
            var result = Centered("Result", canvasGo.transform, 0, -110, 700, 50);
            var resultText = Text(result, "ECHT", fontMono, 36);
            result.SetActive(false);

            var ui = canvasGo.AddComponent<InteractionHoldUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("holdRoot").objectReferenceValue = holdRoot;
            so.FindProperty("fillImage").objectReferenceValue = fillImg;
            so.FindProperty("holdLabel").objectReferenceValue = labelText;
            so.FindProperty("resultLabel").objectReferenceValue = resultText;
            var players = FindInScenes<PlayerInteraction>();
            if (players.Count > 0) so.FindProperty("player").objectReferenceValue = players[0];
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(canvasGo, "Interaktions-HUD erstellen");
            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[HUD] Interaktions-HUD erstellt. Szene speichern nicht vergessen.");
        }

        private static Sprite LoadRingSprite()
        {
            var importer = AssetImporter.GetAtPath(RingPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 200;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
            if (sprite == null) Debug.LogWarning($"[HUD] {RingPath} nicht gefunden – der Ring bleibt ein Quadrat.");
            return sprite;
        }

        private static GameObject Centered(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        private static TextMeshProUGUI Text(GameObject go, string text, TMP_FontAsset font, float size)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = Cream;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        private static List<T> FindInScenes<T>() where T : Component
        {
            var list = new List<T>();
            foreach (var c in Resources.FindObjectsOfTypeAll<T>())
            {
                if (c == null || EditorUtility.IsPersistent(c) || !c.gameObject.scene.IsValid()) continue;
                list.Add(c);
            }
            return list;
        }
    }
}
