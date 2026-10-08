using System;
using System.IO;
using BeMyArms.Product;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>Reproducible shared-rig presentation. No gameplay geometry is authored here.</summary>
    public static class FighterBuilder
    {
        public const string Source = "Assets/Art/Characters/SharedRig/Models";
        public const string Root = "Assets/Art/Characters/SharedRig";
        public const string P1SkinPath = Root + "/Fighter_P1.prefab";
        public const string P2SkinPath = Root + "/Fighter_P2.prefab";
        public const string RiflePath = "Assets/Art/Weapons/Prefabs/BMA_Weapon_Rifle_View.prefab";
        public const string ArmsPath = Root + "/Resources/P2ArmsViewmodel.prefab";

        public static void BuildAll()
        {
            SmoothFighterBuilder.BuildAll();
        }

        static void Import(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new FileNotFoundException(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = true;
            importer.importCameras = importer.importLights = false;
            importer.importBlendShapes = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = c.takeName.Substring(c.takeName.LastIndexOf('|') + 1);
                c.loopTime = c.loopPose = c.name.EndsWith("_Loop");
                c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        public static AnimationClip Clip(string role, string name)
        {
            return SmoothFighterBuilder.Clip(role=="Arms" && name=="Idle_Loop" ? "Rifle_Hold_Loop" : name);
        }

        static AnimatorController Controller(string role)
        {
            string path = Root + "/Controllers/" + role + ".controller";
            // Rebuild in place, preserving the GUID used by prefabs/scenes.
            var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (old != null)
            {
                old.layers = Array.Empty<AnimatorControllerLayer>();
                old.parameters = Array.Empty<AnimatorControllerParameter>();
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path)) if (a != old) Object.DestroyImmediate(a, true);
            }
            var c = old ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            if (old != null) c.AddLayer("Base");
            c.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            c.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            c.AddParameter("Playback", AnimatorControllerParameterType.Float);
            c.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            c.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            c.AddParameter("Alive", AnimatorControllerParameterType.Bool);
            c.AddParameter("Action", AnimatorControllerParameterType.Int);
            var sm = c.layers[0].stateMachine;
            if (role == "Arms")
            {
                sm.defaultState = State(sm, "Hold", Clip(role, "Idle_Loop"));
                return c;
            }
            var locomotion = State(sm, "Locomotion", Directional(c, role, false));
            locomotion.speedParameter = "Playback"; locomotion.speedParameterActive = true;
            sm.defaultState = locomotion;
            var fall = State(sm, "Airborne", Clip(role, "Jump_Loop"));
            var land = State(sm, "Land", Clip(role, "Jump_Land"));
            var death = State(sm, "Death", Clip(role, "Death01"));
            var slide = State(sm, "Slide", Clip(role, "Slide_Loop"));
            var vault = State(sm, "Vault", Clip(role, "ClimbUp_1m"));
            vault.speed=1.5f;
            var dodge = State(sm, "Dodge", Directional(c,role,true));
            dodge.speed=2;
            foreach (var s in new[] { locomotion, fall, land, slide, vault, dodge })
                Transition(s, death, "Alive", AnimatorConditionMode.IfNot, 0, .06f);
            Transition(death, locomotion, "Alive", AnimatorConditionMode.If, 0, .1f);
            Transition(locomotion, fall, "Grounded", AnimatorConditionMode.IfNot, 0, .06f).AddCondition(AnimatorConditionMode.NotEqual,2,"Action");
            Transition(fall, land, "Grounded", AnimatorConditionMode.If, 0, .035f);
            var recover = land.AddTransition(locomotion); recover.hasExitTime = true; recover.exitTime=.55f; recover.duration=.08f;
            Transition(locomotion, slide, "Action", AnimatorConditionMode.Equals, 1, .06f);
            Transition(slide, locomotion, "Action", AnimatorConditionMode.NotEqual, 1, .1f);
            Transition(locomotion, vault, "Action", AnimatorConditionMode.Equals, 2, .06f);
            Transition(vault, locomotion, "Action", AnimatorConditionMode.NotEqual, 2, .1f);
            Transition(locomotion, dodge, "Action", AnimatorConditionMode.Equals, 3, .035f);
            Transition(dodge, locomotion, "Action", AnimatorConditionMode.NotEqual, 3, .1f);
            c.AddLayer("Crouch");
            var layers = c.layers; layers[1].defaultWeight=0;
            layers[1].stateMachine.defaultState=State(layers[1].stateMachine,"Crouch",Directional(c,role,true));
            layers[1].stateMachine.defaultState.speedParameter="Playback";
            layers[1].stateMachine.defaultState.speedParameterActive=true;
            c.layers=layers;
            EditorUtility.SetDirty(c);
            return c;
        }

        static BlendTree Directional(AnimatorController c, string role, bool crouch)
        {
            var tree = new BlendTree { name=crouch ? "Crouch8Way" : "Jog8Way", blendType=BlendTreeType.FreeformCartesian2D,
                blendParameter="MoveX", blendParameterY="MoveZ", useAutomaticThresholds=false };
            AssetDatabase.AddObjectToAsset(tree,c);
            tree.AddChild(Clip(role,crouch ? "Crouch_Idle_Loop" : "Idle_Loop"), Vector2.zero);
            string[] names={"F","FR","R","BR","B","BL","L","FL"};
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;
                tree.AddChild(Clip(role,(crouch ? "Crouch_" : "Jog_")+names[i]+"_Loop"),new Vector2(Mathf.Sin(a),Mathf.Cos(a)));
            }
            if(!crouch) tree.AddChild(Clip(role,"Sprint_Loop"),new Vector2(0,1.45f));
            return tree;
        }

        static AnimatorState State(AnimatorStateMachine sm,string name,Motion motion)
        { var s=sm.AddState(name); s.motion=motion; s.writeDefaultValues=true; return s; }
        static AnimatorStateTransition Transition(AnimatorState a,AnimatorState b,string p,AnimatorConditionMode mode,float value,float duration)
        { var t=a.AddTransition(b); t.hasExitTime=false; t.hasFixedDuration=true; t.duration=duration; t.AddCondition(mode,value,p); return t; }

        static GameObject Model(string path,Transform parent,Vector3 position)
        {
            var o=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            PrefabUtility.UnpackPrefabInstance(o,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            o.name="Model"; o.transform.SetParent(parent,false); o.transform.localPosition=position;
            o.transform.localRotation=Quaternion.Euler(0,180,0);
            Materials(o);
            return o;
        }

        static void BuildSkin(string role,RigRole rigRole,string path,Vector3 offset)
        {
            var root=new GameObject("Fighter_"+role);
            try {
            var descriptor=root.AddComponent<SkinDescriptor>(); descriptor.Role=rigRole; descriptor.SkinId="fighter_"+role; descriptor.RigId="quaternius";
            var model=Model(Source+"/Fighter_"+role+".fbx",root.transform,offset);
            Animator(model,role);
            Save(root,path);
            } finally { if(root!=null) Object.DestroyImmediate(root); }
        }
        static void Animator(GameObject model,string role)
        {
            var controller=Controller(role);
            var a=model.GetComponent<Animator>();
            if(a==null) a=model.AddComponent<Animator>();
            a.runtimeAnimatorController=controller; a.avatar=null; a.applyRootMotion=false; a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        }

        static void BuildArms()
        {
            var root=new GameObject("P2ArmsViewmodel");
            var model=Model(Source+"/Fighter_Arms.fbx",root.transform,new Vector3(.06f,-1.60f,.19f));
            Animator(model,"Arms");
            var gun=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RiflePath));
            gun.name="ViewmodelWeapon"; gun.transform.SetParent(root.transform,false); gun.transform.localPosition=new Vector3(.18f,-.27f,.32f);
            gun.transform.localScale=Vector3.one*.9f;
            var pose=root.AddComponent<RiflePose>(); pose.Model=model.transform; pose.Weapon=gun.transform; pose.FirstPerson=true;
            Save(root,ArmsPath);
        }

        static void BuildRifle()
        {
            var root=new GameObject("BMA_Weapon_Rifle_View");
            Model(Source+"/Rifle.fbx",root.transform,Vector3.zero);
            Marker(root.transform,"Grip_R",new Vector3(0,-.026f,-.005f),Quaternion.LookRotation(Vector3.forward,Vector3.down));
            Marker(root.transform,"Grip_L",new Vector3(-.035f,.035f,.27f),Quaternion.LookRotation(Vector3.forward,Vector3.right));
            Marker(root.transform,"Muzzle",new Vector3(0,.103f,.725f),Quaternion.identity);
            Save(root,RiflePath);
        }
        static void Marker(Transform parent,string name,Vector3 position,Quaternion rotation)
        { var o=new GameObject(name); o.transform.SetParent(parent,false); o.transform.localPosition=position; o.transform.localRotation=rotation; }
        static void Save(GameObject o,string path)
        { PrefabUtility.SaveAsPrefabAsset(o,path); Object.DestroyImmediate(o); AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate); }

        static void Materials(GameObject model)
        {
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats=r.sharedMaterials;
                for(int i=0;i<mats.Length;i++)
                {
                    if(mats[i]==null) continue;
                    string name=mats[i].name; string path=Root+"/Materials/"+name+".mat";
                    var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(m==null)
                    {
                        Color color=mats[i].color;
                        m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name=name;
                        m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",.35f);
                        m.SetFloat("_Metallic",name.Contains("Armor")||name.Contains("Steel") ? .4f : .05f);
                        if(name.Contains("Lens")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.4f);}
                        AssetDatabase.CreateAsset(m,path);
                    }
                    mats[i]=m;
                }
                r.sharedMaterials=mats;
                if(r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen=true;
            }
        }
    }
}
