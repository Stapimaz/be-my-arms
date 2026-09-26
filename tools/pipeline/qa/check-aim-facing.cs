var fbx = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P2_Body.fbx";
UnityEngine.AnimationClip aim = null;
foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbx))
{
    if (a is UnityEngine.AnimationClip c && c.name.Contains("Pistol_Aim_Neutral")) aim = c;
}
var model = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(fbx));
UnityEngine.Transform F(string n) { foreach (var t in model.GetComponentsInChildren<UnityEngine.Transform>(true)) if (t.name == n) return t; return null; }
aim.SampleAnimation(model, 0.2f);
var chest = F("DEF-spine.003"); var hl = F("DEF-hand.L"); var hr = F("DEF-hand.R");
var l = chest.InverseTransformPoint(hl.position); var r = chest.InverseTransformPoint(hr.position);
UnityEngine.Debug.Log("[aim] chestWorld=" + chest.position.ToString("F3") + " handL(localToChest)=" + l.ToString("F3") + " handR=" + r.ToString("F3"));
UnityEngine.Object.DestroyImmediate(model);
return "aim-check";
