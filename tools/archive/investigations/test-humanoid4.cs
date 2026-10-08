var sb = new System.Text.StringBuilder();
try
{
    var path = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx";
    var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
    importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
    importer.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;
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
    UnityEngine.Avatar av = null;
    int clips = 0;
    foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
    {
        if (a is UnityEngine.Avatar v) av = v;
        if (a is UnityEngine.AnimationClip c && !c.name.StartsWith("__preview__")) clips++;
    }
    sb.Append("avatar=").Append(av != null).Append(" human=").Append(av != null && av.isHuman).Append(" valid=").Append(av != null && av.isValid).Append('\n');
    if (av != null && av.humanDescription.human != null)
    {
        int mapped = 0; foreach (var h in av.humanDescription.human) if (!string.IsNullOrEmpty(h.boneName)) mapped++;
        sb.Append("mapped=").Append(mapped).Append('\n');
    }
    sb.Append("clips=").Append(clips).Append('\n');
}
catch (System.Exception e) { sb.Append("EXCEPTION ").Append(e.GetType().Name).Append(": ").Append(e.Message).Append('\n'); }
UnityEngine.Debug.Log("[humanoid4]\n" + sb);
return sb.ToString();
