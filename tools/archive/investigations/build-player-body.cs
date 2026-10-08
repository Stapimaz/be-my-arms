var sb = new System.Text.StringBuilder();
UnityEngine.GameObject body = null; UnityEngine.GameObject director = null;
BeMyArms.M7.EditorTools.M7PlayerBodyBuilder.EnsurePrefabs(out body, out director);
sb.Append("body=").Append(body != null ? body.name : "null").Append(" director=").Append(director != null ? director.name : "null").Append('\n');
var anim = body.GetComponent<BeMyArms.M7.M7CharacterAnimator>();
sb.Append("animator comp=").Append(anim != null).Append('\n');
if (anim != null)
{
    sb.Append(" p1skin=").Append(anim.P1Skin != null ? anim.P1Skin.name : "null")
      .Append(" p2skin=").Append(anim.P2Skin != null ? anim.P2Skin.name : "null")
      .Append(" p1anim=").Append(anim.P1Animator != null ? anim.P1Animator.name : "null")
      .Append(" p2anim=").Append(anim.P2Animator != null ? anim.P2Animator.name : "null")
      .Append(" aim=").Append(anim.AimPivot != null ? anim.AimPivot.name : "null")
      .Append(" weapon=").Append(anim.Weapon != null ? anim.Weapon.name : "null").Append('\n');
    sb.Append(" ikL target=").Append(anim.ArmIkL != null && anim.ArmIkL.data.target != null ? anim.ArmIkL.data.target.name : "null")
      .Append(" ikR target=").Append(anim.ArmIkR != null && anim.ArmIkR.data.target != null ? anim.ArmIkR.data.target.name : "null").Append('\n');
}
// Validate the shared-body contract on the built prefab.
var rig = body.transform.Find("SharedBody");
var report = rig != null ? BeMyArms.M5.M5RigValidator.Validate(rig.gameObject, BeMyArms.M5.M5RigContract.Default()) : null;
sb.Append("contract=").Append(report != null ? report.ToString() : "no rig").Append('\n');
sb.Append("renderedParts=").Append(body.GetComponentsInChildren<UnityEngine.Renderer>(true).Length).Append('\n');
UnityEngine.Debug.Log("[bodycheck]\n" + sb);
return "player-body-built";
