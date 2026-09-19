namespace BeMyArms.M0
{
    /// <summary>
    /// The combined P1+P2 body has ONE shared health pool [LOCKED]. Both humans are
    /// eliminated together when it dies. M0 only needs the pool to exist and be reachable.
    /// </summary>
    public class SharedBodyHealth : Damageable
    {
        public bool IsEliminated => Health <= 0f;
    }
}
