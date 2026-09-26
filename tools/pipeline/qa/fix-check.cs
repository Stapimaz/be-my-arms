var sb = new System.Text.StringBuilder();
var tuning = BeMyArms.M3.M3SectorWall.Tuning.Default;
float dt = 1f / 60f;
float innerHalf = 69f;
var state = new BeMyArms.M3.M3SectorWall.State();
float offset = 0f;
sb.Append("push outward +15/frame (offset / target / pressure / displayed):\n");
for (int k = 0; k < 14; k++)
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, 15f, innerHalf, dt, out float target);
    offset = target;
    sb.Append("  ").Append(k).Append(": off=").Append(offset.ToString("F2")).Append(" disp=").Append(displayed.ToString("F2")).Append(" p=").Append(state.Pressure.ToString("F2")).Append('\n');
}
sb.Append("release (delta 0), frames 0..14:\n");
for (int k = 0; k < 15; k++)
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, 0f, innerHalf, dt, out float target);
    offset = target;
    sb.Append("  ").Append(k).Append(": disp=").Append(displayed.ToString("F2")).Append(" p=").Append(state.Pressure.ToString("F2")).Append('\n');
}
sb.Append("inward -10 clears pressure:\n");
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, -10f, innerHalf, dt, out float target);
    offset = target;
    sb.Append("  off=").Append(offset.ToString("F2")).Append(" disp=").Append(displayed.ToString("F2")).Append(" p=").Append(state.Pressure.ToString("F2")).Append('\n');
}
UnityEngine.Debug.Log("[wall]\n" + sb);
return sb.ToString();
