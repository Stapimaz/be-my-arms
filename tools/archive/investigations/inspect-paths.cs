var path = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx";
var sb = new System.Text.StringBuilder();
UnityEngine.AnimationClip clip = null;
foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
    if (a is UnityEngine.AnimationClip c && c.name == "Rig|Idle_Loop") clip = c;
var binds = UnityEditor.AnimationUtility.GetCurveBindings(clip);
var seen = new System.Collections.Generic.HashSet<string>();
foreach (var b in binds)
{
    if (!seen.Add(b.path)) continue;
    if (seen.Count > 14) break;
    sb.Append('[').Append(b.path).Append("] ").Append(b.propertyName).Append('\n');
}
var go = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path));
clip.SampleAnimation(go, 0.4f);
void Find(UnityEngine.Transform t, string n, System.Text.StringBuilder s)
{
    if (t.name == n) s.Append("  ").Append(n).Append(" localRot=").Append(t.localRotation.eulerAngles.ToString("F1")).Append('\n');
    for (int i = 0; i < t.childCount; i++) Find(t.GetChild(i), n, s);
}
Find(go.transform, "DEF-head", sb);
Find(go.transform, "DEF-hand.L", sb);
UnityEngine.Object.DestroyImmediate(go);
UnityEngine.Debug.Log("[paths]\n" + sb);
return sb.ToString();
