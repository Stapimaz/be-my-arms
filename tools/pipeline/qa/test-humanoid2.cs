var sb = new System.Text.StringBuilder();
try
{
    var kaykit = "Assets/ThirdParty/KayKitCharacterAnimations/Rig_Medium_MovementAdvanced.fbx";
    var ki = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(kaykit);
    sb.Append("kaykit importer=").Append(ki != null).Append('\n');
    if (ki != null) { ki.animationType = UnityEditor.ModelImporterAnimationType.Human; ki.SaveAndReimport(); }
    UnityEngine.Avatar kav = null;
    foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(kaykit)) if (a is UnityEngine.Avatar av) kav = av;
    sb.Append("kaykit avatar=").Append(kav != null).Append(" human=").Append(kav != null && kav.isHuman).Append(" valid=").Append(kav != null && kav.isValid).Append('\n');

    var path = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx";
    var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
    sb.Append("quat importer=").Append(importer != null).Append('\n');
    importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
    var hd = importer.humanDescription;
    var bones = new System.Collections.Generic.List<UnityEngine.HumanBone>();
    System.Action<string, string> add = (human, bone) => bones.Add(new UnityEngine.HumanBone { humanName = human, boneName = bone, limit = new UnityEngine.HumanLimit { useDefaultValues = true } });
    add("Hips", "DEF-hips"); add("Spine", "DEF-spine.001"); add("Chest", "DEF-spine.002"); add("UpperChest", "DEF-spine.003");
    add("Neck", "DEF-neck"); add("Head", "DEF-head");
    add("LeftShoulder", "DEF-shoulder.L"); add("LeftUpperArm", "DEF-upper_arm.L"); add("LeftLowerArm", "DEF-forearm.L"); add("LeftHand", "DEF-hand.L");
    add("RightShoulder", "DEF-shoulder.R"); add("RightUpperArm", "DEF-upper_arm.R"); add("RightLowerArm", "DEF-forearm.R"); add("RightHand", "DEF-hand.R");
    add("LeftUpperLeg", "DEF-thigh.L"); add("LeftLowerLeg", "DEF-shin.L"); add("LeftFoot", "DEF-foot.L"); add("LeftToes", "DEF-toe.L");
    add("RightUpperLeg", "DEF-thigh.R"); add("RightLowerLeg", "DEF-shin.R"); add("RightFoot", "DEF-foot.R"); add("RightToes", "DEF-toe.R");
    hd.human = bones.ToArray();
    hd.skeleton = new UnityEngine.SkeletonBone[0];
    importer.humanDescription = hd;
    importer.SaveAndReimport();
    UnityEngine.Avatar qav = null;
    foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path)) if (a is UnityEngine.Avatar av) qav = av;
    sb.Append("quat avatar=").Append(qav != null).Append(" human=").Append(qav != null && qav.isHuman).Append(" valid=").Append(qav != null && qav.isValid).Append('\n');
    if (qav != null)
    {
        var qhd = qav.humanDescription;
        int mapped = 0;
        if (qhd.human != null) foreach (var h in qhd.human) if (!string.IsNullOrEmpty(h.boneName)) mapped++;
        sb.Append("quat mapped=").Append(mapped).Append('\n');
    }
}
catch (System.Exception e)
{
    sb.Append("EXCEPTION ").Append(e.GetType().Name).Append(": ").Append(e.Message).Append('\n').Append(e.StackTrace).Append('\n');
}
UnityEngine.Debug.Log("[humanoid2]\n" + sb);
return sb.ToString();
