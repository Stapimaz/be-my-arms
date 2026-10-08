var model=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(BeMyArms.M7.EditorTools.M7SmoothFighterBuilder.ModelPath));
var result=new System.Collections.Generic.List<string>();
try {
model.GetComponent<UnityEngine.Animator>().avatar=null;
model.transform.rotation=UnityEngine.Quaternion.identity;
foreach(string name in new[]{"Idle_Loop","Jog_F_Loop","Jog_B_Loop","Jog_L_Loop","Jog_R_Loop","Crouch_F_Loop","Crouch_B_Loop","Crouch_L_Loop","Crouch_R_Loop","Slide_Loop","Rifle_Hold_Loop"}) {
var c=BeMyArms.M7.EditorTools.M7SmoothFighterBuilder.Clip(name);
var l=BeMyArms.M7.M7RiflePose.Find(model.transform,"foot_l");var r=BeMyArms.M7.M7RiflePose.Find(model.transform,"foot_r");
var hip=BeMyArms.M7.M7RiflePose.Find(model.transform,"pelvis");
var sb=new System.Text.StringBuilder(name+" length="+c.length);
for(int i=0;i<=4;i++){c.SampleAnimation(model,c.length*i/4f);sb.Append("\n").Append(i).Append(" L=").Append(l.position.ToString("F3")).Append(" R=").Append(r.position.ToString("F3")).Append(" pelvis=").Append(hip.position.ToString("F3"));}
result.Add(sb.ToString());
}
} finally { UnityEngine.Object.DestroyImmediate(model); }
return result;
