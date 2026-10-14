#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EvidenceRun.Bootstrap;
using EvidenceRun.Core;
using EvidenceRun.Gameplay.Actors;
using EvidenceRun.Gameplay.Guards;
using EvidenceRun.Gameplay.Player;
using EvidenceRun.Gameplay.Session;
using EvidenceRun.Presentation.HUD;
using EvidenceRun.Presentation.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EvidenceRun.Editor
{
    /// <summary>
    /// Editor-only checks of the authored UI and its existing gameplay connections.
    /// Drives visible Button pointer handlers, relocates the player for objective checks,
    /// and uses actual noise/sight/guard capture. Never builds a player or saves a scene.
    /// </summary>
    [InitializeOnLoad]
    public static class DesertVillageUIValidation
    {
        private const string MenuPath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string SceneName = "EvidenceRun_DesertVillage";
        private const string ActiveKey = "EvidenceRun.UIValidation.Active";
        private const string StageKey = "EvidenceRun.UIValidation.Stage";
        private const string Report = "Logs/desert-ui-playmode-validation.txt";
        private const string ArtifactRoot = "Docs/DesertVillage/UI";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private enum Stage
        {
            Menu, Initial, Suspicion, Pickup, FeedbackExpiry, Extraction, Restart,
            Chase, Retry, SecondPickup, SecondExtraction, WinMenu,
            LastInitial, LastChase, LoseMenu, Finish
        }

        private static GameBootstrap bootstrap;
        private static GameSession session;
        private static GameHUD hud;
        private static PlayerController player;
        private static ActorMotor motor;
        private static Rigidbody body;
        private static Guard[] guards;
        private static Guard chaseGuard;
        private static Vector3 spawn;
        private static bool entered;
        private static bool finishing;
        private static bool sawChase;
        private static bool sawSuspicion;
        private static bool capturedChase;
        private static int failures;
        private static int warnings;
        private static int screenshots;
        private static double started;
        private static float lastRuntimeTime;

        static DesertVillageUIValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Application.logMessageReceived += CaptureLog;
        }

        [MenuItem("Evidence Run/Desert Village UI/Run Editor Play Mode Validation")]
        public static void RunPlayModeChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(ActiveKey, false))
                throw new InvalidOperationException("Exit the current Play Mode run before starting UI validation.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the open scenes before UI validation; this harness never saves or discards scene edits.");

            Directory.CreateDirectory("Logs");
            Directory.CreateDirectory(ArtifactRoot);
            File.WriteAllText(Report, "DESERT VILLAGE UI — UNITY EDITOR PLAY MODE VALIDATION\n" + DateTime.UtcNow.ToString("u") +
                "\nMETHOD: visible authored Button pointer-click handlers with UI raycast checks; actual ObjectiveSystem after player relocation; actual NoiseBus, sight and guard catch. No player build.\n" +
                "RENDER METHOD: actual live Canvas and camera rendered to 1280x720, 1600x900 and 1920x1080 RenderTextures. Canvas mode/camera settings are temporarily changed and restored; no scene is saved. This is UI layout/render verification, not browser profiling or a hardware mouse test.\n");
            EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
            SessionState.SetInt(StageKey, (int)Stage.Menu);
            SessionState.SetBool(ActiveKey, true);
            failures = warnings = screenshots = 0;
            entered = finishing = false;
            EditorApplication.isPlaying = true;
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                entered = false;
                lastRuntimeTime = -1f;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if ((Stage)SessionState.GetInt(StageKey, 0) != Stage.Finish)
                    Append("INCOMPLETE: Play Mode exited before the UI validation finished.");
                SessionState.SetBool(ActiveKey, false);
            }
        }

        private static void Update()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || EditorApplication.isPaused || finishing) return;
            if (lastRuntimeTime == Time.unscaledTime) return;
            lastRuntimeTime = Time.unscaledTime;
            try
            {
                Stage stage = (Stage)SessionState.GetInt(StageKey, 0);
                if (!entered)
                {
                    entered = true;
                    started = EditorApplication.timeSinceStartup;
                    Append("STAGE " + stage);
                    Enter(stage);
                }
                Tick(stage, EditorApplication.timeSinceStartup - started);
            }
            catch (Exception ex)
            {
                Check(false, "Validation exception: " + ex);
                Complete();
            }
        }

        private static void Enter(Stage stage)
        {
            switch (stage)
            {
                case Stage.Suspicion:
                    chaseGuard = guards[0];
                    NoiseEvent noise = new NoiseEvent(chaseGuard.transform.position + chaseGuard.transform.forward * 1.5f, 8f, player.gameObject);
                    bootstrap.NoiseBus.Emit(in noise);
                    break;
                case Stage.Pickup:
                case Stage.SecondPickup:
                    MovePlayer(Read<Transform>(bootstrap, "evidenceTarget").position);
                    break;
                case Stage.FeedbackExpiry:
                    // This staging position is outside both objectives and away from patrol lanes.
                    MovePlayer(spawn + Vector3.right * 6f);
                    break;
                case Stage.Extraction:
                case Stage.SecondExtraction:
                    MovePlayer(Read<Transform>(bootstrap, "extractionTarget").position);
                    break;
                case Stage.Restart:
                    ClickCallback(hud, "RestartMission", Read<GameObject>(hud, "winPanel"));
                    break;
                case Stage.Retry:
                    ClickCallback(hud, "RestartMission", Read<GameObject>(hud, "losePanel"));
                    break;
                case Stage.WinMenu:
                    ClickCallback(hud, "ReturnToMainMenu", Read<GameObject>(hud, "winPanel"));
                    break;
                case Stage.LoseMenu:
                    ClickCallback(hud, "ReturnToMainMenu", Read<GameObject>(hud, "losePanel"));
                    break;
                case Stage.Chase:
                case Stage.LastChase:
                    BeginChase();
                    break;
            }
        }

        private static void Tick(Stage stage, double elapsed)
        {
            if (elapsed > 35) throw new TimeoutException("UI validation stage: " + stage);
            switch (stage)
            {
                case Stage.Menu:
                    if (elapsed < 1) return;
                    ValidateMenu();
                    CaptureSet("menu", true);
                    ClickMenuStart();
                    Next(Stage.Initial);
                    break;
                case Stage.Initial:
                    if (!ReadyScene(elapsed)) return;
                    Bind();
                    ValidateInitial("Start Mission");
                    CaptureSet("hud-initial", true);
                    Next(Stage.Suspicion);
                    break;
                case Stage.Suspicion:
                    if (elapsed < .3) return;
                    Check(GuardState(chaseGuard) == (int)GuardStateId.Suspicious, "Actual NoiseBus stimulus produces a Suspicious guard.");
                    CheckAlert(false);
                    hud.Initialize(session, bootstrap.GameEvents);
                    hud.Tick();
                    CheckAlert(false);
                    Check(Read<int>(hud, "_suspiciousGuardCount") == guards.Count(g => GuardState(g) == (int)GuardStateId.Suspicious), "Repeated Initialize with the same session preserves current counts without duplicate subscriptions.");
                    CaptureSet("hud-suspicious", false);
                    Next(Stage.Pickup);
                    break;
                case Stage.Pickup:
                    if (elapsed < .2) return;
                    ValidatePickup();
                    CaptureSet("hud-evidence", false);
                    Next(Stage.FeedbackExpiry);
                    break;
                case Stage.FeedbackExpiry:
                    if (elapsed < Read<float>(hud, "feedbackDurationSeconds") + .2f) return;
                    Check(!Read<GameObject>(hud, "evidenceFeedback").activeSelf, "Evidence feedback expires after its configured brief duration.");
                    Check(session.State == GameSessionState.Playing, "Feedback expiry leaves the mission playing and does not change game-state rules.");
                    Next(Stage.Extraction);
                    break;
                case Stage.Extraction:
                    if (elapsed < .2) return;
                    ValidateOutcome(true);
                    CaptureSet("mission-complete", true);
                    Next(Stage.Restart);
                    break;
                case Stage.Restart:
                    if (!ReadyScene(elapsed)) return;
                    Bind();
                    ValidateInitial("Mission Complete / Restart Mission");
                    Next(Stage.Chase);
                    break;
                case Stage.Chase:
                case Stage.LastChase:
                    sawChase |= guards.Any(g => GuardState(g) == (int)GuardStateId.Chase);
                    sawSuspicion |= guards.Any(g => GuardState(g) == (int)GuardStateId.Suspicious);
                    if (sawChase && !capturedChase && session.State == GameSessionState.Playing && Read<TextMeshProUGUI>(hud, "alertText").text == "GUARD PURSUIT")
                    {
                        capturedChase = true;
                        CheckAlert(true);
                        if (stage == Stage.Chase) CaptureSet("hud-pursuit", false);
                    }
                    if (session.State != GameSessionState.Lost && elapsed < 16) return;
                    Check(sawSuspicion && sawChase && capturedChase, "Actual sight detection advances Suspicious → Chase and presents pursuit before catch.");
                    ValidateOutcome(false);
                    if (stage == Stage.Chase)
                    {
                        CaptureSet("mission-failed", true);
                        Next(Stage.Retry);
                    }
                    else Next(Stage.LoseMenu);
                    break;
                case Stage.Retry:
                    if (!ReadyScene(elapsed)) return;
                    Bind();
                    ValidateInitial("Mission Failed / Retry Mission");
                    Next(Stage.SecondPickup);
                    break;
                case Stage.SecondPickup:
                    if (elapsed < .15) return;
                    Check(session.HasEvidence, "Retry allows a fresh evidence pickup through ObjectiveSystem.");
                    Next(Stage.SecondExtraction);
                    break;
                case Stage.SecondExtraction:
                    if (elapsed < .15) return;
                    ValidateOutcome(true);
                    Next(Stage.WinMenu);
                    break;
                case Stage.WinMenu:
                    if (!ReadyMenu(elapsed)) return;
                    ValidateMenu();
                    Check(true, "Mission Complete / Main Menu loads MainMenu and removes gameplay objects.");
                    ClickMenuStart();
                    Next(Stage.LastInitial);
                    break;
                case Stage.LastInitial:
                    if (!ReadyScene(elapsed)) return;
                    Bind();
                    ValidateInitial("Main Menu relaunch");
                    Next(Stage.LastChase);
                    break;
                case Stage.LoseMenu:
                    if (!ReadyMenu(elapsed)) return;
                    ValidateMenu();
                    Check(true, "Mission Failed / Main Menu loads MainMenu and removes gameplay objects.");
                    Complete();
                    break;
            }
        }

        private static bool ReadyScene(double elapsed)
        {
            return SceneManager.GetActiveScene().name == SceneName && elapsed > .9;
        }

        private static bool ReadyMenu(double elapsed)
        {
            return SceneManager.GetActiveScene().name == "MainMenu" && elapsed > .6;
        }

        private static void Bind()
        {
            Scene scene = SceneManager.GetActiveScene();
            bootstrap = InScene<GameBootstrap>(scene).Single();
            hud = Read<GameHUD>(bootstrap, "gameHUD");
            session = Read<GameSession>(bootstrap, "_gameSession");
            player = Read<PlayerController>(bootstrap, "playerController");
            motor = Read<ActorMotor>(player, "actorMotor");
            body = player.GetComponent<Rigidbody>();
            guards = Read<Guard[]>(bootstrap, "guards");
            spawn = player.transform.position;
            // Deterministic test positioning. ActorMotor/physics/guards remain live.
            player.enabled = false;
            motor.SetIntent(default);
        }

        private static void ValidateMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Check(scene.name == "MainMenu", "MainMenu is active.");
            MainMenuController[] menus = InScene<MainMenuController>(scene);
            Check(menus.Length == 1 && Read<string>(menus[0], "gameplaySceneName") == SceneName, "One MainMenuController targets the desert mission.");
            Check(InScene<GameBootstrap>(scene).Length == 0 && InScene<GameHUD>(scene).Length == 0 && InScene<GameLoop>(scene).Length == 0, "Menu has no active gameplay bootstrap, loop or HUD.");
            Check(InScene<EventSystem>(scene).Length == 1, "Menu has exactly one EventSystem.");
            string text = string.Join("\n", InScene<TextMeshProUGUI>(scene).Where(t => t.gameObject.activeInHierarchy).Select(t => t.text));
            Check(text.Contains("EVIDENCE RUN") && text.Contains("DESERT INFILTRATION"), "Menu renders the requested title and subtitle.");
            Check(new[] { "WASD", "Shift", "Ctrl", "Space", "G" }.All(control => text.IndexOf(control, StringComparison.OrdinalIgnoreCase) >= 0), "Menu contains the supported movement, run, crouch, jump and distraction controls.");
            Check(text.IndexOf("drag", StringComparison.OrdinalIgnoreCase) >= 0, "Menu explains the existing mouse-drag camera input honestly.");
            ValidateCursor("menu");
        }

        private static void ValidateInitial(string trigger)
        {
            Scene scene = SceneManager.GetActiveScene();
            Check(session.State == GameSessionState.Playing && !session.HasEvidence, trigger + " enters a fresh Playing session without evidence.");
            Check(InScene<GameBootstrap>(scene).Length == 1 && InScene<GameHUD>(scene).Length == 1 && InScene<GameLoop>(scene).Length == 1 && InScene<EventSystem>(scene).Length == 1 && InScene<PlayerController>(scene).Length == 1,
                trigger + " creates one bootstrap, HUD, loop, EventSystem and player.");
            Check(Read<Transform>(bootstrap, "evidenceTarget").gameObject.activeSelf, trigger + " restores the evidence object.");
            Check(Read<TextMeshProUGUI>(hud, "objectiveLabel").text == "OBJECTIVE 01" && Read<TextMeshProUGUI>(hud, "objectiveText").text == "Locate the evidence", trigger + " restores initial objective label and text.");
            Check(Read<GameObject>(hud, "gameplayHud").activeSelf && !Read<GameObject>(hud, "winPanel").activeSelf && !Read<GameObject>(hud, "losePanel").activeSelf, trigger + " shows HUD and deactivates both result overlays.");
            Check(!Read<GameObject>(hud, "alertIndicator").activeSelf && !Read<GameObject>(hud, "evidenceFeedback").activeSelf, trigger + " clears stale alerts and pickup feedback.");
            Check(!Read<GameObject>(hud, "winPanel").GetComponentsInChildren<Graphic>(false).Any(g => g.isActiveAndEnabled && g.raycastTarget) &&
                !Read<GameObject>(hud, "losePanel").GetComponentsInChildren<Graphic>(false).Any(g => g.isActiveAndEnabled && g.raycastTarget), "Hidden result overlays contain no active raycast targets.");
            ValidateCursor("gameplay (existing LMB-drag camera)");
        }

        private static void ValidatePickup()
        {
            Check(session.HasEvidence && !Read<Transform>(bootstrap, "evidenceTarget").gameObject.activeSelf, "Actual ObjectiveSystem pickup sets session evidence and hides its visual.");
            Check(Read<TextMeshProUGUI>(hud, "objectiveLabel").text == "OBJECTIVE 02" && Read<TextMeshProUGUI>(hud, "objectiveText").text == "Proceed to extraction", "EvidencePicked updates the objective label and action.");
            Check(Read<GameObject>(hud, "evidenceFeedback").activeSelf && Read<TextMeshProUGUI>(hud, "evidenceFeedbackText").text == "Evidence secured. Proceed to extraction.", "EvidencePicked displays the brief evidence-secured status.");
        }

        private static void CheckAlert(bool chase)
        {
            TextMeshProUGUI label = Read<TextMeshProUGUI>(hud, "alertText");
            Image accent = Read<Image>(hud, "alertAccent");
            Color expected = Read<Color>(hud, chase ? "chaseColor" : "suspiciousColor");
            Check(Read<GameObject>(hud, "alertIndicator").activeSelf && label.text == (chase ? "GUARD PURSUIT" : "SUSPICIOUS ACTIVITY"), (chase ? "Chase" : "Suspicious") + " shows the correct honest alert label.");
            Check(ColorDifference(label.color, expected) < .01f && ColorDifference(accent.color, expected) < .01f, (chase ? "Chase" : "Suspicious") + " applies its configured warning color to text and accent.");
            Check(Read<int>(hud, "_suspiciousGuardCount") == guards.Count(g => GuardState(g) == (int)GuardStateId.Suspicious) && Read<int>(hud, "_chasingGuardCount") == guards.Count(g => GuardState(g) == (int)GuardStateId.Chase), "Event-driven HUD alert counts match actual guard states.");
            if (chase)
                Check(guards.Any(g => GuardState(g) == (int)GuardStateId.Suspicious), "Pursuit styling takes precedence while another actual guard remains Suspicious.");
        }

        private static float ColorDifference(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a);
        }

        private static void ValidateOutcome(bool won)
        {
            Check(session.State == (won ? GameSessionState.Won : GameSessionState.Lost), (won ? "Extraction after evidence" : "Actual guard catch") + " reaches the correct session outcome.");
            Check(Read<GameObject>(hud, "winPanel").activeSelf == won && Read<GameObject>(hud, "losePanel").activeSelf != won, "Only the appropriate result overlay is active.");
            Check(!Read<GameObject>(hud, "gameplayHud").activeSelf && !Read<GameObject>(hud, "alertIndicator").activeSelf && !Read<GameObject>(hud, "evidenceFeedback").activeSelf, "Result presentation hides gameplay HUD, guard alert and transient evidence message.");
            ValidateCursor(won ? "mission complete" : "mission failed");
        }

        private static void ValidateCursor(string context)
        {
            Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, context + " uses a visible, unlocked cursor, matching the existing LMB-drag input and allowing button navigation.");
        }

        private static void ClickMenuStart()
        {
            ClickCallback(InScene<MainMenuController>(SceneManager.GetActiveScene()).Single(), "StartMission", null);
        }

        private static void ClickCallback(UnityEngine.Object target, string method, GameObject scope)
        {
            Button[] buttons = scope != null ? scope.GetComponentsInChildren<Button>(true) : InScene<Button>(SceneManager.GetActiveScene());
            Button[] matches = buttons.Where(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentTarget(i) == target && b.onClick.GetPersistentMethodName(i) == method)).ToArray();
            Check(matches.Length == 1, "Exactly one authored button connects to " + target.GetType().Name + "." + method + " in this scope.");
            if (matches.Length != 1) throw new InvalidOperationException("Ambiguous or missing button callback: " + method);
            Button button = matches[0];
            Check(button.gameObject.activeInHierarchy && button.IsInteractable(), button.name + " is visible and interactable.");
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.GetComponent<RectTransform>();
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Vector2 center = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            PointerEventData click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = center };
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(click, hits);
            Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), button.name + " is the frontmost UI raycast target at its center.");
            ExecuteEvents.Execute(button.gameObject, click, ExecuteEvents.pointerClickHandler);
            Append("ACTION pointerClick dispatched to " + button.name + " → " + method + ".");
        }

        private static void BeginChase()
        {
            sawChase = sawSuspicion = capturedChase = false;
            chaseGuard = null;
            foreach (Guard candidate in guards)
            {
                Vector3 point = candidate.transform.position + candidate.transform.forward * 8f;
                point.y = spawn.y;
                if (!ClearLane(candidate.transform.position, point)) continue;
                MovePlayer(point);
                if (!candidate.SightSensor.Evaluate().Visible) continue;
                chaseGuard = candidate;
                Append("SCENARIO player relocated eight metres ahead of " + candidate.name + " in its visible, capsule-clear patrol lane.");
                break;
            }
            Check(chaseGuard != null, "An actual visible unobstructed guard chase lane is available.");
            if (chaseGuard == null) throw new InvalidOperationException("No suitable actual-geometry chase lane.");
            Guard otherGuard = guards.First(g => g != chaseGuard);
            NoiseEvent noise = new NoiseEvent(otherGuard.transform.position + otherGuard.transform.forward * 1.5f, 8f, player.gameObject);
            bootstrap.NoiseBus.Emit(in noise);
        }

        private static bool ClearLane(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0;
            if (Physics.OverlapCapsule(to + Vector3.up * .5f, to - Vector3.up * .5f, .47f, Physics.AllLayers, QueryTriggerInteraction.Ignore).Any(c => c.attachedRigidbody == null && c.bounds.max.y > to.y - .85f)) return false;
            return !Physics.CapsuleCastAll(from + Vector3.up * .5f, from - Vector3.up * .5f, .48f, delta.normalized, delta.magnitude, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                .Any(h => h.collider.attachedRigidbody == null && h.collider.bounds.max.y > from.y - .85f);
        }

        private static void MovePlayer(Vector3 position)
        {
            motor.SetIntent(default);
            motor.Stop();
            body.position = position;
            player.transform.position = position;
            Physics.SyncTransforms();
        }

        private static void CaptureSet(string state, bool allSizes)
        {
            Capture(state, 1280, 720);
            if (!allSizes) return;
            Capture(state, 1600, 900);
            Capture(state, 1920, 1080);
        }

        private static void Capture(string state, int width, int height)
        {
            Scene scene = SceneManager.GetActiveScene();
            Canvas[] canvases = InScene<Canvas>(scene).Where(c => c.isRootCanvas && c.isActiveAndEnabled).ToArray();
            UnityEngine.Camera camera = InScene<UnityEngine.Camera>(scene).FirstOrDefault(c => c.enabled);
            if (camera == null) throw new InvalidOperationException("A scene camera is required to capture the actual UI.");
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            RenderMode[] modes = canvases.Select(c => c.renderMode).ToArray();
            UnityEngine.Camera[] cameras = canvases.Select(c => c.worldCamera).ToArray();
            float[] distances = canvases.Select(c => c.planeDistance).ToArray();
            Texture2D result = null;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                foreach (Canvas canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + 1f;
                    UpdateScaler(canvas);
                }
                Canvas.ForceUpdateCanvases();
                foreach (TextMeshProUGUI text in InScene<TextMeshProUGUI>(scene).Where(t => t.isActiveAndEnabled)) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                ValidateLayout(state, width, height, canvases);
                camera.Render();
                RenderTexture.active = target;
                result = new Texture2D(width, height, TextureFormat.RGB24, false);
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                result.Apply();
                string path = ArtifactRoot + "/" + state + "-" + width + "x" + height + ".png";
                File.WriteAllBytes(path, result.EncodeToPNG());
                screenshots++;
                Append("CAPTURE " + path);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = cameras[i];
                    canvases[i].planeDistance = distances[i];
                    UpdateScaler(canvases[i]);
                }
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = previousActive;
                if (result != null) UnityEngine.Object.DestroyImmediate(result);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void UpdateScaler(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
                typeof(CanvasScaler).GetMethod("Handle", Fields)?.Invoke(scaler, null);
        }

        private static void ValidateLayout(string state, int width, int height, Canvas[] canvases)
        {
            string prefix = state + " at " + width + "×" + height + ": ";
            foreach (Canvas canvas in canvases)
            {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                Check(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize, prefix + "Canvas scales with screen size.");
                RectTransform root = (RectTransform)canvas.transform;
                Vector3[] corners = new Vector3[4];
                foreach (TextMeshProUGUI text in canvas.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t => t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text)))
                {
                    Check(!text.isTextOverflowing, prefix + "text fits: " + text.name + "." +
                        (text.isTextOverflowing ? " Metrics: rect=" + text.rectTransform.rect.size + "; preferred=" + text.GetPreferredValues() + "; rendered=" + text.textBounds.size + "; first overflow index=" + text.firstOverflowCharacterIndex + "." : string.Empty));
                    text.rectTransform.GetWorldCorners(corners);
                    bool inside = corners.All(c => { Vector3 p = root.InverseTransformPoint(c); return p.x >= root.rect.xMin - 1f && p.x <= root.rect.xMax + 1f && p.y >= root.rect.yMin - 1f && p.y <= root.rect.yMax + 1f; });
                    Check(inside, prefix + "text rectangle is on screen: " + text.name + ".");
                }
                foreach (Button button in canvas.GetComponentsInChildren<Button>(false).Where(b => b.isActiveAndEnabled))
                {
                    ColorBlock colors = button.colors;
                    Check(button.transition == Selectable.Transition.ColorTint && ColorDifference(colors.normalColor, colors.highlightedColor) > .01f && ColorDifference(colors.normalColor, colors.pressedColor) > .01f && ColorDifference(colors.normalColor, colors.disabledColor) > .01f,
                        prefix + button.name + " defines distinct normal, highlighted, pressed and disabled feedback.");
                    button.GetComponent<RectTransform>().GetWorldCorners(corners);
                    Check(corners.All(c => { Vector3 p = root.InverseTransformPoint(c); return root.rect.Contains(new Vector2(p.x, p.y)); }), prefix + button.name + " remains entirely on screen.");
                }
            }
        }

        private static int GuardState(Guard guard)
        {
            object machine = Read<object>(guard, "_stateMachine");
            return machine == null ? -1 : (int)machine.GetType().GetProperty("Current").GetValue(machine);
        }

        private static T Read<T>(object target, string field)
        {
            if (target == null) return default;
            FieldInfo info = target.GetType().GetField(field, Fields);
            if (info == null) throw new MissingFieldException(target.GetType().Name, field);
            return (T)info.GetValue(target);
        }

        private static T[] InScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static void Next(Stage stage)
        {
            SessionState.SetInt(StageKey, (int)stage);
            entered = false;
        }

        private static void Check(bool ok, string message)
        {
            if (!ok) failures++;
            Append((ok ? "PASS " : "FAIL ") + message);
        }

        private static void Append(string message)
        {
            Directory.CreateDirectory("Logs");
            File.AppendAllText(Report, message + "\n");
        }

        private static void CaptureLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false) || type == LogType.Log) return;
            if (type == LogType.Warning)
            {
                warnings++;
                Append("PLAY MODE WARNING " + message + "\n" + stack);
            }
            else
            {
                failures++;
                Append("PLAY MODE ERROR " + type + ": " + message + "\n" + stack);
            }
        }

        private static void Complete()
        {
            finishing = true;
            SessionState.SetInt(StageKey, (int)Stage.Finish);
            Append("RESULT: " + failures + " failed checks/errors; " + warnings + " Play Mode warnings; " + screenshots + " rendered UI captures.");
            Append("LIMITATIONS: scripted button pointer events, not OS mouse automation. Objective checks use explicit player relocation. Resolution checks render the live Canvas at each size rather than resizing the docked Game view. No complete human stealth playthrough, browser performance assertion, or player build.");
            Directory.CreateDirectory("Docs/DesertVillage/reports");
            File.Copy(Report, "Docs/DesertVillage/reports/desert-ui-playmode-validation.txt", true);
            Debug.Log("Desert UI Play Mode validation complete: " + failures + " failed checks/errors; " + warnings + " warnings. See " + Report);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
