using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvidenceRun.Bootstrap;
using EvidenceRun.Gameplay.Actors;
using EvidenceRun.Gameplay.Guards;
using EvidenceRun.Gameplay.Player;
using EvidenceRun.Presentation.Camera;
using EvidenceRun.Presentation.HUD;
using EvidenceRun.Presentation.VisionCone;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EvidenceRun.Editor
{
    /// <summary>Builds the desert village scene from the existing gameplay setup and asset pack.</summary>
    public static class DesertVillageBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/EvidenceRun_DesertVillage.unity";
        const string SourceScene = "Assets/_Project/Scenes/EvidenceRun.unity";
        const string Pack = "Assets/LowPolyShooterPack/Prefabs/";
        const string Materials = "Assets/_Project/Materials/DesertVillage";
        static Material atlas, sand, plaster, dark, teal, ochre;
        static Transform buildings, props, environment, objectives;

        [MenuItem("Evidence Run/Desert Village/Create or rebuild scene")]
        public static void CreateFromMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild Desert Village",
                    "Replace the generated desert scene? Manual edits to that scene will be lost. The original EvidenceRun scene is never modified.", "Rebuild", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save or close modified scenes before generating.");
            // Copy the source on disk before touching any hierarchy; retain its explicitly wired HUD and systems.
            File.Copy(SourceScene, ScenePath, true);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var boot = InScene<GameBootstrap>(scene).Single();
            var bs = new SerializedObject(boot);
            var loop = Ref<GameLoop>(bs, "gameLoop");
            var hud = Ref<GameHUD>(bs, "gameHUD");
            var camera = InScene<Camera>(scene).Single(c => c.CompareTag("MainCamera"));
            var canvas = hud.GetComponentInParent<Canvas>() ?? InScene<Canvas>(scene).Single();
            var events = InScene<EventSystem>(scene).Single();
            var projectileRoot = Ref<Transform>(bs, "projectilePoolRoot");
            var impactRoot = Ref<Transform>(bs, "impactEffectPoolRoot");
            var evidence = Ref<Transform>(bs, "evidenceTarget");
            // Extract referenced gameplay objects before removing template environment and actors.
            var gameplay = Group("Gameplay");
            foreach (var t in new[] { boot.transform, loop.transform, hud.transform, canvas.transform, events.transform,
                         camera.transform, projectileRoot, impactRoot })
                if (t != canvas.transform && t.IsChildOf(canvas.transform)) continue;
                else t.SetParent(gameplay, true);
            evidence.SetParent(null, true);
            foreach (var root in scene.GetRootGameObjects())
                if (root != gameplay.gameObject && root != evidence.gameObject) Object.DestroyImmediate(root);

            environment = Group("Environment");
            buildings = Group("Buildings");
            props = Group("Props");
            objectives = Group("Objectives");
            var guardRoot = Group("Guards");
            var lighting = Group("Lighting");
            CreateMaterials();
            CreateGroundAndBoundary();
            CreateVillage();
            DressCoverAndRoutes();
            SetLighting(lighting);

            var playerObject = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"), scene);
            playerObject.name = "Player - southern approach";
            playerObject.transform.SetParent(gameplay);
            playerObject.transform.SetPositionAndRotation(new Vector3(0, 1.05f, -22), Quaternion.identity);
            var player = playerObject.GetComponent<PlayerController>();
            Set(playerObject.GetComponent<PlayerInputReader>(), "playerCamera", camera);
            ConfigureGroundCast(playerObject.GetComponent<ActorMotor>());
            var rig = camera.GetComponent<ThirdPersonCamera>();
            Set(rig, "target", player.transform);
            Set(rig, "distance", 4.3f); Set(rig, "height", 1.1f);
            camera.farClipPlane = 125;
            camera.nearClipPlane = .12f;
            camera.allowHDR = false;
            camera.transform.position = new Vector3(0, 3.25f, -26.15f);
            camera.transform.LookAt(player.transform.position + Vector3.up * 1.1f);

            var guards = new[]
            {
                CreateGuard("01 - checkpoint patrol", guardRoot, player.transform, new [] { V(0,-9), V(0,2) }),
                CreateGuard("02 - market crossing patrol", guardRoot, player.transform, new [] { V(-22,7), V(-3,7) }),
                CreateGuard("03 - records courtyard patrol", guardRoot, player.transform, new [] { V(2.4f,11), V(2.4f,21) })
            };
            evidence.name = "Evidence - records laptop (pickup hides this group)";
            evidence.SetParent(objectives, true);
            evidence.position = new Vector3(0, 1, 18.7f);
            // Keep the original laptop and its materials; normalize the imported visual to a readable desktop size.
            var visual = evidence.GetChild(0);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.Euler(0, 180, 0);
            var laptopBounds = BoundsOf(visual.gameObject);
            visual.localScale *= 1.05f / Mathf.Max(laptopBounds.size.x, laptopBounds.size.z);
            laptopBounds = BoundsOf(visual.gameObject);
            visual.position += new Vector3(-laptopBounds.center.x, .83f - laptopBounds.min.y, 18.7f - laptopBounds.center.z);
            foreach (var c in visual.GetComponentsInChildren<Collider>()) c.enabled = false;
            var desk = Place("Props/Table02", "Records desk", 0, 19, 90, Vector3.one);
            desk.transform.position += Vector3.down * .04f;
            Marker("Evidence signal", evidence, new Vector3(0, 1.9f, 0), teal);
            Sign("Records compound sign", "03  /  RECORDS", new Vector3(0,3.3f,22.9f), 0, 4.8f, ochre);
            Sign("Evidence instruction", "RETRIEVE THE LAPTOP", new Vector3(0,2.8f,20.7f), 0, 4.2f, dark);

            var extraction = Group("Extraction - return with evidence", objectives);
            extraction.position = new Vector3(0, 1, -22);
            for (int i = -1; i <= 1; i += 2)
            {
                Cube("Extraction edge", objectives, new Vector3(i*2, .025f, -22), new Vector3(.13f,.035f,4), teal, false);
                Cube("Extraction edge", objectives, new Vector3(0, .025f, -22+i*2), new Vector3(4,.035f,.13f), teal, false);
            }
            Sign("Extraction sign", "01  /  EXTRACTION", new Vector3(-4.6f,2.8f,-23), 0, 4.2f, teal);
            Place("Props/Flagpole02", "Extraction rally flag", -4.7f,-22,90,new Vector3(.65f,.65f,.65f));
            Set(boot, "playerController", player);
            Set(boot, "evidenceTarget", evidence);
            Set(boot, "extractionTarget", extraction);
            bs.Update();
            var gs = bs.FindProperty("guards"); gs.arraySize = guards.Length;
            for (int i = 0; i < guards.Length; i++) gs.GetArrayElementAtIndex(i).objectReferenceValue = guards[i];
            bs.ApplyModifiedPropertiesWithoutUndo();
            AddMissionLegend(canvas.transform);

            Physics.SyncTransforms();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0,0,4), Quaternion.Euler(52,0,0), 48);
            Debug.Log("Desert village saved: " + ScenePath);
        }

        static void CreateMaterials()
        {
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            atlas = MaterialAsset("Village Atlas", Color.white);
            var source = Load<Material>("Assets/LowPolyShooterPack/Materials/Atlas.mat");
            atlas.CopyPropertiesFromMaterial(source); atlas.enableInstancing = true;
            atlas.SetFloat("_Metallic", 0); atlas.SetFloat("_Glossiness", 0);
            sand = MaterialAsset("Warm Sand", new Color(.72f,.60f,.43f));
            sand.mainTexture = Load<Texture2D>("Assets/LowPolyShooterPack/Textures/Sand.png");
            sand.mainTextureScale = new Vector2(18,18);
            plaster = MaterialAsset("Dusty Plaster", new Color(.58f,.43f,.28f));
            dark = MaterialAsset("Military Charcoal", new Color(.12f,.17f,.15f));
            teal = MaterialAsset("Extraction Teal", new Color(.17f,.68f,.59f));
            ochre = MaterialAsset("Records Ochre", new Color(.86f,.40f,.13f));
        }
        static Material MaterialAsset(string name, Color color)
        {
            string path = Materials + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
            m.color = color; m.SetFloat("_Glossiness",0); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        static void CreateGroundAndBoundary()
        {
            Cube("Village sand foundation", environment, new Vector3(0,-.4f,3),new Vector3(62,.8f,64),sand);
            // Long perimeter silhouettes use scaled pack walls, with their original collision mesh.
            foreach (float x in new [] {-29f,29f})
                foreach (float z in new [] {-21f,-6f,9f,24f})
                    Place("Props/SandWall01", "Perimeter sand wall", x,z,0,new Vector3(2,2.25f,4));
            foreach (float z in new [] {-28f,33f})
                foreach (float x in new [] {-22f,-7.5f,7.5f,22f})
                    Place("Props/SandWall01", "Perimeter sand wall",x,z,90,new Vector3(2,2.25f,4));
            // Dunes remain outside usable routes; no micro-colliders under the player.
            var terrain = Group("Dunes and windblown sand", environment);
            for (int i=0;i<14;i++)
            {
                float x = i%2==0 ? -26.5f : 26.5f;
                var dune=Place("Props/Sand","Windblown dune",x,-22+i*4, i*37,new Vector3(2.2f,2.2f,2.2f),false);
                dune.transform.SetParent(terrain,true);
            }
            Cube("Outer desert horizon",environment,new Vector3(0,-1.2f,0),new Vector3(220,.6f,220),sand,false);
        }
        static void CreateVillage()
        {
            Building("Building01", "Entry house / west", -9,-11,0);
            Building("Building02", "Entry house / east", 9,-10,180);
            Building("Building03", "Market / west", -8,-1,0);
            Building("Building01", "Market / east", 10,2,90);
            Building("Building08", "Damaged west residence", -9,13,0);
            Building("Building04", "East compound", 10,15,180);
            Building("Building02", "Records headquarters", 0,26,180);
            Building("Building02", "West courtyard house",-21,3,0);
            Building("Building01", "Western alley house",-21,-11,0);
            Building("Building01", "Western courtyard ruin",-21,18,180);
            Building("Building03", "Eastern stores",22,3,180);
            Building("Building08", "Abandoned eastern block",22,-12,180);
            Building("Building06", "Eastern watchtower",22,23,0);
            Building("Building07", "Western lookout",-21,27,180);
            Place("Props/MarketplaceTop01","Abandoned market canopy",-9,4.4f,0,Vector3.one);
            Place("Props/MarketplaceTop02","East market canopy",10,6.2f,180,Vector3.one);
            Place("Props/Flagpole01","Records compound flag",4,24,180,new Vector3(.85f,.85f,.85f));
        }
        static void DressCoverAndRoutes()
        {
            // Tall stacked supply crates provide actual sight occlusion; low barriers are traversal choices.
            Crates(-3.8f,-17,2); Crates(4,-4.8f,2); Crates(-13.9f,-5.8f,2);
            Crates(-15.2f,12,2); Crates(4.2f,20.5f,2); Crates(17,-19,2);
            Place("Props/RuinedCar","Checkpoint wreck",5,-17,65,Vector3.one);
            Place("Props/MetalBarrier02","Checkpoint barricade",-5,-18,90,Vector3.one);
            Place("Props/SandWall01","Checkpoint cover",-7.5f,-17,90,new Vector3(1.3f,1.7f,1));
            Place("Props/SandWall03","Optional jump barrier",-14.6f,-15.8f,90,new Vector3(1,1,1.7f));
            Place("Props/SandWall01","East lane cover",16,-4.5f,90,new Vector3(1.3f,1.7f,1.4f));
            Place("Props/SandWall01","Records west screen",-4.8f,19,0,new Vector3(1.2f,1.8f,1));
            Place("Props/SandWall01","Records east screen",4.8f,17,0,new Vector3(1.2f,1.8f,1));
            Place("Props/SandWall04","Damaged courtyard wall",-17,21,90,new Vector3(1.3f,1.3f,2));
            Place("Props/Well","Dry courtyard well",-20,11,0,Vector3.one);
            Place("Props/Table01","Abandoned stall",-9,4.2f,90,Vector3.one);
            Place("Props/Sack01","Market sacks",-11,4.3f,20,Vector3.one);
            Place("Props/Sack03","Market supplies",-10.8f,3.3f,0,new Vector3(.8f,.8f,.8f));
            Place("Props/Barrel01","Water drums",-17,-11,0,Vector3.one);
            Place("Props/Barrel02","Checkpoint fuel drum",7,-18,0,Vector3.one);
            Place("Props/Barrel03","East store drum",17,8.8f,0,Vector3.one);
            Place("Props/Fuel","Fuel can",6,-18.5f,15,Vector3.one);
            Place("Props/GasCylinder03","Abandoned cylinder",12.7f,1,0,Vector3.one);
            Place("Props/Newspapers","Scattered records",1.2f,20,15,Vector3.one,false);
            Place("Props/ClothesLine","Abandoned washing line",-16,-1,0,Vector3.one,false);
            foreach (var p in new [] {new Vector2(-25,-19),new Vector2(-25,23),new Vector2(25,28),new Vector2(26,-21),new Vector2(18,27),new Vector2(-24,-4)})
                Place("Trees&Plants/Plant11B (1)","Sparse dry scrub",p.x,p.y,p.x*11, new Vector3(.7f,.7f,.7f),false);
            Place("Trees&Plants/Tree7A","Courtyard palm",-24,11,0,new Vector3(.65f,.65f,.65f),false);
            Place("Trees&Plants/Tree7A","Eastern palm",25,14,40,new Vector3(.7f,.7f,.7f),false);
            Sign("Village checkpoint sign", "02  /  VILLAGE", new Vector3(5.7f,2.8f,-16),0,3.7f,dark);
            Sign("West alley route sign", "MARKET  /  WEST", new Vector3(-12.5f,2.7f,-14.2f),0,3.2f,dark);
        }
        static void Crates(float x,float z,int levels)
        {
            for(int level=0;level<levels;level++)
                for(int column=0;column<2;column++)
                {
                    var go=Place("Props/Box05","Stacked supply cover",x+column*1.18f,z,0,new Vector3(1.15f,1.3f,1.15f));
                    go.transform.position+=Vector3.up*level*1.3f;
                }
        }
        static void Building(string prefab,string name,float x,float z,float yaw)
        { var go=Place("Buildings/"+prefab,name,x,z,yaw,Vector3.one);go.transform.SetParent(buildings,true); }
        static GameObject Place(string prefab,string name,float x,float z,float yaw,Vector3 scale,bool collision=true)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Pack+prefab+".prefab"));
            go.name=name;go.transform.SetParent(props);go.transform.SetPositionAndRotation(new Vector3(x,0,z),Quaternion.Euler(0,yaw,0));
            go.transform.localScale=scale;
            var b=BoundsOf(go);go.transform.position+=Vector3.up*-b.min.y;
            foreach(var c in go.GetComponentsInChildren<Collider>()) { c.gameObject.layer=3; c.enabled=collision; }
            foreach(var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats=r.sharedMaterials;
                for(int i=0;i<mats.Length;i++) if(mats[i]!=null && AssetDatabase.GetAssetPath(mats[i])=="Assets/LowPolyShooterPack/Materials/Atlas.mat") mats[i]=atlas;
                r.sharedMaterials=mats;
                if(!collision) r.shadowCastingMode=ShadowCastingMode.Off;
                r.reflectionProbeUsage=ReflectionProbeUsage.Off;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
            }
            return go;
        }
        static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool collision=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);
            go.transform.position=position;go.transform.localScale=size;go.layer=3;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision) {Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;}
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
            return go;
        }
        static void Sign(string name,string label,Vector3 pos,float yaw,float width,Material material)
        {
            var board=Cube(name,props,pos,new Vector3(width,.65f,.10f),material,false);
            board.transform.rotation=Quaternion.Euler(0,yaw,0);
            var text=new GameObject("Lettering").AddComponent<TextMeshPro>();
            text.transform.SetParent(board.transform,false);
            text.transform.localPosition=new Vector3(0,0,-.65f);
            text.transform.localScale=new Vector3(1/width,1/.65f,10);
            text.rectTransform.sizeDelta=new Vector2(width-.2f,.5f);text.fontSize=2.5f;
            text.alignment=TextAlignmentOptions.Center;text.color=new Color(.95f,.91f,.77f);text.text=label;
            text.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            Cube(name+" post",props,new Vector3(pos.x,pos.y/2,pos.z+.12f),new Vector3(.10f,pos.y,.10f),dark,false);
        }
        static void Marker(string name,Transform parent,Vector3 local,Material material)
        {
            var go=Cube(name,parent,parent.position+local,new Vector3(.3f,.3f,.3f),material,false);
            go.transform.rotation=Quaternion.Euler(0,45,45);
            GameObjectUtility.SetStaticEditorFlags(go,0); // The pickup visual must disappear as one group.
        }
        static Guard CreateGuard(string name,Transform parent,Transform target,Vector3[] points)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/_Project/Prefabs/Guard/Guard.prefab"));
            go.name=name;go.transform.SetParent(parent);go.transform.position=points[0];
            go.transform.rotation=Quaternion.LookRotation(points[1]-points[0]);
            var guard=go.GetComponent<Guard>();Set(guard,"target",target);
            var route=Group(name+" - waypoints",parent);
            var so=new SerializedObject(guard);var wp=so.FindProperty("patrolWaypoints");wp.arraySize=points.Length;
            for(int i=0;i<points.Length;i++){var p=Group("Patrol "+(i+1),route);p.position=points[i];wp.GetArrayElementAtIndex(i).objectReferenceValue=p;}
            so.ApplyModifiedPropertiesWithoutUndo();
            ConfigureGroundCast(go.GetComponent<ActorMotor>());
            foreach(var cone in go.GetComponentsInChildren<VisionConeRenderer>()) {Set(cone,"rayCount",24);Set(cone,"updateInterval",.05f);}
            return guard;
        }
        static void ConfigureGroundCast(ActorMotor motor)
        { Set(motor,"groundCheckOffset",-.55f);Set(motor,"groundCheckDistance",.30f);Set(motor,"groundLayers",8); }
        static Vector3 V(float x,float z)=>new Vector3(x,1.02f,z);
        static void SetLighting(Transform parent)
        {
            var sun=new GameObject("Desert sun").AddComponent<Light>();sun.transform.SetParent(parent);
            sun.type=LightType.Directional;sun.color=new Color(1,.94f,.82f);sun.intensity=.95f;
            sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.shadows=LightShadows.Hard;
            sun.shadowStrength=.65f;sun.shadowBias=.04f;sun.shadowNormalBias=.25f;
            sun.shadowCustomResolution=1024;
            RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.58f,.65f,.69f);
            RenderSettings.ambientEquatorColor=new Color(.62f,.54f,.42f);
            RenderSettings.ambientGroundColor=new Color(.34f,.28f,.21f);
            RenderSettings.ambientIntensity=1;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=new Color(.73f,.70f,.60f);RenderSettings.fogStartDistance=65;RenderSettings.fogEndDistance=160;
            var skyPath=Materials+"/Desert Sky.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if(sky==null){sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,skyPath);}
            sky.SetColor("_SkyTint",new Color(.58f,.62f,.65f));sky.SetColor("_GroundColor",new Color(.65f,.52f,.35f));
            sky.SetFloat("_AtmosphereThickness",.75f);sky.SetFloat("_Exposure",1.05f);
            RenderSettings.skybox=sky;RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity=0;EditorUtility.SetDirty(sky);
        }
        static void AddMissionLegend(Transform canvas)
        {
            var go=new GameObject("Desert mission guide",typeof(RectTransform));go.transform.SetParent(canvas,false);
            var label=go.AddComponent<TextMeshProUGUI>();label.text="DESERT VILLAGE  /  EVIDENCE RUN\n<color=#D6BE8F>Reach the records courtyard. Return to the teal extraction marker.</color>\n<size=17>WASD Move   SHIFT Run   CTRL Crouch   SPACE Jump   G Throw   Hold LMB + drag Look</size>";
            label.fontSize=21;label.color=new Color(.95f,.92f,.82f);label.raycastTarget=false;
            var rt=label.rectTransform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,0);
            rt.anchoredPosition=new Vector2(28,20);rt.sizeDelta=new Vector2(1120,100);
        }
        public static void CapturePreviews()
        {
            Directory.CreateDirectory("Docs/DesertVillage");
            Capture("overview",new Vector3(42,52,-50),new Vector3(0,0,5),false);
            Capture("entry",new Vector3(0,3.25f,-26),new Vector3(0,2,-9),false);
            Capture("records",new Vector3(-3,3,14),new Vector3(0,1.5f,21),false);
        }
        static void Capture(string name,Vector3 position,Vector3 target,bool orthographic)
        {
            var cam=new GameObject("Temporary preview camera").AddComponent<Camera>();
            cam.transform.position=position;cam.transform.LookAt(target);cam.fieldOfView=56;cam.farClipPlane=180;cam.orthographic=orthographic;
            var rt=new RenderTexture(1600,1000,24);cam.targetTexture=rt;cam.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();File.WriteAllBytes("Docs/DesertVillage/"+name+".png",texture.EncodeToPNG());
            RenderTexture.active=previous;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);Object.DestroyImmediate(cam.gameObject);
        }
        static Bounds BoundsOf(GameObject go)
        {var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length==0)return new Bounds(go.transform.position,Vector3.zero);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        static Transform Group(string name,Transform parent=null){var t=new GameObject(name).transform;t.SetParent(parent);return t;}
        static T Load<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing asset: "+path);
        static IEnumerable<T> InScene<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true));
        static T Ref<T>(SerializedObject so,string field) where T:Object=>so.FindProperty(field).objectReferenceValue as T??throw new InvalidOperationException("Missing source reference "+field);
        static void Set(Object target,string field,Object value){var s=new SerializedObject(target);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        static void Set(Object target,string field,float value){var s=new SerializedObject(target);s.FindProperty(field).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        static void Set(Object target,string field,int value){var s=new SerializedObject(target);s.FindProperty(field).intValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    }
}
