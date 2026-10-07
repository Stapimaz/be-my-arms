using BeMyArms.M7.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.Tests
{
    public class M7FighterPresentationTests
    {
        [TestCase("F",0)] [TestCase("R",90)] [TestCase("B",180)] [TestCase("L",270)]
        public void DirectionalClipsHaveDistinctPlantedFootTravelAndClosedLoops(string direction,float degrees)
        {
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7SmoothFighterBuilder.ModelPath));
            try
            {
                model.GetComponent<Animator>().avatar=null;
                var foot=M7RiflePose.Find(model.transform,"DEF-foot.L");
                foreach(string prefix in new[]{"Jog_","Crouch_"})
                {
                    var clip=M7FighterBuilder.Clip("P1",prefix+direction+"_Loop");
                    clip.SampleAnimation(model,0); Vector3 start=foot.position;
                    clip.SampleAnimation(model,clip.length*.24f); Vector3 mid=foot.position;
                    var travel=mid-start; travel.y=0;
                    Vector3 expected=Quaternion.Euler(0,degrees,0)*Vector3.back;
                    Assert.Greater(travel.magnitude,.06f,"clip must carry real foot travel");
                    // Inspect the planted portion, whose timing differs between artist-authored
                    // running/backward clips, rather than assuming a generated quarter-cycle.
                    bool plantedTravel=false;
                    for(int i=0;i<60;i++)
                    {
                        clip.SampleAnimation(model,clip.length*i/60f);var a=foot.position;
                        clip.SampleAnimation(model,clip.length*(i+1)/60f);var b=foot.position;
                        var v=b-a;v.y=0;
                        if(a.y<.18f && b.y<.18f && v.magnitude>.001f && Vector3.Dot(v.normalized,expected)>.75f)plantedTravel=true;
                    }
                    Assert.IsTrue(plantedTravel,"stance foot must travel opposite simulated direction");
                    clip.SampleAnimation(model,clip.length); Assert.Less(Vector3.Distance(start,foot.position),.025f,"seamless cycle");
                    foreach(var bone in model.GetComponentsInChildren<Transform>())
                        Assert.IsFalse(float.IsNaN(bone.position.x)||float.IsInfinity(bone.position.x),bone.name);
                }
            }
            finally { Object.DestroyImmediate(model); }
        }

        [Test]
        public void RoleMaterialsAreUniformDistinctAndMatchTheFpsArms()
        {
            var body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7PlayerBodyBuilder.BodyPrefabPath));
            var arms=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7FighterBuilder.ArmsPath));
            try {
                var driver=body.GetComponent<M7CharacterAnimator>();
                var p1=driver.P1Skin.GetComponentInChildren<Renderer>().sharedMaterial;
                var p2=driver.P2Skin.GetComponentInChildren<Renderer>().sharedMaterial;
                Assert.AreNotSame(p1,p2);
                foreach(var r in driver.P1Skin.GetComponentsInChildren<Renderer>())
                    foreach(var material in r.sharedMaterials)Assert.AreSame(p1,material,r.name);
                foreach(var r in driver.P2Skin.GetComponentsInChildren<Renderer>())
                    foreach(var material in r.sharedMaterials)Assert.AreSame(p2,material,r.name);
                foreach(var r in arms.GetComponentsInChildren<SkinnedMeshRenderer>())
                    foreach(var material in r.sharedMaterials)Assert.AreSame(p2,material,r.name);
                var server=body.GetComponent<BeMyArms.M3.M3DuelBody>();var prediction=body.GetComponent<BeMyArms.M3.M3DuelClient>();
                Assert.AreEqual(45f,server.NeckYawLimitDegrees);Assert.AreEqual(server.NeckYawLimitDegrees,prediction.NeckYawLimitDegrees);
                Assert.AreEqual(25f,server.BodyFollowThresholdDegrees);Assert.AreEqual(server.BodyFollowThresholdDegrees,prediction.BodyFollowThresholdDegrees);
                Assert.AreEqual(server.BodyFollowSpeedDegreesPerSecond,prediction.BodyFollowSpeedDegreesPerSecond);
            } finally {Object.DestroyImmediate(body);Object.DestroyImmediate(arms);}
        }

        [Test]
        public void HeadRemainsAnatomicallyBoundedWithOpposingP1LookAndP2Aim()
        {
            var body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7PlayerBodyBuilder.BodyPrefabPath));
            try {
                var driver=body.GetComponent<M7CharacterAnimator>();
                var head=M7RiflePose.Find(driver.P1Animator.transform,"Head");var chest=M7RiflePose.Find(driver.P1Animator.transform,"spine_03");
                Quaternion basis=Quaternion.Inverse(driver.P1Animator.transform.rotation)*head.rotation;
                driver.SetHeadLook(0,0);
                foreach(var stance in new[]{"Idle_Loop","Crouch_Idle_Loop","Jog_L_Loop"})
                foreach(float look in new[]{-45f,45f})foreach(float aim in new[]{-85f,85f})foreach(float pitch in new[]{-80f,0f,80f})
                {
                    M7SmoothFighterBuilder.Clip(stance).SampleAnimation(driver.P1Animator.gameObject,0);
                    driver.SetUpperBodyAim(aim,pitch);driver.SetHeadLook(look,0);
                    var relative=(Quaternion.Inverse(chest.rotation)*head.rotation*Quaternion.Inverse(basis)).eulerAngles;
                    Assert.LessOrEqual(Mathf.Abs(BeMyArms.M2.M2BodySim.Normalize(relative.y)),50.01f);
                    Assert.LessOrEqual(Mathf.Abs(BeMyArms.M2.M2BodySim.Normalize(relative.x)),35.01f);
                    Assert.LessOrEqual(Mathf.Abs(BeMyArms.M2.M2BodySim.Normalize(relative.z)),15.01f);
                }
            } finally {Object.DestroyImmediate(body);}
        }

        [TestCase(false,0f)] [TestCase(true,0f)] [TestCase(false,90f)] [TestCase(true,-120f)] [TestCase(true,180f)]
        public void KickExtendsTheFootForwardWithAStraightenedKnee_ThenReturns(bool heavy,float yaw)
        {
            var body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7PlayerBodyBuilder.BodyPrefabPath));
            try {
                var driver=body.GetComponent<M7CharacterAnimator>();var clip=M7SmoothFighterBuilder.Clip("Idle_Loop");
                // Runtime clients rotate Presentation, NOT the network root hosting this driver.
                // Exercise the actual hierarchy discrepancy missed by the old zero-yaw fixture.
                driver.P1Animator.transform.rotation=Quaternion.Euler(0,yaw,0);
                var forward=driver.P1Animator.transform.forward;
                var foot=M7RiflePose.Find(driver.P1Animator.transform,"foot_r");
                var thigh=M7RiflePose.Find(driver.P1Animator.transform,"thigh_r");var shin=M7RiflePose.Find(driver.P1Animator.transform,"calf_r");
                clip.SampleAnimation(driver.P1Animator.gameObject,0);var ready=foot.position;
                driver.ApplyKickPose(.22f,heavy);var chamber=foot.position;
                clip.SampleAnimation(driver.P1Animator.gameObject,0);driver.ApplyKickPose(.34f,heavy);
                Assert.Greater(Vector3.Dot(foot.position-chamber,forward),.4f,"kick must thrust forward, not just lift");
                Assert.Greater(Vector3.Dot(foot.position-thigh.position,forward),.75f,"sole extends almost a full leg ahead of the hip");
                Assert.Greater(Vector3.Dot((shin.position-thigh.position).normalized,(foot.position-shin.position).normalized),.8f,"strike knee extends");
                clip.SampleAnimation(driver.P1Animator.gameObject,0);driver.ApplyKickPose(1,heavy);
                Assert.Less(Vector3.Distance(ready,foot.position),.001f);
            } finally {Object.DestroyImmediate(body);}
        }

        [Test]
        public void FpsReadyWristsContinueAlongForearmsAndHandsAreInsideTheCameraFrame()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7FighterBuilder.ArmsPath));
            var camera=new GameObject("GripFrameCamera").AddComponent<Camera>();camera.fieldOfView=68;
            try {
                var pose=go.GetComponent<M7RiflePose>();M7SmoothFighterBuilder.Clip("Rifle_Hold_Loop").SampleAnimation(pose.Model.gameObject,0);
                pose.Pose(false,0);
                Assert.IsFalse(go.GetComponentInChildren<Animator>().enabled,"rifle hold must not inherit world breathing");
                Assert.AreEqual(1,System.Array.FindAll(go.GetComponentsInChildren<SkinnedMeshRenderer>(),r=>r.enabled).Length);
                foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.enabled)
                    foreach(var w in skin.sharedMesh.boneWeights)if(w.weight0>.5f)
                        Assert.IsFalse(skin.bones[w.boneIndex0].name.EndsWith("_r"),"visible rifle hold must be the support hand");
                foreach(string s in new[]{"l"}) {
                    var elbow=M7RiflePose.Find(pose.Model,"lowerarm_"+s);var wrist=M7RiflePose.Find(pose.Model,"hand_"+s);
                    var knuckle=M7RiflePose.Find(pose.Model,"middle_01_"+s);
                    Assert.Less(Vector3.Angle(wrist.position-elbow.position,knuckle.position-wrist.position),40f,s+" anatomical wrist bend");
                    var screen=camera.WorldToViewportPoint((wrist.position+knuckle.position)*.5f);
                    Assert.That(screen.x,Is.InRange(.15f,.95f));Assert.That(screen.y,Is.InRange(.02f,.55f));Assert.Greater(screen.z,.05f);
                    var ready=wrist.position;
                    for(int i=0;i<120;i++) {pose.Pose(false,i/60f);Assert.Less(Vector3.Distance(ready,wrist.position),.00001f,"steady weapon-mounted hold");}
                }
            }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(camera.gameObject);}
        }

        [Test]
        public void RoleSurfacesShareOneSkeletonAndAnimationAuthority()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7PlayerBodyBuilder.BodyPrefabPath));
            try {
                var driver=go.GetComponent<M7CharacterAnimator>();
                Assert.AreSame(driver.P1Animator,driver.P2Animator);
                var p1=driver.P1Skin.GetComponentsInChildren<SkinnedMeshRenderer>();
                var p2=driver.P2Skin.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.IsNotNull(p2);
                foreach(var bone in p2.bones) if(bone!=null)Assert.IsTrue(bone.IsChildOf(driver.P1Animator.transform),"P2 skin must reference shared bones");
                Assert.IsFalse(driver.P1Animator.applyRootMotion);
            } finally {Object.DestroyImmediate(go);}
        }

        [TestCase("F")] [TestCase("B")] [TestCase("L")] [TestCase("R")]
        public void CrouchFeetNeverExchangeAnatomicalLanes(string direction)
        {
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7SmoothFighterBuilder.ModelPath));
            try {
                model.GetComponent<Animator>().avatar=null;
                var l=M7RiflePose.Find(model.transform,"foot_l");var r=M7RiflePose.Find(model.transform,"foot_r");
                var clip=M7SmoothFighterBuilder.Clip("Crouch_"+direction+"_Loop");
                for(int i=0;i<=120;i++) {
                    clip.SampleAnimation(model,clip.length*i/120f);
                    Assert.Less(l.position.x,r.position.x-.035f,"crouched feet crossed at phase "+i);
                    Assert.Greater(l.position.y,-.02f);Assert.Greater(r.position.y,-.02f);
                }
            } finally {Object.DestroyImmediate(model);}
        }

        [Test]
        public void FpsGripsAndMagazineReloadStayReachable_AndResetToReady()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7FighterBuilder.ArmsPath));
            try
            {
                var pose=go.GetComponent<M7RiflePose>();
                foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    Assert.IsFalse(renderer.name.StartsWith("Shoulder") || renderer.name.StartsWith("Mechanical upper"),"camera-local mesh must exclude world shoulder armour");
                var animator=go.GetComponentInChildren<Animator>();
                M7FighterBuilder.Clip("Arms","Idle_Loop").SampleAnimation(animator.gameObject,0);
                pose.Pose(false,0);
                Assert.Less(pose.LeftGripError,.02f); Assert.Less(pose.RightGripError,.02f);
                var magazine=M7RiflePose.Find(go.transform,"Magazine"); Vector3 ready=magazine.position;
                pose.Pose(true,1);
                Assert.AreEqual(2,System.Array.FindAll(go.GetComponentsInChildren<SkinnedMeshRenderer>(),r=>r.enabled).Length,"second hand enters during reload");
                for(int i=0;i<44;i++)
                {
                    M7FighterBuilder.Clip("Arms","Idle_Loop").SampleAnimation(animator.gameObject,0);
                    pose.Pose(true,1+i*.05f);
                    Assert.Less(pose.LeftGripError,.035f,$"left reload reach at {i}");
                    Assert.Less(pose.RightGripError,.02f,$"right grip at {i}");
                }
                pose.ResetPresentation(); pose.Pose(false,4);
                Assert.IsTrue(magazine.gameObject.activeSelf);
                Assert.Less(Vector3.Distance(ready,magazine.position),.001f);
                Assert.AreEqual(1,System.Array.FindAll(go.GetComponentsInChildren<SkinnedMeshRenderer>(),r=>r.enabled).Length,"back to one support hand");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(-85,-80,false)] [TestCase(-85,0,false)] [TestCase(0,0,false)] [TestCase(85,0,false)] [TestCase(85,80,false)]
        [TestCase(-85,-80,true)] [TestCase(-85,0,true)] [TestCase(0,0,true)] [TestCase(85,0,true)] [TestCase(85,80,true)]
        public void WorldRifleGripsFollowExtremeLegalAim(float yaw,float pitch,bool crouch)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(M7PlayerBodyBuilder.BodyPrefabPath));
            try
            {
                var driver=go.GetComponent<M7CharacterAnimator>();
                M7FighterBuilder.Clip("P2",crouch ? "Crouch_Idle_Loop" : "Idle_Loop").SampleAnimation(driver.P2Animator.gameObject,0);
                driver.SetUpperBodyAim(yaw,pitch);
                var chest=M7RiflePose.Find(driver.P1Animator.transform,"spine_03");
                driver.AimPivot.rotation=Quaternion.Euler(pitch,yaw,0);
                driver.AimPivot.position=chest.position+driver.AimPivot.rotation*new Vector3(.08f,-.04f,.04f);
                var pose=go.GetComponent<M7RiflePose>(); pose.Pose(false,0);
                Assert.Less(pose.LeftGripError,.035f,"support-hand reach"); Assert.Less(pose.RightGripError,.035f,"trigger-hand reach");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
