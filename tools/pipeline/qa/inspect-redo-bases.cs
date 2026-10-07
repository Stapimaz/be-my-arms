var go=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(BeMyArms.M7.EditorTools.M7SmoothFighterBuilder.ModelPath));
var result=new System.Collections.Generic.List<string>();
try {
go.GetComponent<UnityEngine.Animator>().avatar=null;
foreach(string clip in new[]{"Idle_Loop","Jog_F_Loop","Jog_L_Loop","Crouch_F_Loop","Rifle_Hold_Loop"}) {
BeMyArms.M7.EditorTools.M7SmoothFighterBuilder.Clip(clip).SampleAnimation(go,0);
foreach(string bone in new[]{"Rig","root","pelvis","spine_03","Head","hand_l","hand_r"}) {
var t=BeMyArms.M7.M7RiflePose.Find(go.transform,bone);
result.Add(clip+" "+bone+" local rot="+t.localEulerAngles.ToString("F2")+" world rot="+t.eulerAngles.ToString("F2"));
}
}
} finally {UnityEngine.Object.DestroyImmediate(go);}
return result;
