namespace BeMyArms.M1
{
    /// <summary>Maps P1 movement posture to a weapon-spread multiplier. Data lives in M1Tuning.</summary>
    public static class AccuracyModel
    {
        public static float SpreadMultiplier(MovementState state, AccuracyRow[] rows)
        {
            if (rows != null)
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i] != null && rows[i].state == state) return rows[i].spreadMultiplier;
                }
            }
            // A missing row must not silently become "perfectly accurate".
            return DefaultSpreadMultiplier(state);
        }

        /// <summary>Standing still is most accurate; heavy movement and kicks are penalized.</summary>
        public static float DefaultSpreadMultiplier(MovementState state)
        {
            switch (state)
            {
                case MovementState.Idle: return 1f;
                case MovementState.Walk: return 1.6f;
                case MovementState.Sprint: return 3.0f;
                case MovementState.Jump: return 4.0f;
                case MovementState.Dodge: return 4.0f;
                case MovementState.Slide: return 4.0f;
                case MovementState.Vault: return 4.5f;
                case MovementState.KickLight: return 3.5f;
                case MovementState.KickHeavy: return 5.0f;
                default: return 1f;
            }
        }
    }
}
