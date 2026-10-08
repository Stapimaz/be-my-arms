var sb = new System.Text.StringBuilder();
var arms = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P2_Arms.fbx";
var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(arms);
sb.Append("arms animType=").Append(importer.animationType).Append(" avatarSetup=").Append(importer.avatarSetup).Append('\n');
int clips = 0, human = 0;
foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(arms))
    if (a is UnityEngine.AnimationClip c && !c.name.StartsWith("__preview__")) { clips++; if (c.isHumanMotion) human++; }
sb.Append("armsClips=").Append(clips).Append(" humanClips=").Append(human).Append('\n');
var vm = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Art/Characters/Quaternius/Prefabs/Resources/M7_P2ArmsViewmodel.prefab");
var anim = vm != null ? vm.GetComponentInChildren<UnityEngine.Animator>(true) : null;
sb.Append("viewmodel animator=").Append(anim != null)
  .Append(" ctrl=").Append(anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null")
  .Append(" avatar=").Append(anim != null && anim.avatar != null ? anim.avatar.name : "null").Append('\n');
UnityEngine.Debug.Log("[checkarms]\n" + sb);
return sb.ToString();
