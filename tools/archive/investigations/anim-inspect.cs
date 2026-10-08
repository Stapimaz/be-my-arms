var sb = new System.Text.StringBuilder();
var anims = UnityEngine.Object.FindObjectsByType<UnityEngine.Animator>(UnityEngine.FindObjectsInactive.Exclude);
foreach (var a in anims)
{
    if (a.transform.parent == null || a.transform.parent.name != "P1Skin_clay_p1") continue;
    sb.Append("anim=").Append(a.gameObject.name)
      .Append(" ctrl=").Append(a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "null")
      .Append(" enabled=").Append(a.enabled).Append(" active=").Append(a.isActiveAndEnabled)
      .Append(" init=").Append(a.isInitialized)
      .Append(" layers=").Append(a.layerCount)
      .Append(" w0=").Append(a.GetLayerWeight(0).ToString("F2"))
      .Append(" w1=").Append(a.GetLayerWeight(1).ToString("F2"))
      .Append(" clips=").Append(a.GetCurrentAnimatorClipInfoCount(0))
      .Append('\n');
}
UnityEngine.Debug.Log("[animinspect]\n" + sb);
return sb.ToString();
