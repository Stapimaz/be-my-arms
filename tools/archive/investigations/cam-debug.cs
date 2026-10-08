var sb = new System.Text.StringBuilder();
var cams = UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(UnityEngine.FindObjectsInactive.Exclude);
foreach (var c in cams)
{
    sb.Append(c.name).Append(" enabled=").Append(c.enabled).Append(" depth=").Append(c.depth)
      .Append(" clear=").Append(c.clearFlags).Append(" mask=").Append(c.cullingMask)
      .Append(" fov=").Append(c.fieldOfView.ToString("F1"))
      .Append(" pos=").Append(c.transform.position.ToString("F2"))
      .Append(" euler=").Append(c.transform.eulerAngles.ToString("F1"))
      .Append('\n');
}
UnityEngine.Debug.Log("[camdbg]\n" + sb);
return sb.ToString();
