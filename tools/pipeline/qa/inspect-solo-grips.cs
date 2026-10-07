var vm=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(BeMyArms.M7.EditorTools.M7FighterBuilder.ArmsPath));
try {
var pose=vm.GetComponent<BeMyArms.M7.M7RiflePose>();
BeMyArms.M7.EditorTools.M7SmoothFighterBuilder.Clip("Rifle_Hold_Loop").SampleAnimation(pose.Model.gameObject,0);
pose.Pose(false,0);
var results=new System.Collections.Generic.List<object>();
foreach(int frame in new[]{0,15,30,45,60,90,120}){
var clip=BeMyArms.M7.EditorTools.M7SmoothFighterBuilder.Clip("Rifle_Hold_Loop");clip.SampleAnimation(pose.Model.gameObject,clip.length*frame/120f);pose.Pose(false,0);
results.AddRange(new[]{"l","r"}.Select(s=> {
var elbow=BeMyArms.M7.M7RiflePose.Find(pose.Model,"lowerarm_"+s);
var wrist=BeMyArms.M7.M7RiflePose.Find(pose.Model,"hand_"+s);
var middle=BeMyArms.M7.M7RiflePose.Find(pose.Model,"middle_01_"+s);
return new { Frame=frame,Side=s, Elbow=elbow.position.ToString("F3"), Wrist=wrist.position.ToString("F3"), Middle=middle.position.ToString("F3"),
Bend=UnityEngine.Vector3.Angle(wrist.position-elbow.position,middle.position-wrist.position) }; }));}
return results;
}finally{UnityEngine.Object.DestroyImmediate(vm);}
