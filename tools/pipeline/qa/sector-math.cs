var sb = new System.Text.StringBuilder();
float bodyYaw = 0f;
float innerHalf = 69f;
float yaw = 0f;
sb.Append("outward +15 steps:\n");
for (int k = 0; k < 10; k++)
{
    float prev = BeMyArms.M2.M2BodySim.Normalize(yaw - bodyYaw);
    yaw = BeMyArms.M3.M3DuelClient.ApplySectorResistance(yaw, 15f, bodyYaw, innerHalf);
    float now = BeMyArms.M2.M2BodySim.Normalize(yaw - bodyYaw);
    sb.Append("  step ").Append(k).Append(": offset=").Append(now.ToString("F2")).Append(" (+").Append((now - prev).ToString("F2")).Append(")\n");
}
float inwardPrev = BeMyArms.M2.M2BodySim.Normalize(yaw - bodyYaw);
yaw = BeMyArms.M3.M3DuelClient.ApplySectorResistance(yaw, -12f, bodyYaw, innerHalf);
float inwardNow = BeMyArms.M2.M2BodySim.Normalize(yaw - bodyYaw);
sb.Append("inward -12: offset=").Append(inwardNow.ToString("F2")).Append(" (delta=").Append((inwardNow - inwardPrev).ToString("F2")).Append(")\n");
UnityEngine.Debug.Log("[sector]\n" + sb);
return sb.ToString();
