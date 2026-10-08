var sb = new System.Text.StringBuilder();
var anims = UnityEngine.Object.FindObjectsByType<UnityEngine.Animator>(UnityEngine.FindObjectsInactive.Exclude);
foreach (var a in anims)
{
    if (a.transform.parent == null || a.transform.parent.name != "P1Skin_clay_p1") continue;
    var hips = (UnityEngine.Transform)null;
    foreach (var t in a.GetComponentsInChildren<UnityEngine.Transform>(true)) if (t.name == "DEF-hips") { hips = t; break; }
    var thigh = (UnityEngine.Transform)null;
    foreach (var t in a.GetComponentsInChildren<UnityEngine.Transform>(true)) if (t.name == "DEF-thigh.L") { thigh = t; break; }
    sb.Append("P1 layer1W=").Append(a.GetLayerWeight(1).ToString("F2"))
      .Append(" speed=").Append(a.GetFloat("Speed").ToString("F2"))
      .Append(" animSpeed=").Append(a.speed.ToString("F1"))
      .Append(" hipsEuler=").Append(hips != null ? hips.localRotation.eulerAngles.ToString("F1") : "-")
      .Append(" thighWorld=").Append(thigh != null ? thigh.position.ToString("F3") : "-")
      .Append('\n');
}
UnityEngine.Debug.Log("[animhip]\n" + sb);
return sb.ToString();
