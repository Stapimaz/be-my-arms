var sb = new System.Text.StringBuilder();
var anims = UnityEngine.Object.FindObjectsByType<UnityEngine.Animator>(UnityEngine.FindObjectsInactive.Exclude);
sb.Append("animators=").Append(anims.Length).Append('\n');
foreach (var a in anims)
{
    string ctrl = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "null";
    string av = a.avatar != null ? (a.avatar.name + " valid=" + a.avatar.isValid + " human=" + a.avatar.isHuman) : "null";
    var si = a.GetCurrentAnimatorStateInfo(0);
    sb.Append("  root=").Append(a.transform.root.name).Append(" go=").Append(a.transform.parent != null ? a.transform.parent.name : "-").Append('/').Append(a.gameObject.name)
      .Append(" ctrl=").Append(ctrl).Append(" avatar=").Append(av)
      .Append(" enabled=").Append(a.enabled).Append(" cull=").Append(a.cullingMode)
      .Append(" speed=").Append(a.GetFloat("Speed").ToString("0.00"))
      .Append(" grounded=").Append(a.GetBool("Grounded"))
      .Append(" vs=").Append(a.GetFloat("VerticalSpeed").ToString("0.00"))
      .Append(" alive=").Append(a.GetBool("Alive"))
      .Append(" stateFull=").Append(si.fullPathHash).Append(" t=").Append(si.normalizedTime.ToString("0.000")).Append(" len=").Append(si.length.ToString("0.00"))
      .Append('\n');
    if (a.runtimeAnimatorController != null)
        foreach (var c in a.runtimeAnimatorController.animationClips)
            sb.Append("      clip ").Append(c.name).Append(" len=").Append(c.length.ToString("0.00")).Append(" loop=").Append(c.isLooping).Append(" wrap=").Append(c.wrapMode).Append('\n');
}
// Bone visibility check: find DEF-head under each skin and report world position.
UnityEngine.Transform FindBone(string rootName, string bone)
{
    var go = UnityEngine.GameObject.Find(rootName);
    if (go == null) return null;
    foreach (var t in go.GetComponentsInChildren<UnityEngine.Transform>(true)) if (t.name == bone) return t;
    return null;
}
foreach (var skin in new[] { "P1Skin_clay_p1", "P2Skin_clay_p2" })
{
    var b = FindBone(skin, "DEF-head");
    if (b != null) sb.Append("bone ").Append(skin).Append(" DEF-head=").Append(b.position.ToString("F3")).Append(" localRot=").Append(b.localRotation.eulerAngles.ToString("F0")).Append('\n');
}
UnityEngine.Debug.Log("[animdiag]\n" + sb);
return sb.ToString();
