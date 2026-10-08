var t = typeof(UnityEditor.Animations.AnimatorController);
var sb = new System.Text.StringBuilder();
foreach (var m in t.GetMethods())
{
    if (m.Name != "AddLayer" && m.Name != "RemoveLayer") continue;
    sb.Append(m.ReturnType.Name).Append(" ").Append(m.Name).Append('(');
    var ps = m.GetParameters();
    for (int i = 0; i < ps.Length; i++) { if (i > 0) sb.Append(", "); sb.Append(ps[i].ParameterType.Name).Append(' ').Append(ps[i].Name); }
    sb.Append(")\n");
}
var lt = typeof(UnityEditor.Animations.AnimatorControllerLayer);
sb.Append("Layer ctors:\n");
foreach (var ctor in lt.GetConstructors())
{
    sb.Append("  ("); var ps = ctor.GetParameters();
    for (int i = 0; i < ps.Length; i++) { if (i > 0) sb.Append(", "); sb.Append(ps[i].ParameterType.Name); }
    sb.Append(")\n");
}
foreach (var p in lt.GetProperties()) if (p.Name == "name" || p.Name == "stateMachine" || p.Name == "defaultWeight" || p.Name == "blendingMode") sb.Append("prop ").Append(p.Name).Append(" set=").Append(p.CanWrite).Append('\n');
UnityEngine.Debug.Log("[api]\n" + sb);
return sb.ToString();
