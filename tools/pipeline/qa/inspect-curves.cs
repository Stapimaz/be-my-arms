var path = "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx";
var sb = new System.Text.StringBuilder();
foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
{
    if (!(a is UnityEngine.AnimationClip c) || c.name.StartsWith("__preview__")) continue;
    var binds = UnityEditor.AnimationUtility.GetCurveBindings(c);
    float maxDelta = 0f;
    string sample = "";
    foreach (var b in binds)
    {
        var curve = UnityEditor.AnimationUtility.GetEditorCurve(c, b);
        if (curve == null || curve.length < 2) continue;
        float min = float.MaxValue, max = float.MinValue;
        foreach (var k in curve.keys) { min = UnityEngine.Mathf.Min(min, k.value); max = UnityEngine.Mathf.Max(max, k.value); }
        float d = max - min;
        if (d > maxDelta) { maxDelta = d; sample = b.path + "/" + b.propertyName + " [" + min.ToString("F3") + ".." + max.ToString("F3") + "]"; }
    }
    sb.Append(c.name).Append(" maxCurveDelta=").Append(maxDelta.ToString("F4")).Append("  ").Append(sample).Append('\n');
}
UnityEngine.Debug.Log("[curves]\n" + sb);
return sb.ToString();
