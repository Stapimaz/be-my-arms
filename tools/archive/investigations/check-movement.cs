var sb = new System.Text.StringBuilder();
System.Action<string, float, float, float> probe = (tag, lookYaw, moveX, moveZ) =>
{
    var sim = new BeMyArms.M2.M2BodySim();
    sim.Initialize(0f);
    sim.State.LookYaw = lookYaw;
    sim.State.BodyYaw = 0f; // body faces +Z
    var input = new BeMyArms.M2.M2P1Input { MoveX = moveX, MoveZ = moveZ };
    sim.ApplyP1(input, 0.01f);
    sb.Append(tag).Append(" look=").Append(lookYaw).Append(" -> dPos=(")
      .Append(sim.State.PosX.ToString("F3")).Append(',').Append(sim.State.PosZ.ToString("F3"))
      .Append(") MoveForward=").Append(sim.State.MoveForward.ToString("F2"))
      .Append(" MoveRight=").Append(sim.State.MoveRight.ToString("F2")).Append('\n');
};
// body faces +Z, look also +Z: W should be forward (MoveForward=1)
probe("W aligned", 0f, 0f, 1f);
// body faces +Z, look +X (90): W should move +X and read as strafe-right
probe("W look+X", 90f, 0f, 1f);
// look +Z, D should move +X and read strafe-right
probe("D look+Z", 0f, 1f, 0f);
// look +Z, S should move -Z and read backward
probe("S look+Z", 0f, 0f, -1f);
UnityEngine.Debug.Log("[movement]\n" + sb);
return sb.ToString();
