var fbx = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P2_Arms.fbx";
UnityEngine.AnimationClip aim = null;
foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbx))
    if (a is UnityEngine.AnimationClip c && c.name == "Rig|Pistol_Aim_Neutral") aim = c;
var sb = new System.Text.StringBuilder();
sb.Append("clip=").Append(aim != null ? aim.name : "null").Append(" len=").Append(aim != null ? aim.length : -1).Append('\n');
var model = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(fbx));
UnityEngine.Transform F(string n) { foreach (var t in model.GetComponentsInChildren<UnityEngine.Transform>(true)) if (t.name == n) return t; return null; }
var root = model.transform;
UnityEngine.Debug.Log("[pose] chest before=" + F("DEF-spine.003").position.ToString("F3") + " handL=" + F("DEF-hand.L").position.ToString("F3"));
aim.SampleAnimation(model, 0.1f);
sb.Append("chest=").Append(F("DEF-spine.003").position.ToString("F3")).Append('\n');
sb.Append("handL local=").Append(root.InverseTransformPoint(F("DEF-hand.L").position).ToString("F3"));
sb.Append(" world=").Append(F("DEF-hand.L").position.ToString("F3")).Append('\n');
sb.Append("handR local=").Append(root.InverseTransformPoint(F("DEF-hand.R").position).ToString("F3"));
sb.Append(" world=").Append(F("DEF-hand.R").position.ToString("F3")).Append('\n');
sb.Append("forearmL=").Append(root.InverseTransformPoint(F("DEF-forearm.L").position).ToString("F3")).Append('\n');
sb.Append("forearmR=").Append(root.InverseTransformPoint(F("DEF-forearm.R").position).ToString("F3")).Append('\n');
UnityEngine.Object.DestroyImmediate(model);
UnityEngine.Debug.Log("[posesample]\n" + sb);
return "sampled";
