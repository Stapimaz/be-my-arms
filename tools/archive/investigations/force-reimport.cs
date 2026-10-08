var sb = new System.Text.StringBuilder();
UnityEditor.AssetDatabase.Refresh();
foreach (var p in new[] {
    "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx",
    "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P2_Body.fbx",
    "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P2_Arms.fbx" })
{
    UnityEditor.AssetDatabase.ImportAsset(p, UnityEditor.ImportAssetOptions.ForceUpdate);
    foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p))
    {
        if (!(a is UnityEngine.AnimationClip c) || c.name.StartsWith("__preview__") || c.name != "Rig|Idle_Loop") continue;
        var binds = UnityEditor.AnimationUtility.GetCurveBindings(c);
        float maxDelta = 0f;
        foreach (var b in binds)
        {
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(c, b);
            if (curve == null || curve.length < 2) continue;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var k in curve.keys) { min = UnityEngine.Mathf.Min(min, k.value); max = UnityEngine.Mathf.Max(max, k.value); }
            maxDelta = UnityEngine.Mathf.Max(maxDelta, max - min);
        }
        sb.Append(System.IO.Path.GetFileName(p)).Append(" Idle maxDelta=").Append(maxDelta.ToString("F4")).Append('\n');
    }
}
UnityEngine.Debug.Log("[reimport]\n" + sb);
return sb.ToString();
