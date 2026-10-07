var model=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(BeMyArms.M7.EditorTools.M7SmoothFighterBuilder.ModelPath));
var result=new System.Collections.Generic.List<string>();
try {
model.GetComponent<UnityEngine.Animator>().avatar=null;
foreach(var direction in new[]{"L","R"})foreach(var prefix in new[]{"Jog_","Crouch_"}) {
var clip=BeMyArms.M7.EditorTools.M7SmoothFighterBuilder.Clip(prefix+direction+"_Loop");
var foot=BeMyArms.M7.M7RiflePose.Find(model.transform,"foot_l");
for(int i=0;i<60;i++) {
clip.SampleAnimation(model,clip.length*i/60f);var a=foot.position;
clip.SampleAnimation(model,clip.length*(i+1)/60f);var b=foot.position;var v=b-a;v.y=0;
if(a.y<.18f && b.y<.18f)result.Add(prefix+direction+" "+i+" p="+a.ToString("F3")+" v="+v.ToString("F3")+" dot="+UnityEngine.Vector3.Dot(v.normalized,direction=="L"?UnityEngine.Vector3.right:UnityEngine.Vector3.left));
}
}
} finally {UnityEngine.Object.DestroyImmediate(model);}
return result;
