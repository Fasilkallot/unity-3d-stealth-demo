#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace EvidenceRun.Editor
{
    /// <summary>Builds the desert mission for WebGL and reports its scene budget.</summary>
    public static class DesertVillageBuild
    {
        private const string OutputPath = "Builds/DesertVillageWebGL";
        private const string BuildReportPath = "Logs/desert-webgl-build.txt";
        private const string BudgetReportPath = "Logs/desert-scene-budget.txt";
        private static readonly string[] RequiredScenes =
        {
            "Assets/_Project/Scenes/MainMenu.unity",
            "Assets/_Project/Scenes/EvidenceRun.unity",
            "Assets/_Project/Scenes/EvidenceRun_DesertVillage.unity"
        };

        [MenuItem("Evidence Run/Desert Village/Build WebGL")]
        public static void BuildWebGL()
        {
            Directory.CreateDirectory("Logs");
            var text = new StringBuilder();
            text.AppendLine("DESERT VILLAGE WEBGL BUILD");
            AppendEnvironment(text);
            text.AppendLine("Existing PlayerSettings, QualitySettings, packages and compression settings are preserved.");
            text.AppendLine("Build output: " + Path.GetFullPath(OutputPath));
            File.WriteAllText(BuildReportPath, text.ToString());

            try
            {
                RequireEditMode();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Save modified scenes before building their on-disk versions.");
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                    throw new InvalidOperationException("This Unity installation does not include WebGL Build Support.");

                // Use an explicit list so the menu is the entry point and both missions are available,
                // while retaining every other enabled scene and leaving Build Settings untouched.
                var scenePaths = new List<string> { RequiredScenes[0] };
                foreach (string path in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).Concat(RequiredScenes))
                    if (!scenePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                        scenePaths.Add(path);
                foreach (string path in scenePaths)
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                        throw new InvalidOperationException("Build scene is missing: " + path);
                text.AppendLine("Scenes:");
                foreach (string path in scenePaths) text.AppendLine("  " + path);
                text.AppendLine("Compression: " + PlayerSettings.WebGL.compressionFormat);
                text.AppendLine("Decompression fallback: " + PlayerSettings.WebGL.decompressionFallback);
                text.AppendLine("Status: building");
                File.WriteAllText(BuildReportPath, text.ToString());

                Directory.CreateDirectory(OutputPath);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenePaths.ToArray(),
                    locationPathName = OutputPath,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None
                });
                if (report == null)
                    throw new InvalidOperationException("Unity returned no BuildReport.");
                BuildSummary summary = report.summary;
                text.AppendLine("Result: " + summary.result);
                text.AppendLine("Errors: " + summary.totalErrors);
                text.AppendLine("Warnings: " + summary.totalWarnings);
                text.AppendLine("Total size (bytes): " + summary.totalSize);
                text.AppendLine("Total time: " + summary.totalTime);
                text.AppendLine("Reported output: " + summary.outputPath);
                foreach (BuildStep step in report.steps)
                    foreach (BuildStepMessage message in step.messages)
                        if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning)
                            text.AppendLine(message.type + " [" + step.name + "] " + message.content);
                text.AppendLine("A successful build is not a browser gameplay or frame-rate measurement.");
                File.WriteAllText(BuildReportPath, text.ToString());
                if (summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("WebGL build " + summary.result + "; see " + BuildReportPath);
                Debug.Log("WebGL build succeeded. Report: " + BuildReportPath + "; output: " + OutputPath);
            }
            catch (Exception exception)
            {
                text.AppendLine("Build command failure: " + exception);
                File.WriteAllText(BuildReportPath, text.ToString());
                throw;
            }
        }

        [MenuItem("Evidence Run/Desert Village/Collect Scene Budget")]
        public static void CollectBudget()
        {
            RequireEditMode();
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) || scene.isDirty)
                throw new InvalidOperationException("Open and save the scene before collecting its budget.");

            Renderer[] renderers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).ToArray();
            var materials = new HashSet<Material>();
            var meshes = new HashSet<Mesh>();
            long triangleInstances = 0;
            int staticRenderers = 0;
            int instancingEligible = 0;
            int missingMaterialSlots = 0;
            var details = new StringBuilder();
            foreach (Renderer renderer in renderers)
            {
                Material[] slots = renderer.sharedMaterials;
                foreach (Material material in slots)
                    if (material != null) materials.Add(material);
                    else missingMaterialSlots++;
                bool batchingStatic = GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                if (batchingStatic) staticRenderers++;
                bool instancing = renderer is MeshRenderer && slots.Length > 0 && slots.All(m => m != null && m.enableInstancing);
                if (instancing) instancingEligible++;
                Mesh mesh = MeshFor(renderer);
                long triangles = TriangleCount(mesh);
                triangleInstances += triangles;
                if (mesh != null) meshes.Add(mesh);
                details.AppendLine($"  {ObjectPath(renderer.transform)} | type={renderer.GetType().Name} | active={renderer.gameObject.activeInHierarchy && renderer.enabled} | triangles={triangles} | static={batchingStatic} | instancing materials={instancing} | materials={slots.Length} | shadows={renderer.shadowCastingMode}");
            }

            var text = new StringBuilder();
            text.AppendLine("DESERT VILLAGE SCENE INVENTORY");
            AppendEnvironment(text);
            text.AppendLine("Saved scene: " + scene.path);
            text.AppendLine("Renderers including inactive objects/all LOD levels: " + renderers.Length);
            text.AppendLine("Enabled renderers on active objects: " + renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy));
            text.AppendLine("MeshRenderers: " + renderers.Count(r => r is MeshRenderer));
            text.AppendLine("SkinnedMeshRenderers: " + renderers.Count(r => r is SkinnedMeshRenderer));
            text.AppendLine("Unique meshes: " + meshes.Count);
            text.AppendLine("Unique shared materials: " + materials.Count);
            text.AppendLine("Missing material slots: " + missingMaterialSlots);
            text.AppendLine("Triangle instances (all LODs/inactive objects): " + triangleInstances);
            text.AppendLine("Unique mesh triangles: " + meshes.Sum(TriangleCount));
            text.AppendLine("Renderers marked BatchingStatic: " + staticRenderers);
            text.AppendLine("MeshRenderers whose material slots all enable instancing: " + instancingEligible);
            text.AppendLine("Materials enabling instancing: " + materials.Count(m => m.enableInstancing));
            text.AppendLine("This inventory is not a draw-call count, visible-triangle count or FPS profile. It includes all LODs, excludes runtime-generated meshes/pools, and does not prove batching occurs.");
            text.AppendLine("Shared materials:");
            foreach (Material material in materials.OrderBy(m => AssetDatabase.GetAssetPath(m)).ThenBy(m => m.name))
                text.AppendLine($"  {material.name} | asset={AssetDatabase.GetAssetPath(material)} | shader={(material.shader != null ? material.shader.name : "MISSING")} | instancing={material.enableInstancing}");
            text.AppendLine("Renderer inventory:");
            text.Append(details);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(BudgetReportPath, text.ToString());
            Debug.Log("Scene budget saved: " + BudgetReportPath);
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for compilation and exit Play Mode before running this command.");
        }

        private static Mesh MeshFor(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skin) return skin.sharedMesh;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static long TriangleCount(Mesh mesh)
        {
            if (mesh == null) return 0;
            long total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                    total += (long)mesh.GetIndexCount(i) / 3;
            return total;
        }

        private static string ObjectPath(Transform transform)
        {
            return transform.parent == null ? transform.name : ObjectPath(transform.parent) + "/" + transform.name;
        }

        private static void AppendEnvironment(StringBuilder text)
        {
            text.AppendLine("Recorded UTC: " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            text.AppendLine("Unity: " + Application.unityVersion);
            text.AppendLine("Host OS: " + SystemInfo.operatingSystem);
            text.AppendLine("Host CPU: " + SystemInfo.processorType + "; logical processors=" + SystemInfo.processorCount);
            text.AppendLine("Host RAM (MB): " + SystemInfo.systemMemorySize);
            text.AppendLine("Editor GPU: " + SystemInfo.graphicsDeviceName + "; vendor=" + SystemInfo.graphicsDeviceVendor);
            text.AppendLine("Editor graphics API: " + SystemInfo.graphicsDeviceType + "; " + SystemInfo.graphicsDeviceVersion);
            text.AppendLine("GPU memory (MB): " + SystemInfo.graphicsMemorySize);
            text.AppendLine("Editor supports instancing: " + SystemInfo.supportsInstancing);
            text.AppendLine("Render pipeline: " + (GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.name));
            text.AppendLine("Editor quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            text.AppendLine("Host/editor hardware above is not representative-browser performance evidence.");
        }
    }
}
#endif
