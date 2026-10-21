using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using NotMyBlood.Core;
using NotMyBlood.Map;
using NotMyBlood.Network;
using NotMyBlood.Player;
using NotMyBlood.UI;

namespace NotMyBlood.EditorTools
{
    /// <summary>
    /// Tools > Not My Blood > Multiplayer einrichten (Etappe 1)
    /// - macht aus dem Szenen-Player ein Netzwerk-Prefab (mit Körper, Visier und Namensschild)
    /// - legt NetworkManager, SessionManager, Lobby-Kamera und das Hauptmenü an
    /// - schaltet den alten Szenen-Player aus (bleibt als Vorlage in der Szene)
    /// Kann mehrfach ausgeführt werden.
    /// </summary>
    public static class NetworkSetupBuilder
    {
        private const string PrefabDir = "Assets/03_Prefabs/Network";
        private const string PrefabPath = PrefabDir + "/NetworkPlayer.prefab";
        private const string VisorMaterialPath = "Assets/04_Materials/Network_Visor.mat";
        private const string SpriteDir = "Assets/07_UI/Notebook/Sprites";
        private const string FontDir = "Assets/07_UI/Notebook/Fonts";
        private const string HudShadowPath = "Assets/07_UI/HUD/PatrickHand HUD Schatten.mat";
        private const string OfflinePlayerName = "Player (Offline-Vorlage, aus)";

        private static readonly Color Ink = Hex("3a2416");
        private static readonly Color InkSoft = Hex("7a5a3c");
        private static readonly Color PageColor = Hex("f5e9cc");
        private static readonly Color Blood = Hex("a8322a");

        private static TMP_FontAsset fontBody, fontTitle, fontMono;
        private static Material hudShadow;

        [MenuItem("Tools/Not My Blood/Multiplayer einrichten (Etappe 1)")]
        public static void Build()
        {
            fontBody = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/PatrickHand-Regular SDF.asset");
            fontTitle = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/CaveatBrush-Regular SDF.asset");
            fontMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/SpecialElite-Regular SDF.asset");
            hudShadow = AssetDatabase.LoadAssetAtPath<Material>(HudShadowPath);
            if (fontBody == null || fontTitle == null || fontMono == null)
            {
                EditorUtility.DisplayDialog("Multiplayer", "Die Notizbuch-Schriften fehlen. Erst \"Notizbuch-UI erstellen\" ausführen.", "OK");
                return;
            }

            var scenePlayer = FindScenePlayer();
            if (scenePlayer == null)
            {
                EditorUtility.DisplayDialog("Multiplayer", "Kein Player mit PlayerController in der Szene gefunden.", "OK");
                return;
            }

            var prefab = BuildPlayerPrefab(scenePlayer);
            if (prefab == null) return;

            BuildNetworkManager(prefab);
            var session = EnsureObject<SessionManager>("Session");
            var lobbyCamera = BuildLobbyCamera();
            BuildLobbyCanvas(lobbyCamera);
            EnsureEventSystem();

            // Alter Szenen-Player: aus, bleibt aber als Vorlage liegen
            Undo.RecordObject(scenePlayer, "Player ausschalten");
            scenePlayer.name = OfflinePlayerName;
            scenePlayer.SetActive(false);

            // Runde startet nicht mehr automatisch, sondern über das Menü (Solo) bzw. später durch den Host
            foreach (var round in Object.FindObjectsByType<GameRoundManager>(FindObjectsInactive.Include))
            {
                SetBool(round, "startOnPlay", false);
            }

            ClearOldHudReferences();
            EnsureSceneInBuild();
            ApplyPlayerSettings();

            Selection.activeGameObject = session.gameObject;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[Multiplayer] Eingerichtet: NetworkPlayer-Prefab, NetworkManager, Session, Hauptmenü. Szene speichern (Cmd+S) und Play drücken!");
        }

        // ------------------------------------------------------------------ Spieler-Prefab

        private static GameObject FindScenePlayer()
        {
            foreach (var controller in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include))
            {
                if (PrefabUtility.IsPartOfPrefabAsset(controller)) continue;
                if (controller.GetComponent<OnlinePlayer>() != null) continue;
                return controller.gameObject;
            }
            return null;
        }

        private static GameObject BuildPlayerPrefab(GameObject scenePlayer)
        {
            EnsureFolder(PrefabDir);

            var copy = Object.Instantiate(scenePlayer);
            copy.name = "NetworkPlayer";
            copy.SetActive(true);

            try
            {
                // Körper (Kapsel ohne eigenen Collider – der CharacterController ist schon der Collider)
                var oldBody = copy.transform.Find("Body");
                if (oldBody != null) Object.DestroyImmediate(oldBody.gameObject);
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(copy.transform, false);
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
                body.transform.localScale = Vector3.one;

                // Visier vorne, damit man sieht, wohin jemand schaut
                var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visor.name = "Visor";
                Object.DestroyImmediate(visor.GetComponent<Collider>());
                visor.transform.SetParent(body.transform, false);
                visor.transform.localPosition = new Vector3(0f, 0.55f, 0.42f);
                visor.transform.localScale = new Vector3(0.7f, 0.16f, 0.2f);
                visor.GetComponent<Renderer>().sharedMaterial = VisorMaterial();

                // Namensschild über dem Kopf
                var tag = new GameObject("NameTag", typeof(RectTransform));
                tag.transform.SetParent(copy.transform, false);
                tag.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                var tagText = tag.AddComponent<TextMeshPro>();
                tagText.font = fontBody;
                if (hudShadow != null) tagText.fontSharedMaterial = hudShadow;
                tagText.text = "Spieler";
                tagText.fontSize = 2.2f;
                tagText.alignment = TextAlignmentOptions.Center;
                tagText.textWrappingMode = TextWrappingModes.NoWrap;
                tagText.color = Color.white;
                ((RectTransform)tag.transform).sizeDelta = new Vector2(3f, 0.6f);

                // Netzwerk
                if (copy.GetComponent<NetworkObject>() == null) copy.AddComponent<NetworkObject>();
                var nt = copy.GetComponent<NetworkTransform>();
                if (nt == null) nt = copy.AddComponent<NetworkTransform>();
                nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                nt.SyncRotAngleX = false;
                nt.SyncRotAngleZ = false;
                nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
                var online = copy.GetComponent<OnlinePlayer>();
                if (online == null) online = copy.AddComponent<OnlinePlayer>();

                var controller = copy.GetComponent<PlayerController>();
                var interaction = copy.GetComponent<PlayerInteraction>();
                var identity = copy.GetComponent<PlayerIdentity>();
                if (identity == null) identity = copy.AddComponent<PlayerIdentity>();
                var cam = copy.GetComponentInChildren<Camera>(true);
                var listener = copy.GetComponentInChildren<AudioListener>(true);

                SetRef(online, "controller", controller);
                SetRef(online, "interaction", interaction);
                SetRef(online, "playerCamera", cam);
                SetRef(online, "audioListener", listener);
                SetRef(online, "body", body.transform);
                SetRef(online, "nameTag", tag.transform);
                SetRef(online, "nameTagText", tagText);
                var so = new SerializedObject(online);
                var renderers = so.FindProperty("bodyRenderers");
                renderers.arraySize = 2;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<Renderer>();
                renderers.GetArrayElementAtIndex(1).objectReferenceValue = visor.GetComponent<Renderer>();
                so.ApplyModifiedPropertiesWithoutUndo();

                SetRef(identity, "playerRenderer", body.GetComponent<Renderer>());
                var idSo = new SerializedObject(identity);
                idSo.FindProperty("playerName").stringValue = "Spieler";
                idSo.ApplyModifiedPropertiesWithoutUndo();

                // Steuerung, Kamera und Ohren nur beim eigenen Spieler (schaltet OnlinePlayer ein)
                if (controller != null) controller.enabled = false;
                if (interaction != null) interaction.enabled = false;
                if (cam != null) cam.enabled = false;
                if (listener != null) listener.enabled = false;

                var prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath, out bool ok);
                if (!ok || prefab == null)
                {
                    Debug.LogError("[Multiplayer] Prefab konnte nicht gespeichert werden.");
                    return null;
                }
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        private static Material VisorMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(VisorMaterialPath);
            if (mat != null) return mat;
            EnsureFolder(Path.GetDirectoryName(VisorMaterialPath).Replace('\\', '/'));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Network_Visor" };
            var dark = new Color(0.08f, 0.09f, 0.11f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", dark);
            mat.color = dark;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
            AssetDatabase.CreateAsset(mat, VisorMaterialPath);
            return mat;
        }

        // ------------------------------------------------------------------ NetworkManager

        private static void BuildNetworkManager(GameObject playerPrefab)
        {
            var nm = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (nm == null)
            {
                var go = new GameObject("NetworkManager");
                Undo.RegisterCreatedObjectUndo(go, "NetworkManager erstellen");
                nm = go.AddComponent<NetworkManager>();
            }
            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null) transport = nm.gameObject.AddComponent<UnityTransport>();

            Undo.RecordObject(nm, "NetworkManager einstellen");
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.NetworkConfig.ConnectionApproval = false;

            // Standard-Prefab-Liste (wird von Netcode automatisch gepflegt) eintragen
            var defaultList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (defaultList != null && !nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(defaultList))
            {
                nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(defaultList);
            }
            EditorUtility.SetDirty(nm);
        }

        private static T EnsureObject<T>(string name) where T : Component
        {
            var existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name + " erstellen");
            return go.AddComponent<T>();
        }

        // ------------------------------------------------------------------ Lobby-Kamera

        private static Camera BuildLobbyCamera()
        {
            var existing = GameObject.Find("LobbyCamera");
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var marker in Object.FindObjectsByType<MapMarker>(FindObjectsInactive.Exclude))
            {
                center += marker.transform.position;
                count++;
            }
            if (count > 0) center /= count;

            var go = new GameObject("LobbyCamera", typeof(Camera), typeof(AudioListener));
            Undo.RegisterCreatedObjectUndo(go, "Lobby-Kamera erstellen");
            go.transform.position = center + new Vector3(-9f, 12f, -11f);
            go.transform.LookAt(center);
            var cam = go.GetComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.06f);
            return cam;
        }

        // ------------------------------------------------------------------ Hauptmenü

        private static void BuildLobbyCanvas(Camera lobbyCamera)
        {
            var existing = GameObject.Find("LobbyCanvas");
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var canvasGo = new GameObject("LobbyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;   // über allem (Notizbuch = 50)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // --- Hauptmenü
            var menu = Stretch(New("MenuPanel", canvasGo.transform));
            Img(Stretch(New("Dim", menu.transform)), null, Image.Type.Simple, new Color(0.04f, 0.03f, 0.04f, 0.55f), raycast: true);
            var card = Card(menu.transform, 900, 840);

            Txt(TL(New("Title", card.transform), 0, 34, 900, 120), "Not My Blood", fontTitle, 104, Blood, TextAlignmentOptions.Center);
            Txt(TL(New("Subtitle", card.transform), 0, 150, 900, 34), "Social Deduction · Forensik", fontBody, 28, InkSoft, TextAlignmentOptions.Center);

            Txt(TL(New("NameLabel", card.transform), 90, 214, 720, 34), "Dein Name", fontBody, 27, InkSoft, TextAlignmentOptions.Left);
            var nameInput = InputField(card.transform, "NameInput", "z. B. Lukas", fontBody, 32, 90, 250, 720, 72);

            var host = MakeButton(card.transform, "HostButton", "Spiel hosten", 90, 346, 720, 84, 38, bold: true);

            Txt(TL(New("JoinLabel", card.transform), 90, 452, 720, 34), "oder mit Code beitreten", fontBody, 27, InkSoft, TextAlignmentOptions.Left);
            var codeInput = InputField(card.transform, "CodeInput", "CODE", fontMono, 34, 90, 488, 470, 78);
            var join = MakeButton(card.transform, "JoinButton", "Beitreten", 580, 488, 230, 78, 34, bold: false);

            var solo = MakeButton(card.transform, "SoloButton", "Solo testen (mit Dummies)", 90, 596, 720, 64, 28, bold: false);

            var status = Txt(TL(New("Status", card.transform), 90, 676, 720, 90), "", fontBody, 25, InkSoft, TextAlignmentOptions.Top, wrap: true);
            var quit = MakeButton(card.transform, "QuitButton", "Beenden", 690, 776, 150, 46, 24, bold: false);

            // --- Code-Anzeige oben links (im Spiel)
            var info = New("SessionInfo", canvasGo.transform);
            var irt = Rt(info);
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0f, 1f);
            irt.anchoredPosition = new Vector2(36f, -30f);
            irt.sizeDelta = new Vector2(900f, 44f);
            var infoText = Txt(info, "Code: <b>ABC123</b>   ·   3 Spieler   ·   [Esc] Menü", fontBody, 28, Color.white, TextAlignmentOptions.Left);
            if (hudShadow != null) infoText.fontSharedMaterial = hudShadow;
            info.SetActive(false);

            // --- Pause
            var pause = Stretch(New("PausePanel", canvasGo.transform));
            Img(Stretch(New("Dim", pause.transform)), null, Image.Type.Simple, new Color(0.04f, 0.03f, 0.04f, 0.55f), raycast: true);
            var pCard = Card(pause.transform, 720, 660);
            Txt(TL(New("Title", pCard.transform), 0, 34, 720, 84), "Pause", fontTitle, 76, Ink, TextAlignmentOptions.Center);
            var pCode = Txt(TL(New("Code", pCard.transform), 0, 128, 720, 56), "Code: <b>ABC123</b>", fontMono, 40, Blood, TextAlignmentOptions.Center);
            Txt(TL(New("Hint", pCard.transform), 0, 186, 720, 32), "Gib diesen Code deinen Mitspielern.", fontBody, 25, InkSoft, TextAlignmentOptions.Center);
            var pPlayers = Txt(TL(New("Players", pCard.transform), 110, 236, 500, 210), "Im Spiel:", fontBody, 28, Ink, TextAlignmentOptions.TopLeft, wrap: true);
            pPlayers.enableAutoSizing = true;
            pPlayers.fontSizeMin = 18;
            pPlayers.fontSizeMax = 28;
            var resume = MakeButton(pCard.transform, "ResumeButton", "Weiter   [Esc]", 110, 466, 500, 70, 32, bold: true);
            var leave = MakeButton(pCard.transform, "LeaveButton", "Spiel verlassen", 110, 552, 500, 64, 28, bold: false);
            pause.SetActive(false);

            // --- Script
            var ui = canvasGo.AddComponent<LobbyUI>();
            SetRef(ui, "menuPanel", menu);
            SetRef(ui, "nameInput", nameInput);
            SetRef(ui, "codeInput", codeInput);
            SetRef(ui, "hostButton", host);
            SetRef(ui, "joinButton", join);
            SetRef(ui, "soloButton", solo);
            SetRef(ui, "quitButton", quit);
            SetRef(ui, "statusText", status);
            SetRef(ui, "sessionInfo", info);
            SetRef(ui, "sessionInfoText", infoText);
            SetRef(ui, "pausePanel", pause);
            SetRef(ui, "pauseCodeText", pCode);
            SetRef(ui, "pausePlayersText", pPlayers);
            SetRef(ui, "resumeButton", resume);
            SetRef(ui, "leaveButton", leave);
            SetRef(ui, "lobbyCamera", lobbyCamera);

            Undo.RegisterCreatedObjectUndo(canvasGo, "Hauptmenü erstellen");
        }

        private static TMP_InputField InputField(Transform parent, string name, string placeholder, TMP_FontAsset font, float size,
            float x, float y, float w, float h)
        {
            var root = TL(New(name, parent), x, y, w, h);
            var bg = Img(root, "nb_row", Image.Type.Sliced, null, raycast: true);

            var area = Stretch(New("Text Area", root.transform), 22, 6, 22, 10);
            area.AddComponent<RectMask2D>();
            var ph = Txt(Stretch(New("Placeholder", area.transform)), placeholder, font, size, new Color(InkSoft.r, InkSoft.g, InkSoft.b, 0.55f), TextAlignmentOptions.Left);
            ph.fontStyle = FontStyles.Italic;
            var text = Txt(Stretch(New("Text", area.transform)), "", font, size, Ink, TextAlignmentOptions.Left);

            var field = root.AddComponent<TMP_InputField>();
            field.textViewport = Rt(area);
            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = bg;
            field.fontAsset = font;
            field.pointSize = size;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.customCaretColor = true;
            field.caretColor = Ink;
            field.caretWidth = 3;
            field.selectionColor = new Color(Blood.r, Blood.g, Blood.b, 0.3f);
            var nav = field.navigation;
            nav.mode = Navigation.Mode.None;
            field.navigation = nav;
            return field;
        }

        private static Button MakeButton(Transform parent, string name, string label, float x, float y, float w, float h, float size, bool bold)
        {
            var go = TL(New(name, parent), x, y, w, h);
            var img = Img(go, "nb_row", Image.Type.Sliced, null, raycast: true);
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = new Color(0.97f, 0.97f, 0.97f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.86f, 0.84f, 0.8f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;

            var text = Txt(Stretch(New("Label", go.transform), 0, 0, 0, 4), bold ? $"<b>{label}</b>" : label, fontBody, size,
                bold ? Blood : Ink, TextAlignmentOptions.Center);
            text.raycastTarget = false;
            return button;
        }

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

        // ------------------------------------------------------------------ Szene / Projekt

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length > 0) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem erstellen");
        }

        /// <summary>Das HUD zeigte auf den alten Szenen-Player – leeren, damit es den eigenen Netzwerk-Spieler sucht.</summary>
        private static void ClearOldHudReferences()
        {
            foreach (var ui in Object.FindObjectsByType<InteractionPromptUI>(FindObjectsInactive.Include)) SetRef(ui, "player", null);
            foreach (var ui in Object.FindObjectsByType<InteractionHoldUI>(FindObjectsInactive.Include)) SetRef(ui, "player", null);
            foreach (var ui in Object.FindObjectsByType<ControlHintsUI>(FindObjectsInactive.Include)) SetRef(ui, "player", null);
        }

        private static void EnsureSceneInBuild()
        {
            string path = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(path)) return;
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Not My Blood";
            PlayerSettings.runInBackground = true;          // sonst friert das Spiel im Hintergrund ein und verliert die Verbindung
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
        }

        // ------------------------------------------------------------------ Helfer

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

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

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Multiplayer] Feld \"{field}\" nicht gefunden auf {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.boolValue = value;
            so.ApplyModifiedProperties();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
