using System.Collections.Generic;
using System.IO;
using BeMyArms.M5;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>
    /// Builds the Quaternius-derived presentation bodies: the P1 and P2 skinned skins (each with a
    /// Mecanim Animator), the P2 two-bone arm IK rig (Animation Rigging), and the P2 first-person
    /// viewmodel. Everything here is presentation; the authoritative shared-body rig, hitboxes, stats
    /// and anchors are untouched and remain the M5/M6 contract.
    ///
    /// The three FBX in Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies share one
    /// 53-bone humanoid rig; the mesh was split by tools/pipeline/build-quaternius-bodies.py to
    /// enforce the locked P1 (no arms) / P2 (no head) anatomy.
    /// </summary>
    public static class M7CharacterBodyBuilder
    {
        public const string RootDir = "Assets/Art/Characters/Quaternius";
        public const string PrefabDir = RootDir + "/Prefabs";
        public const string MaterialDir = RootDir + "/Materials";
        public const string ControllerDir = RootDir + "/Controllers";

        public const string P1ModelPath = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx";
        public const string P2ModelPath = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P2_Body.fbx";
        public const string ArmsModelPath = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P2_Arms.fbx";

        public const string P1SkinPrefabPath = PrefabDir + "/BMA_P1_Clay.prefab";
        public const string P2SkinPrefabPath = PrefabDir + "/BMA_P2_Clay.prefab";
        public const string ArmsPrefabPath = PrefabDir + "/Resources/M7_P2ArmsViewmodel.prefab";

        public const string P1ControllerPath = ControllerDir + "/M7_P1_Locomotion.controller";
        public const string P2ControllerPath = ControllerDir + "/M7_P2_Locomotion.controller";
        public const string ArmsControllerPath = ControllerDir + "/M7_P2_ArmsAim.controller";

        public const string RiflePrefabPath = "Assets/Art/Weapons/Prefabs/BMA_Weapon_Rifle_Kenney.prefab";

        // The shared rig's chest is at y=1.35 and Cosmetic_P2 sits at +(0,0.13,0.06) from it, while
        // the imported model's own chest (spine.003) is at y≈1.315. The skin therefore drops its
        // model so those two chest heights coincide when mounted at the socket.
        static readonly Vector3 P2ModelOffset = new Vector3(0f, -1.445f, -0.065f);

        // First-person viewmodel: the arms model is authored with its chest at ~y=1.35, so it is
        // dropped just below the camera and pushed slightly behind it so only the arms and weapon
        // sit in front of the near clip plane.
        static readonly Vector3 ViewmodelModelOffset = new Vector3(0f, -1.435f, -0.20f);

        [MenuItem("Be My Arms/M7/Build Character Bodies")]
        public static void BuildFromMenu() => BuildAll();

        public static void BuildAll()
        {
            EnsureImportSettings();
            AssetDatabase.Refresh();
            EnsureFolders();

            BuildLocomotionController(P1ControllerPath, P1ModelPath);
            BuildLocomotionController(P2ControllerPath, P2ModelPath);
            BuildArmsAimController(ArmsControllerPath, ArmsModelPath);

            BuildP1Skin();
            BuildP2Skin();
            BuildArmsViewmodel();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[M7char] built P1/P2 skins + P2 arms viewmodel from the Quaternius rig");
        }

        /// <summary>Idempotent import settings: Generic rig, animations on, no editor-only objects.</summary>
        public static void EnsureImportSettings()
        {
            ApplyImportSettings(P1ModelPath);
            ApplyImportSettings(P2ModelPath);
            ApplyImportSettings(ArmsModelPath);
        }

        static void ApplyImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Generic) { importer.animationType = ModelImporterAnimationType.Generic; dirty = true; }
            if (!importer.importAnimation) { importer.importAnimation = true; dirty = true; }
            if (importer.importCameras) { importer.importCameras = false; dirty = true; }
            if (importer.importLights) { importer.importLights = false; dirty = true; }
            if (importer.importBlendShapes) { importer.importBlendShapes = false; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }

        static void EnsureFolders()
        {
            EnsureFolder(RootDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(ControllerDir);
        }

        // ---- Animator controllers ----

        static AnimationClip Clip(string fbxPath, string suffix)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__") &&
                    (clip.name == suffix || clip.name.EndsWith("|" + suffix)))
                    return clip;
            }
            Debug.LogWarning($"[M7char] missing clip '{suffix}' in {fbxPath}");
            return null;
        }

        static AnimatorController BuildLocomotionController(string path, string fbx)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Alive", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            var blend = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blend, controller);
            blend.AddChild(Clip(fbx, "Idle_Loop"), 0f);
            blend.AddChild(Clip(fbx, "Walk_Loop"), 0.45f);
            blend.AddChild(Clip(fbx, "Jog_Fwd_Loop"), 1.0f);
            blend.AddChild(Clip(fbx, "Sprint_Loop"), 1.6f);

            AnimatorState loco = sm.AddState("Locomotion", new Vector3(0f, 0f, 0f));
            loco.motion = blend;
            loco.writeDefaultValues = true;
            sm.defaultState = loco;

            AnimatorState jump = sm.AddState("Jump", new Vector3(260f, -60f, 0f));
            jump.motion = Clip(fbx, "Jump_Start");
            AnimatorState fall = sm.AddState("Fall", new Vector3(520f, -60f, 0f));
            fall.motion = Clip(fbx, "Jump_Loop");
            AnimatorState land = sm.AddState("Land", new Vector3(780f, -60f, 0f));
            land.motion = Clip(fbx, "Jump_Land");
            AnimatorState death = sm.AddState("Death", new Vector3(520f, 150f, 0f));
            death.motion = Clip(fbx, "Death01");

            Transition(loco, jump, 0.08f, ("Grounded", AnimatorConditionMode.IfNot, 0f), ("VerticalSpeed", AnimatorConditionMode.Greater, 0.2f));
            Transition(loco, fall, 0.08f, ("Grounded", AnimatorConditionMode.IfNot, 0f), ("VerticalSpeed", AnimatorConditionMode.Less, 0.2f));
            Transition(jump, fall, 0.1f, ("VerticalSpeed", AnimatorConditionMode.Less, 0.2f));
            Transition(fall, land, 0.02f, ("Grounded", AnimatorConditionMode.If, 0f));
            Transition(land, loco, 0.05f, 0.85f, true, ("Grounded", AnimatorConditionMode.If, 0f));
            Transition(loco, death, 0.05f, ("Alive", AnimatorConditionMode.IfNot, 0f));

            AssetDatabase.SaveAssets();
            return controller;
        }

        static AnimatorController BuildArmsAimController(string path, string fbx)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState aim = sm.AddState("Aim", new Vector3(0f, 0f, 0f));
            AnimationClip aimClip = Clip(fbx, "Pistol_Aim_Neutral") ?? Clip(fbx, "Pistol_Idle_Loop");
            aim.motion = aimClip;
            sm.defaultState = aim;

            AnimatorState shoot = sm.AddState("Shoot", new Vector3(260f, 0f, 0f));
            shoot.motion = Clip(fbx, "Pistol_Shoot") ?? aimClip;
            AnimatorState reload = sm.AddState("Reload", new Vector3(260f, 120f, 0f));
            reload.motion = Clip(fbx, "Pistol_Reload") ?? aimClip;

            Transition(aim, shoot, 0.02f, ("Shoot", AnimatorConditionMode.If, 0f));
            Transition(shoot, aim, 0.05f, 0.9f, true);
            Transition(aim, reload, 0.05f, ("Reload", AnimatorConditionMode.If, 0f));
            Transition(reload, aim, 0.1f, 0.9f, true);

            AssetDatabase.SaveAssets();
            return controller;
        }

        static void Transition(AnimatorState from, AnimatorState to, float duration, params (string param, AnimatorConditionMode mode, float threshold)[] conditions)
            => Transition(from, to, duration, 0f, false, conditions);

        static void Transition(AnimatorState from, AnimatorState to, float duration, float exitTime, bool hasExitTime, params (string param, AnimatorConditionMode mode, float threshold)[] conditions)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = hasExitTime;
            t.exitTime = exitTime;
            t.duration = duration;
            t.hasFixedDuration = true;
            for (int i = 0; i < conditions.Length; i++)
                t.AddCondition(conditions[i].mode, conditions[i].threshold, conditions[i].param);
        }

        // ---- Skins ----

        static void BuildP1Skin()
        {
            GameObject root = FreshSkinRoot("BMA_P1_Clay", M5RigRole.P1, "clay_p1");
            GameObject model = AttachModel(P1ModelPath, root.transform, Vector3.zero);
            ConfigureSkinMaterials(model, new Color(0.86f, 0.82f, 0.74f), new Color(0.68f, 0.62f, 0.55f));
            AddAnimator(model, P1ControllerPath);
            Save(root, P1SkinPrefabPath);
        }

        static void BuildP2Skin()
        {
            GameObject root = FreshSkinRoot("BMA_P2_Clay", M5RigRole.P2, "clay_p2");
            GameObject model = AttachModel(P2ModelPath, root.transform, P2ModelOffset);
            ConfigureSkinMaterials(model, new Color(0.30f, 0.36f, 0.44f), new Color(0.22f, 0.27f, 0.34f));
            Animator animator = AddAnimator(model, P2ControllerPath);
            AddArmRig(model, animator, "Rig");
            Save(root, P2SkinPrefabPath);
        }

        static void BuildArmsViewmodel()
        {
            var root = new GameObject("BMA_P2_Arms");
            GameObject model = AttachModel(ArmsModelPath, root.transform, ViewmodelModelOffset);
            ConfigureSkinMaterials(model, new Color(0.30f, 0.36f, 0.44f), new Color(0.22f, 0.27f, 0.34f));
            Animator animator = AddAnimator(model, ArmsControllerPath);
            AddArmRig(model, animator, "Rig");

            var aimPivot = new GameObject("AimPivot");
            aimPivot.transform.SetParent(root.transform, false);
            aimPivot.transform.localPosition = new Vector3(0f, -0.12f, -0.05f);
            aimPivot.transform.localRotation = Quaternion.identity;

            GameObject weapon = InstantiatePrefab(RiflePrefabPath, aimPivot.transform);
            if (weapon != null)
            {
                weapon.name = "ViewmodelWeapon";
                weapon.transform.localPosition = new Vector3(0.10f, -0.02f, 0.38f);
                weapon.transform.localRotation = Quaternion.identity;
                weapon.transform.localScale = Vector3.one * 0.8f;
            }

            Transform gripR = Marker(aimPivot.transform, "HandTarget_R", new Vector3(0.11f, -0.07f, 0.12f));
            Transform gripL = Marker(aimPivot.transform, "HandTarget_L", new Vector3(0.02f, -0.03f, 0.40f));
            TwoBoneIKConstraint ikL = FindConstraint(model, "ArmIK_L");
            TwoBoneIKConstraint ikR = FindConstraint(model, "ArmIK_R");
            if (ikL != null) ikL.data.target = gripL;
            if (ikR != null) ikR.data.target = gripR;

            Save(root, ArmsPrefabPath);
        }

        static Transform Marker(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        static TwoBoneIKConstraint FindConstraint(GameObject root, string name)
        {
            foreach (TwoBoneIKConstraint c in root.GetComponentsInChildren<TwoBoneIKConstraint>(true))
                if (c.gameObject.name == name) return c;
            return null;
        }

        static GameObject FreshSkinRoot(string name, M5RigRole role, string skinId)
        {
            var root = new GameObject(name);
            var descriptor = root.AddComponent<M5SkinDescriptor>();
            descriptor.Role = role;
            descriptor.SkinId = skinId;
            descriptor.RigId = "quaternius";
            return root;
        }

        static GameObject AttachModel(string modelPath, Transform parent, Vector3 localPosition)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null) throw new FileNotFoundException("missing model: " + modelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Model";
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            // The Blender-exported rig faces -Z; rotate it so the fighter faces the Unity forward
            // axis the shared-body simulation drives.
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        static Animator AddAnimator(GameObject model, string controllerPath)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            return animator;
        }

        /// <summary>Two-bone IK (shoulder → elbow → hand) with placeholder targets that the runtime
        /// re-binds to the body's grip transforms.</summary>
        static void AddArmRig(GameObject model, Animator animator, string rigName)
        {
            var rigGo = new GameObject(rigName);
            rigGo.transform.SetParent(model.transform, false);
            var rig = rigGo.AddComponent<Rig>();

            CreateTwoBone(rigGo.transform, "ArmIK_L", model, "DEF-upper_arm.L", "DEF-forearm.L", "DEF-hand.L");
            CreateTwoBone(rigGo.transform, "ArmIK_R", model, "DEF-upper_arm.R", "DEF-forearm.R", "DEF-hand.R");

            var builder = model.GetComponent<RigBuilder>();
            if (builder == null) builder = model.AddComponent<RigBuilder>();
            builder.layers.Clear();
            builder.layers.Add(new RigLayer(rig, true));
        }

        static void CreateTwoBone(Transform rigParent, string name, GameObject model, string rootBone, string midBone, string tipBone)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rigParent, false);
            var constraint = go.AddComponent<TwoBoneIKConstraint>();
            ref TwoBoneIKConstraintData data = ref constraint.data;
            data.root = Find(model.transform, rootBone);
            data.mid = Find(model.transform, midBone);
            data.tip = Find(model.transform, tipBone);
            data.target = go.transform; // rebound at runtime to the actual grip
            data.hint = null;
            data.targetPositionWeight = 1f;
            data.targetRotationWeight = 1f;
            constraint.weight = 1f;
        }

        static void ConfigureSkinMaterials(GameObject model, Color main, Color joint)
        {
            Material mainMat = GetOrCreateMaterial("BMA_Clay_Body", main, 0.22f);
            Material jointMat = GetOrCreateMaterial("BMA_Clay_Joint", joint, 0.35f);
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] source = renderer.sharedMaterials;
                var materials = new Material[source.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    string n = source[i] != null ? source[i].name : "";
                    materials[i] = n.IndexOf("Joint", System.StringComparison.OrdinalIgnoreCase) >= 0 ? jointMat : mainMat;
                }
                if (materials.Length == 0) materials = new[] { mainMat };
                renderer.sharedMaterials = materials;
                renderer.updateWhenOffscreen = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        static Material GetOrCreateMaterial(string name, Color color, float smoothness)
        {
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.shader != shader) material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject InstantiatePrefab(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            return instance;
        }

        static void Save(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
