using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using NotMyBlood.Core;
using NotMyBlood.Player;
using NotMyBlood.UI;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Tools > Not My Blood > Verdächtige + Abstimmung UI erstellen  (Schritt 4 + 5)
    /// - hängt VoteManager, MeetingController und PlayerKnowledge an den GameManager
    /// - baut im Notizbuch die Seite "Verdächtige" (Karten pro Spieler, Status-Stempel, Werkzeuge)
    /// - baut ein "RoundCanvas" mit Abstimmungsfenster und Auflösung
    /// Wichtig: Wird die Notizbuch-UI später neu erstellt, diesen Menüpunkt danach nochmals ausführen.
    /// </summary>
    public static class RoundUIBuilder
    {
        private const string SpriteDir = "Assets/07_UI/Notebook/Sprites";
        private const string FontDir = "Assets/07_UI/Notebook/Fonts";

        private static readonly Color Ink = Hex("3a2416");
        private static readonly Color InkSoft = Hex("7a5a3c");
        private static readonly Color PageColor = Hex("f5e9cc");
        private static readonly Color StickyInk = Hex("4a3414");

        private static TMP_FontAsset fontBody, fontTitle, fontMono;

        [MenuItem("Tools/Not My Blood/Verdächtige + Abstimmung UI erstellen")]
        public static void Build()
        {
            if (!LoadFonts())
            {
                EditorUtility.DisplayDialog("Runden-UI", "Die Notizbuch-Schriften fehlen. Erst \"Notizbuch-UI erstellen\" ausführen.", "OK");
                return;
            }

            EnsureManagers();
            EnsureEventSystem();
            bool notebookOk = BuildSuspectsPage();
            BuildRoundCanvas();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Runden-UI] Fertig: Verdächtige-Seite " + (notebookOk ? "im Notizbuch" : "ÜBERSPRUNGEN (kein Notizbuch)") +
                      ", Abstimmung + Auflösung, Manager am GameManager. Szene speichern!");
        }

        // ------------------------------------------------------------------ Manager

        private static void EnsureManagers()
        {
            var rounds = Object.FindObjectsByType<GameRoundManager>(FindObjectsInactive.Include);
            GameObject host;
            if (rounds.Length > 0)
            {
                host = rounds[0].gameObject;
            }
            else
            {
                host = new GameObject("GameManager", typeof(GameRoundManager));
                Undo.RegisterCreatedObjectUndo(host, "GameManager erstellen");
                Debug.Log("[Runden-UI] Kein GameRoundManager gefunden – \"GameManager\" erstellt.");
            }

            if (host.GetComponent<VoteManager>() == null) Undo.AddComponent<VoteManager>(host);
            if (host.GetComponent<MeetingController>() == null) Undo.AddComponent<MeetingController>(host);
            if (host.GetComponent<PlayerKnowledge>() == null) Undo.AddComponent<PlayerKnowledge>(host);
        }

        /// <summary>Wird vom Notizbuch-Builder aufgerufen, damit die Seite "Verdächtige" nach einem Neuaufbau nicht fehlt.</summary>
        public static void RebuildSuspectsPage()
        {
            if (LoadFonts()) BuildSuspectsPage();
        }

        private static bool LoadFonts()
        {
            fontBody = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/PatrickHand-Regular SDF.asset");
            fontTitle = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/CaveatBrush-Regular SDF.asset");
            fontMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/SpecialElite-Regular SDF.asset");
            return fontBody != null && fontTitle != null && fontMono != null;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length > 0) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem erstellen");
        }

        // ------------------------------------------------------------------ Notizbuch: Verdächtige

        private static bool BuildSuspectsPage()
        {
            var canvas = GameObject.Find("NotebookCanvas");
            Transform panel = canvas != null ? canvas.transform.Find("NotebookPanel") : null;
            Transform left = panel != null ? panel.Find("Book/LeftPage/Front/Content_Verdaechtige") : null;
            Transform right = panel != null ? panel.Find("Book/RightPage/Front/Content_Verdaechtige") : null;
            Transform templates = panel != null ? panel.Find("Templates") : null;
            if (left == null || right == null || templates == null)
            {
                Debug.LogWarning("[Runden-UI] Notizbuch nicht gefunden – erst \"Notizbuch-UI erstellen\" ausführen.");
                return false;
            }

            ClearChildren(left);
            ClearChildren(right);
            var oldTemplate = templates.Find("SuspectRowTemplate");
            if (oldTemplate != null) Undo.DestroyObjectImmediate(oldTemplate.gameObject);

            // Linke Seite
            var banner = New("Banner", left);
            TL(banner, 169, 44, 380, 86);
            Img(banner, "nb_banner");
            Txt(Stretch(New("Label", banner.transform), 40, 8, 40, 16), "Verdächtige", fontTitle, 54, Ink, TextAlignmentOptions.Center);

            var subtitle = New("Subtitle", left);
            TL(subtitle, 58, 136, 602, 31);
            Txt(subtitle, "Was du über die anderen herausgefunden hast", fontBody, 23, InkSoft, TextAlignmentOptions.Center);

            var list = New("SuspectList", left);
            TL(list, 58, 190, 602, 580);
            var vlg = list.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var rowTemplate = BuildSuspectRow(templates);

            // Rechte Seite
            var title = New("Title", right);
            TL(title, 58, 44, 602, 64);
            Txt(title, "Werkzeuge", fontTitle, 56, Ink, TextAlignmentOptions.TopLeft);

            var needles = New("Needles", right);
            TL(needles, 58, 124, 602, 40);
            var needlesText = Txt(needles, "Nadeltests übrig: <b>2</b> von 2", fontBody, 30, Ink, TextAlignmentOptions.Left);

            var info = New("HowTo", right);
            TL(info, 58, 186, 602, 300);
            Txt(info,
                "<b>E</b> auf einen Mitspieler: <b>Nadeltest</b> → Blutgruppe.\n<size=80%><color=#7a5a3c>2 pro Runde. Er merkt, dass du es warst!</color></size>\n\n" +
                "<b>Shift</b> (ducken) + <b>E halten</b>: <b>Foto vom Schuh</b> → Schuhprofil.\n<size=80%><color=#7a5a3c>Unbegrenzt, aber du musst nah ran.</color></size>\n\n" +
                "<b>Kleidung</b> siehst du einfach.\n\n" +
                "Klick auf einen <b>Stempel</b>: unklar → verdächtig → entlastet.",
                fontBody, 26, Ink, TextAlignmentOptions.TopLeft, wrap: true);

            var squiggle = New("Squiggle", right);
            TL(squiggle, 58, 520, 610, 16);
            Img(squiggle, "nb_squiggle");

            var sticky = New("StickyNote", right);
            var srt = Rt(sticky);
            srt.anchorMin = srt.anchorMax = new Vector2(0, 1);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(210, -766);
            srt.sizeDelta = new Vector2(348, 168);
            srt.localEulerAngles = new Vector3(0, 0, 3);
            Img(sticky, "nb_sticky", Image.Type.Sliced);
            var tape = New("Tape", sticky.transform);
            var trt = Rt(tape);
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0, -24);
            trt.sizeDelta = new Vector2(110, 32);
            trt.localEulerAngles = new Vector3(0, 0, -3);
            Img(tape, "nb_tape");
            Txt(Stretch(New("Text", sticky.transform), 46, 48, 46, 38),
                "<color=#a8322a><b>Merke:</b></color> Wer zu allen echten Spuren passt, ist dein Mörder.",
                fontBody, 26, StickyInk, TextAlignmentOptions.TopLeft, wrap: true);

            // Script am NotebookPanel verknüpfen
            var page = panel.GetComponent<SuspectsPageUI>();
            if (page == null) page = Undo.AddComponent<SuspectsPageUI>(panel.gameObject);
            SetRef(page, "listContent", list.transform);
            SetRef(page, "rowTemplate", rowTemplate);
            SetRef(page, "needlesText", needlesText);
            return true;
        }

        private static SuspectRow BuildSuspectRow(Transform templates)
        {
            var row = New("SuspectRowTemplate", templates);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 88;
            Img(row, "nb_row", Image.Type.Sliced);

            var name = New("Name", row.transform);
            TL(name, 18, 8, 330, 36);
            var nameText = Txt(name, "Dummy 2", fontBody, 30, Ink, TextAlignmentOptions.Left);
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            var chips = New("Chips", row.transform);
            TL(chips, 14, 46, 420, 34);
            var hlg = chips.AddComponent<HorizontalLayoutGroup>();
            Configure(hlg, 8, TextAnchor.MiddleLeft);
            var blood = BuildChip(chips.transform, "BloodChip", "Blut ?");
            var shoe = BuildChip(chips.transform, "ShoeChip", "Schuh ?");
            var color = BuildChip(chips.transform, "ColorChip", "Rot");

            var stamp = New("StatusStamp", row.transform);
            var st = Rt(stamp);
            st.anchorMin = st.anchorMax = new Vector2(1, 0.5f);
            st.pivot = new Vector2(1, 0.5f);
            st.anchoredPosition = new Vector2(-16, 2);
            st.sizeDelta = new Vector2(150, 44);
            st.localEulerAngles = new Vector3(0, 0, 4);
            var stampImg = Img(stamp, "nb_chip", Image.Type.Sliced, null, raycast: true);
            var button = stamp.AddComponent<Button>();
            SetupButton(button, stampImg);
            var stampText = Txt(Stretch(New("Label", stamp.transform)), "UNKLAR", fontMono, 18, Ink, TextAlignmentOptions.Center);
            stampText.characterSpacing = 4;

            var comp = row.AddComponent<SuspectRow>();
            SetRef(comp, "nameText", nameText);
            SetRef(comp, "bloodChip", blood);
            SetRef(comp, "shoeChip", shoe);
            SetRef(comp, "colorChip", color);
            SetRef(comp, "statusButton", button);
            SetRef(comp, "statusImage", stampImg);
            SetRef(comp, "statusText", stampText);
            return comp;
        }

        private static NotebookChip BuildChip(Transform parent, string name, string text)
        {
            var chip = New(name, parent);
            Img(chip, "nb_chip", Image.Type.Sliced);
            var hlg = chip.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 4, 2);
            Configure(hlg, 6, TextAnchor.MiddleCenter);

            var dot = New("Dot", chip.transform);
            Fixed(dot, 14, 14);
            var dotImg = Img(dot, "nb_dot");
            dot.SetActive(false);

            var label = Txt(New("Label", chip.transform), text, fontMono, 19, Ink, TextAlignmentOptions.Center);

            var comp = chip.AddComponent<NotebookChip>();
            SetRef(comp, "label", label);
            SetRef(comp, "colorDot", dotImg);
            return comp;
        }

        // ------------------------------------------------------------------ Abstimmung + Auflösung

        private static void BuildRoundCanvas()
        {
            var existing = GameObject.Find("RoundCanvas");
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var canvasGo = new GameObject("RoundCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;   // über dem HUD (10), unter dem Notizbuch (50)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var templates = New("Templates", canvasGo.transform);
            templates.SetActive(false);

            // --- Abstimmung
            var voting = Stretch(New("VotingPanel", canvasGo.transform));
            Img(Stretch(New("Dim", voting.transform)), null, Image.Type.Simple, new Color(0f, 0f, 0f, 0.55f), raycast: true);
            var card = Card(voting.transform, 900, 740);
            var vTitle = Txt(TL(New("Title", card.transform), 0, 34, 900, 74), "Abstimmung", fontTitle, 64, Ink, TextAlignmentOptions.Center);
            var vSub = Txt(TL(New("Subtitle", card.transform), 0, 112, 900, 36), "Wer ist der Mörder? Geheime Wahl · 00:30", fontBody, 26, InkSoft, TextAlignmentOptions.Center);
            var vList = TL(New("Candidates", card.transform), 70, 165, 760, 490);
            var vlg = vList.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var vStatus = Txt(TL(New("Status", card.transform), 0, 672, 900, 40), "Klick auf den Spieler, den du für den Mörder hältst.", fontBody, 24, InkSoft, TextAlignmentOptions.Center);
            var buttonTemplate = BuildCandidateButton(templates.transform);
            voting.SetActive(false);

            var votingUi = canvasGo.AddComponent<VotingUI>();
            SetRef(votingUi, "root", voting);
            SetRef(votingUi, "titleText", vTitle);
            SetRef(votingUi, "subtitleText", vSub);
            SetRef(votingUi, "statusText", vStatus);
            SetRef(votingUi, "listContent", vList.transform);
            SetRef(votingUi, "buttonTemplate", buttonTemplate);

            // --- Auflösung
            var resolution = Stretch(New("ResolutionPanel", canvasGo.transform));
            Img(Stretch(New("Dim", resolution.transform)), null, Image.Type.Simple, new Color(0f, 0f, 0f, 0.6f), raycast: true);
            var rCard = Card(resolution.transform, 1000, 880);
            var rTitle = Txt(TL(New("Title", rCard.transform), 0, 40, 1000, 84), "Die Ermittler gewinnen", fontTitle, 72, Ink, TextAlignmentOptions.Center);
            var rBody = Txt(TL(New("Body", rCard.transform), 90, 145, 820, 600), "…", fontBody, 27, Ink, TextAlignmentOptions.TopLeft, wrap: true);
            rBody.overflowMode = TextOverflowModes.Ellipsis;
            rBody.enableAutoSizing = true;
            rBody.fontSizeMin = 18;
            rBody.fontSizeMax = 27;

            var buttonGo = New("NewRoundButton", rCard.transform);
            var brt = Rt(buttonGo);
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.anchoredPosition = new Vector2(0, 40);
            brt.sizeDelta = new Vector2(340, 74);
            var bImg = Img(buttonGo, "nb_row", Image.Type.Sliced, null, raycast: true);
            var newRound = buttonGo.AddComponent<Button>();
            SetupButton(newRound, bImg);
            Txt(Stretch(New("Label", buttonGo.transform), 0, 0, 0, 4), "Neue Runde   [R]", fontBody, 32, Ink, TextAlignmentOptions.Center);
            resolution.SetActive(false);

            var resolutionUi = canvasGo.AddComponent<ResolutionUI>();
            SetRef(resolutionUi, "root", resolution);
            SetRef(resolutionUi, "titleText", rTitle);
            SetRef(resolutionUi, "bodyText", rBody);
            SetRef(resolutionUi, "newRoundButton", newRound);

            Undo.RegisterCreatedObjectUndo(canvasGo, "Runden-UI erstellen");
        }

        private static VoteCandidateButton BuildCandidateButton(Transform parent)
        {
            var root = New("CandidateButtonTemplate", parent);
            var le = root.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 88;
            var bg = Img(root, "nb_row", Image.Type.Sliced, null, raycast: true);
            var button = root.AddComponent<Button>();
            SetupButton(button, bg);

            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(22, 22, 0, 4);
            Configure(hlg, 16, TextAnchor.MiddleLeft);

            var dot = New("Dot", root.transform);
            Fixed(dot, 22, 22);
            var dotImg = Img(dot, "nb_dot");

            var name = Txt(New("Name", root.transform), "Dummy 2", fontBody, 34, Ink, TextAlignmentOptions.Left);
            var nle = name.gameObject.AddComponent<LayoutElement>();
            nle.minWidth = 220;

            var detail = Txt(New("Detail", root.transform), "Rot · Blut ? · Schuh ?", fontMono, 18, InkSoft, TextAlignmentOptions.Right);
            var dle = detail.gameObject.AddComponent<LayoutElement>();
            dle.flexibleWidth = 1;

            var comp = root.AddComponent<VoteCandidateButton>();
            SetRef(comp, "button", button);
            SetRef(comp, "background", bg);
            SetRef(comp, "colorDot", dotImg);
            SetRef(comp, "nameText", name);
            SetRef(comp, "detailText", detail);
            SetRef(comp, "normalSprite", LoadSprite("nb_row"));
            SetRef(comp, "selectedSprite", LoadSprite("nb_row_selected"));
            return comp;
        }

        /// <summary>Papier-Karte in der Bildmitte (gleicher Look wie das Notizbuch).</summary>
        private static GameObject Card(Transform parent, float w, float h)
        {
            var card = New("Card", parent);
            var rt = Rt(card);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
            Img(card, "nb_sheet", Image.Type.Sliced, PageColor, raycast: true);
            Img(Stretch(New("PaperNoise", card.transform), 4, 4, 4, 4), "nb_noise", Image.Type.Tiled);
            return card;
        }

        // ------------------------------------------------------------------ Helfer

        private static GameObject New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Rt(GameObject go) => (RectTransform)go.transform;

        private static GameObject TL(GameObject go, float x, float y, float w, float h)
        {
            var rt = Rt(go);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        private static GameObject Stretch(GameObject go, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            var rt = Rt(go);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return go;
        }

        private static void Fixed(GameObject go, float w, float h)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = h;
        }

        private static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");

        private static Image Img(GameObject go, string sprite, Image.Type type = Image.Type.Simple, Color? color = null, bool raycast = false)
        {
            var img = go.AddComponent<Image>();
            if (!string.IsNullOrEmpty(sprite)) img.sprite = LoadSprite(sprite);
            img.type = type;
            img.color = color ?? Color.white;
            img.raycastTarget = raycast;
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
            t.raycastTarget = false;
            return t;
        }

        private static void Configure(HorizontalOrVerticalLayoutGroup group, float spacing, TextAnchor alignment)
        {
            group.spacing = spacing;
            group.childAlignment = alignment;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
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
            colors.disabledColor = new Color(0.9f, 0.9f, 0.9f, 0.9f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Runden-UI] Feld \"{field}\" nicht gefunden auf {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
