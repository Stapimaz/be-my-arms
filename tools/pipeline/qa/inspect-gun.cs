var path = "Assets/ThirdParty/QuaterniusLowPolyGunsPack/AssaultRifle_1.fbx";
UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate);
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
var sb = new System.Text.StringBuilder();
sb.Append("prefab=").Append(prefab != null).Append('\n');
// world bounds
var rends = prefab.GetComponentsInChildren<UnityEngine.Renderer>(true);
var wb = new UnityEngine.Bounds();
bool first = true;
foreach (var r in rends)
{
    if (first) { wb = r.bounds; first = false; } else wb.Encapsulate(r.bounds);
}
sb.Append("renderers=").Append(rends.Length).Append(" worldCenter=").Append(wb.center.ToString("F3")).Append(" worldSize=").Append(wb.size.ToString("F3")).Append('\n');
// local-to-root space bounds
UnityEngine.Transform root = prefab.transform;
var lb = new UnityEngine.Bounds();
first = true;
foreach (var r in rends)
{
    var lc = root.InverseTransformPoint(r.bounds.center);
    var ls = r.bounds.size;
    var b = new UnityEngine.Bounds(lc, ls);
    if (first) { lb = b; first = false; } else lb.Encapsulate(b);
}
sb.Append("root localCenter=").Append(lb.center.ToString("F3")).Append(" localSize=").Append(lb.size.ToString("F3")).Append('\n');
void Dump(UnityEngine.Transform t, int d)
{
    sb.Append(new string(' ', d * 2)).Append(t.name).Append(" local=").Append(t.localPosition.ToString("F2")).Append(" rot=").Append(t.localRotation.eulerAngles.ToString("F0")).Append(" scale=").Append(t.localScale.ToString("F3")).Append('\n');
    if (d < 3) for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), d + 1);
}
Dump(root, 0);
sb.Append("materials=").Append(string.Join(",", System.Array.ConvertAll(rends.Length > 0 ? rends[0].sharedMaterials : new UnityEngine.Material[0], m => m != null ? m.name : "null"))).Append('\n');
UnityEngine.Debug.Log("[gun]\n" + sb);
return sb.ToString();
