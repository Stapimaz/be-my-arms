namespace BeMyArms.Core
{
    /// <summary>
    /// The combined P1+P2 body has ONE shared health pool [LOCKED]. Both humans are
    /// eliminated together when it dies. Core only needs the pool to exist and be reachable.
    /// </summary>
    public class SharedBodyHealth : Damageable
    {
        public bool IsEliminated => Health <= 0f;
    }
}
