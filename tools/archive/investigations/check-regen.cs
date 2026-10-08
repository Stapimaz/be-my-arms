var sb = new System.Text.StringBuilder();
sb.Append(BeMyArms.M7.EditorTools.M7PipelineCommands.Validate()).Append('\n');
// Map colliders for the Cinemachine deoccluder.
int withColliders = 0, total = 0;
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Art/MapKit/Prefabs" }))
{
    var p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(p);
    if (go == null) continue;
    total++;
    if (go.GetComponentsInChildren<UnityEngine.Collider>(true).Length > 0) withColliders++;
}
sb.Append("mapPrefabs=").Append(total).Append(" withColliders=").Append(withColliders).Append('\n');
// Build settings.
foreach (var s in UnityEditor.EditorBuildSettings.scenes) sb.Append("scene ").Append(s.enabled).Append(' ').Append(s.path).Append('\n');
UnityEngine.Debug.Log("[regencheck]\n" + sb);
return "regen-check";
