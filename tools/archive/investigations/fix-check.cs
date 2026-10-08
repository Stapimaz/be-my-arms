var sb = new System.Text.StringBuilder();
var tuning = BeMyArms.M3.M3SectorWall.Tuning.Default;
float dt = 1f / 60f;
float innerHalf = 69f;
var state = new BeMyArms.M3.M3SectorWall.State();
float offset = 0f;
sb.Append("push outward +15/frame (offset / displayed / kick / latched):\n");
for (int k = 0; k < 14; k++)
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, 15f, innerHalf, dt, out float target);
    offset = target;
    sb.Append("  ").Append(k).Append(": off=").Append(offset.ToString("F2")).Append(" disp=").Append(displayed.ToString("F2"))
      .Append(" kick=").Append(state.Kick.ToString("F3")).Append(" latch=").Append(state.EdgeLatched).Append('\n');
}
sb.Append("hold at edge +5/frame, does NOT retrigger:\n");
for (int k = 0; k < 6; k++)
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, 5f, innerHalf, dt, out float target);
    offset = target;
    sb.Append("  ").Append(k).Append(": off=").Append(offset.ToString("F2")).Append(" disp=").Append(displayed.ToString("F2"))
      .Append(" kick=").Append(state.Kick.ToString("F3")).Append(" latch=").Append(state.EdgeLatched).Append('\n');
}
sb.Append("release (delta 0): kick decays to zero, try the same edge again -> no retrigger:\n");
for (int k = 0; k < 12; k++)
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, 0f, innerHalf, dt, out float target);
    offset = target;
    if (k % 4 == 0) sb.Append("  rel").Append(k).Append(": kick=").Append(state.Kick.ToString("F3")).Append(" disp=").Append(displayed.ToString("F2")).Append('\n');
}
sb.Append("inward -10 resets latch + is 1:1:\n");
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, -10f, innerHalf, dt, out float target);
    offset = target;
    sb.Append("  off=").Append(offset.ToString("F2")).Append(" disp=").Append(displayed.ToString("F2"))
      .Append(" kick=").Append(state.Kick.ToString("F3")).Append(" latch=").Append(state.EdgeLatched).Append('\n');
}
sb.Append("push outward again after reset -> new kick:\n");
{
    float displayed = BeMyArms.M3.M3SectorWall.Step(ref state, tuning, offset, 15f, innerHalf, dt, out float target);
    offset = target;
    sb.Append("  off=").Append(offset.ToString("F2")).Append(" disp=").Append(displayed.ToString("F2"))
      .Append(" kick=").Append(state.Kick.ToString("F3")).Append(" latch=").Append(state.EdgeLatched).Append('\n');
}
UnityEngine.Debug.Log("[wall]\n" + sb);
return sb.ToString();
