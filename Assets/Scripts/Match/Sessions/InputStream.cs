using System;
using BeMyArms.Networking;

namespace BeMyArms.Match
{
    /// <summary>Shared command rules for frame sampling, fixed-tick consumption and safe holding.</summary>
    public static class InputStream
    {
        public const float TickSeconds = 1f / 60f;
        public const float SilenceTimeoutSeconds = 0.25f;

        public static void Accumulate(ref P1Input pending, in P1Input frame)
        {
            pending.MoveX = frame.MoveX;
            pending.MoveZ = frame.MoveZ;
            pending.Sprint = frame.Sprint;
            pending.AlignBody = frame.AlignBody;
            pending.Crouch = frame.Crouch;
            pending.LookYawDelta += frame.LookYawDelta;
            pending.LookPitchDelta += frame.LookPitchDelta;
            pending.Jump |= frame.Jump;
            pending.Dodge |= frame.Dodge;
            pending.Slide |= frame.Slide;
            pending.LightKick |= frame.LightKick;
            pending.HeavyKick |= frame.HeavyKick;
        }

        public static P1Input Held(in P1Input input) => new P1Input
        {
            Sequence = input.Sequence, ControlEpoch = input.ControlEpoch,
            MoveX = input.MoveX, MoveZ = input.MoveZ, Sprint = input.Sprint,
            AlignBody = input.AlignBody, Crouch = input.Crouch
        };

        public static P1Input LookOnly(in P1Input input) => new P1Input
        {
            Sequence = input.Sequence, ControlEpoch = input.ControlEpoch,
            AlignBody = input.AlignBody,
            LookYawDelta = input.LookYawDelta, LookPitchDelta = input.LookPitchDelta
        };

        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        public static bool FireGate(ref bool armed, bool active, bool held, bool pressed)
        {
            if (!active) { armed = false; return false; }
            if (!held && !pressed) armed = true;
            return armed && (held || pressed);
        }
        public static bool Valid(in P1Input i) => Finite(i.MoveX) && Finite(i.MoveZ) &&
            Finite(i.LookYawDelta) && Finite(i.LookPitchDelta) && Math.Abs(i.MoveX) <= 1f &&
            Math.Abs(i.MoveZ) <= 1f && Math.Abs(i.LookYawDelta) <= 180f && Math.Abs(i.LookPitchDelta) <= 180f;
        public static bool Valid(in P2Input i) => Finite(i.AimYaw) && Finite(i.AimPitch) && Math.Abs(i.AimPitch) <= 80.001f;
    }
}
