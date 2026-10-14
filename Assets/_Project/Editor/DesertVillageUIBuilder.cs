using System;
using System.IO;
using System.Linq;
using EvidenceRun.Presentation.HUD;
using EvidenceRun.Presentation.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EvidenceRun.Editor
{
    /// <summary>Authors the main menu and desert mission UI.</summary>
    public static class DesertVillageUIBuilder
    {
        const string MenuPath = "Assets/_Project/Scenes/MainMenu.unity";
        const string VillagePath = "Assets/_Project/Scenes/EvidenceRun_DesertVillage.unity";
        const string ArtPath = "Assets/_Project/Art/UI/DesertVillageBriefing.png";
        static readonly Color Ink = Hex("18211E"), Panel = Hex("222C26"), Border = Hex("495044");
        static readonly Color Sand = Hex("CABB98"), White = Hex("F1EBDD"), Muted = Hex("ABA992");
        static readonly Color Amber = Hex("C49B55"), Red = Hex("B76A5C"), Green = Hex("8C9F73");
        static TMP_FontAsset heading, body;

        [MenuItem("Evidence Run/Desert Village UI/Apply interface design")]
        public static void ApplyFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Apply();
        }

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save modified scenes before applying the UI design.");
            PrepareHeadingFont();
            body = Load<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            PrepareBriefingImage();
            ApplyMenu();
            ApplyHUD();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MenuPath);
            Debug.Log("Desert military UI saved in MainMenu and EvidenceRun_DesertVillage. No player build was run.");
        }

        static void PrepareBriefingImage()
        {
            if (!File.Exists(ArtPath))
            {
                const string preview = "Docs/DesertVillage/overview.png";
                if (!File.Exists(preview)) throw new FileNotFoundException("Capture the village overview before authoring the menu.", preview);
                Directory.CreateDirectory(Path.GetDirectoryName(ArtPath));
                File.Copy(preview, ArtPath);
            }
            AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
        }

        static void PrepareHeadingFont()
        {
            const string path = "Assets/_Project/Art/UI/Desert Oswald SDF.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) == null &&
                !AssetDatabase.CopyAsset("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Oswald Bold SDF.asset", path))
                throw new InvalidOperationException("Could not copy the existing heading font.");
            heading = Load<TMP_FontAsset>(path);
            // Bake the small ASCII set once in the editor instead of growing a dynamic atlas during play.
            string characters = new string(Enumerable.Range(32,95).Select(i => (char)i).ToArray());
            if (!heading.HasCharacters(characters))
            {
                heading.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                var fontData = new SerializedObject(heading);
                fontData.FindProperty("m_SourceFontFile").objectReferenceValue = Load<Font>("Assets/TextMesh Pro/Examples & Extras/Fonts/Oswald-Bold.ttf");
                fontData.ApplyModifiedPropertiesWithoutUndo();
                if (!heading.TryAddCharacters(characters, out string missing))
                    throw new InvalidOperationException("Heading font lacks characters: " + missing);
            }
            heading.atlasPopulationMode = AtlasPopulationMode.Static;
            var data = new SerializedObject(heading);
            var clear = data.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) { clear.boolValue = false; data.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(heading);
        }

        static void ApplyMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(MenuPath);
            Canvas canvas = One<Canvas>(scene);
            var controllers = All<MainMenuController>(scene).ToArray();
            // Retain the controller when replacing its UI hierarchy.
            var controller = controllers.FirstOrDefault(c => new SerializedObject(c).FindProperty("gameplaySceneName").stringValue == "EvidenceRun")
                ?? controllers.FirstOrDefault() ?? throw new InvalidOperationException("No existing menu controller.");
            controller.transform.SetParent(null, false);
            foreach (var extra in controllers.Where(c => c != controller)) Object.DestroyImmediate(extra);
            foreach (Transform child in canvas.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in controller.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            controller.transform.SetParent(canvas.transform, false);
            Set(controller, "gameplaySceneName", "EvidenceRun_DesertVillage");
            ConfigureCanvas(canvas);
            var root = Full("Mission briefing", canvas.transform);
            FullImage("Background", root, Ink);
            Plate("Top rule", root, 64, 52, 1472, 1, Border);
            Text("Briefing identifier", root, "ER  /  OPERATIONS", 64, 64, 650, 26, 17, Sand, true, 2);
            Text("Mission type", root, "FIELD BRIEF     /     DESERT VILLAGE", 1010, 64, 526, 26, 16, Muted, false, 1, TextAlignmentOptions.Right);
            Text("Title", root, "EVIDENCE RUN", 60, 139, 590, 134, 88, White, true);
            Text("Subtitle", root, "DESERT INFILTRATION", 66, 282, 530, 40, 25, Sand, true, 4);
            Plate("Title divider", root, 66, 338, 54, 3, Sand);
            Text("Brief heading", root, "MISSION BRIEF", 66, 357, 500, 28, 16, Muted, false, 2);
            Text("Briefing", root, "Enter the hostile village. Retrieve the evidence from the records courtyard and reach extraction without being caught.", 66, 400, 514, 126, 25, White);
            Text("Tactical note", root, "Use cover. Break sightlines. Throw distractions.", 66, 532, 525, 48, 19, Muted);
            Button("Start Mission", root, "START MISSION", 64, 600, 520, 72, Sand, Ink, controller.StartMission, true);
            Text("Start note", root, "INSERTION POINT  /  SOUTHERN PERIMETER", 66, 686, 560, 24, 14, Muted, false, 1);

            var imageFrame = Plate("Village field image", root, 656, 152, 880, 520, Border);
            var picture = Rect("Static village overview", imageFrame.transform, 2, 2, 876, 516).gameObject.AddComponent<RawImage>();
            picture.texture = Load<Texture2D>(ArtPath);
            picture.color = new Color(.82f, .84f, .77f, 1);
            picture.uvRect = new Rect(0, .04f, 1, .93f);
            picture.raycastTarget = false;
            Plate("Image caption backing", imageFrame.transform, 2, 452, 876, 66, Ink);
            Text("Image caption", imageFrame.transform, "AREA OF OPERATIONS", 24, 470, 540, 28, 18, White, true, 2);
            Text("Image index", imageFrame.transform, "01 / VILLAGE", 628, 474, 224, 24, 14, Sand, false, 1, TextAlignmentOptions.Right);
            Plate("Photo corner top", root, 646, 142, 35, 3, Sand);
            Plate("Photo corner left", root, 646, 142, 3, 35, Sand);
            Text("Route caption", root, "INFILTRATE    /    RETRIEVE    /    EXTRACT", 658, 689, 878, 30, 17, Muted, false, 2);

            Plate("Controls separator", root, 64, 744, 1472, 1, Border);
            Text("Controls heading", root, "FIELD CONTROLS", 64, 765, 220, 26, 15, Sand, true, 2);
            string[] keys = { "WASD", "MOUSE", "LEFT SHIFT", "LEFT CTRL", "SPACE", "G" };
            string[] labels = { "Move", "Hold LMB + drag to look", "Run", "Crouch", "Jump", "Throw distraction" };
            float[] widths = { 165, 330, 210, 210, 165, 320 };
            float x = 64;
            for (int i = 0; i < keys.Length; i++)
            {
                Text("Key " + keys[i], root, keys[i], x, 806, widths[i] - 12, 27, 18, White, true, 1);
                Text("Action " + i, root, labels[i], x, 841, widths[i] - 12, 25, 17, Muted);
                x += widths[i];
            }
            EditorSceneManager.SaveScene(scene);
        }

        static void ApplyHUD()
        {
            Scene scene = EditorSceneManager.OpenScene(VillagePath);
            var hud = One<GameHUD>(scene);
            var canvas = hud.GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Existing HUD has no canvas.");
            hud.transform.SetParent(null, false);
            foreach (Transform child in canvas.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in hud.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            hud.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)hud.transform);
            ConfigureCanvas(canvas);
            var gameplay = Full("Gameplay HUD", hud.transform);
            var objective = Plate("Objective panel", gameplay, 32, 32, 456, 124, Panel);
            Plate("Objective accent", objective.transform, 0, 0, 4, 124, Sand);
            var objectiveLabel = Text("Objective stage", objective.transform, "OBJECTIVE 01", 24, 17, 400, 24, 15, Sand, true, 2);
            var objectiveText = Text("Objective", objective.transform, "Locate the evidence", 24, 46, 411, 48, 29, White, true);
            Text("Mission label", objective.transform, "DESERT VILLAGE  /  EVIDENCE RUN", 24, 97, 405, 19, 12, Muted, false, 1);
            var alert = Plate("Guard alert", gameplay, 0, 0, 308, 82, Panel);
            Corner(alert.rectTransform, new Vector2(1,1), new Vector2(-32,-32), new Vector2(308,82));
            var accent = Plate("Alert accent", alert.transform, 0, 0, 4, 82, Amber);
            Text("Alert category", alert.transform, "GUARD ACTIVITY", 23, 14, 266, 21, 13, Muted, false, 2);
            var alertText = Text("Alert status", alert.transform, "SUSPICIOUS", 23, 37, 266, 40, 24, Amber, true, 1);
            alert.gameObject.SetActive(false);
            var hint = Plate("Controls hint", gameplay, 0, 0, 1032, 38, new Color(Ink.r,Ink.g,Ink.b,.94f));
            Corner(hint.rectTransform, new Vector2(0,0), new Vector2(32,24), new Vector2(1032,38));
            Text("Controls", hint.transform, "WASD  Move     SHIFT  Run     CTRL  Crouch     SPACE  Jump     G  Distraction     Hold LMB + drag  Look", 14, 8, 1006, 23, 16, White);
            var feedback = Plate("Evidence feedback", gameplay, 0, 0, 490, 58, Panel);
            Corner(feedback.rectTransform, new Vector2(.5f,0), new Vector2(0,84), new Vector2(490,58));
            Plate("Evidence accent", feedback.transform, 0, 0, 4, 58, Green);
            var feedbackText = Text("Evidence message", feedback.transform, "EVIDENCE SECURED", 24, 13, 446, 36, 22, Green, true, 1);
            feedback.gameObject.SetActive(false);

            GameObject win = ResultPanel(hud.transform, true, hud);
            GameObject lose = ResultPanel(hud.transform, false, hud);
            Set(hud, "objectiveText", objectiveText);
            Set(hud, "objectiveLabel", objectiveLabel);
            Set(hud, "gameplayHud", gameplay.gameObject);
            Set(hud, "winPanel", win);
            Set(hud, "losePanel", lose);
            Set(hud, "alertIndicator", alert.gameObject);
            Set(hud, "alertText", alertText);
            Set(hud, "alertAccent", accent);
            Set(hud, "evidenceFeedback", feedback.gameObject);
            Set(hud, "evidenceFeedbackText", feedbackText);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static GameObject ResultPanel(Transform parent, bool won, GameHUD hud)
        {
            Color accent = won ? Green : Red;
            var overlay = Full(won ? "Mission complete overlay" : "Mission failed overlay", parent);
            var scrim = FullImage("Input blocking backdrop", overlay, new Color(.045f,.064f,.054f,.83f));
            scrim.raycastTarget = true;
            var card = Plate("Mission report", overlay, 0, 0, 704, 484, Panel);
            Corner(card.rectTransform, new Vector2(.5f,.5f), Vector2.zero, new Vector2(704,484));
            Plate("Outcome accent", card.transform, 0, 0, 704, 5, accent);
            Text("Report type", card.transform, "FIELD REPORT  /  " + (won ? "EXTRACTION" : "CONTACT"), 40, 39, 624, 27, 17, accent, true, 2);
            Text("Outcome heading", card.transform, won ? "MISSION COMPLETE" : "MISSION FAILED", 36, 99, 638, 108, 67, White, true);
            Text("Outcome detail", card.transform, won ? "Evidence secured.\nExtraction complete." : "You were caught by a guard.\nReassess the route and try again.", 40, 207, 624, 78, 25, White);
            Plate("Report separator", card.transform, 40, 317, 624, 1, Border);
            Button(won ? "Restart Mission" : "Retry Mission", card.transform, won ? "RESTART MISSION" : "RETRY MISSION", 40, 350, 356, 66, accent, Ink, hud.RestartMission, true);
            Button("Main Menu", card.transform, "MAIN MENU", 412, 350, 252, 66, Border, White, hud.ReturnToMainMenu, false);
            Text("Report footer", card.transform, "EVIDENCE RUN     /     DESERT INFILTRATION", 40, 441, 624, 22, 13, Muted, false, 1);
            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        static Button Button(string name, Transform parent, string label, float x, float y, float w, float h, Color fill, Color text, UnityAction action, bool primary)
        {
            var image = Plate(name, parent, x, y, w, h, fill);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock {
                normalColor = Color.white, highlightedColor = new Color(1.15f,1.15f,1.12f),
                pressedColor = new Color(.71f,.72f,.68f), selectedColor = new Color(1.1f,1.1f,1.06f),
                disabledColor = new Color(.48f,.49f,.46f,.66f), colorMultiplier = 1, fadeDuration = .09f
            };
            var nav = button.navigation; nav.mode = Navigation.Mode.Automatic; button.navigation = nav;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            Text("Label", image.transform, label, 20, 10, w - (primary ? 74 : 40), h - 20, primary ? 28 : 24, text, true, 1, TextAlignmentOptions.MidlineLeft);
            if (primary) Text("Arrow", image.transform, ">", w-49, 10, 28, h-20, 30, text, true, 0, TextAlignmentOptions.Center);
            return button;
        }

        static void ConfigureCanvas(Canvas canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
        static RectTransform Full(string name, Transform parent) { var rt = Rect(name,parent,0,0,0,0); Stretch(rt); return rt; }
        static void Stretch(RectTransform rt) { rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,.5f);rt.offsetMin=rt.offsetMax=Vector2.zero;rt.localScale=Vector3.one; }
        static Image FullImage(string name,Transform parent,Color color) { var image=Full(name,parent).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image; }
        static Image Plate(string name,Transform parent,float x,float y,float w,float h,Color color) { var image=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image; }
        static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();rt.gameObject.layer=5;rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        static void Corner(RectTransform rt,Vector2 anchor,Vector2 pos,Vector2 size) {rt.anchorMin=rt.anchorMax=rt.pivot=anchor;rt.anchoredPosition=pos;rt.sizeDelta=size;}
        static TextMeshProUGUI Text(string name,Transform parent,string value,float x,float y,float w,float h,float size,Color color,bool condensed=false,float tracking=0,TextAlignmentOptions align=TextAlignmentOptions.TopLeft)
        {
            var text=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font=condensed?heading:body;text.text=value;text.fontSize=size;text.color=color;text.characterSpacing=tracking;text.alignment=align;
            text.raycastTarget=false;text.enableAutoSizing=false;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;return text;
        }
        static Color Hex(string value) {ColorUtility.TryParseHtmlString("#"+value,out Color color);return color;}
        static T Load<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing UI asset: "+path);
        static System.Collections.Generic.IEnumerable<T> All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true));
        static T One<T>(Scene scene) where T:Component => All<T>(scene).Single();
        static void Set(Object target,string field,Object value) {var so=new SerializedObject(target);var p=so.FindProperty(field)??throw new InvalidOperationException("Missing field "+field);p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        static void Set(Object target,string field,string value) {var so=new SerializedObject(target);so.FindProperty(field).stringValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
