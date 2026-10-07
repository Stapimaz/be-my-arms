using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeMyArms.M5;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>CC0 smooth base + authored CC0 motion, retargeted once to ONE native skeleton.</summary>
    public static class M7SmoothFighterBuilder
    {
        public const string Root = "Assets/Art/Characters/Pass2Redo";
        public const string ModelPath = Root + "/Models/Fighter_Shared.fbx";
        const string ArmsModelPath = Root + "/Models/Fighter_Arms.fbx";
        const string Animations = Root + "/Animations/";
        const string Baked = Root + "/Baked/";

        public static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Baked + name + ".anim")
            ?? throw new InvalidOperationException("Missing baked smooth-fighter clip " + name);

        public static void BuildAll()
        {
            Directory.CreateDirectory(Baked); Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Controllers"); Directory.CreateDirectory(Root + "/Resources");
            AssetDatabase.Refresh();
            Import(ModelPath, false); Import(ArmsModelPath, false);
            foreach (var file in Directory.GetFiles(Animations, "*.fbx")) Import(file.Replace('\\','/'), true);
            Bake("Idle_Loop", "UAL1_Standard", "Idle_Loop");
            Bake("Crouch_Idle_Loop", "UAL1_Standard", "Crouch_Idle_Loop");
            Bake("Jog_F_Loop", "Rig_Medium_MovementBasic", "Running_A");
            Bake("Jog_B_Loop", "Rig_Medium_MovementAdvanced", "Walking_Backwards", duration: .8f);
            Bake("Jog_L_Loop", "Rig_Medium_MovementAdvanced", "Running_Strafe_Left");
            Bake("Jog_R_Loop", "Rig_Medium_MovementAdvanced", "Running_Strafe_Right");
            Bake("Sprint_Loop", "UAL1_Standard", "Sprint_Loop");
            // Preserve the authored foot arcs of each directional clip. Crouched variants are
            // adapted once, offline, to a lower pelvis with explicit forward knee planes and
            // a shorter lateral stride. No runtime leg offset/crossing compensation.
            Bake("Crouch_F_Loop", "UAL1_Standard", "Crouch_Fwd_Loop");
            foreach (string d in new[]{"B","L","R"})
                Bake("Crouch_"+d+"_Loop", "Rig_Medium_MovementAdvanced", d=="B" ? "Walking_Backwards" : "Running_Strafe_"+(d=="L" ? "Left" : "Right"), crouch:true, duration:1.1f);
            Bake("Jump_Loop", "UAL1_Standard", "Jump_Loop");
            Bake("Jump_Land", "UAL1_Standard", "Jump_Land");
            Bake("Death01", "UAL1_Standard", "Death01");
            Bake("Slide_Loop", "UAL2_Standard", "Slide_Loop");
            Bake("Rifle_Hold_Loop", "Rig_Medium_CombatRanged", "Ranged_2H_Aiming");
            var controller = Controller();
            BuildSkin(controller);
            BuildArms();
            AssetDatabase.SaveAssets();
        }

        static void Import(string path, bool animation)
        {
            var i=(ModelImporter)AssetImporter.GetAtPath(path);
            i.preserveHierarchy=true; i.optimizeGameObjects=false;
            if(path==ArmsModelPath)
            {
                i.animationType=ModelImporterAnimationType.Generic;i.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
                i.importAnimation=false;i.SaveAndReimport();return;
            }
            i.animationType=ModelImporterAnimationType.Human; i.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            i.importAnimation=animation; i.importCameras=i.importLights=false;
            i.animationCompression=ModelImporterAnimationCompression.Off;
            var map=new List<HumanBone>();
            bool kay=path.Contains("Rig_Medium");
            void Add(string human,string q,string k) => map.Add(new HumanBone{humanName=human,boneName=kay?k:q,limit=new HumanLimit{useDefaultValues=true}});
            Add("Hips","pelvis","hips"); Add("Spine","spine_01","spine"); Add("Chest","spine_02","chest");
            if(!kay){Add("UpperChest","spine_03",""); Add("Neck","neck_01","");}
            Add("Head","Head","head");
            foreach(var s in new[]{("Left","l"),("Right","r")})
            {
                if(!kay) Add(s.Item1+"Shoulder","clavicle_"+s.Item2,"");
                Add(s.Item1+"UpperArm","upperarm_"+s.Item2,"upperarm."+s.Item2);
                Add(s.Item1+"LowerArm","lowerarm_"+s.Item2,"lowerarm."+s.Item2);
                Add(s.Item1+"Hand","hand_"+s.Item2,"wrist."+s.Item2);
                Add(s.Item1+"UpperLeg","thigh_"+s.Item2,"upperleg."+s.Item2);
                Add(s.Item1+"LowerLeg","calf_"+s.Item2,"lowerleg."+s.Item2);
                Add(s.Item1+"Foot","foot_"+s.Item2,"foot."+s.Item2);
                Add(s.Item1+"Toes","ball_"+s.Item2,"toes."+s.Item2);
                if(!kay) foreach(string f in new[]{"Thumb","Index","Middle","Ring","Little"})
                    for(int n=1;n<=3;n++) Add(s.Item1+f+(n==1?"Proximal":n==2?"Intermediate":"Distal"),(f=="Little"?"pinky":f.ToLowerInvariant())+"_0"+n+"_"+s.Item2,"");
            }
            var hd=i.humanDescription; hd.human=map.ToArray(); hd.hasTranslationDoF=false;
            i.humanDescription=hd;
            var clips=i.defaultClipAnimations;
            foreach(var c in clips)
            {
                c.name=c.takeName.Split('|').Last(); c.loopTime=c.loopPose=c.name.EndsWith("Loop") || c.name.StartsWith("Running") || c.name.StartsWith("Walking") || c.name=="Ranged_2H_Aiming";
                c.lockRootRotation=c.lockRootHeightY=c.lockRootPositionXZ=true;
                // Normalize the SOURCE RIG's forward axis, not the changing average facing
                // of each individual strafe clip (which would rotate its foot travel).
                c.keepOriginalOrientation=true; c.rotationOffset=kay ? 0f : 180f;
                c.keepOriginalPositionY=true; c.keepOriginalPositionXZ=true;
            }
            i.clipAnimations=clips; i.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var avatar=model.GetComponent<Animator>().avatar;
            if(avatar==null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Invalid humanoid avatar: "+path);
        }

        static void Bake(string name,string library,string sourceName,bool crouch=false,float duration=0)
        {
            var clip=AssetDatabase.LoadAllAssetsAtPath(Animations+library+".fbx").OfType<AnimationClip>().First(c=>c.name==sourceName);
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            var animator=go.GetComponent<Animator>(); animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var graph=PlayableGraph.Create("Bake authored CC0 motion"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable=AnimationClipPlayable.Create(graph,clip); playable.SetApplyFootIK(true);
            AnimationPlayableOutput.Create(graph,"Retarget",animator).SetSourcePlayable(playable); graph.Play();
            try
            {
                var bones=go.GetComponentsInChildren<Transform>().Where(t=>t!=go.transform).ToArray();
                var keys=new Dictionary<Transform,List<(float time,Vector3 pos,Quaternion rot)>>();
                foreach(var b in bones) keys[b]=new();
                float length=duration>0?duration:clip.length;
                int frames=Mathf.Max(2,Mathf.RoundToInt(length*60));
                var hip=animator.GetBoneTransform(HumanBodyBones.Hips);
                var feet=new[]{animator.GetBoneTransform(HumanBodyBones.LeftFoot),animator.GetBoneTransform(HumanBodyBones.RightFoot)};
                float groundDrop=0,phaseOffset=0;
                bool locomotion=name.StartsWith("Jog_") || name.StartsWith("Crouch_") && name!="Crouch_Idle_Loop" || name=="Sprint_Loop";
                if(locomotion)
                {
                    float ankle=float.MaxValue,leftContact=float.MaxValue;int contactFrame=0;
                    for(int f=0;f<=frames;f++)
                    {
                        playable.SetTime(clip.length*f/frames);graph.Evaluate(0);
                        ankle=Mathf.Min(ankle,feet[0].position.y,feet[1].position.y);
                        if(feet[0].position.y<leftContact){leftContact=feet[0].position.y;contactFrame=f;}
                    }
                    // Register the retargeted sole contact to this mesh's floor origin once.
                    // Different source limb proportions must not leave all stance feet floating.
                    if(library.StartsWith("Rig_Medium_Movement"))groundDrop=Mathf.Max(0,ankle-.086f);
                    // All cardinal blend children share the same left stance phase. Retain the
                    // artist's trajectory; only offset its cyclic sampling, including the seam.
                    phaseOffset=(float)contactFrame/frames-.2f;
                }
                for(int frame=0;frame<=frames;frame++)
                {
                    float time=length*frame/frames;
                    float phase=locomotion ? Mathf.Repeat((float)frame/frames+phaseOffset,1f) : (float)frame/frames;
                    playable.SetTime(clip.length*phase); graph.Evaluate(0);
                    go.transform.localPosition=Vector3.zero; go.transform.localRotation=Quaternion.identity;
                    hip.position+=Vector3.down*groundDrop;
                    if(crouch)
                    {
                        var targets=feet.Select(t=>t.position).ToArray();
                        hip.position+=Vector3.down*.30f;
                        for(int side=0;side<2;side++)
                        {
                            var target=targets[side];
                            // Shorten side steps uniformly in the floor plane: shrinking only X
                            // converts the source's slight forward bias into a diagonal stride.
                            float lane=side==0 ? -.16f : .16f;
                            target.x=lane+(target.x-lane)*.22f;
                            target.y=.086f+(target.y-.086f)*.25f;
                            target.z*=name.Contains("_L_") || name.Contains("_R_") ? .22f : .65f;
                            var upper=animator.GetBoneTransform(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                            var lower=animator.GetBoneTransform(side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                            M7RiflePose.Solve(upper,lower,feet[side],target,feet[side].rotation,Vector3.forward);
                        }
                    }
                    foreach(var b in bones) keys[b].Add((time,b.localPosition,b.localRotation));
                }
                var baked=new AnimationClip{name=name,frameRate=60};
                foreach(var b in bones)
                {
                    string path=AnimationUtility.CalculateTransformPath(b,go.transform);
                    var values=keys[b];
                    for(int axis=0;axis<3;axis++) baked.SetCurve(path,typeof(Transform),"m_LocalPosition."+"xyz"[axis],new AnimationCurve(values.Select(k=>new Keyframe(k.time,k.pos[axis])).ToArray()));
                    Quaternion previous=values[0].rot;
                    var rotations=new List<(float time,Quaternion rot)>();
                    foreach(var k in values){var q=k.rot;if(Quaternion.Dot(previous,q)<0) q=new Quaternion(-q.x,-q.y,-q.z,-q.w); rotations.Add((k.time,q));previous=q;}
                    for(int axis=0;axis<4;axis++) baked.SetCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[axis],new AnimationCurve(rotations.Select(k=>new Keyframe(k.time,k.rot[axis])).ToArray()));
                }
                baked.EnsureQuaternionContinuity();
                var settings=AnimationUtility.GetAnimationClipSettings(baked); settings.loopTime=name.EndsWith("Loop"); settings.loopBlend=false;
                AnimationUtility.SetAnimationClipSettings(baked,settings);
                string asset=Baked+name+".anim";
                var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);
                if(old!=null){EditorUtility.CopySerialized(baked,old);Object.DestroyImmediate(baked);EditorUtility.SetDirty(old);} else AssetDatabase.CreateAsset(baked,asset);
            }
            finally {graph.Destroy(); Object.DestroyImmediate(go);}
        }

        static AnimatorController Controller()
        {
            string path=Root+"/Controllers/Shared.controller";
            var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(c!=null){c.layers=Array.Empty<AnimatorControllerLayer>();c.parameters=Array.Empty<AnimatorControllerParameter>();foreach(var a in AssetDatabase.LoadAllAssetsAtPath(path))if(a!=c)Object.DestroyImmediate(a,true);c.AddLayer("Base");}
            else c=AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach(string p in new[]{"MoveX","MoveZ","Playback","VerticalSpeed","Crouch"})c.AddParameter(p,AnimatorControllerParameterType.Float);
            c.AddParameter("Alive",AnimatorControllerParameterType.Bool);c.AddParameter("Grounded",AnimatorControllerParameterType.Bool);c.AddParameter("Action",AnimatorControllerParameterType.Int);
            var sm=c.layers[0].stateMachine;
            var stance=new BlendTree{name="Stance",blendType=BlendTreeType.Simple1D,blendParameter="Crouch",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(stance,c);
            stance.AddChild(Directional(c,false),0);stance.AddChild(Directional(c,true),1);
            var locomotion=sm.AddState("Locomotion");locomotion.motion=stance;locomotion.speedParameter="Playback";locomotion.speedParameterActive=true;sm.defaultState=locomotion;
            foreach(var entry in new[]{("Airborne","Jump_Loop",0),("Slide","Slide_Loop",1),("Death","Death01",-1)})
            {
                var s=sm.AddState(entry.Item1);s.motion=Clip(entry.Item2);
                var enter=sm.AddAnyStateTransition(s);enter.hasExitTime=false;enter.duration=.045f;enter.canTransitionToSelf=false;
                enter.AddCondition(entry.Item3==-1?AnimatorConditionMode.IfNot:AnimatorConditionMode.If,0,entry.Item3==-1?"Alive":"Alive");
                if(entry.Item3>=0){enter.AddCondition(AnimatorConditionMode.Equals,entry.Item3,"Action");if(entry.Item3==0)enter.AddCondition(AnimatorConditionMode.IfNot,0,"Grounded");}
                var exit=s.AddTransition(locomotion);exit.hasExitTime=false;exit.duration=.09f;
                if(entry.Item3==-1)exit.AddCondition(AnimatorConditionMode.If,0,"Alive");
                else if(entry.Item3==0)exit.AddCondition(AnimatorConditionMode.If,0,"Grounded");
                else exit.AddCondition(AnimatorConditionMode.NotEqual,entry.Item3,"Action");
            }
            EditorUtility.SetDirty(c);return c;
        }

        static BlendTree Directional(AnimatorController c,bool crouch)
        {
            var tree=new BlendTree{name=crouch?"AuthoredCrouch4Way":"AuthoredRun4Way",blendType=BlendTreeType.FreeformDirectional2D,blendParameter="MoveX",blendParameterY="MoveZ",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,c);
            tree.AddChild(Clip(crouch?"Crouch_Idle_Loop":"Idle_Loop"),Vector2.zero);
            foreach(var d in new[]{("F",Vector2.up),("B",Vector2.down),("L",Vector2.left),("R",Vector2.right)})tree.AddChild(Clip((crouch?"Crouch_":"Jog_")+d.Item1+"_Loop"),d.Item2);
            if(!crouch)tree.AddChild(Clip("Sprint_Loop"),Vector2.up*1.55f);
            return tree;
        }

        static GameObject Model(string path,Transform parent)
        {
            var o=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));o.name="Model";o.transform.SetParent(parent,false);
            foreach(var r in o.GetComponentsInChildren<Renderer>())
            {
                var mats=r.sharedMaterials;
                for(int n=0;n<mats.Length;n++)
                {
                    if(mats[n]==null)continue;
                    string asset=Root+"/Materials/"+mats[n].name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(asset);
                    if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=mats[n].name;AssetDatabase.CreateAsset(m,asset);}
                    m.SetColor("_BaseColor",mats[n].color);m.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(m);mats[n]=m;
                }
                r.sharedMaterials=mats;if(r is SkinnedMeshRenderer sk)sk.updateWhenOffscreen=true;
            }
            return o;
        }

        static void BuildSkin(AnimatorController controller)
        {
            var root=new GameObject("SmoothFighter_P1");
            try
            {
                var skin=root.AddComponent<M5SkinDescriptor>();skin.Role=M5RigRole.P1;skin.SkinId="smooth_p1";skin.RigId="quaternius";
                var model=Model(ModelPath,root.transform);var a=model.GetComponent<Animator>();a.avatar=null;a.runtimeAnimatorController=controller;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.SaveAsPrefabAsset(root,M7FighterBuilder.P1SkinPath);
            }finally{Object.DestroyImmediate(root);}
            root=new GameObject("SmoothFighter_P2");
            try{var skin=root.AddComponent<M5SkinDescriptor>();skin.Role=M5RigRole.P2;skin.SkinId="smooth_p2";skin.RigId="quaternius";PrefabUtility.SaveAsPrefabAsset(root,M7FighterBuilder.P2SkinPath);}finally{Object.DestroyImmediate(root);}
        }

        static void BuildArms()
        {
            const string old="Assets/Art/Characters/Pass2/Resources/M7_P2ArmsViewmodel.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(old)!=null)AssetDatabase.MoveAsset(old,"Assets/Art/Characters/Pass2/Legacy_ArmsViewmodel.prefab");
            var root=new GameObject("M7_P2ArmsViewmodel");
            try
            {
                var model=Model(ArmsModelPath,root.transform);model.transform.localPosition=new Vector3(.02f,-1.78f,.10f);
                var a=model.GetComponent<Animator>();if(a==null)a=model.AddComponent<Animator>();a.avatar=null;a.applyRootMotion=false;
                string path=Root+"/Controllers/Arms.controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPathWithClip(path,Clip("Rifle_Hold_Loop"));
                controller.layers[0].stateMachine.defaultState.motion=Clip("Rifle_Hold_Loop");a.runtimeAnimatorController=controller;
                var gun=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7FighterBuilder.RiflePath));gun.name="ViewmodelWeapon";gun.transform.SetParent(root.transform,false);gun.transform.localPosition=new Vector3(.16f,-.26f,.20f);gun.transform.localScale=Vector3.one*.9f;
                var pose=root.AddComponent<M7RiflePose>();pose.Model=model.transform;pose.Weapon=gun.transform;pose.FirstPerson=true;
                AuthorGrips(model,gun);
                PrefabUtility.SaveAsPrefabAsset(root,M7FighterBuilder.ArmsPath);
            }finally{Object.DestroyImmediate(root);}
        }

        public static void AuthorGrips(GameObject model,GameObject gun)
        {
            Clip("Rifle_Hold_Loop").SampleAnimation(model,0);
            var pose=model.GetComponentInParent<M7RiflePose>();
            var fingers=new List<M7RiflePose.FingerRotation>();
            // Define each grip from the mesh's anatomical palm/finger axes. The saved sockets
            // and per-joint rotations are the authored pose, not an additive hand-roll fix.
            foreach(string side in new[]{"L","R"})
            {
                string s=side.ToLowerInvariant();
                var hand=M7RiflePose.Find(model.transform,"hand_"+s);
                var middle=M7RiflePose.Find(model.transform,"middle_01_"+s);
                var index=M7RiflePose.Find(model.transform,"index_01_"+s);
                var pinky=M7RiflePose.Find(model.transform,"pinky_01_"+s);
                Vector3 longitudinal=hand.InverseTransformDirection((middle.position-hand.position).normalized);
                Vector3 across=hand.InverseTransformDirection((index.position-pinky.position).normalized);
                Vector3 palm=Vector3.Cross(longitudinal,across).normalized;
                var anatomical=Quaternion.LookRotation(longitudinal,palm);
                var grip=M7RiflePose.Find(gun.transform,"Grip_"+side);
                grip.localPosition=side=="R" ? new Vector3(.042f,-.027f,-.075f) : new Vector3(-.09f,.03f,.27f);
                Vector3 fingerForward=side=="R" ? Vector3.forward : Vector3.right;
                Vector3 contact=side=="R" ? Vector3.left : Vector3.up;
                grip.localRotation=Quaternion.LookRotation(fingerForward,contact)*Quaternion.Inverse(anatomical);
                // In the authored hand space, curl toward the palm contact normal. Joint values
                // differ for the trigger index, three wrapping fingers, and opposed thumb.
                Vector3 bend=Vector3.Cross(longitudinal,palm).normalized;
                foreach(string f in new[]{"index","middle","ring","pinky","thumb"})
                    for(int joint=1;joint<=3;joint++)
                    {
                        var t=M7RiflePose.Find(model.transform,f+"_0"+joint+"_"+s);
                        float angle=f=="thumb" ? (joint==1?22:35) : f=="index" && side=="R" ? (joint==1?15:28) : (joint==1?48:joint==2?72:45);
                        Vector3 axis=t.InverseTransformDirection(hand.TransformDirection(bend));
                        Quaternion rotation=t.localRotation*Quaternion.AngleAxis(angle,axis);
                        fingers.Add(new M7RiflePose.FingerRotation{Bone=t,Rotation=rotation});
                    }
            }
            if(pose!=null) pose.AuthoredFingers=fingers.ToArray();
        }
    }
}
