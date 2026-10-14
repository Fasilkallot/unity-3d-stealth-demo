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
using EvidenceRun.Gameplay.Projectiles;
using EvidenceRun.Gameplay.Session;
using EvidenceRun.Presentation.Effects;
using EvidenceRun.Presentation.HUD;
using EvidenceRun.Presentation.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EvidenceRun.Editor
{
    /// <summary>
    /// Editor-only integration checks. Play checks inject actor intents and relocate the player;
    /// they do not replace a human controls pass or representative WebGL profiling.
    /// </summary>
    [InitializeOnLoad]
    public static class DesertVillageValidation
    {
        public const string ScenePath = "Assets/_Project/Scenes/EvidenceRun_DesertVillage.unity";
        public const string SceneName = "EvidenceRun_DesertVillage";
        private const string ActiveKey = "EvidenceRun.DesertValidation.Active";
        private const string StageKey = "EvidenceRun.DesertValidation.Stage";
        private const string RuntimeReport = "Logs/desert-playmode-validation.txt";
        private const string StaticReport = "Logs/desert-scene-validation.txt";
        private const string RouteReport = "Logs/desert-route-validation.txt";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private enum Stage
        {
            Boot, Patrol, Walk, Run, Crouch, JumpSettle, Jump, Throw, Noise,
            Evidence, Extraction, WinMenu, SecondBoot, Chase, LossMenu, Finish
        }

        private static GameBootstrap bootstrap;
        private static PlayerController player;
        private static ActorMotor motor;
        private static Rigidbody body;
        private static GameSession session;
        private static GameHUD hud;
        private static Guard[] guards;
        private static Guard chaseGuard;
        private static Vector3 spawn;
        private static Vector3 measurementStart;
        private static Vector3 chaseStart;
        private static Vector3[] patrolStarts;
        private static double stageStarted;
        private static bool stageEntered;
        private static bool sawChase;
        private static bool sawSuspicious;
        private static bool sawEffect;
        private static bool sawProjectile;
        private static int impacts;
        private static float jumpMaxY;
        private static int failures;
        private static double lastRuntimeTick;
        private static bool finishing;

        static DesertVillageValidation()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.update += Update;
            Application.logMessageReceived += CaptureError;
            if (SessionState.GetBool(ActiveKey, false))
                stageStarted = EditorApplication.timeSinceStartup;
        }

        [MenuItem("Evidence Run/Desert Village/Validate Scene")]
        public static void ValidateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run static validation outside Play Mode.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(StaticReport, "DESERT VILLAGE STATIC VALIDATION\n" + DateTime.UtcNow.ToString("u") + "\n");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            int errors = 0;
            Action<bool, string> check = (ok, description) =>
            {
                if (!ok) errors++;
                File.AppendAllText(StaticReport, (ok ? "PASS " : "FAIL ") + description + "\n");
            };
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Transform[] transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                check(transforms.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing MonoBehaviour scripts.");
                GameBootstrap[] boots = InScene<GameBootstrap>(scene);
                check(boots.Length == 1, "Exactly one GameBootstrap.");
                check(InScene<GameLoop>(scene).Length == 1, "Exactly one GameLoop.");
                check(InScene<PlayerController>(scene).Length == 1, "Exactly one PlayerController.");
                check(InScene<GameHUD>(scene).Length == 1, "Exactly one GameHUD.");
                check(InScene<UnityEngine.EventSystems.EventSystem>(scene).Length == 1, "Exactly one EventSystem.");
                Renderer[] renderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToArray();
                check(renderers.All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")), "All renderers have valid materials and shaders.");
                MeshFilter[] meshes = roots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).ToArray();
                check(meshes.All(m => m.sharedMesh != null || !m.gameObject.activeInHierarchy || m.GetComponent<EvidenceRun.Presentation.VisionCone.VisionConeRenderer>() != null), "All active MeshFilters have meshes or the existing runtime vision-mesh generator.");

                foreach (MonoBehaviour component in roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
                {
                    if (component == null) continue;
                    SerializedObject serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            check(false, "Broken serialized reference: " + component.name + "." + property.propertyPath);
                }
                if (boots.Length == 1)
                {
                    GameBootstrap b = boots[0];
                    string[] references = { "gameLoop", "poolConfig", "projectilePrefab", "projectilePoolRoot", "playerController", "throwableConfig", "impactEffectPrefab", "impactEffectPoolRoot", "objectiveConfig", "evidenceTarget", "extractionTarget", "gameHUD" };
                    foreach (string field in references) check(Read<UnityEngine.Object>(b, field) != null, "Bootstrap." + field + " assigned.");
                    Guard[] assigned = Read<Guard[]>(b, "guards");
                    Guard[] sceneGuards = InScene<Guard>(scene);
                    check(assigned != null && assigned.Length > 0 && assigned.All(g => g != null) && assigned.Distinct().Count() == assigned.Length && new HashSet<Guard>(assigned).SetEquals(sceneGuards), "Bootstrap guard array matches scene guards exactly.");
                    Physics.SyncTransforms();
                    foreach (Guard guard in sceneGuards)
                    {
                        foreach (string field in new[] { "guardConfig", "target", "sightSensor", "guardPerception", "actorMotor" })
                            check(Read<UnityEngine.Object>(guard, field) != null, guard.name + "." + field + " assigned.");
                        Transform[] route = Read<Transform[]>(guard, "patrolWaypoints");
                        check(route != null && route.Length > 0 && route.All(t => t != null), guard.name + " has a complete patrol route.");
                        check(!OverlapsEnvironment(guard.transform.position, scene), guard.name + " spawn capsule is clear.");
                        if (route == null || route.Length == 0 || route.Any(t => t == null)) continue;
                        Vector3 from = guard.transform.position;
                        for (int i = 0; i <= route.Length; i++)
                        {
                            Vector3 to = route[i % route.Length].position;
                            to.y = from.y;
                            string blocker;
                            check(ClearSegment(from, to, scene, out blocker), guard.name + " patrol segment " + i + " is clear" + (blocker == null ? "." : "; blocker: " + blocker));
                            from = to;
                        }
                    }
                    PlayerController p = Read<PlayerController>(b, "playerController");
                    if (p != null)
                    {
                        check(!OverlapsEnvironment(p.transform.position, scene), "Player spawn capsule is clear.");
                        foreach (string field in new[] { "inputReader", "actorMotor", "playerConfig", "throwOrigin" }) check(Read<UnityEngine.Object>(p, field) != null, "Player." + field + " assigned.");
                        PlayerInputReader input = Read<PlayerInputReader>(p, "inputReader");
                        check(input != null && Read<Camera>(input, "playerCamera") != null, "Player input camera assigned.");
                    }
                    GameHUD h = Read<GameHUD>(b, "gameHUD");
                    if (h != null) foreach (string field in new[] { "objectiveText", "winPanel", "losePanel", "alertIndicator" }) check(Read<UnityEngine.Object>(h, field) != null, "HUD." + field + " assigned.");
                }
                check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath), "Desert scene enabled in Build Settings.");
                check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/MainMenu.unity", StringComparison.Ordinal)), "MainMenu enabled for restart.");
                File.AppendAllText(StaticReport, "RESULT: " + errors + " failed checks.\nNOTE: capsule sweeps verify authored patrol corridors, not arbitrary obstacle navigation.\n");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("Desert static validation: " + errors + " failed checks. " + StaticReport);
        }

        [MenuItem("Evidence Run/Desert Village/Validate Infiltration Routes")]
        public static void ValidateRoutes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run route validation outside Play Mode.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(RouteReport, "DESERT VILLAGE AUTHORED ROUTE VALIDATION\n" + DateTime.UtcNow.ToString("u") +
                "\nMETHOD: full-size standing player capsule (height 2m, radius .5m, pivot Y1.05), endpoint overlaps, bidirectional segment sweeps and floor support samples at <=1m intervals. Dynamic actors excluded. No player teleport or runtime input used.\n");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            int errors = 0;
            Action<bool, string> check = (ok, description) =>
            {
                if (!ok) errors++;
                File.AppendAllText(RouteReport, (ok ? "PASS " : "FAIL ") + description + "\n");
            };
            try
            {
                Physics.SyncTransforms();
                Vector3[][] routes =
                {
                    new[] { RoutePoint(0,-22), RoutePoint(0,-17), RoutePoint(0,-10), RoutePoint(0,2), RoutePoint(0,7), RoutePoint(0,11), RoutePoint(0,17.3f) },
                    new[] { RoutePoint(0,-22), RoutePoint(-12,-20), RoutePoint(-17,-18), RoutePoint(-17,-14), RoutePoint(-14.8f,-14), RoutePoint(-14.8f,-7), RoutePoint(-16,-7), RoutePoint(-16,7), RoutePoint(-3,7), RoutePoint(-3,17.3f), RoutePoint(0,17.3f) }
                };
                string[] names = { "Central exposed approach", "Longer western route with jump-barrier bypass" };
                for (int r = 0; r < routes.Length; r++)
                {
                    Vector3[] route = routes[r];
                    float length = 0;
                    File.AppendAllText(RouteReport, "ROUTE " + names[r] + "\n");
                    for (int i = 0; i < route.Length; i++)
                    {
                        Vector3 point = route[i];
                        check(!OverlapsEnvironment(point, scene, .5f), "Waypoint " + i + " " + point.ToString("F2") + " has standing capsule clearance.");
                        if (i == 0) continue;
                        Vector3 previous = route[i - 1];
                        float distance = Vector3.Distance(previous, point);
                        length += distance;
                        string blocker;
                        bool outward = ClearSegment(previous, point, scene, out blocker, .5f);
                        check(outward, "Segment " + (i-1) + " outbound " + previous.ToString("F2") + " -> " + point.ToString("F2") + (outward ? " clear." : " BLOCKED by " + blocker));
                        bool returning = ClearSegment(point, previous, scene, out blocker, .5f);
                        check(returning, "Segment " + (i-1) + " return" + (returning ? " clear." : " BLOCKED by " + blocker));
                        bool supported = true;
                        Vector3 unsupportedAt = default;
                        int samples = Mathf.Max(1, Mathf.CeilToInt(distance));
                        for (int sample = 0; sample <= samples; sample++)
                        {
                            Vector3 location = Vector3.Lerp(previous, point, sample / (float)samples);
                            bool hasFloor = Physics.RaycastAll(location + Vector3.up * .1f, Vector3.down, 1.3f, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                                .Any(hit => hit.collider.gameObject.scene == scene && hit.collider.attachedRigidbody == null && hit.normal.y > .9f && Mathf.Abs(hit.point.y - (location.y - 1.05f)) < .1f);
                            if (hasFloor) continue;
                            supported = false;
                            unsupportedAt = location;
                            break;
                        }
                        check(supported, "Segment " + (i-1) + " has level floor support" + (supported ? "." : "; missing at " + unsupportedAt.ToString("F2")));
                    }
                    GameBootstrap b = InScene<GameBootstrap>(scene).Single();
                    Transform evidence = Read<Transform>(b, "evidenceTarget");
                    ObjectiveConfig objective = Read<ObjectiveConfig>(b, "objectiveConfig");
                    check(Vector3.Distance(route[route.Length - 1], evidence.position) <= objective.EvidencePickupRadius, "Route endpoint enters actual evidence pickup radius without crossing the desk.");
                    check(Vector3.Distance(route[0], Read<Transform>(b, "extractionTarget").position) <= objective.ExtractionRadius, "Route origin enters extraction radius for the return journey.");
                    File.AppendAllText(RouteReport, "LENGTH " + length.ToString("F1") + "m one way.\n");
                }

                string shortcutBlocker;
                bool jumpShortcutClear = ClearSegment(RoutePoint(-14.6f,-18), RoutePoint(-14.6f,-14), scene, out shortcutBlocker, .5f);
                File.AppendAllText(RouteReport, "DIAGNOSTIC optional direct low-barrier crossing: " + (jumpShortcutClear ? "standing capsule clear" : "standing capsule blocked by " + shortcutBlocker) + ". The validated western route goes around it; actual obstacle-jump traversal is not claimed by this static check.\n");
                bool chaseShortcutClear = ClearSegment(RoutePoint(0,-9), RoutePoint(-16,-14), scene, out shortcutBlocker, .5f);
                File.AppendAllText(RouteReport, "DIAGNOSTIC direct chase shortcut from checkpoint to western alley: " + (chaseShortcutClear ? "clear on this segment" : "blocked by " + shortcutBlocker) + ". This is a geometry sample, not a live AI chase test; WaypointMover cannot plan around blocking buildings.\n");
                File.AppendAllText(RouteReport, "RESULT: " + errors + " failed checks.\nLIMITATIONS: routes prove standing physical clearance and floor support only. They do not prove undetected passage, camera clearance, jumping over the optional barrier, or AI navigation around corners.\n");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("Desert route validation: " + errors + " failed checks. " + RouteReport);
        }

        private static Vector3 RoutePoint(float x, float z) { return new Vector3(x, 1.05f, z); }

        [MenuItem("Evidence Run/Desert Village/Run Play Mode Checks")]
        public static void RunPlayModeChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(ActiveKey, false))
                throw new InvalidOperationException("A Play Mode run is already active.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            File.WriteAllText(RuntimeReport, "DESERT VILLAGE EDITOR PLAY MODE VALIDATION\n" + DateTime.UtcNow.ToString("u") + "\nMETHOD: real runtime systems, injected ActorIntent, public weapon/noise calls, and explicit player relocation. Not a human keyboard/mouse test or WebGL FPS profile.\n");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetInt(StageKey, (int)Stage.Boot);
            SessionState.SetBool(ActiveKey, true);
            failures = 0;
            finishing = false;
            stageEntered = false;
            EditorApplication.isPlaying = true;
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stageStarted = EditorApplication.timeSinceStartup;
                stageEntered = false;
                lastRuntimeTick = Time.time;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (!finishing && (Stage)SessionState.GetInt(StageKey, 0) != Stage.Finish)
                    Append("INCOMPLETE: Play Mode was exited before validation finished.");
                SessionState.SetBool(ActiveKey, false);
            }
        }

        private static void Update()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || EditorApplication.isPaused || finishing) return;
            if (Time.time == (float)lastRuntimeTick) return;
            lastRuntimeTick = Time.time;
            try
            {
                Stage stage = (Stage)SessionState.GetInt(StageKey, 0);
                if (!stageEntered) { stageEntered = true; stageStarted = EditorApplication.timeSinceStartup; Enter(stage); }
                Tick(stage);
            }
            catch (Exception ex)
            {
                Check(false, "Validation exception: " + ex);
                Complete();
            }
        }

        private static void Enter(Stage stage)
        {
            Append("STAGE " + stage);
            switch (stage)
            {
                case Stage.Patrol:
                    patrolStarts = guards.Select(g => g.transform.position).ToArray();
                    break;
                case Stage.Walk: BeginMovement(false, false); break;
                case Stage.Run: BeginMovement(true, false); break;
                case Stage.Crouch: BeginMovement(false, true); break;
                case Stage.JumpSettle:
                    MovePlayer(spawn);
                    motor.SetIntent(default);
                    break;
                case Stage.Jump:
                    Check(motor.IsGrounded, "Player ground cast detects actual floor before jump.");
                    jumpMaxY = player.transform.position.y;
                    motor.SetIntent(new ActorIntent { Jump = true, AimPoint = player.transform.position + Vector3.forward });
                    break;
                case Stage.Throw:
                    player.enabled = true;
                    MovePlayer(spawn);
                    impacts = 0; sawEffect = false; sawProjectile = false;
                    bootstrap.GameEvents.ProjectileImpact += OnImpact;
                    IWeapon weapon = Read<IWeapon>(player, "_weapon");
                    Transform origin = Read<Transform>(player, "throwOrigin");
                    Check(weapon != null && weapon.TryUse(origin.position, origin.position + Vector3.forward * 4f - Vector3.up * 1.2f, player.gameObject), "Existing throwable weapon accepted a throw.");
                    break;
                case Stage.Noise:
                    chaseGuard = guards[0];
                    Vector3 noisePoint = chaseGuard.transform.position + chaseGuard.transform.forward * 1.5f;
                    NoiseEvent noise = new NoiseEvent(noisePoint, 8f, player.gameObject);
                    bootstrap.NoiseBus.Emit(in noise);
                    break;
                case Stage.Evidence:
                    MovePlayer(Read<Transform>(bootstrap, "evidenceTarget").position);
                    break;
                case Stage.Extraction:
                    MovePlayer(Read<Transform>(bootstrap, "extractionTarget").position + Vector3.up * .05f);
                    break;
                case Stage.WinMenu:
                    hud.ReturnToMainMenu();
                    break;
                case Stage.SecondBoot:
                    MainMenuController menu = InScene<MainMenuController>(SceneManager.GetActiveScene()).FirstOrDefault(m => Read<string>(m, "gameplaySceneName") == SceneName);
                    Check(menu != null, "Main menu has a desert mission controller.");
                    if (menu != null) menu.StartMission();
                    else SceneManager.LoadScene(SceneName);
                    break;
                case Stage.Chase:
                    sawChase = false; sawSuspicious = false;
                    bootstrap.GameEvents.GuardStateChanged += OnGuardState;
                    chaseGuard = null;
                    foreach (Guard candidate in guards)
                    {
                        Vector3 point = candidate.transform.position + candidate.transform.forward * 8f;
                        point.y = spawn.y;
                        string blocker;
                        if (OverlapsEnvironment(point, candidate.gameObject.scene) || !ClearSegment(candidate.transform.position, point, candidate.gameObject.scene, out blocker)) continue;
                        MovePlayer(point);
                        Physics.SyncTransforms();
                        if (!candidate.SightSensor.Evaluate().Visible) continue;
                        chaseGuard = candidate;
                        chaseStart = candidate.transform.position;
                        Append("SCENARIO: player relocated eight metres ahead of " + candidate.name + " in a capsule-clear, sight-visible authored lane.");
                        break;
                    }
                    Check(chaseGuard != null, "A clear actual-geometry chase lane is available from a guard's patrol pose.");
                    break;
                case Stage.LossMenu:
                    hud.ReturnToMainMenu();
                    break;
            }
        }

        private static void Tick(Stage stage)
        {
            double elapsed = EditorApplication.timeSinceStartup - stageStarted;
            switch (stage)
            {
                case Stage.Boot:
                    if (elapsed < 1.0) return;
                    Bind();
                    Check(session != null && session.State == GameSessionState.Playing, "Bootstrap starts a Playing session.");
                    Check(bootstrap.NoiseBus != null && bootstrap.NoiseBus.ListenerCount == guards.Length, "NoiseBus registers every guard.");
                    Check(Read<ProjectileSystem>(bootstrap, "_projectileSystem") != null && Read<ImpactEffectSystem>(bootstrap, "_impactEffectSystem") != null, "Projectile and impact systems initialized.");
                    Check(player.enabled && motor.enabled && guards.All(g => g.Context != null), "Player motor and all guard contexts initialized.");
                    Next(Stage.Patrol); break;
                case Stage.Patrol:
                    if (elapsed < 2.0) return;
                    for (int i = 0; i < guards.Length; i++)
                        Check(Vector3.Distance(patrolStarts[i], guards[i].transform.position) > .5f, guards[i].name + " moves along patrol in the live scene.");
                    Next(Stage.Walk); break;
                case Stage.Walk:
                case Stage.Run:
                case Stage.Crouch:
                    if (elapsed < .75) return;
                    PlayerConfig config = Read<PlayerConfig>(player, "playerConfig");
                    float expected = config.MoveSpeed * (stage == Stage.Run ? config.RunSpeedMultiplier : stage == Stage.Crouch ? config.CrouchSpeedMultiplier : 1f);
                    float speed = new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
                    float moved = Vector3.Distance(measurementStart, player.transform.position);
                    Check(speed > expected * .8f && speed < expected * 1.15f && moved > .5f, stage + " injected intent: speed=" + speed.ToString("F2") + " m/s; displacement=" + moved.ToString("F2") + " m; expected=" + expected.ToString("F2") + ".");
                    Next(stage == Stage.Walk ? Stage.Run : stage == Stage.Run ? Stage.Crouch : Stage.JumpSettle); break;
                case Stage.JumpSettle:
                    if (elapsed > .4) Next(Stage.Jump);
                    break;
                case Stage.Jump:
                    jumpMaxY = Mathf.Max(jumpMaxY, player.transform.position.y);
                    if (elapsed > .08) motor.SetIntent(default);
                    if (elapsed < 1.9) return;
                    Check(jumpMaxY - spawn.y > 1f, "Injected jump produces upward displacement: " + (jumpMaxY - spawn.y).ToString("F2") + " m.");
                    Check(motor.IsGrounded && Mathf.Abs(player.transform.position.y - spawn.y) < .2f, "Player lands and ground check recovers.");
                    Next(Stage.Throw); break;
                case Stage.Throw:
                    sawProjectile |= Read<ProjectileSystem>(bootstrap, "_projectileSystem").ActiveCount > 0;
                    sawEffect |= Read<ImpactEffectSystem>(bootstrap, "_impactEffectSystem").ActiveCount > 0;
                    if (elapsed < 1.5) return;
                    bootstrap.GameEvents.ProjectileImpact -= OnImpact;
                    Check(sawProjectile, "Pooled projectile becomes active.");
                    Check(impacts > 0, "Thrown projectile hits actual level geometry and publishes impact/noise.");
                    Check(sawEffect, "Pooled impact effect becomes active.");
                    Next(Stage.Noise); break;
                case Stage.Noise:
                    if (elapsed < .35) return;
                    Check(GuardState(chaseGuard) == (int)GuardStateId.Suspicious, "NoiseBus event makes guard Suspicious.");
                    Check(Read<GameObject>(hud, "alertIndicator").activeSelf, "Suspicious guard activates HUD alert indicator.");
                    Next(Stage.Evidence); break;
                case Stage.Evidence:
                    if (elapsed < .2) return;
                    Check(session.HasEvidence, "Relocating player inside objective radius collects evidence through ObjectiveSystem.");
                    Check(!Read<Transform>(bootstrap, "evidenceTarget").gameObject.activeSelf, "EvidencePicked disables evidence visual root.");
                    Check(Read<TextMeshProUGUI>(hud, "objectiveText").text == "Proceed to extraction", "HUD updates evidence objective.");
                    Next(Stage.Extraction); break;
                case Stage.Extraction:
                    if (elapsed < .25) return;
                    Check(session.State == GameSessionState.Won, "Extraction after evidence reaches Won state.");
                    Check(Read<GameObject>(hud, "winPanel").activeSelf && !Read<GameObject>(hud, "losePanel").activeSelf, "Win panel displayed exclusively.");
                    Next(Stage.WinMenu); break;
                case Stage.WinMenu:
                    if (SceneManager.GetActiveScene().name != "MainMenu") { if (elapsed > 10) throw new TimeoutException("Win return did not load MainMenu."); return; }
                    Check(true, "Win ReturnToMainMenu() loads existing MainMenu.");
                    Next(Stage.SecondBoot); break;
                case Stage.SecondBoot:
                    if (SceneManager.GetActiveScene().name != SceneName || elapsed < 1.0) { if (elapsed > 10) throw new TimeoutException("Menu did not relaunch desert scene."); return; }
                    Bind();
                    Check(session.State == GameSessionState.Playing && !session.HasEvidence && Read<Transform>(bootstrap, "evidenceTarget").gameObject.activeSelf, "Menu relaunch creates clean session and restores evidence.");
                    Next(Stage.Chase); break;
                case Stage.Chase:
                    if (chaseGuard == null) { Next(Stage.LossMenu); return; }
                    sawChase |= guards.Any(g => GuardState(g) == (int)GuardStateId.Chase);
                    sawSuspicious |= guards.Any(g => GuardState(g) == (int)GuardStateId.Suspicious);
                    if (session.State != GameSessionState.Lost && elapsed < 12) return;
                    Check(sawSuspicious && sawChase, "Actual sight detection advances Suspicious then Chase.");
                    Check(Vector3.Distance(chaseStart, chaseGuard.transform.position) > 1f, "Guard physically pursues player in actual unobstructed level lane.");
                    Check(session.State == GameSessionState.Lost, "Guard catch reaches Lost state.");
                    Check(Read<GameObject>(hud, "losePanel").activeSelf && !Read<GameObject>(hud, "winPanel").activeSelf, "Lose panel displayed exclusively.");
                    bootstrap.GameEvents.GuardStateChanged -= OnGuardState;
                    Next(Stage.LossMenu); break;
                case Stage.LossMenu:
                    if (SceneManager.GetActiveScene().name != "MainMenu") { if (elapsed > 10) throw new TimeoutException("Loss return did not load MainMenu."); return; }
                    Check(true, "Loss ReturnToMainMenu() loads existing MainMenu.");
                    Complete(); break;
            }
            if (elapsed > 30) throw new TimeoutException("Stage timeout: " + stage);
        }

        private static void Bind()
        {
            bootstrap = InScene<GameBootstrap>(SceneManager.GetActiveScene()).Single();
            player = Read<PlayerController>(bootstrap, "playerController");
            motor = Read<ActorMotor>(player, "actorMotor");
            body = player.GetComponent<Rigidbody>();
            session = Read<GameSession>(bootstrap, "_gameSession");
            hud = Read<GameHUD>(bootstrap, "gameHUD");
            guards = Read<Guard[]>(bootstrap, "guards");
            spawn = player.transform.position;
        }

        private static void BeginMovement(bool run, bool crouch)
        {
            player.enabled = false;
            MovePlayer(spawn);
            measurementStart = spawn;
            motor.SetIntent(new ActorIntent { Move = Vector2.right, Run = run, Crouch = crouch, AimPoint = spawn + Vector3.right * 10f });
        }

        private static void MovePlayer(Vector3 position)
        {
            motor.SetIntent(default);
            motor.Stop();
            body.position = position;
            player.transform.position = position;
            Physics.SyncTransforms();
        }

        private static bool OverlapsEnvironment(Vector3 position, Scene scene, float radius = .47f)
        {
            return Physics.OverlapCapsule(position + Vector3.up * .5f, position - Vector3.up * .5f, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                .Any(c => c.gameObject.scene == scene && c.attachedRigidbody == null && c.bounds.max.y > position.y - .85f);
        }

        private static bool ClearSegment(Vector3 from, Vector3 to, Scene scene, out string blocker, float radius = .48f)
        {
            Vector3 delta = to - from; delta.y = 0;
            blocker = null;
            if (delta.sqrMagnitude < .01f) return true;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(from + Vector3.up * .5f, from - Vector3.up * .5f, radius, delta.normalized, delta.magnitude, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene != scene || hit.collider.attachedRigidbody != null || hit.collider.bounds.max.y < from.y - .85f) continue;
                blocker = HierarchyPath(hit.collider.transform);
                return false;
            }
            return true;
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        private static T[] InScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static T Read<T>(object owner, string field)
        {
            if (owner == null) return default;
            FieldInfo info = owner.GetType().GetField(field, Fields);
            if (info == null) throw new MissingFieldException(owner.GetType().Name, field);
            return (T)info.GetValue(owner);
        }

        private static int GuardState(Guard guard)
        {
            object machine = Read<object>(guard, "_stateMachine");
            return machine == null ? -1 : (int)machine.GetType().GetProperty("Current").GetValue(machine);
        }

        private static void OnImpact(ProjectileImpactEvent impact) { impacts++; }
        private static void OnGuardState(GuardStateChangedEvent change)
        {
            sawChase |= change.CurrentStateId == (int)GuardStateId.Chase;
            sawSuspicious |= change.CurrentStateId == (int)GuardStateId.Suspicious;
        }

        private static void Next(Stage stage)
        {
            SessionState.SetInt(StageKey, (int)stage);
            stageEntered = false;
        }

        private static void Check(bool ok, string description)
        {
            if (!ok) failures++;
            Append((ok ? "PASS " : "FAIL ") + description);
        }

        private static void Append(string line)
        {
            Directory.CreateDirectory("Logs");
            File.AppendAllText(RuntimeReport, line + "\n");
        }

        private static void CaptureError(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false) || type == LogType.Log || type == LogType.Warning) return;
            failures++;
            Append("RUNTIME ERROR " + type + ": " + message + "\n" + stack);
        }

        private static void Complete()
        {
            finishing = true;
            SessionState.SetInt(StageKey, (int)Stage.Finish);
            Append("RESULT: " + failures + " failed checks/errors.\nLIMITATIONS: no human controls test; no arbitrary obstacle-routing proof; no WebGL performance measurement. Crouch does not resize capsule or sight target.\n");
            Debug.Log("Desert Play Mode checks complete: " + failures + " failed checks/errors. See " + RuntimeReport);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
