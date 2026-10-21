using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using NotMyBlood.Core;
using NotMyBlood.UI;
using NotMyBlood.Clues;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Baut das komplette Notizbuch-UI per Klick:
    /// Tools > Not My Blood > Notizbuch-UI erstellen
    ///
    /// - stellt die Sprites richtig ein (Sprite, 9-Slice-Ränder, 200 PPU weil 2x exportiert)
    /// - erstellt die TextMeshPro-Schriften aus den TTF-Dateien
    /// - legt Canvas, Panels, Texte und Templates an und verknüpft alle Scripts
    /// - sorgt für ein EventSystem mit dem New Input System
    /// Positionen entsprechen dem Mockup (Referenzauflösung 1920x1080).
    /// </summary>
    public static class NotebookUIBuilder
    {
        private const string RootDir = "Assets/07_UI/Notebook";
        private const string SpriteDir = RootDir + "/Sprites";
        private const string FontDir = RootDir + "/Fonts";
        private const float SpritePixelsPerUnit = 200f;   // Sprites sind doppelt so gross exportiert

        private const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "äöüÄÖÜß·…–—„“”‚‘’«»éèêàâçëïîôûù°";

        // Farben aus dem Mockup
        private static readonly Color Ink = Hex("3a2416");
        private static readonly Color InkSoft = Hex("7a5a3c");
        private static readonly Color PageColor = Hex("f5e9cc");
        private static readonly Color PageEdge = Hex("e3cf9f");
        private static readonly Color PageBack = Hex("d8c08c");
        private static readonly Color Blood = Hex("a8322a");
        private static readonly Color StickyInk = Hex("4a3414");

        // 9-Slice-Ränder in Pixeln: (links, unten, rechts, oben)
        private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "nb_cover", new Vector4(80, 80, 80, 80) },
            { "nb_sheet", new Vector4(32, 32, 32, 32) },
            { "nb_row", new Vector4(32, 40, 32, 32) },
            { "nb_row_selected", new Vector4(32, 40, 32, 32) },
            { "nb_row_empty", new Vector4(32, 40, 32, 32) },
            { "nb_frame", new Vector4(28, 28, 28, 28) },
            { "nb_frame_dark", new Vector4(36, 46, 36, 36) },
            { "nb_chip", new Vector4(22, 22, 22, 22) },
            { "nb_key", new Vector4(22, 28, 22, 22) },
            { "nb_tab_active", new Vector4(44, 44, 4, 44) },
            { "nb_tab_inactive", new Vector4(44, 44, 4, 44) },
            { "nb_sticky", new Vector4(72, 72, 72, 72) },
            { "nb_pill", new Vector4(16, 16, 16, 16) },
            { "nb_scroll_track", new Vector4(18, 18, 18, 18) },
            { "nb_scroll_handle", new Vector4(20, 20, 20, 20) },
            { "nb_shadow", new Vector4(160, 160, 160, 160) },
        };

        // Diese Sprites werden gekachelt (Wrap Mode Repeat)
        private static readonly HashSet<string> RepeatSprites = new HashSet<string> { "nb_noise", "nb_leader" };

        private static TMP_FontAsset fontBody;
        private static TMP_FontAsset fontTitle;
        private static TMP_FontAsset fontMono;

        [MenuItem("Tools/Not My Blood/Notizbuch-UI erstellen")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(SpriteDir))
            {
                EditorUtility.DisplayDialog("Notizbuch-UI", $"Ordner {SpriteDir} fehlt. Sind die Sprites im Projekt?", "OK");
                return;
            }

            var existing = GameObject.Find("NotebookCanvas");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Notizbuch-UI",
                        "In der Szene gibt es schon ein \"NotebookCanvas\". Soll es ersetzt werden?",
                        "Ersetzen", "Abbrechen"))
                {
                    return;
                }
                Undo.DestroyObjectImmediate(existing);
            }

            EditorUtility.DisplayProgressBar("Notizbuch-UI", "Sprites einstellen …", 0.1f);
            try
            {
                ConfigureSprites();

                EditorUtility.DisplayProgressBar("Notizbuch-UI", "Schriften erstellen …", 0.4f);
                fontBody = GetOrCreateFont("PatrickHand-Regular");
                fontTitle = GetOrCreateFont("CaveatBrush-Regular");
                fontMono = GetOrCreateFont("SpecialElite-Regular");

                EditorUtility.DisplayProgressBar("Notizbuch-UI", "Szene vorbereiten …", 0.6f);
                DisableOldNotebooks();
                EnsureEventSystem();
                EnsureClueManager();

                EditorUtility.DisplayProgressBar("Notizbuch-UI", "UI aufbauen …", 0.8f);
                var canvas = BuildCanvas();
                Undo.RegisterCreatedObjectUndo(canvas, "Notizbuch-UI erstellen");
                RoundUIBuilder.RebuildSuspectsPage();   // Seite "Verdächtige" (Schritt 4) gleich mit aufbauen
                EditorSceneManager.MarkSceneDirty(canvas.scene);
                Selection.activeGameObject = canvas;

                Debug.Log("[Notizbuch] UI erstellt. Play drücken und mit Tab öffnen. Szene speichern nicht vergessen (Ctrl/Cmd+S).");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ Assets

        private static void ConfigureSprites()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                Vector4 border = Borders.TryGetValue(name, out var b) ? b : Vector4.zero;
                var wrap = RepeatSprites.Contains(name) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

                bool alreadyOk = importer.textureType == TextureImporterType.Sprite
                                 && Mathf.Approximately(importer.spritePixelsPerUnit, SpritePixelsPerUnit)
                                 && importer.spriteBorder == border
                                 && importer.wrapMode == wrap
                                 && !importer.mipmapEnabled;
                if (alreadyOk) continue;

                // FullRect-Mesh ist für Tiled/Sliced-Images nötig
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = SpritePixelsPerUnit;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = wrap;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static TMP_FontAsset GetOrCreateFont(string fileName)
        {
            string assetPath = $"{FontDir}/{fileName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{fileName}.ttf");
            if (font == null)
            {
                Debug.LogWarning($"[Notizbuch] Schrift {fileName}.ttf nicht gefunden – nehme die TMP-Standardschrift.");
                return TMP_Settings.defaultFontAsset;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                Debug.LogWarning($"[Notizbuch] Konnte keine TMP-Schrift aus {fileName}.ttf erstellen – nehme die Standardschrift. " +
                                 "Du kannst sie auch selbst erstellen: Window > TextMeshPro > Font Asset Creator.");
                return TMP_Settings.defaultFontAsset;
            }

            fontAsset.name = fileName + " SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            if (fontAsset.atlasTextures != null)
            {
                foreach (var texture in fontAsset.atlasTextures)
                {
                    if (texture == null) continue;
                    texture.name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }

            fontAsset.TryAddCharacters(Charset, out string _);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static Sprite LoadSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");
            if (sprite == null) Debug.LogWarning($"[Notizbuch] Sprite {name}.png nicht gefunden.");
            return sprite;
        }

        // ------------------------------------------------------------------ Szene

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

        private static void DisableOldNotebooks()
        {
            foreach (var old in FindInScenes<NotebookUI>())
            {
                if (!old.enabled) continue;
                Undo.RecordObject(old, "Altes Notizbuch deaktivieren");
                old.enabled = false;
                Debug.Log($"[Notizbuch] Altes NotebookUI auf \"{old.gameObject.name}\" deaktiviert. Du kannst das alte Objekt löschen.", old);
            }
        }

        private static void EnsureEventSystem()
        {
            var systems = FindInScenes<EventSystem>();
            if (systems.Count == 0)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                Undo.RegisterCreatedObjectUndo(go, "EventSystem erstellen");
                Debug.Log("[Notizbuch] EventSystem mit InputSystemUIInputModule erstellt.");
                return;
            }

            var es = systems[0];
            var oldModule = es.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                Undo.DestroyObjectImmediate(oldModule);
                Debug.Log("[Notizbuch] StandaloneInputModule entfernt (funktioniert nicht mit dem New Input System).");
            }
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                var module = Undo.AddComponent<InputSystemUIInputModule>(es.gameObject);
                module.AssignDefaultActions();
                Debug.Log("[Notizbuch] InputSystemUIInputModule zum EventSystem hinzugefügt.");
            }
        }

        private static void EnsureClueManager()
        {
            if (FindInScenes<ClueManager>().Count > 0) return;
            var go = new GameObject("ClueManager", typeof(ClueManager));
            Undo.RegisterCreatedObjectUndo(go, "ClueManager erstellen");
            Debug.Log("[Notizbuch] Kein ClueManager gefunden – habe einen erstellt.");
        }

        // ------------------------------------------------------------------ UI aufbauen

        private static GameObject BuildCanvas()
        {
            var canvasGo = new GameObject("NotebookCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;

            var notebookUI = canvasGo.AddComponent<NotebookUI>();

            // Panel, das NotebookUI ein- und ausschaltet
            var panel = New("NotebookPanel", canvasGo.transform);
            Stretch(panel);
            var display = panel.AddComponent<NotebookDisplay>();
            var tabs = panel.AddComponent<NotebookTabs>();
            SetRef(notebookUI, "notebookPanel", panel);

            var dim = New("Dim", panel.transform);
            Stretch(dim);
            Img(dim, null, Image.Type.Simple, new Color(0.04f, 0.03f, 0.04f, 0.45f), raycast: true);
            var vignette = New("Vignette", panel.transform);
            Stretch(vignette);
            Img(vignette, "nb_vignette");

            // Buch (1540 x 924), leicht nach rechts versetzt wegen der Tabs
            var book = New("Book", panel.transform);
            var bookRt = Rt(book);
            bookRt.anchorMin = bookRt.anchorMax = bookRt.pivot = new Vector2(0.5f, 0.5f);
            bookRt.anchoredPosition = new Vector2(20, 0);
            bookRt.sizeDelta = new Vector2(1540, 924);

            var shadow = New("Shadow", book.transform);
            Stretch(shadow, -60, -30, -60, -90);
            Img(shadow, "nb_shadow", Image.Type.Sliced);

            // Tabs (hinter dem Einband)
            var tabsRoot = New("Tabs", book.transform);
            Stretch(tabsRoot);
            string[] tabNames = { "Spuren", "Verdaechtige", "Notizen" };
            string[] tabIcons = { "icon_magnifier", "icon_person", "icon_pencil" };
            var tabButtons = new Button[3];
            var tabImages = new Image[3];
            var tabRects = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                var tab = New("Tab_" + tabNames[i], tabsRoot.transform);
                var rt = Rt(tab);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(20, -(120 + i * 112));
                rt.sizeDelta = new Vector2(104, 96);
                var bg = Img(tab, i == 0 ? "nb_tab_active" : "nb_tab_inactive", Image.Type.Sliced, raycast: true);
                var button = tab.AddComponent<Button>();
                SetupButton(button, bg);

                var icon = New("Icon", tab.transform);
                var irt = Rt(icon);
                irt.anchorMin = irt.anchorMax = new Vector2(1, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = new Vector2(-62, 0);
                irt.sizeDelta = new Vector2(50, 50);
                Img(icon, tabIcons[i]).preserveAspect = true;

                tabButtons[i] = button;
                tabImages[i] = bg;
                tabRects[i] = rt;
            }

            var cover = New("Cover", book.transform);
            Stretch(cover);
            Img(cover, "nb_cover", Image.Type.Tiled);

            var leftFront = BuildPageStack(book.transform, "LeftPage", 30, gutterOnRight: true);

            // Ringbindung
            var rings = New("Rings", book.transform);
            TL(rings, 710, 58, 120, 810);
            for (int i = 0; i < 14; i++)
            {
                var ring = New("Ring_" + i, rings.transform);
                TL(ring, 0, 20 + i * (770f / 13f) - 12, 120, 24);
                Img(ring, "nb_ring");
            }

            var rightFront = BuildPageStack(book.transform, "RightPage", 792, gutterOnRight: false);

            // Templates (unsichtbar, werden vom NotebookDisplay kopiert)
            var templates = New("Templates", panel.transform);
            Stretch(templates);
            templates.SetActive(false);

            // ---------- Tab "Spuren": linke Seite
            var leftSpuren = New("Content_Spuren", leftFront.transform);
            Stretch(leftSpuren);
            Banner(leftSpuren.transform, "Spuren");
            var subtitle = New("Subtitle", leftSpuren.transform);
            TL(subtitle, 58, 136, 602, 31);
            Txt(subtitle, "Fakten-Protokoll · wird automatisch ausgefüllt", fontBody, 23, InkSoft, TextAlignmentOptions.Center);

            var listContent = BuildClueList(leftSpuren.transform, out var scrollRect);
            var rowTemplate = BuildRow(templates.transform);
            var emptyRowTemplate = New("EmptyRowTemplate", templates.transform);
            Img(emptyRowTemplate, "nb_row_empty", Image.Type.Tiled);
            var emptyLe = emptyRowTemplate.AddComponent<LayoutElement>();
            emptyLe.minHeight = emptyLe.preferredHeight = 90;

            var emptyListHint = New("EmptyListHint", leftSpuren.transform);
            TL(emptyListHint, 58, 330, 568, 90);
            Txt(emptyListHint, "Noch keine Spuren gefunden.\nUntersuche den Tatort mit [E].", fontBody, 28, InkSoft, TextAlignmentOptions.Center, wrap: true);

            // ---------- Tab "Spuren": rechte Seite
            var rightSpuren = New("Content_Spuren", rightFront.transform);
            Stretch(rightSpuren);

            var detailRoot = New("Detail", rightSpuren.transform);
            Stretch(detailRoot);

            var bigFrame = New("BigIcon", detailRoot.transform);
            TL(bigFrame, 58, 44, 156, 161);
            Img(bigFrame, "nb_frame_dark", Image.Type.Sliced);
            var bigIcon = New("Icon", bigFrame.transform);
            TL(bigIcon, 22, 22, 112, 112);
            var detailIcon = Img(bigIcon, "icon_blood");
            detailIcon.preserveAspect = true;

            var title = New("Title", detailRoot.transform);
            TL(title, 242, 48, 418, 64);
            var detailTitle = Txt(title, "Blutspur", fontTitle, 60, Ink, TextAlignmentOptions.TopLeft);
            detailTitle.overflowMode = TextOverflowModes.Ellipsis;

            var description = New("Description", detailRoot.transform);
            TL(description, 242, 118, 418, 100);
            var detailDescription = Txt(description, "Getrocknetes Blut auf dem Küchenboden, direkt neben dem Tisch.", fontBody, 26, Ink, TextAlignmentOptions.TopLeft, wrap: true);

            var detailTrait = FactRow(detailRoot.transform, 0, "Merkmal", "Blutgruppe");
            var detailLocation = FactRow(detailRoot.transform, 1, "Fundort", "Küche");
            var detailTime = FactRow(detailRoot.transform, 2, "Gefunden", "00:42");

            // Stempel
            var stamp = New("Stamp", detailRoot.transform);
            var srt = Rt(stamp);
            srt.anchorMin = srt.anchorMax = new Vector2(0, 1);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(568, -313);
            srt.sizeDelta = new Vector2(160, 150);
            srt.localEulerAngles = new Vector3(0, 0, 9);
            Img(stamp, "nb_stamp");
            var stampValueGo = New("Value", stamp.transform);
            TL(stampValueGo, 0, 16, 160, 88);
            var stampValue = Txt(stampValueGo, "A", fontMono, 84, new Color(Blood.r, Blood.g, Blood.b, 0.9f), TextAlignmentOptions.Center);
            AutoSize(stampValue, 28, 84);
            var stampLabelGo = New("Label", stamp.transform);
            TL(stampLabelGo, 12, 104, 136, 26);
            var stampLabel = Txt(stampLabelGo, "BLUTGRUPPE", fontMono, 17, new Color(Blood.r, Blood.g, Blood.b, 0.9f), TextAlignmentOptions.Center);
            stampLabel.characterSpacing = 8;
            AutoSize(stampLabel, 9, 17);

            var detailEmptyHint = New("DetailEmptyHint", rightSpuren.transform);
            TL(detailEmptyHint, 58, 120, 602, 140);
            Txt(detailEmptyHint, "Hier stehen die Details zur Spur,\ndie du links anklickst.", fontBody, 30, InkSoft, TextAlignmentOptions.Center, wrap: true);
            detailEmptyHint.SetActive(false);

            // Übersicht nach Merkmal
            var squiggle = New("Squiggle", rightSpuren.transform);
            TL(squiggle, 58, 382, 610, 16);
            Img(squiggle, "nb_squiggle");

            var secTitle = New("OverviewTitle", rightSpuren.transform);
            TL(secTitle, 58, 409, 602, 53);
            var secHlg = secTitle.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(secHlg, 14, TextAnchor.LowerLeft);
            Txt(New("Title", secTitle.transform), "Übersicht", fontTitle, 42, Ink, TextAlignmentOptions.BottomLeft);
            var secSmall = Txt(New("Subtitle", secTitle.transform), "alle Spuren nach Merkmal", fontBody, 22, InkSoft, TextAlignmentOptions.BottomLeft);
            secSmall.margin = new Vector4(0, 0, 0, 6);

            var bloodChips = OverviewRow(rightSpuren.transform, 0, "icon_blood", "Blut");
            var shoeChips = OverviewRow(rightSpuren.transform, 1, "icon_shoe", "Schuh");
            var fiberChips = OverviewRow(rightSpuren.transform, 2, "icon_fiber", "Faser");
            var chipTemplate = BuildChip(templates.transform, "ChipTemplate");

            // Notizzettel
            var sticky = New("StickyNote", rightSpuren.transform);
            var strt = Rt(sticky);
            strt.anchorMin = strt.anchorMax = new Vector2(0, 1);
            strt.pivot = new Vector2(0.5f, 0.5f);
            strt.anchoredPosition = new Vector2(210, -766);
            strt.sizeDelta = new Vector2(348, 168);
            strt.localEulerAngles = new Vector3(0, 0, 3);
            Img(sticky, "nb_sticky", Image.Type.Sliced);
            var tape = New("Tape", sticky.transform);
            var trt = Rt(tape);
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0, -24);
            trt.sizeDelta = new Vector2(110, 32);
            trt.localEulerAngles = new Vector3(0, 0, -3);
            Img(tape, "nb_tape");
            var stickyText = New("Text", sticky.transform);
            Stretch(stickyText, 46, 48, 46, 38);
            Txt(stickyText, "<color=#a8322a><b>Merke:</b></color> Pro Merkmal gibt es genau 1 echte Spur!", fontBody, 27, StickyInk, TextAlignmentOptions.TopLeft, wrap: true);

            // ---------- Platzhalter-Seiten für die anderen Tabs
            var leftSuspects = PlaceholderPage(leftFront.transform, "Content_Verdaechtige", "Verdächtige",
                "Hier markierst du später jeden Mitspieler als verdächtig, unklar oder entlastet.");
            var rightSuspects = New("Content_Verdaechtige", rightFront.transform);
            Stretch(rightSuspects);
            var leftNotes = PlaceholderPage(leftFront.transform, "Content_Notizen", "Notizen",
                "Hier schreibst du später eigene Notizen auf, z. B. was du in der Cutscene gesehen hast.");
            var rightNotes = New("Content_Notizen", rightFront.transform);
            Stretch(rightNotes);
            rightSuspects.SetActive(false); leftSuspects.SetActive(false);
            rightNotes.SetActive(false); leftNotes.SetActive(false);

            // ---------- Hinweise unten (für alle Tabs)
            var hints = New("Hints", leftFront.transform);
            TL(hints, 58, 789, 602, 38);
            ConfigureLayout(hints.AddComponent<HorizontalLayoutGroup>(), 30, TextAnchor.MiddleLeft);
            var hintDetails = New("Hint_Details", hints.transform);
            ConfigureLayout(hintDetails.AddComponent<HorizontalLayoutGroup>(), 8, TextAnchor.MiddleLeft);
            Key(hintDetails.transform, "Klick");
            Txt(New("Text", hintDetails.transform), "Details", fontBody, 24, InkSoft, TextAlignmentOptions.Left);
            var hintTabs = New("Hint_Tabs", hints.transform);
            ConfigureLayout(hintTabs.AddComponent<HorizontalLayoutGroup>(), 8, TextAnchor.MiddleLeft);
            Key(hintTabs.transform, "Q");
            Key(hintTabs.transform, "E");
            Txt(New("Text", hintTabs.transform), "Seite wechseln", fontBody, 24, InkSoft, TextAlignmentOptions.Left);

            var close = New("CloseHint", rightFront.transform);
            var crt = Rt(close);
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0);
            crt.pivot = new Vector2(1, 0);
            crt.anchoredPosition = new Vector2(-54, 28);
            crt.sizeDelta = new Vector2(200, 38);
            ConfigureLayout(close.AddComponent<HorizontalLayoutGroup>(), 8, TextAnchor.MiddleRight);
            Key(close.transform, "Tab");
            Txt(New("Text", close.transform), "Schliessen", fontBody, 24, InkSoft, TextAlignmentOptions.Left);

            // ---------- Scripts verknüpfen
            SetRef(display, "listContent", listContent);
            SetRef(display, "rowTemplate", rowTemplate);
            SetRef(display, "emptyRowTemplate", emptyRowTemplate);
            SetRef(display, "emptyListHint", emptyListHint);
            SetRef(display, "scrollRect", scrollRect);
            SetRef(display, "detailRoot", detailRoot);
            SetRef(display, "detailEmptyHint", detailEmptyHint);
            SetRef(display, "detailIcon", detailIcon);
            SetRef(display, "detailTitle", detailTitle);
            SetRef(display, "detailDescription", detailDescription);
            SetRef(display, "detailTrait", detailTrait);
            SetRef(display, "detailLocation", detailLocation);
            SetRef(display, "detailTime", detailTime);
            SetRef(display, "stampValue", stampValue);
            SetRef(display, "stampLabel", stampLabel);
            SetRef(display, "chipTemplate", chipTemplate);
            SetRef(display, "bloodChips", bloodChips);
            SetRef(display, "shoeChips", shoeChips);
            SetRef(display, "fiberChips", fiberChips);
            SetRef(display, "bloodIcon", LoadSprite("icon_blood"));
            SetRef(display, "shoeIcon", LoadSprite("icon_shoe"));
            SetRef(display, "fiberIcon", LoadSprite("icon_fiber"));

            var pages = new[]
            {
                new[] { leftSpuren, rightSpuren },
                new[] { leftSuspects, rightSuspects },
                new[] { leftNotes, rightNotes },
            };
            var so = new SerializedObject(tabs);
            var tabArray = so.FindProperty("tabs");
            tabArray.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var element = tabArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("name").stringValue = tabNames[i];
                element.FindPropertyRelative("button").objectReferenceValue = tabButtons[i];
                element.FindPropertyRelative("background").objectReferenceValue = tabImages[i];
                element.FindPropertyRelative("rect").objectReferenceValue = tabRects[i];
                var pageArray = element.FindPropertyRelative("pages");
                pageArray.arraySize = pages[i].Length;
                for (int p = 0; p < pages[i].Length; p++)
                {
                    pageArray.GetArrayElementAtIndex(p).objectReferenceValue = pages[i][p];
                }
            }
            so.FindProperty("activeSprite").objectReferenceValue = LoadSprite("nb_tab_active");
            so.FindProperty("inactiveSprite").objectReferenceValue = LoadSprite("nb_tab_inactive");
            so.ApplyModifiedPropertiesWithoutUndo();

            return canvasGo;
        }

        private static GameObject BuildPageStack(Transform parent, string name, float x, bool gutterOnRight)
        {
            var stack = New(name, parent);
            TL(stack, x, 30, 718, 860);

            var back2 = New("Back2", stack.transform);
            TL(back2, 0, 12, 718, 860);
            Img(back2, "nb_sheet", Image.Type.Sliced, PageBack);
            var back1 = New("Back1", stack.transform);
            TL(back1, 0, 6, 718, 860);
            Img(back1, "nb_sheet", Image.Type.Sliced, PageEdge);

            var front = New("Front", stack.transform);
            Stretch(front);
            Img(front, "nb_sheet", Image.Type.Sliced, PageColor);

            var noise = New("PaperNoise", front.transform);
            Stretch(noise, 4, 4, 4, 4);
            Img(noise, "nb_noise", Image.Type.Tiled);

            var gutter = New("GutterShadow", front.transform);
            var grt = Rt(gutter);
            grt.anchorMin = new Vector2(gutterOnRight ? 1 : 0, 0);
            grt.anchorMax = new Vector2(gutterOnRight ? 1 : 0, 1);
            grt.pivot = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(40, -8);
            grt.anchoredPosition = new Vector2(gutterOnRight ? -24 : 24, 0);
            grt.localScale = new Vector3(gutterOnRight ? 1 : -1, 1, 1);
            Img(gutter, "nb_gutter");

            return front;
        }

        private static void Banner(Transform parent, string text)
        {
            var banner = New("Banner", parent);
            TL(banner, 169, 44, 380, 86);
            Img(banner, "nb_banner");
            var label = New("Label", banner.transform);
            Stretch(label, 40, 8, 40, 16);
            Txt(label, text, fontTitle, 54, Ink, TextAlignmentOptions.Center);
        }

        private static GameObject PlaceholderPage(Transform parent, string name, string title, string text)
        {
            var page = New(name, parent);
            Stretch(page);
            Banner(page.transform, title);
            var body = New("Text", page.transform);
            TL(body, 58, 200, 602, 200);
            Txt(body, text + "\n\n<size=24><color=#7a5a3c>Kommt in einer späteren Version.</color></size>", fontBody, 30, Ink, TextAlignmentOptions.Top, wrap: true);
            return page;
        }

        private static RectTransform BuildClueList(Transform parent, out ScrollRect scrollRect)
        {
            var root = New("ClueList", parent);
            TL(root, 58, 200, 602, 576);
            scrollRect = root.AddComponent<ScrollRect>();

            var viewport = New("Viewport", root.transform);
            TL(viewport, -6, 0, 580, 576);
            viewport.AddComponent<RectMask2D>();
            Img(viewport, null, Image.Type.Simple, new Color(1, 1, 1, 0), raycast: true);

            var content = New("Content", viewport.transform);
            var crt = Rt(content);
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 0, 8);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Scrollbar
            var bar = New("Scrollbar", root.transform);
            TL(bar, 584, 0, 18, 576);
            Img(bar, "nb_scroll_track", Image.Type.Sliced, null, raycast: true);
            var scrollbar = bar.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var sliding = New("Sliding Area", bar.transform);
            Stretch(sliding);
            var handle = New("Handle", sliding.transform);
            var hrt = Rt(handle);
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = new Vector2(-3, 0);
            hrt.offsetMax = new Vector2(3, 0);
            var handleImg = Img(handle, "nb_scroll_handle", Image.Type.Sliced, null, raycast: true);
            scrollbar.handleRect = hrt;
            scrollbar.targetGraphic = handleImg;
            NoNavigation(scrollbar);

            scrollRect.content = crt;
            scrollRect.viewport = Rt(viewport);
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 25;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            return crt;
        }

        private static NotebookClueRow BuildRow(Transform parent)
        {
            var row = New("ClueRowTemplate", parent);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 90;   // 86 sichtbar + 4 Schatten
            var bg = Img(row, "nb_row", Image.Type.Sliced, raycast: true);
            var button = row.AddComponent<Button>();
            SetupButton(button, bg);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 16, 0, 4);
            ConfigureLayout(hlg, 16, TextAnchor.MiddleLeft);

            var bar = New("SelectionBar", row.transform);
            bar.AddComponent<LayoutElement>().ignoreLayout = true;
            var brt = Rt(bar);
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 0.5f);
            brt.anchoredPosition = new Vector2(-4, 2);
            brt.sizeDelta = new Vector2(8, -32);
            Img(bar, "nb_pill", Image.Type.Sliced, Blood);
            bar.SetActive(false);

            var frame = New("IconFrame", row.transform);
            Fixed(frame, 60, 60);
            var frameImg = Img(frame, "nb_frame", Image.Type.Sliced);
            var icon = New("Icon", frame.transform);
            Stretch(icon, 7, 7, 7, 7);
            var iconImg = Img(icon, "icon_blood");
            iconImg.preserveAspect = true;

            var textBlock = New("Text", row.transform);
            var tle = textBlock.AddComponent<LayoutElement>();
            tle.minWidth = 0;
            tle.preferredWidth = 0;
            tle.flexibleWidth = 1;
            var vlg = textBlock.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(vlg, 2, TextAnchor.MiddleLeft);
            var title = Txt(New("Title", textBlock.transform), "Blutspur", fontBody, 33, Ink, TextAlignmentOptions.Left);
            title.overflowMode = TextOverflowModes.Ellipsis;
            var meta = Txt(New("Meta", textBlock.transform), "Küche · 00:42", fontBody, 21, InkSoft, TextAlignmentOptions.Left);
            meta.overflowMode = TextOverflowModes.Ellipsis;

            var chip = BuildChip(row.transform, "ValueChip");

            var newMarker = New("NewMarker", row.transform);
            newMarker.AddComponent<LayoutElement>().ignoreLayout = true;
            var nrt = Rt(newMarker);
            nrt.anchorMin = nrt.anchorMax = new Vector2(1, 1);
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.anchoredPosition = new Vector2(-176, -8);
            nrt.sizeDelta = new Vector2(64, 44);
            nrt.localEulerAngles = new Vector3(0, 0, 8);
            Txt(newMarker, "neu!", fontTitle, 30, Blood, TextAlignmentOptions.Center);
            newMarker.SetActive(false);

            var rowComp = row.AddComponent<NotebookClueRow>();
            SetRef(rowComp, "button", button);
            SetRef(rowComp, "background", bg);
            SetRef(rowComp, "iconFrame", frameImg);
            SetRef(rowComp, "icon", iconImg);
            SetRef(rowComp, "titleText", title);
            SetRef(rowComp, "metaText", meta);
            SetRef(rowComp, "valueChip", chip);
            SetRef(rowComp, "newMarker", newMarker);
            SetRef(rowComp, "selectionBar", bar);
            SetRef(rowComp, "normalBackground", LoadSprite("nb_row"));
            SetRef(rowComp, "selectedBackground", LoadSprite("nb_row_selected"));
            SetRef(rowComp, "normalFrame", LoadSprite("nb_frame"));
            SetRef(rowComp, "selectedFrame", LoadSprite("nb_frame_dark"));
            return rowComp;
        }

        private static NotebookChip BuildChip(Transform parent, string name)
        {
            var chip = New(name, parent);
            Img(chip, "nb_chip", Image.Type.Sliced);
            var hlg = chip.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 5, 3);
            ConfigureLayout(hlg, 8, TextAnchor.MiddleCenter);

            var dot = New("Dot", chip.transform);
            Fixed(dot, 14, 14);
            var dotImg = Img(dot, "nb_dot");
            dot.SetActive(false);

            var label = Txt(New("Label", chip.transform), "A", fontMono, 23, Ink, TextAlignmentOptions.Center);

            var comp = chip.AddComponent<NotebookChip>();
            SetRef(comp, "label", label);
            SetRef(comp, "colorDot", dotImg);
            return comp;
        }

        private static TextMeshProUGUI FactRow(Transform parent, int index, string label, string value)
        {
            var row = New("Fact_" + label, parent);
            TL(row, 58, 230 + index * 42, 380, 37);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(hlg, 10, TextAnchor.MiddleLeft);
            hlg.childForceExpandHeight = true;

            Txt(New("Label", row.transform), label, fontBody, 27, InkSoft, TextAlignmentOptions.Left);

            var dotsWrap = New("Dots", row.transform);
            var dle = dotsWrap.AddComponent<LayoutElement>();
            dle.minWidth = 20;
            dle.flexibleWidth = 1;
            var dots = New("Line", dotsWrap.transform);
            var drt = Rt(dots);
            drt.anchorMin = new Vector2(0, 0);
            drt.anchorMax = new Vector2(1, 0);
            drt.pivot = new Vector2(0.5f, 0);
            drt.anchoredPosition = new Vector2(0, 8);
            drt.sizeDelta = new Vector2(0, 8);
            Img(dots, "nb_leader", Image.Type.Tiled);

            return Txt(New("Value", row.transform), value, fontBody, 27, Ink, TextAlignmentOptions.Right);
        }

        private static RectTransform OverviewRow(Transform parent, int index, string iconName, string label)
        {
            var row = New("Overview_" + label, parent);
            TL(row, 58, 472 + index * 64, 602, 50);
            ConfigureLayout(row.AddComponent<HorizontalLayoutGroup>(), 14, TextAnchor.MiddleLeft);

            var frame = New("IconFrame", row.transform);
            Fixed(frame, 50, 50);
            Img(frame, "nb_frame", Image.Type.Sliced);
            var icon = New("Icon", frame.transform);
            Stretch(icon, 7, 7, 7, 7);
            Img(icon, iconName).preserveAspect = true;

            var name = Txt(New("Name", row.transform), label, fontBody, 29, Ink, TextAlignmentOptions.Left);
            var nle = name.gameObject.AddComponent<LayoutElement>();
            nle.minWidth = nle.preferredWidth = 90;

            var chips = New("Chips", row.transform);
            var cle = chips.AddComponent<LayoutElement>();
            cle.flexibleWidth = 1;
            cle.minHeight = 40;
            ConfigureLayout(chips.AddComponent<HorizontalLayoutGroup>(), 10, TextAnchor.MiddleLeft);
            return Rt(chips);
        }

        private static void Key(Transform parent, string text)
        {
            var key = New("Key_" + text, parent);
            Img(key, "nb_key", Image.Type.Sliced);
            var hlg = key.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(9, 9, 0, 3);
            ConfigureLayout(hlg, 0, TextAnchor.MiddleCenter);
            var le = key.AddComponent<LayoutElement>();
            le.minWidth = 34;
            le.minHeight = le.preferredHeight = 35;
            Txt(New("Label", key.transform), text, fontMono, 17, Ink, TextAlignmentOptions.Center);
        }

        // ------------------------------------------------------------------ Helfer

        private static GameObject New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Rt(GameObject go) => (RectTransform)go.transform;

        /// <summary>Position wie im Mockup: x/y ab oben links des Eltern-Elements.</summary>
        private static RectTransform TL(GameObject go, float x, float y, float w, float h)
        {
            var rt = Rt(go);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Füllt das Eltern-Element mit Abständen (links, oben, rechts, unten).</summary>
        private static RectTransform Stretch(GameObject go, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            var rt = Rt(go);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        private static void Fixed(GameObject go, float w, float h)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = h;
        }

        private static Image Img(GameObject go, string sprite, Image.Type type = Image.Type.Simple, Color? color = null, bool raycast = false)
        {
            var img = go.AddComponent<Image>();
            img.sprite = LoadSprite(sprite);
            img.type = type;
            img.color = color ?? Color.white;
            img.raycastTarget = raycast;
            if (type == Image.Type.Sliced || type == Image.Type.Tiled) img.fillCenter = true;
            return img;
        }

        private static TextMeshProUGUI Txt(GameObject go, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment, bool wrap = false)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void AutoSize(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        private static void ConfigureLayout(HorizontalOrVerticalLayoutGroup group, float spacing, TextAnchor alignment)
        {
            group.spacing = spacing;
            group.childAlignment = alignment;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
        }

        private static void SetupButton(Button button, Graphic target)
        {
            button.targetGraphic = target;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = new Color(0.97f, 0.97f, 0.97f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.88f, 0.86f, 0.82f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            NoNavigation(button);
        }

        private static void NoNavigation(Selectable selectable)
        {
            var nav = selectable.navigation;
            nav.mode = Navigation.Mode.None;
            selectable.navigation = nav;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Notizbuch] Feld \"{field}\" nicht gefunden auf {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
