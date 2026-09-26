var sb = new System.Text.StringBuilder();
var tuning = BeMyArms.M3.M3SectorWall.Tuning.Default;
float dt = 1f / 60f;
float innerHalf = 69f;
var state = new BeMyArms.M3.M3SectorWall.State();
float offset = 0f;
sb.Append("push outward +15/frame (offset / displayed / compression):\n");
for (int k = 0; k < 12; k++)
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, 15f, innerHalf, dt, out float target);
    offset = target;
    sb.Append("  ").Append(k).Append(": off=").Append(offset.ToString("F2")).Append(" disp=").Append(displayed.ToString("F2")).Append(" comp=").Append(state.Compression.ToString("F3")).Append('\n');
}
sb.Append("release (delta 0), frames 0..14:\n");
float prev = 0f; bool monotonic = true;
for (int k = 0; k < 15; k++)
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, 0f, innerHalf, dt, out float target);
    offset = target;
    if (k > 0 && displayed > prev + 1e-4f) monotonic = false;
    prev = displayed;
    sb.Append("  ").Append(k).Append(": disp=").Append(displayed.ToString("F2")).Append(" rebound=").Append(state.Rebound.ToString("F3")).Append(" vel=").Append(state.Velocity.ToString("F3")).Append('\n');
}
sb.Append("no oscillation=").Append(monotonic).Append(" settled=").Append(prev.ToString("F2")).Append('\n');
sb.Append("inward -10 clears rebound + compression:\n");
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, -10f, innerHalf, dt, out float target);
    offset = target;
    sb.Append("  off=").Append(offset.ToString("F2")).Append(" disp=").Append(displayed.ToString("F2")).Append(" rebound=").Append(state.Rebound.ToString("F3")).Append(" comp=").Append(state.Compression.ToString("F3")).Append('\n');
}
UnityEngine.Debug.Log("[wall]\n" + sb);
return sb.ToString();
