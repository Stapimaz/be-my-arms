var path = "Assets/TempWeight.controller";
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path) != null)
    UnityEditor.AssetDatabase.DeleteAsset(path);
var c = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
var sb = new System.Text.StringBuilder();
sb.Append("created baseW=").Append(c.layers[0].defaultWeight).Append('\n');
c.AddLayer("Crouch");
sb.Append("after AddLayer len=").Append(c.layers.Length).Append(" baseW=").Append(c.layers[0].defaultWeight).Append(" lastW=").Append(c.layers[c.layers.Length - 1].defaultWeight).Append('\n');
c.layers[0].defaultWeight = 1f;
c.layers[c.layers.Length - 1].defaultWeight = 0f;
UnityEditor.AssetDatabase.SaveAssets();
var c2 = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
sb.Append("reloaded len=").Append(c2.layers.Length).Append(" baseW=").Append(c2.layers[0].defaultWeight).Append(" lastW=").Append(c2.layers[c2.layers.Length - 1].defaultWeight).Append('\n');
sb.Append("lastSM=").Append(c2.layers[c2.layers.Length - 1].stateMachine != null).Append('\n');
UnityEditor.AssetDatabase.DeleteAsset(path);
UnityEngine.Debug.Log("[weight]\n" + sb);
return sb.ToString();
