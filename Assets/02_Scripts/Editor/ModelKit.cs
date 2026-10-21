using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Bausteine für die Greybox-Modelle (Spuren, Koffer, Sicherungskasten, Villa):
    /// Texturen laden, Materialien anlegen, Quader und flache "Aufkleber" (Decals) bauen.
    /// Texturen kommen aus Tools/TextureGen/generate_clue_textures.py.
    /// </summary>
    public static class ModelKit
    {
        public const string TextureDir = "Assets/08_Models/Textures";
        public const string MaterialDir = "Assets/08_Models/Materials";
        public const string FontDir = "Assets/07_UI/Notebook/Fonts";

        /// <summary>So weit liegen Aufkleber über der Fläche (gegen Flimmern).</summary>
        public const float DecalLift = 0.004f;

        // ------------------------------------------------------------------ Ordner

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ Texturen

        /// <summary>Lädt eine Textur aus 08_Models/Textures und stellt den Import für Aufkleber ein.</summary>
        public static Texture2D Texture(string fileName)
        {
            string path = $"{TextureDir}/{fileName}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[Modelle] Textur fehlt: {path}. Projekt neu laden (Assets > Refresh) oder Tools/TextureGen/generate_clue_textures.py ausführen.");
                return null;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (!importer.mipMapsPreserveCoverage)
            {
                // Bleibt auch von weitem gleich "dicht" (sonst verschwinden Ränder bei Alpha-Clipping)
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Materialien

        /// <summary>Einfarbiges URP-Lit-Material (wird angelegt oder aktualisiert).</summary>
        public static Material Solid(string name, string hex, float smoothness = 0.2f, float metallic = 0f)
        {
            var mat = LoadOrCreate(name, out bool created);
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            Save(mat, created);
            return mat;
        }

        /// <summary>
        /// Material für Aufkleber mit Transparenz (Alpha-Clipping): Blut, Schuhabdrücke, Faser, Kreide.
        /// Bleibt "Lit", damit es im Stromausfall auch dunkel wird.
        /// </summary>
        public static Material Cutout(string name, Texture2D texture, string tintHex = "ffffff", float smoothness = 0.2f)
        {
            var mat = LoadOrCreate(name, out bool created);
            ColorUtility.TryParseHtmlString("#" + tintHex, out var tint);
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", tint);
            mat.SetColor("_Color", tint);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);

            mat.SetFloat("_Surface", 0f);      // Opaque
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            Save(mat, created);
            return mat;
        }

        private static Material LoadOrCreate(string name, out bool created)
        {
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            created = mat == null;
            if (created)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader) { name = name };
            }
            return mat;
        }

        private static void Save(Material mat, bool created)
        {
            if (created) AssetDatabase.CreateAsset(mat, $"{MaterialDir}/{mat.name}.mat");
            else EditorUtility.SetDirty(mat);
        }

        // ------------------------------------------------------------------ Bauteile

        public static GameObject Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Quader aus Mitte + Grösse (lokal zum Parent).</summary>
        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material,
            bool collider = false, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (!castShadows) renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (!collider) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        /// <summary>Zylinder (z. B. Griffe, Knöpfe). height = ganze Höhe entlang der lokalen Y-Achse.</summary>
        public static GameObject Cylinder(string name, Transform parent, Vector3 center, float diameter, float height,
            Material material, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>
        /// Flacher Aufkleber auf der lokalen XZ-Ebene (zeigt nach lokal oben).
        /// width = Breite (lokal X), length = Länge (lokal Z). Kein Collider, kein Schatten.
        /// </summary>
        public static GameObject Decal(string name, Transform parent, Material material, float width, float length,
            Vector3 localPosition = default, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition + Vector3.up * DecalLift;
            // Das Unity-Quad liegt in XY und zeigt nach -Z → 90° um X kippen, dann zeigt es nach oben
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(width, length, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Beschriftung als 3D-Text (TextMeshPro). Zeigt nach lokal -Z (wie eine Vorderseite).</summary>
        public static TextMeshPro Label(string name, Transform parent, string text, Vector3 localPosition, Vector3 euler,
            float fontSize, Color color, Vector2 size, string fontFile = "SpecialElite-Regular SDF")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(euler);
            var tmp = go.AddComponent<TextMeshPro>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/{fontFile}.asset");
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)go.transform).sizeDelta = size;
            return tmp;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        /// <summary>Entfernt Umlaute für Objektnamen in der Hierarchie.</summary>
        public static string Ascii(string s) =>
            s.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Replace(' ', '_');
    }
}
