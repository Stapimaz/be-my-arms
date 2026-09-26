var sb = new System.Text.StringBuilder();
// sector mapping
float bodyYaw = 0f, innerHalf = 69f, yaw = 0f;
sb.Append("sector soft-zone ").Append(BeMyArms.M3.M3DuelClient.SectorSoftZoneDegrees).Append(" deg; outward +15 steps:\n");
for (int k = 0; k < 12; k++)
{
    float prev = BeMyArms.M2.M2BodySim.Normalize(yaw - bodyYaw);
    yaw = BeMyArms.M3.M3DuelClient.ApplySectorResistance(yaw, 15f, bodyYaw, innerHalf);
    float now = BeMyArms.M2.M2BodySim.Normalize(yaw - bodyYaw);
    sb.Append("  ").Append(now.ToString("F2")).Append(" (+").Append((now - prev).ToString("F2")).Append(")\n");
}
float ip = BeMyArms.M2.M2BodySim.Normalize(yaw - bodyYaw);
yaw = BeMyArms.M3.M3DuelClient.ApplySectorResistance(yaw, -12f, bodyYaw, innerHalf);
sb.Append("inward -12 -> ").Append(BeMyArms.M2.M2BodySim.Normalize(yaw - bodyYaw).ToString("F2")).Append(" (delta=").Append((BeMyArms.M2.M2BodySim.Normalize(yaw - bodyYaw) - ip).ToString("F2")).Append(")\n");

// weapon grip roll
var wp = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Art/Weapons/Prefabs/BMA_Weapon_Rifle_Quaternius.prefab");
UnityEngine.Transform gr = null, gl = null;
foreach (var t in wp.GetComponentsInChildren<UnityEngine.Transform>(true)) { if (t.name == "Grip_R") gr = t; if (t.name == "Grip_L") gl = t; }
sb.Append("Grip_R euler=").Append(gr != null ? gr.localRotation.eulerAngles.ToString("F1") : "null").Append(" pos=").Append(gr != null ? gr.localPosition.ToString("F3") : "-").Append('\n');
sb.Append("Grip_L euler=").Append(gl != null ? gl.localRotation.eulerAngles.ToString("F1") : "null").Append(" pos=").Append(gl != null ? gl.localPosition.ToString("F3") : "-").Append('\n');
UnityEngine.Debug.Log("[fixcheck]\n" + sb);
return sb.ToString();
