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
    /// Baut das komplette HUD (weisse Schrift mit Schatten):
    /// - oben Mitte: Phase, Restzeit, Fortschrittsbalken (RoundTimerUI)
    /// - unten links: Tasten-Hinweise mit Icons (ControlHintsUI)
    /// - Bildmitte: Fadenkreuz, Interaktions-Hinweis (InteractionPromptUI), Halte-Ring + Ergebnis (InteractionHoldUI)
    /// </summary>
    public static class HudBuilder
    {
        private const string HudDir = "Assets/07_UI/HUD";
        private const string FontDir = "Assets/07_UI/Notebook/Fonts";
        private static readonly Color White = Color.white;
        private static readonly Color Cream = new Color(0.96f, 0.91f, 0.80f);

        private static TMP_FontAsset fontBody, fontMono;
        private static Material bodyShadow, monoShadow;

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

            ConfigureSprites();
            fontBody = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/PatrickHand-Regular SDF.asset");
            fontMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/SpecialElite-Regular SDF.asset");
            bodyShadow = ShadowMaterial(fontBody, "PatrickHand HUD Schatten");
            monoShadow = ShadowMaterial(fontMono, "SpecialElite HUD Schatten");

            var canvasGo = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;   // unter dem Notizbuch (50)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var players = FindInScenes<PlayerInteraction>();
            var player = players.Count > 0 ? players[0] : null;

            BuildTimer(canvasGo.transform);
            BuildHints(canvasGo.transform, player);
            BuildCenter(canvasGo.transform, player);

            Undo.RegisterCreatedObjectUndo(canvasGo, "Interaktions-HUD erstellen");
            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[HUD] HUD erstellt (Timer, Tasten-Hinweise, Interaktion). Szene speichern nicht vergessen.");
        }

        // ------------------------------------------------------------------ Bereiche

        private static void BuildTimer(Transform canvas)
        {
            var root = Anchored("RoundTimer", canvas, new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(250, 96));
            Img(root, "hud_pill", Image.Type.Sliced, White);

            var phase = Anchored("Phase", root.transform, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(240, 26));
            var phaseText = Text(phase, "ERMITTLUNG", fontMono, monoShadow, 17, new Color(1, 1, 1, 0.85f));
            phaseText.characterSpacing = 30;

            var time = Anchored("Time", root.transform, new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(240, 48));
            var timeText = Text(time, "06:00", fontMono, monoShadow, 44, White);

            var track = Anchored("ProgressTrack", root.transform, new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(200, 4));
            Img(track, "hud_bar", Image.Type.Sliced, new Color(1, 1, 1, 0.2f));
            var fill = Stretch("Fill", track.transform);
            var fillImg = Img(fill, "hud_bar", Image.Type.Filled, White);
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0.35f;

            var ui = canvas.gameObject.AddComponent<RoundTimerUI>();
            Set(ui, "root", root);
            Set(ui, "phaseText", phaseText);
            Set(ui, "timeText", timeText);
            Set(ui, "progressFill", fillImg);
        }

        private static void BuildHints(Transform canvas, PlayerInteraction player)
        {
            var root = Anchored("ControlHints", canvas, new Vector2(0f, 0f), new Vector2(36, 32), new Vector2(420, 190), pivot: new Vector2(0f, 0f));

            var shade = Anchored("Shade", root.transform, new Vector2(0f, 0f), new Vector2(-200, -200), new Vector2(640, 540), pivot: new Vector2(0f, 0f));
            Img(shade, "hud_shade", Image.Type.Simple, White);
            shade.AddComponent<LayoutElement>().ignoreLayout = true;

            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;

            HintRow(root.transform, "hud_icon_notebook", "Tab", "Notizbuch");
            HintRow(root.transform, "hud_icon_search", "E", "Untersuchen");
            var analyze = HintRow(root.transform, "hud_icon_kit", "E halten", "Analysieren");
            HintRow(root.transform, "hud_icon_crouch", "Shift", "Ducken");

            var ui = canvas.gameObject.AddComponent<ControlHintsUI>();
            Set(ui, "root", root);
            Set(ui, "analyzeRow", analyze);
            if (player != null) Set(ui, "player", player);
        }

        private static void BuildCenter(Transform canvas, PlayerInteraction player)
        {
            // Fadenkreuz
            var dot = Anchored("Crosshair", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
            Img(dot, null, Image.Type.Simple, new Color(1, 1, 1, 0.85f));

            // Interaktions-Hinweis unter dem Fadenkreuz
            var prompt = Anchored("InteractionPrompt", canvas, new Vector2(0.5f, 0.5f), new Vector2(0, -46), new Vector2(900, 90), pivot: new Vector2(0.5f, 1f));
            var vlg = prompt.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;
            var mainRow = Row(prompt.transform, "Main");
            Key(mainRow.transform, "E");
            var promptText = Text(New("Text", mainRow.transform), "Untersuchen: Blutspur", fontBody, bodyShadow, 27, White, TextAlignmentOptions.Left);
            var holdRow = Row(prompt.transform, "Hold");
            Key(holdRow.transform, "E halten");
            Text(New("Text", holdRow.transform), "Analysieren", fontBody, bodyShadow, 22, new Color(1, 1, 1, 0.85f), TextAlignmentOptions.Left);
            prompt.SetActive(false);

            var promptUi = canvas.gameObject.AddComponent<InteractionPromptUI>();
            Set(promptUi, "root", prompt);
            Set(promptUi, "promptText", promptText);
            Set(promptUi, "holdRow", holdRow);
            if (player != null) Set(promptUi, "player", player);

            // Halte-Ring + Ergebnis (E halten)
            var holdRoot = Anchored("HoldIndicator", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 200));
            var ringBg = Anchored("RingBackground", holdRoot.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72));
            Img(ringBg, "hud_ring", Image.Type.Simple, new Color(0f, 0f, 0f, 0.4f));
            var ringFill = Anchored("RingFill", holdRoot.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72));
            var fillImg = Img(ringFill, "hud_ring", Image.Type.Filled, Cream);
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = true;
            fillImg.fillAmount = 0.35f;
            var label = Anchored("Label", holdRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -62), new Vector2(400, 40));
            var labelText = Text(label, "Analysiere Spur … 2.0 s", fontBody, bodyShadow, 26, White);
            holdRoot.SetActive(false);

            var result = Anchored("Result", canvas, new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(700, 50));
            var resultText = Text(result, "Ergebnis: ECHT", fontMono, monoShadow, 34, White);
            result.SetActive(false);

            var holdUi = canvas.gameObject.AddComponent<InteractionHoldUI>();
            Set(holdUi, "holdRoot", holdRoot);
            Set(holdUi, "fillImage", fillImg);
            Set(holdUi, "holdLabel", labelText);
            Set(holdUi, "resultLabel", resultText);
            if (player != null) Set(holdUi, "player", player);
        }

        // ------------------------------------------------------------------ Bausteine

        private static GameObject HintRow(Transform parent, string icon, string key, string label)
        {
            var row = Row(parent, "Hint_" + label);
            var iconGo = New("Icon", row.transform);
            Fixed(iconGo, 26, 26);
            var img = Img(iconGo, icon, Image.Type.Simple, new Color(1, 1, 1, 0.9f));
            img.preserveAspect = true;
            Key(row.transform, key);
            Text(New("Label", row.transform), label, fontBody, bodyShadow, 25, White, TextAlignmentOptions.Left);
            return row;
        }

        private static GameObject Row(Transform parent, string name)
        {
            var row = New(name, parent);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            return row;
        }

        private static void Key(Transform parent, string text)
        {
            var key = New("Key_" + text, parent);
            Img(key, "hud_key", Image.Type.Sliced, White);
            var hlg = key.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(9, 9, 2, 2);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            var le = key.AddComponent<LayoutElement>();
            le.minWidth = 34;
            le.minHeight = le.preferredHeight = 32;
            Text(New("Label", key.transform), text, fontMono, monoShadow, 16, White);
        }

        private static GameObject New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject Anchored(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var go = New(name, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return go;
        }

        private static GameObject Stretch(string name, Transform parent)
        {
            var go = New(name, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        private static void Fixed(GameObject go, float w, float h)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = h;
        }

        private static Image Img(GameObject go, string sprite, Image.Type type, Color color)
        {
            var img = go.AddComponent<Image>();
            if (!string.IsNullOrEmpty(sprite))
            {
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{HudDir}/{sprite}.png");
                if (img.sprite == null) Debug.LogWarning($"[HUD] Sprite {sprite}.png fehlt.");
            }
            img.type = type;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI Text(GameObject go, string text, TMP_FontAsset font, Material material, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (material != null) t.fontSharedMaterial = material;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Kopie des Schrift-Materials mit weichem Schatten (Underlay), damit weisse Schrift überall lesbar ist.</summary>
        private static Material ShadowMaterial(TMP_FontAsset font, string name)
        {
            if (font == null || font.material == null) return null;
            string path = $"{HudDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = new Material(font.material) { name = name };
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.8f));
            mat.SetFloat("_UnderlayOffsetX", 0.5f);
            mat.SetFloat("_UnderlayOffsetY", -0.5f);
            mat.SetFloat("_UnderlayDilate", 0.3f);
            mat.SetFloat("_UnderlaySoftness", 0.6f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void ConfigureSprites()
        {
            var borders = new Dictionary<string, Vector4>
            {
                { "hud_key", new Vector4(20, 20, 20, 20) },
                { "hud_pill", new Vector4(36, 36, 36, 36) },
                { "hud_bar", new Vector4(8, 8, 8, 8) },
            };
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { HudDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                Vector4 border = borders.TryGetValue(name, out var b) ? b : Vector4.zero;
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == border
                    && Mathf.Approximately(importer.spritePixelsPerUnit, 200f)) continue;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 200f;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[HUD] Feld \"{field}\" nicht gefunden auf {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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
