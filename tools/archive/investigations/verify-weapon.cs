var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Art/Weapons/Prefabs/BMA_Weapon_Rifle_Quaternius.prefab");
var sb = new System.Text.StringBuilder();
var root = prefab.transform;
var pts = new System.Collections.Generic.List<UnityEngine.Vector3>();
foreach (var mf in prefab.GetComponentsInChildren<UnityEngine.MeshFilter>(true))
{
    var mesh = mf.sharedMesh; if (mesh == null) continue;
    var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
    foreach (var v in mesh.vertices) pts.Add(m.MultiplyPoint3x4(v));
}
float zmin = 1e9f, zmax = -1e9f;
foreach (var p in pts) { zmin = UnityEngine.Mathf.Min(zmin, p.z); zmax = UnityEngine.Mathf.Max(zmax, p.z); }
float band = 0.12f * (zmax - zmin);
System.Func<float,float,float> cross = (lo, hi) => {
    float ys = -1e9f, yb = 1e9f, xs = -1e9f, xb = 1e9f; bool any=false;
    foreach (var p in pts) { if (p.z < lo || p.z > hi) continue; any=true; yb=UnityEngine.Mathf.Min(yb,p.y); ys=UnityEngine.Mathf.Max(ys,p.y); xb=UnityEngine.Mathf.Min(xb,p.x); xs=UnityEngine.Mathf.Max(xs,p.x); }
    return any ? (ys-yb)+(xs-xb) : -1f;
};
sb.Append("z=[").Append(zmin.ToString("F3")).Append("..").Append(zmax.ToString("F3")).Append("]\n");
sb.Append("rear(-Z) cross=").Append(cross(zmin, zmin+band).ToString("F3")).Append("  front(+Z) cross=").Append(cross(zmax-band, zmax).ToString("F3")).Append('\n');
UnityEngine.Debug.Log("[weaponverify]\n" + sb);
return sb.ToString();
