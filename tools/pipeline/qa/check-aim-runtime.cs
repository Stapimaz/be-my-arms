var sb = new System.Text.StringBuilder();
BeMyArms.M3.M3DuelClient local = null;
foreach (var c in UnityEngine.Object.FindObjectsByType<BeMyArms.M3.M3DuelClient>(UnityEngine.FindObjectsSortMode.None))
    if (c.IsLocalOwnBody) local = c;
if (local == null || local.Body == null) { sb.Append("no local body\n"); }
else
{
    var body = local.Body;
    var root = body.transform;
    sb.Append("role=").Append(local.LocalRoleIndex).Append(" bodyYaw=").Append(local.Body.State.Value.BodyYaw.ToString("0.0"))
      .Append(" aimYaw=").Append(local.LocalAimYaw.ToString("0.0")).Append(" aimPitch=").Append(local.LocalAimPitch.ToString("0.0")).Append('\n');
    sb.Append("bodyPos=").Append(root.position.ToString("F2")).Append(" bodyFwd=").Append(root.forward.ToString("F2")).Append('\n');
    foreach (var hand in new[] { "DEF-hand.L", "DEF-hand.R" })
    {
        UnityEngine.Transform t = null;
        foreach (var c in root.GetComponentsInChildren<UnityEngine.Transform>(true)) if (c.name == hand) { t = c; break; }
        if (t == null) { sb.Append(hand).Append("=missing\n"); continue; }
        var l = root.InverseTransformPoint(t.position);
        sb.Append(hand).Append(" local=").Append(l.ToString("F2")).Append('\n');
    }
    // Animator state sanity.
    foreach (var an in root.GetComponentsInChildren<UnityEngine.Animator>(true))
    {
        var st = an.GetCurrentAnimatorStateInfo(0);
        sb.Append("anim ").Append(an.name).Append(" state=").Append(st.shortNameHash).Append(" normTime=").Append(st.normalizedTime.ToString("0.00")).Append('\n');
    }
}
UnityEngine.Debug.Log("[aimrt]\n" + sb);
return sb.ToString();
