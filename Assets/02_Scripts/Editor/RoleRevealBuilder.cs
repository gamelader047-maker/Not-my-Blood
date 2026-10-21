using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NotMyBlood.UI;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Tools > Not My Blood > Rollen-Karte erstellen
    /// Baut ein eigenes "RoleCanvas" mit der Rollen-Karte (Ermittler / Mörder) für die Cutscene.
    /// Eigenes Canvas, damit es beim Neubau der Runden-UI nicht verloren geht.
    /// </summary>
    public static class RoleRevealBuilder
    {
        private const string CanvasName = "RoleCanvas";
        private const string SpriteDir = "Assets/07_UI/Notebook/Sprites";
        private const string FontDir = "Assets/07_UI/Notebook/Fonts";

        private static readonly Color Ink = ModelKit.Hex("3a2416");
        private static readonly Color InkSoft = ModelKit.Hex("7a5a3c");
        private static readonly Color PageColor = ModelKit.Hex("f5e9cc");
        private static readonly Color Blood = ModelKit.Hex("a8322a");

        [MenuItem("Tools/Not My Blood/Rollen-Karte erstellen")]
        public static void Build()
        {
            var fontBody = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/PatrickHand-Regular SDF.asset");
            var fontTitle = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/CaveatBrush-Regular SDF.asset");
            var fontMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/SpecialElite-Regular SDF.asset");
            if (fontBody == null || fontTitle == null || fontMono == null)
            {
                EditorUtility.DisplayDialog("Rollen-Karte", "Die Notizbuch-Schriften fehlen. Erst \"Notizbuch-UI erstellen\" ausführen.", "OK");
                return;
            }

            var existing = GameObject.Find(CanvasName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;   // über HUD (10) und Runden-UI (40), unter Notizbuch (50) und Menü (60)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Karte (blockiert keine Maus, man kann sich während der Cutscene frei umschauen)
            const float w = 1100f, h = 430f;
            var card = New("RoleCard", canvasGo.transform);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 90f);
            rt.sizeDelta = new Vector2(w, h);
            Img(card, "nb_sheet", Image.Type.Sliced, PageColor);
            Img(Stretch(New("PaperNoise", card.transform), 4, 4, 4, 4), "nb_noise", Image.Type.Tiled, Color.white);

            var accent = Img(TL(New("Accent", card.transform), 40, 24, w - 80, 8), null, Image.Type.Simple, Blood);

            var role = Txt(TL(New("Role", card.transform), 0, 44, w, 104), "Du bist Ermittler", fontTitle, 88, Ink, TextAlignmentOptions.Center);
            var goal = Txt(TL(New("Goal", card.transform), 90, 156, w - 180, 132), RoleRevealUI.InvestigatorGoal, fontBody, 33, Ink,
                TextAlignmentOptions.Top, wrap: true);
            goal.enableAutoSizing = true;
            goal.fontSizeMin = 22;
            goal.fontSizeMax = 33;
            var traits = Txt(TL(New("Traits", card.transform), 40, 300, w - 80, 44),
                "Deine Merkmale:   Blut <b>A</b>   ·   Schuh <b>Kreise</b>   ·   Kleidung <b>Rot</b>", fontMono, 24, InkSoft, TextAlignmentOptions.Center);
            var hint = Txt(TL(New("Hint", card.transform), 40, 358, w - 80, 40),
                "Die Ermittlung beginnt in <b>20</b> s", fontBody, 25, InkSoft, TextAlignmentOptions.Center);

            card.SetActive(false);

            var ui = canvasGo.AddComponent<RoleRevealUI>();
            SetRef(ui, "root", card);
            SetRef(ui, "roleText", role);
            SetRef(ui, "goalText", goal);
            SetRef(ui, "traitsText", traits);
            SetRef(ui, "hintText", hint);
            SetRef(ui, "accent", accent);

            Undo.RegisterCreatedObjectUndo(canvasGo, "Rollen-Karte erstellen");
            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Debug.Log("[Rollen-Karte] RoleCanvas erstellt – erscheint in der Cutscene jeder Runde. Szene speichern!");
        }

        // ------------------------------------------------------------------ Helfer (wie in den anderen UI-Buildern)

        private static GameObject New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject TL(GameObject go, float x, float y, float width, float height)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
            return go;
        }

        private static GameObject Stretch(GameObject go, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return go;
        }

        private static Image Img(GameObject go, string sprite, Image.Type type, Color color)
        {
            var img = go.AddComponent<Image>();
            if (!string.IsNullOrEmpty(sprite)) img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{sprite}.png");
            img.type = type;
            img.color = color;
            img.raycastTarget = false;
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

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Rollen-Karte] Feld \"{field}\" nicht gefunden auf {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
