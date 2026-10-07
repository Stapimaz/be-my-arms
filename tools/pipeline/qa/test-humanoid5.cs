var sb = new System.Text.StringBuilder();
try
{
    var path = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx";
    var avatarPath = "Assets/Art/Characters/Quaternius/Q_Body_Avatar.asset";
    var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);

    // 1. back to Generic so the model hierarchy is loadable
    if (importer.animationType != UnityEditor.ModelImporterAnimationType.Generic)
    {
        importer.animationType = UnityEditor.ModelImporterAnimationType.Generic;
        importer.SaveAndReimport();
    }
    var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);

    // 2. build a Humanoid avatar from the model hierarchy
    var bones = new System.Collections.Generic.List<UnityEngine.HumanBone>();
    System.Action<string, string> add = (human, bone) => bones.Add(new UnityEngine.HumanBone { humanName = human, boneName = bone, limit = new UnityEngine.HumanLimit { useDefaultValues = true } });
    add("Hips", "DEF-hips"); add("Spine", "DEF-spine.001"); add("Chest", "DEF-spine.002"); add("UpperChest", "DEF-spine.003");
    add("Neck", "DEF-neck"); add("Head", "DEF-head");
    add("LeftShoulder", "DEF-shoulder.L"); add("LeftUpperArm", "DEF-upper_arm.L"); add("LeftLowerArm", "DEF-forearm.L"); add("LeftHand", "DEF-hand.L");
    add("RightShoulder", "DEF-shoulder.R"); add("RightUpperArm", "DEF-upper_arm.R"); add("RightLowerArm", "DEF-forearm.R"); add("RightHand", "DEF-hand.R");
    add("LeftUpperLeg", "DEF-thigh.L"); add("LeftLowerLeg", "DEF-shin.L"); add("LeftFoot", "DEF-foot.L"); add("LeftToes", "DEF-toe.L");
    add("RightUpperLeg", "DEF-thigh.R"); add("RightLowerLeg", "DEF-shin.R"); add("RightFoot", "DEF-foot.R"); add("RightToes", "DEF-toe.R");
    var hd = new UnityEngine.HumanDescription { human = bones.ToArray(), skeleton = new UnityEngine.SkeletonBone[0] };
    var built = UnityEngine.AvatarBuilder.BuildHumanAvatar(go, hd);
    sb.Append("built human=").Append(built.isHuman).Append(" valid=").Append(built.isValid).Append('\n');
    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(avatarPath));
    if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Avatar>(avatarPath) != null) UnityEditor.AssetDatabase.DeleteAsset(avatarPath);
    built.name = "Q_Body_Avatar";
    UnityEditor.AssetDatabase.CreateAsset(built, avatarPath);
    UnityEditor.AssetDatabase.SaveAssets();

    // 3. use it as the model's source avatar
    importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
    importer.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CopyFromOther;
    importer.sourceAvatar = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Avatar>(avatarPath);
    importer.SaveAndReimport();

    UnityEngine.Avatar av = null; int clips = 0;
    foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
    {
        if (a is UnityEngine.Avatar v) av = v;
        if (a is UnityEngine.AnimationClip c && !c.name.StartsWith("__preview__")) clips++;
    }
    sb.Append("model avatar=").Append(av != null).Append(" human=").Append(av != null && av.isHuman).Append(" valid=").Append(av != null && av.isValid).Append(" clips=").Append(clips).Append('\n');
}
catch (System.Exception e) { sb.Append("EXCEPTION ").Append(e.GetType().Name).Append(": ").Append(e.Message).Append('\n'); }
UnityEngine.Debug.Log("[humanoid5]\n" + sb);
return sb.ToString();
