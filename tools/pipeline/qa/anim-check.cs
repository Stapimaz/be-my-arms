var sb = new System.Text.StringBuilder();
var anims = UnityEngine.Object.FindObjectsByType<UnityEngine.Animator>(UnityEngine.FindObjectsInactive.Exclude);
foreach (var a in anims)
{
    UnityEngine.Transform thigh = null;
    foreach (var t in a.GetComponentsInChildren<UnityEngine.Transform>(true)) if (t.name == "DEF-thigh.L") { thigh = t; break; }
    var si = a.GetCurrentAnimatorStateInfo(0);
    sb.Append(a.transform.parent != null ? a.transform.parent.name : "-").Append('/').Append(a.gameObject.name)
      .Append(" speed=").Append(a.GetFloat("Speed").ToString("F2"))
      .Append(" t=").Append(si.normalizedTime.ToString("F3"))
      .Append(" thigh=").Append(thigh != null ? thigh.localRotation.eulerAngles.ToString("F2") : "missing")
      .Append('\n');
}
UnityEngine.Debug.Log("[animcheck]\n" + sb);
return sb.ToString();
