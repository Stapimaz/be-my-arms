var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(BeMyArms.M7.EditorTools.M7PlayerBodyBuilder.BodyPrefabPath);
var previewScene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var body = UnityEngine.Object.Instantiate(prefab);
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(body,previewScene);
var anim = body.GetComponent<BeMyArms.M7.M7CharacterAnimator>();
BeMyArms.M7.EditorTools.M7FighterBuilder.Clip("P1", "Idle_Loop").SampleAnimation(anim.P1Animator.gameObject, 0);
BeMyArms.M7.EditorTools.M7FighterBuilder.Clip("P2", "Idle_Loop").SampleAnimation(anim.P2Animator.gameObject, 0);
anim.SetUpperBodyAim(0,0);
anim.SetHeadLook(0,0);
var chest=BeMyArms.M7.M7RiflePose.Find(anim.P1Animator.transform,"spine_03");
anim.AimPivot.position=chest.position+new UnityEngine.Vector3(.08f,-.04f,.04f);
body.GetComponent<BeMyArms.M7.M7RiflePose>().Pose(false,0);
foreach (var t in body.GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.layer=31;
var camGo=new UnityEngine.GameObject("Pass2PreviewCamera");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo,previewScene);
var cam=camGo.AddComponent<UnityEngine.Camera>();
cam.scene=previewScene;
cam.cullingMask=1<<31; cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
cam.backgroundColor=new UnityEngine.Color(.07f,.085f,.11f); cam.fieldOfView=42;
cam.transform.position=new UnityEngine.Vector3(2,1.55f,3.2f); cam.transform.LookAt(new UnityEngine.Vector3(0,1.05f,0));
var lightGo=new UnityEngine.GameObject("Pass2PreviewLight");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo,previewScene);
var light=lightGo.AddComponent<UnityEngine.Light>(); light.type=UnityEngine.LightType.Directional; light.intensity=2;
light.cullingMask=1<<31; light.transform.rotation=UnityEngine.Quaternion.Euler(35,210,0);
var rt=new UnityEngine.RenderTexture(1000,1000,24); cam.targetTexture=rt; cam.Render();
var old=UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active=rt;
var tex=new UnityEngine.Texture2D(1000,1000,UnityEngine.TextureFormat.RGB24,false); tex.ReadPixels(new UnityEngine.Rect(0,0,1000,1000),0,0); tex.Apply();
System.IO.Directory.CreateDirectory("Builds/Pass2Redo/QA"); System.IO.File.WriteAllBytes("Builds/Pass2Redo/QA/fighter.png",tex.EncodeToPNG());
UnityEngine.RenderTexture.active=old;
var pose=body.GetComponent<BeMyArms.M7.M7RiflePose>();
var result=new { pose.LeftGripError, pose.RightGripError, WeaponBounds=anim.Weapon.GetComponentInChildren<UnityEngine.Renderer>().bounds.ToString() };
foreach(var kick in new[]{("chamber",.22f),("strike",.34f)}) {
BeMyArms.M7.EditorTools.M7FighterBuilder.Clip("P1","Idle_Loop").SampleAnimation(anim.P1Animator.gameObject,0);
anim.SetUpperBodyAim(0,0);anim.SetHeadLook(0,0);pose.Pose(false,0);anim.ApplyKickPose(kick.Item2,true);
// Skinning can cache the first pose for repeated manual camera renders in one editor frame.
// Bake this posed surface for the still capture so the fixture shows the actual bone pose.
var skins=body.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>();
var posed=new System.Collections.Generic.List<UnityEngine.GameObject>();
var meshes=new System.Collections.Generic.List<UnityEngine.Mesh>();
foreach(var skin in skins) {
var mesh=new UnityEngine.Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
var surface=new UnityEngine.GameObject("PosedFixtureSurface");surface.layer=31;surface.transform.SetParent(skin.transform,false);
surface.AddComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;surface.AddComponent<UnityEngine.MeshRenderer>().sharedMaterials=skin.sharedMaterials;
skin.enabled=false;posed.Add(surface);
}
cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,1000,1000),0,0);tex.Apply();
System.IO.File.WriteAllBytes("Builds/Pass2Redo/QA/kick-"+kick.Item1+".png",tex.EncodeToPNG());UnityEngine.RenderTexture.active=old;
foreach(var skin in skins)skin.enabled=true;foreach(var surface in posed)UnityEngine.Object.DestroyImmediate(surface);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
}
var vm=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(BeMyArms.M7.EditorTools.M7FighterBuilder.ArmsPath));
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(vm,previewScene);
foreach(var t in vm.GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.layer=30;
BeMyArms.M7.EditorTools.M7FighterBuilder.Clip("Arms","Idle_Loop").SampleAnimation(vm.GetComponentInChildren<UnityEngine.Animator>().gameObject,0);
vm.GetComponent<BeMyArms.M7.M7RiflePose>().Pose(false,0);
cam.targetTexture=null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
rt=new UnityEngine.RenderTexture(1280,720,24); tex=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false); cam.targetTexture=rt;
cam.cullingMask=1<<30; cam.fieldOfView=68; cam.nearClipPlane=.01f;
cam.transform.SetPositionAndRotation(UnityEngine.Vector3.zero,UnityEngine.Quaternion.identity);
light.cullingMask=1<<30;
cam.Render(); UnityEngine.RenderTexture.active=rt; tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0); tex.Apply();
System.IO.File.WriteAllBytes("Builds/Pass2Redo/QA/fps.png",tex.EncodeToPNG()); UnityEngine.RenderTexture.active=old;
UnityEngine.Object.DestroyImmediate(vm);
cam.targetTexture=null;
UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(body); UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(lightGo);
UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);
return result;
