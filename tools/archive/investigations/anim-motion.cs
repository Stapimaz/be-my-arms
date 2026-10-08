var sb = new System.Text.StringBuilder();
var go = UnityEngine.GameObject.Find("P1Skin_clay_p1");
if (go == null) { sb.Append("no skin\n"); }
else
{
    foreach (var bone in new[] { "DEF-hand.L", "DEF-spine.001", "DEF-thigh.L", "DEF-head" })
    {
        UnityEngine.Transform t = null;
        foreach (var c in go.GetComponentsInChildren<UnityEngine.Transform>(true)) if (c.name == bone) { t = c; break; }
        if (t == null) { sb.Append(bone).Append("=missing\n"); continue; }
        sb.Append(bone).Append(" world=").Append(t.position.ToString("F4")).Append(" localRot=").Append(t.localRotation.eulerAngles.ToString("F2")).Append('\n');
    }
    var a = go.GetComponentInChildren<UnityEngine.Animator>(true);
    if (a != null)
    {
        var si = a.GetCurrentAnimatorStateInfo(0);
        sb.Append("state t=").Append(si.normalizedTime.ToString("F3")).Append(" len=").Append(si.length.ToString("F2"))
          .Append(" speed=").Append(si.speed.ToString("F2")).Append(" speedParam=").Append(a.GetFloat("Speed").ToString("F2"))
          .Append(" avatar=").Append(a.avatar != null ? "yes" : "no").Append('\n');
    }
}
UnityEngine.Debug.Log("[motion]\n" + sb);
return sb.ToString();
