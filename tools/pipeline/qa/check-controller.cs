var path = "Assets/Art/Characters/Quaternius/Controllers/M7_P1_Locomotion.controller";
var c = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
var sb = new System.Text.StringBuilder();
sb.Append("layers=").Append(c.layers.Length).Append('\n');
for (int i = 0; i < c.layers.Length; i++)
{
    var layer = c.layers[i];
    sb.Append("  [").Append(i).Append("] ").Append(layer.name).Append(" mode=").Append(layer.blendingMode).Append(" weight=").Append(layer.defaultWeight).Append('\n');
    foreach (var st in layer.stateMachine.states)
        sb.Append("      ").Append(st.state.name).Append(" motion=").Append(st.state.motion != null ? st.state.motion.name : "null").Append('\n');
}
sb.Append("params=");
foreach (var p in c.parameters) sb.Append(p.name).Append(' ');
sb.Append('\n');
UnityEngine.Debug.Log("[ctrl]\n" + sb);
return sb.ToString();
