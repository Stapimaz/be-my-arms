var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(BeMyArms.M7.EditorTools.M7FighterBuilder.ArmsPath);
var results=new System.Collections.Generic.List<object>();
foreach(var z in new[]{-.10f,0f,.10f}) {
var vm=UnityEngine.Object.Instantiate(prefab);
try {
var pose=vm.GetComponent<BeMyArms.M7.M7RiflePose>();
pose.Model.localPosition=new UnityEngine.Vector3(.02f,-1.55f,z);
pose.Weapon.localPosition=new UnityEngine.Vector3(.16f,-.26f,.20f);
BeMyArms.M7.EditorTools.M7FighterBuilder.Clip("Arms","Idle_Loop").SampleAnimation(pose.Model.gameObject,0);
pose.Pose(false,0);
results.Add(new { z,pose.LeftGripError,pose.RightGripError,
shoulder=BeMyArms.M7.M7RiflePose.Find(pose.Model,"upperarm_l").position.ToString(),
elbow=BeMyArms.M7.M7RiflePose.Find(pose.Model,"lowerarm_l").position.ToString() });
}finally {UnityEngine.Object.DestroyImmediate(vm);}
}
return results;
