var sb = new System.Text.StringBuilder();
System.Action<string> check = (p) =>
{
    var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(p);
    sb.Append("== ").Append(p).Append(go == null ? " MISSING" : "").Append('\n');
    if (go == null) return;
    var anim = go.GetComponentInChildren<UnityEngine.Animator>();
    sb.Append("  animator=").Append(anim != null).Append(" ctrl=").Append(anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "none").Append('\n');
    var sk = go.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true);
    sb.Append("  skinned=").Append(sk.Length).Append(sk.Length > 0 ? " verts=" + sk[0].sharedMesh.vertexCount + " mats=" + sk[0].sharedMaterials.Length : "").Append('\n');
    var desc = go.GetComponent<BeMyArms.M5.M5SkinDescriptor>();
    sb.Append("  descriptor=").Append(desc != null ? desc.Role + "/" + desc.SkinId : "none").Append('\n');
    var rb = go.GetComponentInChildren<UnityEngine.Animations.Rigging.RigBuilder>();
    sb.Append("  rigBuilder=").Append(rb != null).Append(rb != null ? " layers=" + rb.layers.Count : "").Append('\n');
    var ik = go.GetComponentsInChildren<UnityEngine.Animations.Rigging.TwoBoneIKConstraint>(true);
    foreach (var c in ik)
    {
        sb.Append("  ").Append(c.gameObject.name).Append(" valid=").Append(c.IsValid())
          .Append(" root=").Append(c.data.root != null ? c.data.root.name : "null")
          .Append(" mid=").Append(c.data.mid != null ? c.data.mid.name : "null")
          .Append(" tip=").Append(c.data.tip != null ? c.data.tip.name : "null")
          .Append(" target=").Append(c.data.target != null ? c.data.target.name : "null").Append('\n');
    }
};
check("Assets/Art/Characters/Quaternius/Prefabs/BMA_P1_Clay.prefab");
check("Assets/Art/Characters/Quaternius/Prefabs/BMA_P2_Clay.prefab");
check("Assets/Art/Characters/Quaternius/Prefabs/BMA_P2_Arms.prefab");
foreach (var ctrlPath in new[] { "Assets/Art/Characters/Quaternius/Controllers/M7_P1_Locomotion.controller", "Assets/Art/Characters/Quaternius/Controllers/M7_P2_Locomotion.controller", "Assets/Art/Characters/Quaternius/Controllers/M7_P2_ArmsAim.controller" })
{
    var c = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
    if (c == null) { sb.Append("== ").Append(ctrlPath).Append(" MISSING\n"); continue; }
    sb.Append("== ").Append(c.name).Append(" states=");
    foreach (var s in c.layers[0].stateMachine.states) sb.Append(s.state.name).Append('(').Append(s.state.motion != null ? s.state.motion.name : "null").Append(") ");
    sb.Append('\n');
}
UnityEngine.Debug.Log("[charcheck]\n" + sb);
return "checked";
