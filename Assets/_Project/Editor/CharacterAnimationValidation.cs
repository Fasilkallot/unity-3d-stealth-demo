#if UNITY_EDITOR
using System;
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
using EvidenceRun.Presentation.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EvidenceRun.Editor
{
    /// <summary>
    /// Editor-only integration checks through the real input -> player -> motor ->
    /// presentation path. Never assigns Animator parameters, plays states, saves scenes,
    /// or builds a player. Guard ticking is isolated after its patrol regression sample.
    /// </summary>
    [InitializeOnLoad]
    public static class CharacterAnimationValidation
    {
        private const string ActiveKey = "EvidenceRun.AnimationValidation.Active";
        private const string StageKey = "EvidenceRun.AnimationValidation.Stage";
        private const string Report = "Logs/character-animation-validation.txt";
        private const string ArtifactRoot = "Docs/DesertVillage/Animation";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] Scenes = { "EvidenceRun", "EvidenceRun_DesertVillage" };
        private static readonly HumanBodyBones[] SampleBones =
        {
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.Spine
        };

        private enum Stage
        {
            Boot, IdleAndGuards, Walk, Run, CrouchIdle, CrouchWalk, Stand,
            Throw, JumpSettle, Jump, Landing, CrouchRejected, Uncrouch,
            SecondJump, SecondLanding, ContactSettle, ContactRejump, ContactAfterLanding, Finish
        }

        private static PlayerController player;
        private static PlayerInputReader input;
        private static ActorMotor motor;
        private static Rigidbody body;
        private static Animator animator;
        private static CharacterAnimationController presentation;
        private static GameBootstrap bootstrap;
        private static GameSession session;
        private static Guard[] guards;
        private static Animator[] guardAnimators;
        private static Vector3[] guardStarts;
        private static Quaternion[][] guardPoses;
        private static float[] guardPoseChanges;
        private static float[] guardSpeeds;
        private static Camera guardVisibilityCamera;
        private static RenderTexture guardVisibilityTexture;
        private static Vector3 spawn;
        private static Vector3 modelLocalPosition;
        private static Quaternion[] baseline;
        private static Quaternion[] standingPose;
        private static float poseChange;
        private static float maxHeight;
        private static float largestVisualOffset;
        private static int acceptedJumps;
        private static int jumpsBeforeStage;
        private static int failures;
        private static int checks;
        private static int warnings;
        private static int sceneIndex;
        private static bool sawJump;
        private static bool sawAction;
        private static bool sawProjectile;
        private static bool capturedJump;
        private static bool sentMidairRequest;
        private static bool contactAcceptedInJump;
        private static bool contactClipRestarted;
        private static float contactSecondAcceptedAt;
        private static float contactPreviousNormalizedTime;
        private static bool entered;
        private static bool finishing;
        private static float started;
        private static float lastRuntimeTime;
        private static double wallStarted;

        static CharacterAnimationValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Application.logMessageReceived += CaptureLog;
        }

        [MenuItem("Evidence Run/Character Animation/Run Editor Play Mode Validation")]
        public static void RunPlayModeChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(ActiveKey, false))
                throw new InvalidOperationException("Exit the current Play Mode run before animation validation.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before animation validation. This harness never saves or discards edits.");
            Directory.CreateDirectory("Logs");
            Directory.CreateDirectory(ArtifactRoot);
            File.WriteAllText(Report, "CHARACTER ANIMATION — UNITY EDITOR PLAY MODE VALIDATION\n" + DateTime.UtcNow.ToString("u") +
                "\nMETHOD: disable hardware PlayerInputReader polling and inject ActorIntent into its existing private storage. Keep the actual PlayerController, ActorMotor, CharacterAnimationController, Rigidbody and Animator running. Observe real state/clip evaluation, humanoid limb rotation and physical movement. Never set Animator parameters or force states.\n" +
                "SCENARIOS: original EvidenceRun and EvidenceRun_DesertVillage. Each scene uses its saved prefab/scene configuration. After an actual guard patrol sample, isolate GuardSystem ticking for deterministic player checks; keep projectile and objective systems running. Movement samples reset the player to its spawn between trials.\n" +
                "CAPTURES: live scene/avatar rendered from a temporary inspection camera; no pose editing or animation sampling. No WebGL or standalone player build.\n");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(StageKey, (int)Stage.Boot);
            failures = checks = warnings = sceneIndex = 0;
            entered = finishing = false;
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + Scenes[0] + ".unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                entered = false;
                lastRuntimeTime = -1;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if ((Stage)SessionState.GetInt(StageKey, 0) != Stage.Finish)
                    Append("INCOMPLETE: Play Mode exited before validation finished.");
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
                    started = Time.unscaledTime;
                    wallStarted = EditorApplication.timeSinceStartup;
                    Append("STAGE " + SceneManager.GetActiveScene().name + "/" + stage);
                    Enter(stage);
                }
                Tick(stage, Time.unscaledTime - started);
                if (EditorApplication.timeSinceStartup - wallStarted > 30)
                    throw new TimeoutException("Animation validation stage timed out: " + stage);
            }
            catch (Exception ex)
            {
                Check(false, "Validation exception: " + ex);
                Complete();
            }
        }

        private static void Enter(Stage stage)
        {
            if (stage == Stage.Boot) return;
            baseline = Pose(animator);
            poseChange = 0;
            maxHeight = player.transform.position.y;
            jumpsBeforeStage = acceptedJumps;
            switch (stage)
            {
                case Stage.IdleAndGuards:
                    SetIntent(default);
                    guardStarts = guards.Select(g => g.transform.position).ToArray();
                    guardPoses = guardAnimators.Select(Pose).ToArray();
                    guardPoseChanges = new float[guards.Length];
                    guardSpeeds = new float[guards.Length];
                    ShowGuardsToInspectionCamera();
                    break;
                case Stage.Walk: Movement(false, false); break;
                case Stage.Run: Movement(true, false); break;
                case Stage.CrouchIdle:
                    ResetPlayer();
                    SetIntent(new ActorIntent { Crouch = true });
                    break;
                case Stage.CrouchWalk: Movement(false, true); break;
                case Stage.Stand:
                case Stage.JumpSettle:
                case Stage.ContactSettle:
                    ResetPlayer();
                    SetIntent(default);
                    break;
                case Stage.Throw:
                    sawAction = sawProjectile = false;
                    SetIntent(new ActorIntent { Throw = true });
                    break;
                case Stage.Jump:
                case Stage.SecondJump:
                    Check(motor.IsGrounded, "Motor reports the real floor before an accepted jump.");
                    sawJump = capturedJump = sentMidairRequest = false;
                    SetIntent(new ActorIntent { Jump = true });
                    break;
                case Stage.Landing:
                    SetIntent(default);
                    break;
                case Stage.CrouchRejected:
                    Check(motor.IsGrounded, "Grounded before crouched jump rejection.");
                    sawJump = false;
                    SetIntent(new ActorIntent { Jump = true, Crouch = true });
                    break;
                case Stage.Uncrouch:
                    sawJump = false;
                    SetIntent(default);
                    break;
                case Stage.SecondLanding:
                    SetIntent(new ActorIntent { Crouch = true });
                    break;
                case Stage.ContactRejump:
                    Check(motor.IsGrounded, "Grounded before immediate-contact re-jump scenario.");
                    contactAcceptedInJump = contactClipRestarted = capturedJump = false;
                    contactSecondAcceptedAt = -1;
                    contactPreviousNormalizedTime = 0;
                    SetIntent(new ActorIntent { Jump = true });
                    break;
                case Stage.ContactAfterLanding:
                    sawJump = false;
                    SetIntent(default);
                    break;
            }
        }

        private static void Tick(Stage stage, float elapsed)
        {
            if (stage != Stage.Boot)
            {
                poseChange = Mathf.Max(poseChange, PoseDifference(baseline, Pose(animator)));
                maxHeight = Mathf.Max(maxHeight, player.transform.position.y);
                largestVisualOffset = Mathf.Max(largestVisualOffset, Vector3.Distance(modelLocalPosition, animator.transform.localPosition));
            }
            switch (stage)
            {
                case Stage.Boot:
                    if (elapsed < 1) return;
                    Bind();
                    Next(Stage.IdleAndGuards);
                    break;
                case Stage.IdleAndGuards:
                    for (int i = 0; i < guards.Length; i++)
                    {
                        guardPoseChanges[i] = Mathf.Max(guardPoseChanges[i], PoseDifference(guardPoses[i], Pose(guardAnimators[i])));
                        guardSpeeds[i] = Mathf.Max(guardSpeeds[i], guardAnimators[i].GetFloat("Speed"));
                    }
                    if (elapsed < 2) return;
                    Check(State("Locomotion") && Clip("Idle"), "Idle evaluates the Locomotion state and an idle clip: " + ClipNames());
                    Check(poseChange > .05f, "Idle humanoid bones change over time (max local angle " + poseChange.ToString("F2") + "°).");
                    standingPose = Pose(animator);
                    for (int i = 0; i < guards.Length; i++)
                    {
                        Check(Vector3.Distance(guardStarts[i], guards[i].transform.position) > .15f, guards[i].name + " physically patrols.");
                        Check(guardAnimators[i].runtimeAnimatorController != null && guardAnimators[i].isHuman && guardAnimators[i].avatar.isValid && !guardAnimators[i].applyRootMotion,
                            guards[i].name + " has a valid humanoid override controller with root motion off.");
                        Check(guardSpeeds[i] > .1f && guardPoseChanges[i] > 2, guards[i].name + " patrol drives real limb motion (max local angle " + guardPoseChanges[i].ToString("F2") + "°).");
                    }
                    ReleaseGuardInspectionCamera();
                    Capture("idle");
                    IsolateGuards();
                    Next(Stage.Walk);
                    break;
                case Stage.Walk:
                case Stage.Run:
                case Stage.CrouchWalk:
                    if (elapsed < .65f) return;
                    bool crouch = stage == Stage.CrouchWalk;
                    Check(State(crouch ? "Crouch Locomotion" : "Locomotion") && Clip(stage == Stage.Run ? "Running" : crouch ? "Crouch Walk" : "Walking"), stage + " selects its real clip: " + ClipNames());
                    Check(new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude > (crouch ? 1 : 3), stage + " physically moves the Rigidbody.");
                    Check(poseChange > 5, stage + " changes actual humanoid limb rotations (max local angle " + poseChange.ToString("F2") + "°).");
                    if (stage == Stage.Walk) Capture("walk");
                    Next(stage == Stage.Walk ? Stage.Run : stage == Stage.Run ? Stage.CrouchIdle : Stage.Stand);
                    break;
                case Stage.CrouchIdle:
                    if (elapsed < .6f) return;
                    Check(State("Crouch Locomotion") && Clip("Crouch Idle"), "Crouched idle selects its real clip: " + ClipNames());
                    Check(PoseDifference(standingPose, Pose(animator)) > 10, "Crouch visibly changes limb pose from standing.");
                    Next(Stage.CrouchWalk);
                    break;
                case Stage.Stand:
                    if (elapsed < .6f) return;
                    Check(State("Locomotion") && !animator.GetBool("Crouch"), "Releasing crouch returns to standing locomotion.");
                    Next(Stage.Throw);
                    break;
                case Stage.Throw:
                    sawAction |= State("Action") && Clip("Throw");
                    sawProjectile |= Read<ProjectileSystem>(bootstrap, "_projectileSystem").ActiveCount > 0;
                    if (elapsed > .08f) SetIntent(default);
                    if (elapsed < 2) return;
                    Check(sawAction && sawProjectile, "The same throw intent evaluates the Throw clip and spawns a real pooled projectile.");
                    Check(State("Locomotion"), "Throw returns to locomotion.");
                    Next(Stage.JumpSettle);
                    break;
                case Stage.JumpSettle:
                    if (elapsed < .5f) return;
                    Next(Stage.Jump);
                    break;
                case Stage.Jump:
                case Stage.SecondJump:
                    sawJump |= State("Jump") && Clip("Jump");
                    if (elapsed > .08f && !sentMidairRequest) SetIntent(default);
                    if (stage == Stage.Jump && elapsed > .35f && !sentMidairRequest)
                    {
                        Check(!motor.IsGrounded, "Second jump request is issued while physically airborne.");
                        SetIntent(new ActorIntent { Jump = true });
                        sentMidairRequest = true;
                    }
                    if (sentMidairRequest && elapsed > .45f) SetIntent(default);
                    if (stage == Stage.SecondJump && elapsed > .2f) SetIntent(new ActorIntent { Crouch = true });
                    if (!capturedJump && sawJump && player.transform.position.y - spawn.y > 1.8f)
                    {
                        Capture(stage == Stage.Jump ? "jump" : "jump-to-crouch");
                        capturedJump = true;
                    }
                    if (elapsed < 2.2f) return;
                    Check(acceptedJumps == jumpsBeforeStage + 1, stage + " produces exactly one accepted motor jump event, including any rejected midair request.");
                    Check(sawJump, stage + " evaluates the connected Jump state and Jumping clip.");
                    Check(maxHeight - spawn.y > 1, stage + " raises the Rigidbody " + (maxHeight - spawn.y).ToString("F2") + " m.");
                    Check(poseChange > 5, stage + " changes actual humanoid limb pose (max local angle " + poseChange.ToString("F2") + "°).");
                    Check(motor.IsGrounded && Mathf.Abs(player.transform.position.y - spawn.y) < .2f, stage + " lands on the actual floor.");
                    Next(stage == Stage.Jump ? Stage.Landing : Stage.SecondLanding);
                    break;
                case Stage.Landing:
                    if (elapsed < .8f) return;
                    Check(State("Locomotion") && animator.GetBool("IsGrounded"), "Landing returns to standing locomotion with IsGrounded true.");
                    Check(acceptedJumps == jumpsBeforeStage && maxHeight - spawn.y < .2f, "Rejected midair input does not cause a queued jump after landing.");
                    Next(Stage.CrouchRejected);
                    break;
                case Stage.CrouchRejected:
                    sawJump |= State("Jump");
                    if (elapsed > .08f) SetIntent(new ActorIntent { Crouch = true });
                    if (elapsed < .7f) return;
                    Check(acceptedJumps == jumpsBeforeStage && !sawJump && maxHeight - spawn.y < .2f, "Crouched jump is rejected physically and does not enter the Jump animation.");
                    Next(Stage.Uncrouch);
                    break;
                case Stage.Uncrouch:
                    sawJump |= State("Jump");
                    if (elapsed < .7f) return;
                    Check(acceptedJumps == jumpsBeforeStage && !sawJump && State("Locomotion"), "Releasing crouch leaves no stale jump trigger or queued physical jump.");
                    Next(Stage.SecondJump);
                    break;
                case Stage.SecondLanding:
                    if (elapsed < .5f) return;
                    Check(State("Crouch Locomotion") && animator.GetBool("IsGrounded") && animator.GetBool("Crouch"), "Holding crouch in the air lands directly into crouch locomotion.");
                    Next(Stage.ContactSettle);
                    break;
                case Stage.ContactSettle:
                    if (elapsed < .5f) return;
                    Next(Stage.ContactRejump);
                    break;
                case Stage.ContactRejump:
                    // Each new frame of true intent is a fresh gameplay request. Requests
                    // in the air are rejected; the first grounded request must re-enter
                    // Jump even when the previous Jump/landing blend has not yet exited.
                    if (acceptedJumps < jumpsBeforeStage + 2)
                    {
                        if (elapsed > 1) SetIntent(new ActorIntent { Jump = true });
                        else if (elapsed > .08f) SetIntent(default);
                    }
                    else
                    {
                        SetIntent(default);
                        float sinceContact = Time.unscaledTime - contactSecondAcceptedAt;
                        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                        if (sinceContact > .02f && sinceContact < .4f)
                            contactClipRestarted |= (current.IsName("Jump") && current.normalizedTime < .25f) ||
                                (animator.IsInTransition(0) && next.IsName("Jump") && next.normalizedTime < .25f);
                        if (!capturedJump && sinceContact > .4f && player.transform.position.y - spawn.y > 1.8f)
                        {
                            Capture("contact-rejump");
                            capturedJump = true;
                        }
                    }
                    if (elapsed < 3.8f) return;
                    Check(acceptedJumps == jumpsBeforeStage + 2, "Repeated requests across descent produce exactly two accepted takeoffs, with no airborne acceptance.");
                    Check(contactAcceptedInJump && contactPreviousNormalizedTime > .25f, "The contact takeoff is accepted while the previous Jump state is still active (old normalized time=" + contactPreviousNormalizedTime.ToString("F2") + ").");
                    Check(contactClipRestarted, "Immediate-contact takeoff restarts the Jump clip rather than holding its previous frame or leaving a pending trigger.");
                    Check(motor.IsGrounded && State("Locomotion"), "Immediate-contact re-jump lands and returns to locomotion.");
                    Next(Stage.ContactAfterLanding);
                    break;
                case Stage.ContactAfterLanding:
                    sawJump |= State("Jump");
                    if (elapsed < .8f) return;
                    Check(acceptedJumps == jumpsBeforeStage && !sawJump && maxHeight - spawn.y < .2f, "Contact re-jump leaves no delayed animation replay or physical jump after its final landing.");
                    Check(largestVisualOffset < .01f, "Avatar root remains aligned with the physics root; max local displacement=" + largestVisualOffset.ToString("F5") + " m.");
                    Check(session.State == GameSessionState.Playing, "Player scenarios preserve a live Playing session.");
                    MeasureSteadyPresentationAllocations();
                    NextSceneOrComplete();
                    break;
            }
        }

        private static void Bind()
        {
            Scene scene = SceneManager.GetActiveScene();
            bootstrap = InScene<GameBootstrap>(scene).Single();
            player = Read<PlayerController>(bootstrap, "playerController");
            input = Read<PlayerInputReader>(player, "inputReader");
            motor = Read<ActorMotor>(player, "actorMotor");
            body = player.GetComponent<Rigidbody>();
            session = Read<GameSession>(bootstrap, "_gameSession");
            CharacterAnimationController[] controllers = player.GetComponentsInChildren<CharacterAnimationController>(true);
            Check(controllers.Length == 1, "Exactly one CharacterAnimationController belongs to the player.");
            presentation = controllers.Single();
            animator = Read<Animator>(presentation, "animator");
            guards = Read<Guard[]>(bootstrap, "guards");
            guardAnimators = guards.Select(g => g.GetComponentInChildren<Animator>()).ToArray();
            Check(animator != null && animator.runtimeAnimatorController != null, "Player Animator has its saved runtime controller assigned.");
            Check(animator.isHuman && animator.avatar != null && animator.avatar.isValid, "Player has a valid humanoid Avatar.");
            Check(!animator.applyRootMotion && presentation.enabled && motor.enabled, "Root motion is disabled; presentation and physics motor are enabled.");
            Check(animator.parameters.Any(p => p.name == "IsGrounded" && p.type == AnimatorControllerParameterType.Bool), "Controller declares the IsGrounded Boolean used by presentation.");
            Check(animator.runtimeAnimatorController.animationClips.All(c => c != null) && animator.runtimeAnimatorController.animationClips.Any(c => c.name.IndexOf("Jump", StringComparison.OrdinalIgnoreCase) >= 0), "Runtime controller contains a real jump clip and no null clip entries.");
            Check(session.State == GameSessionState.Playing, "Bootstrap created a Playing session.");
            input.enabled = false;
            spawn = player.transform.position;
            modelLocalPosition = animator.transform.localPosition;
            acceptedJumps = 0;
            largestVisualOffset = 0;
            motor.Jumped += OnAcceptedJump;
            Append("CONFIG scene=" + scene.name + "; controller=" + animator.runtimeAnimatorController.name + "; avatar=" + animator.avatar.name + "; culling=" + animator.cullingMode + "; spawn=" + spawn.ToString("F2"));
        }

        private static void IsolateGuards()
        {
            GameLoop loop = Read<GameLoop>(bootstrap, "gameLoop");
            typeof(GameLoop).GetField("_guardSystem", Fields).SetValue(loop, null);
            foreach (Guard guard in guards)
            {
                ActorMotor guardMotor = Read<ActorMotor>(guard, "actorMotor");
                guardMotor.SetIntent(default);
                guardMotor.Stop();
                guardMotor.enabled = false;
            }
            Append("ISOLATION: guard AI ticking/motors paused after live patrol check; projectile/objective systems remain active.");
        }

        private static void ShowGuardsToInspectionCamera()
        {
            // Off-screen CullUpdateTransforms is intentional. Make the actors visible to
            // a real temporary camera instead of changing their production culling modes.
            Bounds bounds = new Bounds(player.transform.position, Vector3.one);
            foreach (Guard guard in guards) bounds.Encapsulate(guard.transform.position);
            GameObject temporary = new GameObject("Animation validation guard visibility") { hideFlags = HideFlags.HideAndDontSave };
            guardVisibilityCamera = temporary.AddComponent<Camera>();
            guardVisibilityCamera.orthographic = true;
            guardVisibilityCamera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * .5f + 12;
            guardVisibilityCamera.farClipPlane = 200;
            guardVisibilityCamera.transform.position = bounds.center + Vector3.up * 80;
            guardVisibilityCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            guardVisibilityTexture = new RenderTexture(64, 64, 16);
            guardVisibilityCamera.targetTexture = guardVisibilityTexture;
            Append("VISIBILITY: temporary overhead render camera makes patrol actors visible; original Animator culling modes remain unchanged.");
        }

        private static void ReleaseGuardInspectionCamera()
        {
            if (guardVisibilityCamera != null) UnityEngine.Object.DestroyImmediate(guardVisibilityCamera.gameObject);
            if (guardVisibilityTexture != null)
            {
                guardVisibilityTexture.Release();
                UnityEngine.Object.DestroyImmediate(guardVisibilityTexture);
            }
        }

        private static void Movement(bool run, bool crouch)
        {
            ResetPlayer();
            SetIntent(new ActorIntent { Move = Vector2.right, Run = run, Crouch = crouch });
        }

        private static void ResetPlayer()
        {
            SetIntent(default);
            motor.SetIntent(default);
            motor.Stop();
            body.position = spawn;
            body.rotation = Quaternion.identity;
            player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
            Physics.SyncTransforms();
        }

        private static void SetIntent(ActorIntent intent)
        {
            intent.AimPoint = player.transform.position + Vector3.forward * 20;
            typeof(PlayerInputReader).GetField("_intent", Fields).SetValue(input, intent);
        }

        private static bool State(string name)
        {
            return animator.GetCurrentAnimatorStateInfo(0).IsName(name) ||
                (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(name));
        }

        private static bool Clip(string fragment)
        {
            return animator.GetCurrentAnimatorClipInfo(0).Any(c => c.weight > .1f && c.clip.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (animator.IsInTransition(0) && animator.GetNextAnimatorClipInfo(0).Any(c => c.weight > .1f && c.clip.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static string ClipNames()
        {
            return string.Join(", ", animator.GetCurrentAnimatorClipInfo(0).Where(c => c.weight > .01f).Select(c => c.clip.name + " (" + c.weight.ToString("F2") + ")"));
        }

        private static Quaternion[] Pose(Animator target)
        {
            if (target == null || !target.isHuman) return Array.Empty<Quaternion>();
            return SampleBones.Select(b => target.GetBoneTransform(b)?.localRotation ?? Quaternion.identity).ToArray();
        }

        private static float PoseDifference(Quaternion[] before, Quaternion[] after)
        {
            float largest = 0;
            for (int i = 0; i < Mathf.Min(before.Length, after.Length); i++)
                largest = Mathf.Max(largest, Quaternion.Angle(before[i], after[i]));
            return largest;
        }

        private static void MeasureSteadyPresentationAllocations()
        {
            SetIntent(default);
            MeasureCallback(presentation, "player");
            if (guards.Length > 0)
                MeasureCallback(guards[0].GetComponentInChildren<GuardAnimationController>(), "guard");
            Append("ALLOCATION LIMIT: isolated warmed presentation callbacks with unchanged current intent, measured on the Editor main thread. Excludes Animator internals, changed-input/event paths, physics, complete frames, player builds and frame-rate guarantees.");
        }

        private static void MeasureCallback(MonoBehaviour component, string description)
        {
            MethodInfo method = component.GetType().GetMethod("LateUpdate", Fields) ?? component.GetType().GetMethod("Update", Fields);
            Action tick = (Action)Delegate.CreateDelegate(typeof(Action), component, method);
            for (int i = 0; i < 32; i++) tick();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) tick();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0, description + " steady presentation callback allocated " + bytes + " managed bytes across 10,000 direct warmed invocations.");
        }

        private static void Capture(string name)
        {
            Camera source = InScene<Camera>(SceneManager.GetActiveScene()).First(c => c.enabled);
            GameObject temporary = new GameObject("Animation validation inspection camera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = temporary.AddComponent<Camera>();
            RenderTexture texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D result = null;
            try
            {
                camera.CopyFrom(source);
                camera.enabled = false;
                camera.targetTexture = texture;
                camera.aspect = 1280f / 720;
                camera.fieldOfView = 45;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 80;
                Vector3 framingCenter = new Vector3(player.transform.position.x, spawn.y, player.transform.position.z);
                camera.transform.position = framingCenter + new Vector3(4, 3, 5);
                camera.transform.LookAt(framingCenter + new Vector3(0, 1.5f, 0));
                camera.Render();
                RenderTexture.active = texture;
                result = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                result.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                result.Apply();
                string path = ArtifactRoot + "/" + SceneManager.GetActiveScene().name + "-" + name + ".png";
                File.WriteAllBytes(path, result.EncodeToPNG());
                Append("CAPTURE " + path);
            }
            finally
            {
                RenderTexture.active = previous;
                if (result != null) UnityEngine.Object.DestroyImmediate(result);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        private static void OnAcceptedJump()
        {
            acceptedJumps++;
            if ((Stage)SessionState.GetInt(StageKey, 0) != Stage.ContactRejump || acceptedJumps != jumpsBeforeStage + 2) return;
            AnimatorStateInfo previous = animator.GetCurrentAnimatorStateInfo(0);
            contactAcceptedInJump = previous.IsName("Jump");
            contactPreviousNormalizedTime = previous.normalizedTime;
            contactSecondAcceptedAt = Time.unscaledTime;
            SetIntent(default);
        }
        private static T Read<T>(object target, string name) { return (T)target.GetType().GetField(name, Fields).GetValue(target); }
        private static T[] InScene<T>(Scene scene) where T : Component { return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray(); }
        private static void Next(Stage stage) { SessionState.SetInt(StageKey, (int)stage); entered = false; }
        private static void Check(bool ok, string text) { checks++; if (!ok) failures++; Append((ok ? "PASS " : "FAIL ") + text); }
        private static void Append(string text) { File.AppendAllText(Report, text + "\n"); }

        private static void NextSceneOrComplete()
        {
            motor.Jumped -= OnAcceptedJump;
            sceneIndex++;
            if (sceneIndex >= Scenes.Length) { Complete(); return; }
            SceneManager.LoadScene(Scenes[sceneIndex]);
            Next(Stage.Boot);
        }

        private static void CaptureLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false) || type == LogType.Log) return;
            if (type == LogType.Warning) { warnings++; Append("PLAY MODE WARNING " + message); }
            else { failures++; Append("PLAY MODE ERROR " + type + ": " + message + "\n" + stack); }
        }

        private static void Complete()
        {
            finishing = true;
            ReleaseGuardInspectionCamera();
            SessionState.SetInt(StageKey, (int)Stage.Finish);
            Append("RESULT: " + checks + " checks; " + failures + " failed checks/errors; " + warnings + " Play Mode warnings.");
            Append("LIMITATIONS: injected input and temporary guard isolation, not hardware keyboard automation or a full stealth playthrough. Images are actual camera renders of live bones. The allocation sample is not a complete-frame or player-build profile. No WebGL or standalone build was performed.");
            Directory.CreateDirectory("Docs/DesertVillage/reports");
            File.Copy(Report, "Docs/DesertVillage/reports/character-animation-validation.txt", true);
            Debug.Log("Character animation validation: " + failures + " failed checks/errors; " + warnings + " warnings. See " + Report);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
