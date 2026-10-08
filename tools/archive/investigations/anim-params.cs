var sb = new System.Text.StringBuilder();
var anims = UnityEngine.Object.FindObjectsByType<UnityEngine.Animator>(UnityEngine.FindObjectsInactive.Exclude);
foreach (var a in anims)
{
    if (a.transform.parent == null || a.transform.parent.name != "P1Skin_clay_p1") continue;
    var si = a.GetCurrentAnimatorStateInfo(0);
    sb.Append("P1 moveX=").Append(a.GetFloat("MoveX").ToString("F2"))
      .Append(" moveY=").Append(a.GetFloat("MoveY").ToString("F2"))
      .Append(" speed=").Append(a.GetFloat("Speed").ToString("F2"))
      .Append(" animSpeed=").Append(a.speed.ToString("F1"))
      .Append(" state=").Append(si.fullPathHash)
      .Append('\n');
}
UnityEngine.Debug.Log("[animparams]\n" + sb);
return sb.ToString();
