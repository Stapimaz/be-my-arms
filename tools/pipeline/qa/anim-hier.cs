var sb = new System.Text.StringBuilder();
var go = UnityEngine.GameObject.Find("P1Skin_clay_p1");
void Dump(UnityEngine.Transform t, int d)
{
    sb.Append(new string(' ', d * 2)).Append(t.name);
    var an = t.GetComponent<UnityEngine.Animator>();
    if (an != null) sb.Append(" [Animator ctrl=").Append(an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "null").Append(']');
    sb.Append('\n');
    if (d < 3) for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), d + 1);
}
if (go != null) Dump(go.transform, 0);
var anim = go != null ? go.GetComponentInChildren<UnityEngine.Animator>(true) : null;
if (anim != null)
{
    sb.Append("animPath=").Append(anim.transform.name).Append(" parent=").Append(anim.transform.parent != null ? anim.transform.parent.name : "-").Append('\n');
    sb.Append("hasRigChild=").Append(anim.transform.Find("Rig") != null).Append('\n');
    sb.Append("playableGraphValid=").Append(anim.isActiveAndEnabled).Append('\n');
    var st = anim.GetCurrentAnimatorStateInfo(0);
    sb.Append("weight=").Append(anim.GetLayerWeight(0).ToString("F2")).Append(" stateLen=").Append(st.length.ToString("F2")).Append('\n');
}
UnityEngine.Debug.Log("[hier]\n" + sb);
return sb.ToString();
