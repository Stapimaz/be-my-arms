var sb = new System.Text.StringBuilder();
var body = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Art/Characters/M7PlayerBody.prefab");
var go = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(body);
UnityEngine.Transform F(string n) { foreach (var t in go.GetComponentsInChildren<UnityEngine.Transform>(true)) if (t.name == n) return t; return null; }
UnityEngine.Bounds B(string rootName)
{
    var t = F(rootName); if (t == null) return new UnityEngine.Bounds();
    var rs = t.GetComponentsInChildren<UnityEngine.Renderer>(true); if (rs.Length == 0) return new UnityEngine.Bounds();
    var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); return b;
}
var b1 = B("P1Skin_clay_p1"); var b2 = B("P2Skin_clay_p2");
sb.Append("P1 center=").Append(b1.center.ToString("F3")).Append(" size=").Append(b1.size.ToString("F3")).Append('\n');
sb.Append("P2 center=").Append(b2.center.ToString("F3")).Append(" size=").Append(b2.size.ToString("F3")).Append('\n');
var hl = F("DEF-hand.L"); var hr = F("DEF-hand.R"); var chest = F("DEF-spine.003"); var aim = F("AimPivot"); var bodyGo = go;
sb.Append("handL=").Append(hl != null ? hl.position.ToString("F3") : "null").Append(" handR=").Append(hr != null ? hr.position.ToString("F3") : "null").Append('\n');
sb.Append("chest=").Append(chest != null ? chest.position.ToString("F3") : "null").Append(" aimPivot=").Append(aim != null ? aim.position.ToString("F3") : "null").Append('\n');
UnityEngine.Object.DestroyImmediate(go);
UnityEngine.Debug.Log("[bodyalign]\n" + sb);
return "checked";
