var path = "Assets/TempLayerTest.controller";
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path) != null)
    UnityEditor.AssetDatabase.DeleteAsset(path);
var c = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
var sb = new System.Text.StringBuilder();
sb.Append("before=").Append(c.layers.Length).Append('\n');
var l = c.AddLayer("Crouch");
sb.Append("after=").Append(c.layers.Length).Append(" returned=").Append(l != null ? (l.name + " weight=" + l.defaultWeight + " sm=" + (l.stateMachine != null)) : "null").Append('\n');
l.defaultWeight = 0f;
sb.Append("set weight on returned\n");
foreach (var layer in c.layers) sb.Append("  layer ").Append(layer.name).Append(" w=").Append(layer.defaultWeight).Append('\n');
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.DeleteAsset(path);
UnityEngine.Debug.Log("[layertest]\n" + sb);
return sb.ToString();
